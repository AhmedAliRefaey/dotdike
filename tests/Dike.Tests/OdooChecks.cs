using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dike.Infrastructure.Odoo;

internal static class OdooChecks
{
    private const string FakeKey = "synthetic-odoo-api-key";
    private const string TestBaseUrl = "https://odoo-fixture.example.test";
    private static readonly DateOnly From = new(2026, 11, 1);
    private static readonly DateOnly To = new(2026, 11, 7);
    private static int checks;
    private sealed record Request(string Path, JsonElement Body, string? Authorization, string? Database);
    private sealed class FakeServer : HttpMessageHandler
    {
        internal readonly List<Request> Requests = [];
        internal bool LegacyAccountCompany;
        internal object[] Companies = [Company(1)];
        internal object[] Accounts = [Account()];
        internal object[] Lines = [Line()];
        internal Func<string, JsonElement, HttpResponseMessage?>? Override;
        internal string ExpectedBaseUrl = TestBaseUrl;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (request.Method != HttpMethod.Post || request.RequestUri?.GetLeftPart(UriPartial.Authority) != ExpectedBaseUrl)
                throw new Exception("Unexpected Odoo destination or verb");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var path = request.RequestUri.AbsolutePath;
            Requests.Add(new(path, body.RootElement.Clone(), request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Odoo-Database", out var database) ? database.Single() : null));
            var replacement = Override?.Invoke(path, body.RootElement);
            if (replacement != null) return replacement;
            object result = path switch
            {
                "/json/2/res.users/context_get" => new { uid = 7, allowed_company_ids = new[] { 1 } },
                "/json/2/res.users/read" => new[] { new { id = 7, company_ids = new[] { 1, 2 } } },
                "/json/2/res.company/search_read" => Companies,
                "/json/2/res.currency/read" => new[] { new { id = 3, name = "SAR" } },
                "/json/2/account.account/fields_get" => AccountSchema(LegacyAccountCompany),
                "/json/2/account.account/search_read" => Accounts,
                "/json/2/account.move.line/fields_get" => LineSchema(),
                "/json/2/account.move.line/search_read" => Lines,
                _ => throw new Exception("Unexpected or mutating Odoo method")
            };
            return Json(result);
        }
    }

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private static object Company(int id) => new { id, name = "Synthetic company", currency_id = new object[] { 3, "SAR" } };
    private static Dictionary<string, object?> Account(int id = 10) => new()
    {
        ["id"] = id, ["code"] = "1001", ["name"] = "Synthetic bank", ["account_type"] = "asset_cash",
        ["currency_id"] = false, ["company_ids"] = new[] { 1 }
    };
    private static Dictionary<string, object?> Line(int id = 101) => new()
    {
        ["id"] = id, ["company_id"] = new object[] { 1, "Company" }, ["company_currency_id"] = new object[] { 3, "SAR" },
        ["account_id"] = new object[] { 10, "Bank" }, ["move_id"] = new object[] { 50, "Entry" },
        ["journal_id"] = new object[] { 5, "Bank" }, ["date"] = "2026-11-03", ["parent_state"] = "posted",
        ["debit"] = 125.50m, ["credit"] = 0m, ["balance"] = 125.50m
    };
    private static object Metadata(string type, string? relation = null) => new { type, relation };
    private static Dictionary<string, object> AccountSchema(bool legacy) => new()
    {
        ["id"] = Metadata("integer"), ["name"] = Metadata("char"), ["code"] = Metadata("char"),
        ["account_type"] = Metadata("selection"), ["currency_id"] = Metadata("many2one", "res.currency"),
        [legacy ? "company_id" : "company_ids"] = Metadata(legacy ? "many2one" : "many2many", "res.company")
    };
    private static Dictionary<string, object> LineSchema() => new()
    {
        ["id"] = Metadata("integer"), ["company_id"] = Metadata("many2one", "res.company"),
        ["company_currency_id"] = Metadata("many2one", "res.currency"), ["account_id"] = Metadata("many2one", "account.account"),
        ["move_id"] = Metadata("many2one", "account.move"), ["journal_id"] = Metadata("many2one", "account.journal"),
        ["date"] = Metadata("date"), ["parent_state"] = Metadata("selection"),
        ["debit"] = Metadata("monetary"), ["credit"] = Metadata("monetary"), ["balance"] = Metadata("monetary")
    };
    private static OdooReadOnlyClient Client(FakeServer server, string? database = null) => new(new(server.ExpectedBaseUrl, FakeKey, database), server);
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++;
        Console.WriteLine("PASS Odoo " + name);
    }
    private static async Task Reject(Func<Task> run, string name, string? code = null)
    {
        try { await run(); }
        catch (OdooException error)
        {
            Check(!error.ToString().Contains(FakeKey) && !error.ToString().Contains("PRIVATE_BODY") && (code == null || code == error.Code), name);
            return;
        }
        throw new Exception("Expected Odoo rejection: " + name);
    }
    private static async Task RejectLine(Action<Dictionary<string, object?>> change, string name)
    {
        var row = Line();
        change(row);
        var server = new FakeServer { Lines = [row] };
        using var client = Client(server);
        await Reject(() => client.ReadPostedSampleAsync(1, 10, From, To), name);
    }

    public static async Task RunAsync()
    {
        foreach (var destination in new string?[] { null, "", " ", "odoo.example.test", "http://odoo.example.test", "https://odoo.example.test/odoo", "https://user@odoo.example.test", "https://odoo.example.test:0", "https://odoo.example.test?key=x", "https://odoo.example.test/#fragment", "https://odoo.example.test/\n", "https://odoo.example.test\\" })
            await Reject(() => { _ = new OdooConnection(destination, FakeKey); return Task.CompletedTask; }, "reject invalid or missing endpoint", "endpoint_configuration");
        foreach (var destination in new[] { TestBaseUrl, "https://another.odoo.com", "https://erp.example.test:8443" })
        {
            var configuredServer = new FakeServer { ExpectedBaseUrl = destination };
            using var configuredClient = Client(configuredServer);
            await configuredClient.AuthenticateAsync();
            Check(configuredServer.Requests.Count == 2, "requests use the configured HTTPS endpoint");
        }
        Check(new OdooConnection("https://ODOO-FIXTURE.example.test:443/", FakeKey).BaseUri.AbsoluteUri == TestBaseUrl + "/", "endpoint normalization retains the configured origin");
        await Reject(() => { _ = new OdooConnection(TestBaseUrl, "key\r\nInjected: value"); return Task.CompletedTask; }, "reject header injection", "configuration");
        Check(!new OdooConnection(TestBaseUrl, FakeKey).ToString()!.Contains(FakeKey), "connection ToString hides credentials");
        using (var handler = OdooReadOnlyClient.CreateHandler()) Check(!handler.AllowAutoRedirect && !handler.UseCookies, "production transport disables redirects and cookies");

        var server = new FakeServer();
        using (var client = Client(server))
        {
            var access = await client.AuthenticateAsync();
            Check(access.UserId == 7 && access.PermittedCompanyIds.SequenceEqual([1, 2]), "read own permitted companies instead of default context only");
            var companies = await client.ListCompaniesAsync();
            Check(companies.AuthenticatedUserId == 7 && companies.Rows.Single().Id == 1, "list authenticated companies");
            var accounts = await client.ListAccountsAsync(1);
            Check(accounts.Rows.Single().Id == 10, "list company-scoped accounts");
            await client.ListAccountsAsync(1, bankOnly: true);
            Check(server.Requests.Last().Body.GetProperty("domain").EnumerateArray().Any(term => term[0].GetString() == "account_type" && term[2].GetString() == "asset_cash"),
                "interactive bank selection filters eligible accounts at the source");
            var accountRequest = server.Requests.Last(x => x.Path.EndsWith("account.account/search_read"));
            Check(accountRequest.Body.GetProperty("context").GetProperty("allowed_company_ids").EnumerateArray().Select(x => x.GetInt32()).SequenceEqual([1]) &&
                accountRequest.Body.GetProperty("domain")[0][0].GetString() == "company_ids", "inspect Odoo 19 account schema and pin company context");
            var sample = await client.ReadPostedSampleAsync(1, 10, From, To);
            Check(sample.Rows.Single().Debit == 125.50m && sample.SampleOnly && !sample.HasMore, "read exact SAR posted sample without acceptance claim");
            var glRequest = server.Requests.Last();
            var domain = glRequest.Body.GetProperty("domain");
            Check(domain[0][2].GetInt32() == 1 && domain[1][2].GetInt32() == 10 && domain[2][2].GetString() == "posted" &&
                domain[3][2].GetString() == "2026-11-01" && domain[4][2].GetString() == "2026-11-07" &&
                glRequest.Body.GetProperty("limit").GetInt32() == 11 && glRequest.Body.GetProperty("order").GetString() == "id asc",
                "request explicit posted/company/account/inclusive dates and limit-plus-one");
            Check(server.Requests.All(x => x.Authorization == "bearer " + FakeKey && x.Database == null), "bearer headers on fixed reads without guessed database");
            var before = server.Requests.Count;
            await Reject(() => client.ReadPostedSampleAsync(1, 10, From, From.AddDays(31)), "reject oversized custom period before I/O");
            await Reject(() => client.ReadPostedSampleAsync(1, 10, From, To, 51), "reject oversized sample before I/O");
            Check(server.Requests.Count == before, "invalid filters perform no requests");
            await Reject(() => client.ListAccountsAsync(999), "reject company outside user's assigned companies");
        }
        var legacyAccount = Account(); legacyAccount.Remove("company_ids"); legacyAccount["company_id"] = new object[] { 1, "Company" };
        using (var client = Client(new FakeServer { LegacyAccountCompany = true, Accounts = [legacyAccount] }))
            Check((await client.ListAccountsAsync(1)).Rows.Count == 1, "support verified many2one account-company schema");

        server = new FakeServer { Lines = [Line(101), Line(102)] };
        using (var client = Client(server, "verified_db"))
        {
            var sample = await client.ReadPostedSampleAsync(1, 10, From, To, 1);
            Check(sample.HasMore && sample.Rows.Count == 1 && sample.Rows[0].Id == 101, "sample truncation is explicit");
            Check(server.Requests.All(x => x.Database == "verified_db"), "optional verified database header");
        }
        using (var client = Client(new FakeServer { Lines = [] }))
            Check((await client.ReadPostedSampleAsync(1, 10, From, To)).SampleOnly, "empty sample never proves period completeness");
        using (var client = Client(new FakeServer { Accounts = Enumerable.Range(1, 101).Select(id => (object)Account(id)).ToArray() }))
        {
            var page = await client.ListAccountsAsync(1);
            Check(page.HasMore && page.Rows.Count == 100 && page.NextAfterId == 100, "catalog pagination exposes continuation");
        }
        using (var client = Client(new FakeServer { Accounts = [Account(10)] }))
            await Reject(() => client.ListAccountsAsync(1, 10), "reject non-advancing catalog cursor");
        using (var client = Client(new FakeServer { Lines = [Line(101), Line(101)] }))
            await Reject(() => client.ReadPostedSampleAsync(1, 10, From, To), "reject duplicate journal items");

        await RejectLine(row => row["company_id"] = new object[] { 2, "Other" }, "reject foreign-company row");
        await RejectLine(row => row["account_id"] = new object[] { 11, "Other" }, "reject foreign-account row");
        await RejectLine(row => row["company_currency_id"] = new object[] { 4, "Other" }, "reject changed company currency");
        await RejectLine(row => row["parent_state"] = "draft", "reject draft row");
        await RejectLine(row => row["date"] = "2026-10-31", "reject out-of-period row");
        await RejectLine(row => row["debit"] = 0.001m, "reject fractional SAR precision");
        await RejectLine(row => row["debit"] = 100000000000000000m, "reject SAR overflow");
        await RejectLine(row => row["balance"] = 124m, "reject inconsistent balance");
        await RejectLine(row => row["credit"] = 1m, "reject unsupported double-sided line");
        await RejectLine(row => row["debit"] = -1m, "reject unsupported negative debit");
        server = new FakeServer { Override = (path, _) => path.EndsWith("account.move.line/search_read")
            ? new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new[] { Line() }).Replace("125.50", "125.50000000000000000000000000000001"), Encoding.UTF8, "application/json") } : null };
        using (var client = Client(server)) await Reject(() => client.ReadPostedSampleAsync(1, 10, From, To), "reject precision loss before decimal rounding");
        var foreignAccount = Account(); foreignAccount["company_ids"] = new[] { 2 };
        using (var client = Client(new FakeServer { Accounts = [foreignAccount] }))
            await Reject(() => client.ReadPostedSampleAsync(1, 10, From, To), "reject foreign account membership");
        var nonbank = Account(); nonbank["account_type"] = "expense";
        using (var client = Client(new FakeServer { Accounts = [nonbank] }))
            await Reject(() => client.ReadPostedSampleAsync(1, 10, From, To), "reject non-bank account for bank GL test");
        server = new FakeServer { Override = (path, _) => path.EndsWith("res.currency/read") ? Json(new[] { new { id = 3, name = "USD" } }) : null };
        using (var client = Client(server)) await Reject(() => client.ReadPostedSampleAsync(1, 10, From, To), "reject non-SAR company");
        var missingSchema = LineSchema(); missingSchema.Remove("balance");
        server = new FakeServer { Override = (path, _) => path.EndsWith("account.move.line/fields_get") ? Json(missingSchema) : null };
        using (var client = Client(server)) await Reject(() => client.ReadPostedSampleAsync(1, 10, From, To), "fail closed for unsupported schema");
        Check(server.Requests.All(x => !x.Path.EndsWith("account.move.line/search_read")), "unsupported schema prevents GL fetch");

        foreach (var status in new[] { 401, 403, 302, 429, 500 })
        {
            server = new FakeServer { Override = (_, _) => new((HttpStatusCode)status) { Content = new StringContent("PRIVATE_BODY " + FakeKey) } };
            using var client = Client(server);
            await Reject(() => client.AuthenticateAsync(), "sanitize HTTP " + status);
            Check(server.Requests.Count == 1, "no automatic redirect or retry for HTTP " + status);
        }
        server = new FakeServer { Override = (_, _) => new(HttpStatusCode.OK) { Content = new StringContent("PRIVATE_BODY " + FakeKey) } };
        using (var client = Client(server)) await Reject(() => client.AuthenticateAsync(), "sanitize invalid JSON");
        server = new FakeServer { Override = (_, _) => new(HttpStatusCode.OK) { Content = new StringContent(new string('x', 2 * 1024 * 1024 + 1)) } };
        using (var client = Client(server)) await Reject(() => client.AuthenticateAsync(), "bound response size", "response_limit");
        using (var client = Client(new FakeServer()))
        {
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await client.AuthenticateAsync(cancelled.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Check(true, "propagate cancellation without credentials"); }
        }
        Check(OdooPeriod.Parse("week", "2026-12-29").To == new DateOnly(2027, 1, 4), "weekly range crosses year correctly");
        Check(OdooPeriod.Parse("month", "2028-02") == new OdooPeriod(new(2028, 2, 1), new(2028, 2, 29)), "monthly range handles leap years");
        Check(OdooPeriod.Parse("range", "2026-11-01", "2026-11-17").To == new DateOnly(2026, 11, 17), "custom date filter is inclusive");
        await Reject(() => { OdooPeriod.Parse("range", "2026-11-01", "2026-12-02"); return Task.CompletedTask; }, "custom range bound enforced");
        await Reject(() => { OdooPeriod.Parse("range", "2026-11-17", "2026-11-01"); return Task.CompletedTask; }, "inverted date range rejected");
        Console.WriteLine($"All {checks} Odoo read-only fixture checks passed. No live Odoo requests.");
    }
}
