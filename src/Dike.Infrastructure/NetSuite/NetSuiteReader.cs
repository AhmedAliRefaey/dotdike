using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Dike.Core;

namespace Dike.Infrastructure.NetSuite;

public sealed record GlLine(string TransactionId, string LineId, string BookId, string AccountId,
    string SubsidiaryId, DateOnly Date, string Reference, decimal Debit, decimal Credit)
{
    public decimal SignedAmount => Debit - Credit;
    public string Identity => $"{TransactionId}:{LineId}:{BookId}:{AccountId}";
}
public sealed record GlExtract(string RunId, DateTimeOffset FetchedAt, string AccountId, string SubsidiaryId,
    string AccountingBookId, string Currency, DateOnly From, DateOnly To, IReadOnlyList<GlLine> Lines)
{
    // Null on historical extracts: never assign today's provenance while reading old evidence.
    public string? QueryVersion { get; init; }
    public string? IdentityVersion { get; init; }
    public string? QuerySha256 { get; init; }
}

// Alecto: read-only SuiteQL client. Bound to one company and one credential set per operation.
public sealed class NetSuiteReader(HttpClient http, NetSuiteConnection connection, string companyId) : INetSuiteReader
{
    private string? accessToken;
    public async Task<IReadOnlyList<GlAccount>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        var items = await QueryAsync("SELECT a.id, a.acctnumber, a.fullname, a.accttype, a.subsidiary, c.symbol AS currency FROM account a LEFT JOIN currency c ON c.id = a.currency WHERE a.isinactive = 'F' ORDER BY a.id", [], cancellationToken);
        var accounts = items.Select(x => new GlAccount(Text(x,"id"), Text(x,"acctnumber") + " " + Text(x,"fullname"), Text(x,"subsidiary"), Text(x,"currency"), Text(x,"accttype") == "Bank")).ToArray();
        if (accounts.Any(x => x.Id.Length == 0) || accounts.Select(x => x.Id).Distinct().Count() != accounts.Length)
            throw new NetSuiteException("Account results contain missing or duplicate IDs; no snapshot was saved.");
        return accounts;
    }
    public async Task<GlExtract> ReadGlAsync(string account, string subsidiary, string book, DateOnly from, DateOnly to, CancellationToken ct)
    {
        foreach (var id in new[] { account, subsidiary, book })
            if (!long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n <= 0)
                throw new NetSuiteException("Account, subsidiary and book must be positive internal IDs.");
        if (to < from || to.DayNumber - from.DayNumber > 365) throw new NetSuiteException("Select a valid period of at most 366 days.");
        // Initial release supports SAR in the PRIMARY book only; secondary book currency requires a different verified mapping.
        var validation = await QueryAsync(NetSuiteQueryContract.ScopeSql, [account, subsidiary, book], ct);
        if (validation.Count != 1 || Text(validation[0], "accttype") != "Bank" || Text(validation[0], "accountcurrency") != "SAR" || Text(validation[0], "basecurrency") != "SAR" || Text(validation[0], "isprimary") != "T")
            throw new NetSuiteException("Select an active SAR bank account, a SAR subsidiary, and the primary accounting book. Currency conversion is not enabled.");
        var items = await QueryAsync(NetSuiteQueryContract.PostedGlSql, [account, subsidiary, book, from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)], ct);
        var lines = new List<GlLine>();
        var identities = new HashSet<string>();
        foreach (var item in items)
        {
            var line = new GlLine(Text(item,"transactionid"), Text(item,"lineid"), Text(item,"bookid"), Text(item,"accountid"), Text(item,"subsidiaryid"),
                DateOnly.ParseExact(Text(item,"postingdate"), "yyyy-MM-dd", CultureInfo.InvariantCulture), Text(item,"reference"), Amount(item,"debit"), Amount(item,"credit"));
            if (!NetSuiteMoney.IsValid(line.SignedAmount))
                throw new NetSuiteException("A signed source movement exceeds the SAR money range; no snapshot was saved.");
            if (line.TransactionId.Length == 0 || line.LineId.Length == 0 || !identities.Add(line.Identity))
                throw new NetSuiteException("GL source identities are missing or repeated. Validate the line key in Records Catalog; no snapshot was saved.");
            if (line.AccountId != account || line.SubsidiaryId != subsidiary || line.BookId != book || line.Date < from || line.Date > to)
                throw new NetSuiteException("NetSuite returned a row outside the requested scope; no snapshot was saved.");
            lines.Add(line);
        }
        return new(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, account, subsidiary, book, "SAR", from, to, lines)
        {
            QueryVersion = NetSuiteQueryContract.QueryVersion,
            IdentityVersion = NetSuiteQueryContract.IdentityVersion,
            QuerySha256 = NetSuiteQueryContract.QuerySha256
        };
    }
    public async Task<SourceSnapshot> ReadPostedMovementsAsync(AccountScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (scope.Company != companyId || scope.Currency != "SAR") throw new NetSuiteException("The requested company or currency is outside this connection scope.");
        var extract = await ReadGlAsync(scope.Account, scope.Subsidiary, scope.AccountingBook, from, to, cancellationToken);
        var rows = extract.Lines.Select(x => new Movement(x.Identity, scope, SourceSide.Book, x.SignedAmount, x.Date, x.Reference,
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(x))))).ToArray();
        return new(rows, extract.RunId, extract.FetchedAt, null, true);
    }
    private async Task<List<JsonElement>> QueryAsync(string sql, string[] parameters, CancellationToken ct)
    {
        connection.Validate();
        var results = new List<JsonElement>();
        int? expectedTotal = null;
        for (var offset = 0; offset < 100000; offset += 1000)
        {
            var uri = new Uri(connection.BaseUri, $"services/rest/query/v1/suiteql?limit=1000&offset={offset}");
            using var response = await SendAsync(uri, sql, parameters, ct);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = json.RootElement;
            var items = root.GetProperty("items").EnumerateArray().Select(x => x.Clone()).ToArray();
            var count = root.GetProperty("count").GetInt32();
            var total = root.GetProperty("totalResults").GetInt32();
            var more = root.GetProperty("hasMore").GetBoolean();
            if (total >= 100000) throw new NetSuiteException("This extract reaches the REST result limit. Select a smaller period; no partial snapshot was saved.");
            if (root.GetProperty("offset").GetInt32() != offset || count != items.Length || count > 1000 || (more && count != 1000) || (expectedTotal is not null && expectedTotal != total))
                throw new NetSuiteException("Pagination changed or was incomplete. Retry the extract; no snapshot was saved.");
            expectedTotal = total;
            results.AddRange(items);
            if (!more)
            {
                if (results.Count != total) throw new NetSuiteException("Incomplete result count; no snapshot was saved.");
                return results;
            }
        }
        throw new NetSuiteException("REST row limit exceeded. Select a smaller period.");
    }
    private async Task<HttpResponseMessage> SendAsync(Uri uri, string sql, string[] parameters, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Add("Prefer", "transient");
            request.Content = JsonContent.Create(new { q = sql, @params = parameters });
            if (connection.Mode == "TBA") request.Headers.TryAddWithoutValidation("Authorization", NetSuiteAuthentication.TbaHeader(connection, uri, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            else
            {
                accessToken ??= await FetchTokenAsync(ct);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
            var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return response;
            if (attempt < 2 && ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500))
            {
                var delay = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(1 << attempt);
                response.Dispose();
                if (delay > TimeSpan.FromSeconds(30)) throw new NetSuiteException("NetSuite requested a longer retry delay. Try again later; previous data is unchanged.");
                await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1), ct);
                continue;
            }
            var status = response.StatusCode; response.Dispose();
            throw new NetSuiteException(status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? "NetSuite denied access. Check integration credentials, token/certificate validity and role permissions."
                : $"NetSuite query failed (HTTP {(int)status}). Check Records Catalog fields and permissions. Previous data is unchanged.");
        }
    }
    private async Task<string> FetchTokenAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(connection.BaseUri,"services/rest/auth/oauth2/v1/token"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string,string> { ["grant_type"]="client_credentials", ["client_assertion_type"]="urn:ietf:params:oauth:client-assertion-type:jwt-bearer", ["client_assertion"]=NetSuiteAuthentication.ClientAssertion(connection,DateTimeOffset.UtcNow) })
        };
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new NetSuiteException("NetSuite certificate authentication failed. Check the integration, certificate mapping and role.");
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken:ct);
        return json.RootElement.GetProperty("access_token").GetString() ?? throw new NetSuiteException("NetSuite returned no access token.");
    }
    private static string Text(JsonElement row,string key) => row.TryGetProperty(key,out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : "";
    private static decimal Amount(JsonElement row,string key)
    {
        if (!NetSuiteMoney.TryParse(Text(row,key), out var amount))
            throw new NetSuiteException("A source amount is missing, invalid or exceeds SAR precision or money range; no snapshot was saved.");
        return amount;
    }
}
