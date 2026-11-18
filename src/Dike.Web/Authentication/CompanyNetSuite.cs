using System.Collections.Concurrent;
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

// Local single-instance pilot store. Protects credentials AND financial snapshots at rest.
public sealed class CompanyNetSuite(IDataProtectionProvider protection, IWebHostEnvironment environment, IHttpClientFactory http, IConfiguration configuration)
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> gates = new();
    private string Folder(Guid company) => Path.Combine(configuration["NetSuite:StoragePath"] ?? Path.Combine(environment.ContentRootPath,"App_Data","NetSuite"),company.ToString("N"));
    private IDataProtector Protector(Guid company) => protection.CreateProtector("Dike.NetSuite.v1", company.ToString("N"));
    private async Task<NetSuiteState?> LoadAsync(Guid company, CancellationToken ct)
    {
        var path = Path.Combine(Folder(company),"state.enc");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<NetSuiteState>(Protector(company).Unprotect(await File.ReadAllTextAsync(path,ct)));
    }
    public async Task<NetSuiteState?> ReadAsync(Guid company, CancellationToken ct)
    {
        var gate = gates.GetOrAdd(company,_ => new(1,1)); await gate.WaitAsync(ct);
        try { return await LoadAsync(company,ct); } finally { gate.Release(); }
    }
    private async Task WriteAsync(Guid company, NetSuiteState state, CancellationToken ct)
    {
        Directory.CreateDirectory(Folder(company));
        var path = Path.Combine(Folder(company),"state.enc");
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temp,Protector(company).Protect(JsonSerializer.Serialize(state)),ct);
        File.Move(temp,path,true);
    }
    public async Task ConfigureAsync(Guid company, NetSuiteConnection connection, CancellationToken ct)
    {
        connection.AccountId = connection.AccountId.Trim(); connection.Validate();
        var gate = gates.GetOrAdd(company,_ => new(1,1)); await gate.WaitAsync(ct);
        try
        {
            var previous = await LoadAsync(company,ct);
            if (previous is not null)
                await File.WriteAllTextAsync(Path.Combine(Folder(company),"previous-" + Guid.NewGuid().ToString("N") + ".enc"),Protector(company).Protect(JsonSerializer.Serialize(previous)),ct);
            await WriteAsync(company,new() { Connection = connection },ct);
        }
        finally { gate.Release(); }
    }
    public async Task CollectAsync(Guid company, bool accountsOnly, string account, string subsidiary, string book, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var gate = gates.GetOrAdd(company,_ => new(1,1)); await gate.WaitAsync(ct);
        try
        {
            var state = await LoadAsync(company,ct) ?? throw new NetSuiteException("Configure a NetSuite connection first.");
            try
            {
                using var client = http.CreateClient("NetSuite");
                var reader = new NetSuiteReader(client,state.Connection,company.ToString());
                if (accountsOnly)
                {
                    var accounts = await reader.GetAccountsAsync(ct);
                    state.Accounts = accounts.ToList(); state.AccountsFetchedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    if (!state.Accounts.Any(x=>x.Id == account && x.IsBank && x.Currency == "SAR"))
                        throw new NetSuiteException("Load accounts and select an eligible SAR bank GL account.");
                    var extract = await reader.ReadGlAsync(account,subsidiary,book,from,to,ct);
                    var evidence = Protector(company).Protect(JsonSerializer.Serialize(extract));
                    await File.WriteAllTextAsync(Path.Combine(Folder(company),"run-" + extract.RunId + ".enc"),evidence,ct);
                    state.LatestExtract = extract;
                }
                state.LastError = null;
                await WriteAsync(company,state,ct);
            }
            catch (Exception e) when (e is NetSuiteException or HttpRequestException or TaskCanceledException or JsonException or FormatException or KeyNotFoundException)
            {
                state.LastError = e is NetSuiteException ? e.Message : "NetSuite request failed, timed out or returned an unexpected response. Previous completed data is retained.";
                await WriteAsync(company,state,CancellationToken.None);
                throw new NetSuiteException(state.LastError);
            }
        }
        finally { gate.Release(); }
    }
}
