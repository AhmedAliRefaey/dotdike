using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dike.Core;

namespace Dike.Infrastructure.Odoo;

public sealed class OdooReadOnlyClient : IOdooReadOnlyClient
{
    public const int CatalogPageSize = 100;
    public const int MaxSampleRows = 50;
    public const int MaxPeriodDays = 31;
    private const int MaxResponseBytes = 2 * 1024 * 1024;
    private readonly OdooConnection connection;
    private readonly HttpClient http;

    public OdooReadOnlyClient(OdooConnection connection) : this(connection, CreateHandler()) { }
    internal OdooReadOnlyClient(OdooConnection connection, HttpMessageHandler handler)
    {
        this.connection = connection;
        http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }
    internal static SocketsHttpHandler CreateHandler() => new() { AllowAutoRedirect = false, UseCookies = false };
    public void Dispose() => http.Dispose();

    public async Task<OdooAccess> AuthenticateAsync(CancellationToken ct = default)
    {
        var context = await CallAsync("res.users", "context_get", new { }, ct);
        var uid = Id(context, "uid");
        var users = Rows(await CallAsync("res.users", "read", new { ids = new[] { uid }, fields = new[] { "id", "company_ids" } }, ct), 1);
        if (users.Length != 1 || Id(users[0], "id") != uid) throw InvalidResponse();
        var companyIds = Ids(Field(users[0], "company_ids"));
        if (companyIds.Length == 0) throw new OdooException("scope", "The integration user has no permitted companies.");
        return new OdooAccess(uid, companyIds);
    }

    public async Task<OdooCatalog<OdooCompany>> ListCompaniesAsync(int afterId = 0, CancellationToken ct = default)
    {
        if (afterId < 0) throw InvalidScope();
        var access = await AuthenticateAsync(ct);
        var rows = Rows(await CallAsync("res.company", "search_read", new
        {
            domain = new object[] { new object[] { "id", "in", access.PermittedCompanyIds }, new object[] { "id", ">", afterId } },
            fields = new[] { "id", "name", "currency_id" }, order = "id asc", limit = CatalogPageSize + 1,
            context = Context(access.PermittedCompanyIds)
        }, ct), CatalogPageSize + 1);
        CheckOrder(rows, afterId);
        var companies = rows.Select(Company).ToArray();
        if (companies.Any(x => !access.PermittedCompanyIds.Contains(x.Id))) throw InvalidScope();
        var page = companies.Take(CatalogPageSize).ToArray();
        return new(access.UserId, page, companies.Length > CatalogPageSize,
            companies.Length > CatalogPageSize ? page[^1].Id : null);
    }

    public async Task<OdooCatalog<OdooAccount>> ListAccountsAsync(int companyId, int afterId = 0, CancellationToken ct = default, bool bankOnly = false)
    {
        if (companyId <= 0 || afterId < 0) throw InvalidScope();
        var access = await AuthenticateAsync(ct);
        await GetCompanyAsync(access, companyId, ct);
        var companyField = await AccountCompanyFieldAsync(companyId, ct);
        var filters = new List<object> { new object[] { "id", ">", afterId } };
        if (bankOnly) filters.Add(new object[] { "account_type", "=", "asset_cash" });
        var rows = await AccountsAsync(companyId, companyField, filters.ToArray(), CatalogPageSize + 1, ct);
        CheckOrder(rows, afterId);
        var accounts = rows.Select(row => Account(row, companyField, companyId)).ToArray();
        if (bankOnly && accounts.Any(account => account.AccountType != "asset_cash")) throw InvalidScope();
        var page = accounts.Take(CatalogPageSize).ToArray();
        return new(access.UserId, page, accounts.Length > CatalogPageSize,
            accounts.Length > CatalogPageSize ? page[^1].Id : null);
    }

    public async Task<OdooGlSample> ReadPostedSampleAsync(int companyId, int accountId,
        DateOnly from, DateOnly to, int limit = 10, CancellationToken ct = default)
    {
        if (companyId <= 0 || accountId <= 0 || from > to || to.DayNumber - from.DayNumber >= MaxPeriodDays ||
            limit < 1 || limit > MaxSampleRows)
            throw new OdooException("scope", "Select positive company/account IDs, an inclusive period of 1-31 days and a limit of 1-50 rows.");
        var access = await AuthenticateAsync(ct);
        var company = await GetCompanyAsync(access, companyId, ct);
        var currencies = Rows(await CallAsync("res.currency", "read", new
        {
            ids = new[] { company.CurrencyId }, fields = new[] { "id", "name" }, context = Context([companyId])
        }, ct), 1);
        if (currencies.Length != 1 || Id(currencies[0], "id") != company.CurrencyId || Text(currencies[0], "name") != "SAR")
            throw new OdooException("currency", "This validation slice requires a company whose base currency is SAR.");
        var companyField = await AccountCompanyFieldAsync(companyId, ct);
        var accounts = await AccountsAsync(companyId, companyField, new object[] { new object[] { "id", "=", accountId } }, 1, ct);
        if (accounts.Length != 1) throw new OdooException("scope", "The selected account is not visible in the selected company.");
        var account = Account(accounts[0], companyField, companyId);
        if (account.Id != accountId || account.AccountType != "asset_cash" ||
            (account.CurrencyId is int currencyId && currencyId != company.CurrencyId))
            throw new OdooException("scope", "Select a bank/cash GL account with no foreign-currency restriction in the selected SAR company.");
        var fields = new[] { "id", "company_id", "company_currency_id", "account_id", "move_id", "journal_id", "parent_state", "date", "debit", "credit", "balance" };
        var schema = await SchemaAsync("account.move.line", fields, companyId, ct);
        Require(schema, "id", "integer");
        Require(schema, "company_id", "many2one", "res.company");
        Require(schema, "company_currency_id", "many2one", "res.currency");
        Require(schema, "account_id", "many2one", "account.account");
        Require(schema, "move_id", "many2one", "account.move");
        Require(schema, "journal_id", "many2one", "account.journal");
        Require(schema, "parent_state", "selection");
        Require(schema, "date", "date");
        foreach (var moneyField in new[] { "debit", "credit", "balance" }) Require(schema, moneyField, "monetary");
        var rows = Rows(await CallAsync("account.move.line", "search_read", new
        {
            domain = new object[]
            {
                new object[] { "company_id", "=", companyId }, new object[] { "account_id", "=", accountId },
                new object[] { "parent_state", "=", "posted" }, new object[] { "date", ">=", Date(from) },
                new object[] { "date", "<=", Date(to) }
            },
            fields, order = "id asc", limit = limit + 1, context = Context([companyId])
        }, ct), limit + 1);
        CheckOrder(rows, 0);
        var lines = new List<OdooGlLine>();
        foreach (var row in rows)
        {
            if (Relation(row, "company_id") != companyId || Relation(row, "account_id") != accountId ||
                Relation(row, "company_currency_id") != company.CurrencyId || Text(row, "parent_state") != "posted" ||
                !DateOnly.TryParseExact(Text(row, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                date < from || date > to) throw InvalidScope();
            var debit = Money(row, "debit");
            var credit = Money(row, "credit");
            var balance = Money(row, "balance");
            if (debit < 0 || credit < 0 || (debit > 0 && credit > 0) || balance != debit - credit)
                throw new OdooException("money", "The source debit/credit/balance semantics are unsupported or inconsistent.");
            lines.Add(new(Id(row, "id"), Relation(row, "move_id"), Relation(row, "journal_id"), date, debit, credit, balance));
        }
        return new(access.UserId, companyId, accountId, from, to, limit, lines.Take(limit).ToArray(), lines.Count > limit);
    }

    private async Task<OdooCompany> GetCompanyAsync(OdooAccess access, int companyId, CancellationToken ct)
    {
        if (!access.PermittedCompanyIds.Contains(companyId)) throw InvalidScope();
        var rows = Rows(await CallAsync("res.company", "search_read", new
        {
            domain = new object[] { new object[] { "id", "=", companyId } }, fields = new[] { "id", "name", "currency_id" },
            order = "id asc", limit = 1, context = Context([companyId])
        }, ct), 1);
        if (rows.Length != 1 || Id(rows[0], "id") != companyId) throw InvalidScope();
        return Company(rows[0]);
    }

    private async Task<string> AccountCompanyFieldAsync(int companyId, CancellationToken ct)
    {
        var schema = await SchemaAsync("account.account", ["id", "code", "name", "account_type", "currency_id", "company_ids", "company_id"], companyId, ct);
        Require(schema, "id", "integer");
        Require(schema, "code", "char");
        Require(schema, "name", "char");
        Require(schema, "account_type", "selection");
        Require(schema, "currency_id", "many2one", "res.currency");
        if (schema.TryGetProperty("company_ids", out _))
        {
            Require(schema, "company_ids", "many2many", "res.company");
            return "company_ids";
        }
        Require(schema, "company_id", "many2one", "res.company");
        return "company_id";
    }

    private async Task<JsonElement[]> AccountsAsync(int companyId, string companyField, object[] filters, int limit, CancellationToken ct)
    {
        object[] companyFilter = companyField == "company_ids" ? [companyField, "in", new[] { companyId }] : [companyField, "=", companyId];
        return Rows(await CallAsync("account.account", "search_read", new
        {
            domain = new object[] { companyFilter }.Concat(filters).ToArray(),
            fields = new[] { "id", "code", "name", "account_type", "currency_id", companyField },
            order = "id asc", limit, context = Context([companyId])
        }, ct), limit);
    }

    private Task<JsonElement> SchemaAsync(string model, string[] fields, int companyId, CancellationToken ct) =>
        CallAsync(model, "fields_get", new { allfields = fields, attributes = new[] { "type", "relation" }, context = Context([companyId]) }, ct);

    private static void Require(JsonElement schema, string field, string type, string? relation = null)
    {
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty(field, out var metadata) ||
            Text(metadata, "type") != type || (relation != null && Text(metadata, "relation") != relation))
            throw new OdooException("schema", "The installed Odoo schema or field permissions do not support this read-only test.");
    }

    private async Task<JsonElement> CallAsync(string model, string method, object body, CancellationToken ct)
    {
        // Only private callers select these fixed read methods. There is no arbitrary RPC bridge.
        var path = $"json/2/{model}/{method}";
        if (path is not ("json/2/res.users/context_get" or "json/2/res.users/read" or "json/2/res.company/search_read" or
            "json/2/res.currency/read" or "json/2/account.account/fields_get" or "json/2/account.account/search_read" or
            "json/2/account.move.line/fields_get" or "json/2/account.move.line/search_read"))
            throw new OdooException("method", "Only the fixed read-only test methods are allowed.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(connection.BaseUri, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", connection.ApiKey);
            request.Headers.UserAgent.ParseAdd("DikeOdooValidation/0.1");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (connection.Database != null) request.Headers.Add("X-Odoo-Database", connection.Database);
            request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode is >= 300 and < 400)
                throw new OdooException("redirect", "Odoo returned a redirect. Credentials were not forwarded; verify the trusted endpoint.");
            if (!response.IsSuccessStatusCode)
                throw new OdooException("http_" + (int)response.StatusCode, response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "Odoo rejected the API key. Check its expiry and integration user.",
                    System.Net.HttpStatusCode.Forbidden => "Odoo denied access. Check API entitlement and the integration user's model/company permissions.",
                    System.Net.HttpStatusCode.NotFound => "The JSON-2 endpoint or model was not found. Verify Odoo version, Accounting and database routing.",
                    System.Net.HttpStatusCode.TooManyRequests => "Odoo rate-limited this test. Retry later.",
                    _ => "Odoo could not complete the read-only request. Provider error details were withheld."
                });
            if (response.Content.Headers.ContentLength > MaxResponseBytes) throw ResponseLimit();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(chunk, timeout.Token)) != 0)
            {
                if (buffer.Length + count > MaxResponseBytes) throw ResponseLimit();
                buffer.Write(chunk, 0, count);
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            return document.RootElement.Clone();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new OdooException("timeout", "The Odoo read timed out; no sample was accepted."); }
        catch (Exception e) when (e is HttpRequestException or IOException or JsonException)
        { throw new OdooException("transport", "The Odoo read failed or returned invalid JSON. Response content was withheld."); }
    }

    private static object Context(IReadOnlyList<int> companyIds) => new { allowed_company_ids = companyIds, lang = "en_US", active_test = true };
    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static OdooException InvalidScope() => new("scope", "The requested or returned company/account/date scope is not permitted or does not match.");
    private static OdooException InvalidResponse() => new("response", "Odoo returned an unsupported response shape, identifier or ordering.");
    private static OdooException ResponseLimit() => new("response_limit", "Odoo exceeded the 2 MiB response limit; no result was accepted.");
    private static JsonElement Field(JsonElement row, string field) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(field, out var value) ? value : throw InvalidResponse();
    private static int PositiveId(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var id) && id > 0 ? id : throw InvalidResponse();
    private static int Id(JsonElement row, string field) => PositiveId(Field(row, field));
    private static string Text(JsonElement row, string field)
    {
        var value = Field(row, field);
        return value.ValueKind == JsonValueKind.String && value.GetString() is { Length: <= 4096 } text ? text : throw InvalidResponse();
    }
    private static int Relation(JsonElement row, string field)
    {
        var value = Field(row, field);
        return value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 2 ? PositiveId(value[0]) : throw InvalidResponse();
    }
    private static int[] Ids(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 1000) throw InvalidResponse();
        var ids = value.EnumerateArray().Select(PositiveId).ToArray();
        if (ids.Distinct().Count() != ids.Length) throw InvalidResponse();
        return ids;
    }
    private static JsonElement[] Rows(JsonElement value, int max)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > max) throw InvalidResponse();
        return value.EnumerateArray().ToArray();
    }
    private static void CheckOrder(JsonElement[] rows, int after)
    {
        foreach (var row in rows)
        {
            var id = Id(row, "id");
            if (id <= after) throw InvalidResponse();
            after = id;
        }
    }
    private static OdooCompany Company(JsonElement row)
    {
        var currencyId = Relation(row, "currency_id");
        var label = Field(row, "currency_id")[1];
        if (label.ValueKind != JsonValueKind.String) throw InvalidResponse();
        return new(Id(row, "id"), Text(row, "name"), currencyId, label.GetString()!);
    }
    private static OdooAccount Account(JsonElement row, string companyField, int companyId)
    {
        var inCompany = companyField == "company_ids" ? Ids(Field(row, companyField)).Contains(companyId) : Relation(row, companyField) == companyId;
        if (!inCompany) throw InvalidScope();
        int? currency = Field(row, "currency_id").ValueKind == JsonValueKind.False ? null : Relation(row, "currency_id");
        return new(Id(row, "id"), Text(row, "code"), Text(row, "name"), Text(row, "account_type"), currency);
    }
    private static decimal Money(JsonElement row, string field)
    {
        var value = Field(row, field);
        if (value.ValueKind != JsonValueKind.Number || !SarMoney.TryParseExact(value.GetRawText(), out var amount))
            throw new OdooException("money", "Odoo returned an amount outside the exact SAR precision/range contract.");
        return amount;
    }
}
