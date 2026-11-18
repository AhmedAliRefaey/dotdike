using System.Text.Json;
using Dike.Core;
using Dike.Infrastructure.NetSuite;
using Microsoft.AspNetCore.DataProtection;

namespace Dike.Web.Authentication;

public sealed class NetSuiteState
{
    public NetSuiteConnection Connection { get; set; } = new();
    public List<GlAccount> Accounts { get; set; } = [];
    public DateTimeOffset? AccountsFetchedAt { get; set; }
    public GlExtract? LatestExtract { get; set; }
    public string? LastError { get; set; }
}

// Protects credentials and snapshots before writing to local or shared company storage.
public sealed class CompanyNetSuite(IDataProtectionProvider protection, ICompanyDocumentStorage storage, IHttpClientFactory http)
{
    private IDataProtector Protector(Guid company) => protection.CreateProtector("Dike.NetSuite.v1", company.ToString("N"));
    private async Task<NetSuiteState?> LoadAsync(ICompanyDocumentSession session, Guid company, CancellationToken ct)
    {
        var ciphertext = await session.ReadAsync("state.enc", ct);
        return ciphertext is null ? null : JsonSerializer.Deserialize<NetSuiteState>(Protector(company).Unprotect(ciphertext));
    }
    public async Task<NetSuiteState?> ReadAsync(Guid company, CancellationToken ct)
    {
        await using var session = await storage.OpenAsync(ConnectionProviders.NetSuite, company, ct);
        return await LoadAsync(session, company, ct);
    }
    private async Task WriteAsync(ICompanyDocumentSession session, Guid company, NetSuiteState state, CancellationToken ct)
    {
        await session.WriteAsync("state.enc", Protector(company).Protect(JsonSerializer.Serialize(state)), ct);
    }
    public async Task ConfigureAsync(Guid company, NetSuiteConnection connection, CancellationToken ct)
    {
        connection.AccountId = connection.AccountId.Trim(); connection.Validate();
        await using var session = await storage.OpenAsync(ConnectionProviders.NetSuite, company, ct);
        var previous = await LoadAsync(session, company, ct);
        if (previous is not null)
            await session.WriteAsync("previous-" + Guid.NewGuid().ToString("N") + ".enc", Protector(company).Protect(JsonSerializer.Serialize(previous)), ct);
        await WriteAsync(session, company, new() { Connection = connection }, ct);
        await session.CompleteAsync(ct);
    }
    public async Task CollectAsync(Guid company, bool accountsOnly, string account, string subsidiary, string book, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await using var session = await storage.OpenAsync(ConnectionProviders.NetSuite, company, ct);
        var state = await LoadAsync(session, company, ct) ?? throw new NetSuiteException("Configure a NetSuite connection first.");
        try
        {
            using var client = http.CreateClient("NetSuite");
            var reader = new NetSuiteReader(client, state.Connection, company.ToString());
            if (accountsOnly)
            {
                var accounts = await reader.GetAccountsAsync(ct);
                state.Accounts = accounts.ToList(); state.AccountsFetchedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                if (!state.Accounts.Any(x => x.Id == account && x.IsBank && x.Currency == "SAR"))
                    throw new NetSuiteException("Load accounts and select an eligible SAR bank GL account.");
                var extract = await reader.ReadGlAsync(account, subsidiary, book, from, to, ct);
                var evidence = Protector(company).Protect(JsonSerializer.Serialize(extract));
                await session.WriteAsync("run-" + extract.RunId + ".enc", evidence, ct);
                state.LatestExtract = extract;
            }
            state.LastError = null;
            await WriteAsync(session, company, state, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is (NetSuiteException or HttpRequestException or TaskCanceledException or JsonException or FormatException or KeyNotFoundException))
        {
            state.LastError = e is NetSuiteException ? e.Message : "NetSuite request failed, timed out or returned an unexpected response. Previous completed data is retained.";
            await WriteAsync(session, company, state, CancellationToken.None);
            await session.CompleteAsync(CancellationToken.None);
            throw new NetSuiteException(state.LastError);
        }
        await session.CompleteAsync(ct);
    }
}
