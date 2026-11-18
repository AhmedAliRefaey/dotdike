using System.Security.Claims;
using System.Text.Json;
using Dike.Infrastructure.Odoo;
using Dike.Web.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

internal static class OdooWebChecks
{
    public static async Task RunAsync()
    {
        await using var f = await Fixture.CreateAsync();
        var a = f.Principal("a");
        var b = f.Principal("b");
        var api = f.Connector;
        var period = OdooPeriod.Parse("month", "2026-11");
        var passed = 0;
        async Task Check(string name, Func<Task> action)
        { await action(); passed++; Console.WriteLine($"PASS Odoo web: {name}"); }
        async Task Fetch(params int[] accounts) => await api.FetchAsync(a, (await api.ReadAsync(a)).CatalogVersion, accounts, period, 10);
        async Task EditUser(Action<ApplicationUser> action)
        {
            var user = await f.Db.Users.SingleAsync(x => x.Id == "a");
            action(user); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        }

        await Check("anonymous and incomplete session identities fail closed", async () =>
        {
            await Reject<UnauthorizedAccessException>(() => api.ReadAsync(new ClaimsPrincipal()));
            await Reject<UnauthorizedAccessException>(() => api.ReadAsync(new(new ClaimsIdentity(a.Claims))));
            await Reject<UnauthorizedAccessException>(() => api.ReadAsync(new(new ClaimsIdentity(a.Claims.Where(x => x.Type != "AspNet.Identity.SecurityStamp"), "test"))));
            await Reject<UnauthorizedAccessException>(() => api.ReadAsync(f.Principal("a", "old")));
        });
        await Check("forged Administrator role does not elevate a viewer", async () =>
            await Reject<UnauthorizedAccessException>(() => api.ReadAsync(f.Principal("viewer"))));
        await Check("workspace is resolved from database, ignoring company claims", async () =>
        {
            var forged = new ClaimsPrincipal(new ClaimsIdentity(a.Claims.Append(new Claim("CompanyId", f.B.ToString())), "test"));
            Require((await api.ReadAsync(forged)).DikeCompanyName == "Fixture A");
        });
        await Check("local key belongs only to the explicitly bound development workspace", async () =>
        {
            Require((await api.ReadAsync(a)).CanUseLocalConnection && !(await api.ReadAsync(b)).CanUseLocalConnection);
            await Reject<OdooException>(() => api.ConfigureAsync(b, null, null, true), "local_binding");
            f.Environment.EnvironmentName = "Production";
            Require(!(await api.ReadAsync(a)).CanUseLocalConnection);
            await Reject<OdooException>(() => api.ConfigureAsync(a, null, null, true), "local_binding");
            f.Environment.EnvironmentName = "Development";
        });
        await Check("an incomplete company profile cannot connect", async () =>
        {
            await f.Db.Companies.Where(x => x.Id == f.A).ExecuteUpdateAsync(s => s.SetProperty(x => x.SetupComplete, false));
            await Reject<OdooException>(() => api.ConfigureAsync(a, "fixture-key", null, false), "setup");
            await f.Db.Companies.Where(x => x.Id == f.A).ExecuteUpdateAsync(s => s.SetProperty(x => x.SetupComplete, true));
        });
        await Check("unconfigured workspace cannot load accounts", async () =>
            await Reject<OdooException>(() => api.LoadAccountsAsync(a, 1), "not_configured"));
        await Check("verified local connection exposes a credential-free projection and encrypted file", async () =>
        {
            await api.ConfigureAsync(a, null, null, true);
            var view = await api.ReadAsync(a);
            Require(view.Configured && view.ApiUserId == 2 && view.Companies.Single().Id == 1 && view.MoreCompanies);
            var json = JsonSerializer.Serialize(view);
            var file = await File.ReadAllTextAsync(f.StateFile(f.A));
            Require(!json.Contains(Fixture.Key) && !json.Contains("ApiKey") && !file.Contains(Fixture.Key) && !file.Contains("NApp"));
        });
        await Check("another company sees neither connection nor loaded catalog", async () =>
        {
            var view = await api.ReadAsync(b);
            Require(!view.Configured && view.Companies.Count == 0 && view.Preview == null);
        });
        await Check("copying encrypted state to another company cannot disclose it", async () =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(f.StateFile(f.B))!);
            File.Copy(f.StateFile(f.A), f.StateFile(f.B));
            await Reject<OdooException>(() => api.ReadAsync(b), "storage");
            File.Delete(f.StateFile(f.B));
        });
        await Check("corrupt encrypted state fails closed without overwrite", async () =>
        {
            var original = await File.ReadAllTextAsync(f.StateFile(f.A));
            await File.WriteAllTextAsync(f.StateFile(f.A), "corrupt");
            await Reject<OdooException>(() => api.ReadAsync(a), "storage");
            Require(await File.ReadAllTextAsync(f.StateFile(f.A)) == "corrupt");
            await File.WriteAllTextAsync(f.StateFile(f.A), original);
        });
        await Check("unloaded company selection is rejected before network access", async () =>
        {
            var count = f.Factory.Calls;
            await Reject<OdooException>(() => api.LoadAccountsAsync(a, 999), "selection");
            Require(f.Factory.Calls == count);
        });
        await Check("company cursor appends only the next verified page", async () =>
        {
            await api.MoreCompaniesAsync(a);
            var state = await api.ReadAsync(a);
            Require(state.Companies.Select(x => x.Id).SequenceEqual(new[] { 1, 2 }) && !state.MoreCompanies);
            await Reject<OdooException>(() => api.MoreCompaniesAsync(a), "selection");
        });
        await Check("account loading requests bank accounts and starts a new selection version", async () =>
        {
            await api.LoadAccountsAsync(a, 1);
            var state = await api.ReadAsync(a);
            Require(state.Accounts.Count == 2 && state.MoreAccounts && state.CatalogVersion != Guid.Empty && f.Factory.Client.BankOnly);
        });
        await Check("account pagination invalidates stale selections and excludes foreign currency", async () =>
        {
            var old = (await api.ReadAsync(a)).CatalogVersion;
            await api.LoadAccountsAsync(a, 1, true);
            Require((await api.ReadAsync(a)).Accounts.Count == 3);
            await Reject<OdooException>(() => api.FetchAsync(a, old, [118], period, 10), "stale_selection");
            var count = f.Factory.Calls;
            await Reject<OdooException>(() => Fetch(120), "selection");
            Require(f.Factory.Calls == count);
        });
        await Check("tampered account, duplicate, count, period and row limits fail before network", async () =>
        {
            var version = (await api.ReadAsync(a)).CatalogVersion;
            var count = f.Factory.Calls;
            foreach (var ids in new int[][] { [], [999], [118, 118], [118, 119, 120, 121, 122, 123] })
                await Reject<OdooException>(() => api.FetchAsync(a, version, ids, period, 10), "selection");
            await Reject<OdooException>(() => api.FetchAsync(a, version, [118], new(new(2026, 10, 1), new(2026, 11, 30)), 10), "selection");
            await Reject<OdooException>(() => api.FetchAsync(a, version, [118], period, 51), "selection");
            Require(count == f.Factory.Calls);
        });
        await Check("multiple selected accounts publish together with scoped sample metadata", async () =>
        {
            await Fetch(118, 119);
            var preview = (await api.ReadAsync(a)).Preview!;
            Require(preview.Samples.Count == 2 && preview.Samples.All(x => x.CompanyId == 1 && x.From == period.From && x.To == period.To && x.SampleOnly));
        });
        await Check("failure on second account retains the entire previous saved result", async () =>
        {
            var original = await File.ReadAllTextAsync(f.StateFile(f.A));
            f.Factory.Client.FailAccount = 119;
            await Reject<OdooException>(() => Fetch(118, 119), "http_403");
            Require(original == await File.ReadAllTextAsync(f.StateFile(f.A)));
            f.Factory.Client.FailAccount = null;
        });
        await Check("changed upstream API user cannot replace a saved sample", async () =>
        {
            var old = (await api.ReadAsync(a)).Preview!.RunId;
            f.Factory.Client.ApiUserId = 3;
            await Reject<OdooException>(() => Fetch(118), "selection");
            Require((await api.ReadAsync(a)).Preview!.RunId == old);
            f.Factory.Client.ApiUserId = 2;
        });
        await Check("rejected replacement credentials preserve the existing connection and sample", async () =>
        {
            var original = await File.ReadAllTextAsync(f.StateFile(f.A));
            await Reject<OdooException>(() => api.ConfigureAsync(a, "bad\r\nkey", null, false), "configuration");
            Require(original == await File.ReadAllTextAsync(f.StateFile(f.A)));
        });
        await Check("company switch clears previous accounts and samples", async () =>
        {
            await api.LoadAccountsAsync(a, 2);
            var state = await api.ReadAsync(a);
            Require(state.SelectedCompanyId == 2 && state.Preview == null && state.Accounts.Single().Id == 218);
            await api.LoadAccountsAsync(a, 1);
            await Fetch(118);
        });
        await Check("live role revocation and lockout reject an otherwise valid session", async () =>
        {
            await f.Db.UserRoles.Where(x => x.UserId == "a").ExecuteDeleteAsync();
            await Reject<UnauthorizedAccessException>(() => api.ReadAsync(a));
            f.Db.UserRoles.Add(new() { UserId = "a", RoleId = "admin" }); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
            await EditUser(u => { u.LockoutEnabled = true; u.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5); });
            await Reject<UnauthorizedAccessException>(() => Fetch(118));
            await EditUser(u => u.LockoutEnd = null);
        });
        await Check("revocation during an upstream read cannot publish its result", async () =>
        {
            var original = await File.ReadAllTextAsync(f.StateFile(f.A));
            f.Factory.Client.BeforeSample = () => EditUser(u => u.SecurityStamp = "revoked");
            var version = (await api.ReadAsync(a)).CatalogVersion;
            await Reject<UnauthorizedAccessException>(() => api.FetchAsync(a, version, [118], period, 10));
            Require(original == await File.ReadAllTextAsync(f.StateFile(f.A)));
            f.Factory.Client.BeforeSample = null;
            await EditUser(u => u.SecurityStamp = "stamp-a");
        });
        await Check("moving companies during an upstream read cannot publish to either workspace", async () =>
        {
            var original = await File.ReadAllTextAsync(f.StateFile(f.A));
            f.Factory.Client.BeforeSample = () => EditUser(u => u.CompanyId = f.B);
            var version = (await api.ReadAsync(a)).CatalogVersion;
            await Reject<UnauthorizedAccessException>(() => api.FetchAsync(a, version, [118], period, 10));
            Require(original == await File.ReadAllTextAsync(f.StateFile(f.A)) && !File.Exists(f.StateFile(f.B)));
            f.Factory.Client.BeforeSample = null;
            await EditUser(u => u.CompanyId = f.A);
        });
        await Check("successfully replacing credentials clears prior accounts and preview", async () =>
        {
            await api.ConfigureAsync(a, "replacement-fixture-key", null, false);
            var state = await api.ReadAsync(a);
            Require(state.Configured && state.Accounts.Count == 0 && state.SelectedCompanyId == null && state.Preview == null);
        });
        await Check("missing or invalid endpoints fail before network calls and preserve saved state", async () =>
        {
            var original = await File.ReadAllTextAsync(f.StateFile(f.A));
            var calls = f.Factory.Calls;
            foreach (var endpoint in new string?[] { null, "http://erp.example.test", "https://erp.example.test/odoo" })
            {
                f.Configuration["Odoo:BaseUrl"] = endpoint;
                await Reject<OdooException>(() => api.ConfigureAsync(a, "fixture-key", null, false), "endpoint_configuration");
                await Reject<OdooException>(() => api.ReadAsync(a), "endpoint_configuration");
            }
            Require(f.Factory.Calls == calls && original == await File.ReadAllTextAsync(f.StateFile(f.A)));
            f.Configuration["Odoo:BaseUrl"] = Fixture.BaseUrl;
        });
        await Check("endpoint change hides old data and rejects reads without forwarding credentials", async () =>
        {
            await api.LoadAccountsAsync(a, 1); await Fetch(118);
            var version = (await api.ReadAsync(a)).CatalogVersion;
            var original = await File.ReadAllTextAsync(f.StateFile(f.A));
            var calls = f.Factory.Calls;
            f.Configuration["Odoo:BaseUrl"] = "https://other-erp.example.test:8443";
            var view = await api.ReadAsync(a);
            Require(view.BaseUrl == "https://other-erp.example.test:8443" && !view.Configured && view.NeedsReconnect && view.Companies.Count == 0 && view.Accounts.Count == 0 && view.Preview == null);
            await Reject<OdooException>(() => api.LoadAccountsAsync(a, 1), "endpoint_changed");
            await Reject<OdooException>(() => api.MoreCompaniesAsync(a), "endpoint_changed");
            await Reject<OdooException>(() => api.FetchAsync(a, version, [118], period, 10), "endpoint_changed");
            Require(f.Factory.Calls == calls && original == await File.ReadAllTextAsync(f.StateFile(f.A)));
        });
        await Check("explicit verification binds new credentials to the configured endpoint", async () =>
        {
            await api.ConfigureAsync(a, "new-server-fixture-key", null, false);
            var view = await api.ReadAsync(a);
            Require(view.Configured && !view.NeedsReconnect && view.Preview == null && view.Accounts.Count == 0 && f.Factory.LastEndpoint == "https://other-erp.example.test:8443/");
        });
        await Check("endpoint change during a read cannot publish under a new destination", async () =>
        {
            await api.LoadAccountsAsync(a, 1);
            var version = (await api.ReadAsync(a)).CatalogVersion;
            var original = await File.ReadAllTextAsync(f.StateFile(f.A));
            f.Factory.Client.BeforeSample = () => { f.Configuration["Odoo:BaseUrl"] = Fixture.BaseUrl; return Task.CompletedTask; };
            await Reject<OdooException>(() => api.FetchAsync(a, version, [118], period, 10), "endpoint_changed");
            Require(original == await File.ReadAllTextAsync(f.StateFile(f.A)));
            f.Factory.Client.BeforeSample = null;
        });
        await Check("legacy unbound connection is retained until explicit verification", async () =>
        {
            var saved = (await f.Store.ReadAsync(f.A, default))!;
            saved.BaseUrl = null;
            await f.Store.WriteAsync(f.A, saved, default);
            var original = await File.ReadAllTextAsync(f.StateFile(f.A));
            var calls = f.Factory.Calls;
            var view = await api.ReadAsync(a);
            Require(!view.Configured && view.NeedsReconnect && view.Preview == null);
            await Reject<OdooException>(() => api.LoadAccountsAsync(a, 1), "endpoint_changed");
            Require(calls == f.Factory.Calls && original == await File.ReadAllTextAsync(f.StateFile(f.A)));
            await api.ConfigureAsync(a, null, null, true);
            Require((await api.ReadAsync(a)).Configured && f.Factory.LastEndpoint == Fixture.BaseUrl + "/");
        });
        Console.WriteLine($"Odoo web checks: {passed} passed with isolated SQLite, encrypted storage and fake read-only API.");
    }

    private static void Require(bool result) { if (!result) throw new InvalidOperationException("Odoo web invariant failed."); }
    private static async Task Reject<T>(Func<Task> action, string? code = null) where T : Exception
    {
        try { await action(); }
        catch (T error) { if (code != null) Require(error is OdooException odoo && odoo.Code == code); return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class Factory : IOdooClientFactory
    {
        public FakeClient Client { get; } = new();
        public int Calls { get; private set; }
        public string? LastEndpoint { get; private set; }
        public IOdooReadOnlyClient Create(OdooConnection connection) { Calls++; LastEndpoint = connection.BaseUri.AbsoluteUri; return Client; }
    }
    private sealed class FakeClient : IOdooReadOnlyClient
    {
        public int ApiUserId { get; set; } = 2;
        public int? FailAccount { get; set; }
        public bool BankOnly { get; private set; }
        public Func<Task>? BeforeSample { get; set; }
        public Task<OdooAccess> AuthenticateAsync(CancellationToken ct = default) => Task.FromResult(new OdooAccess(ApiUserId, [1, 2]));
        public Task<OdooCatalog<OdooCompany>> ListCompaniesAsync(int afterId = 0, CancellationToken ct = default) => Task.FromResult(
            new OdooCatalog<OdooCompany>(ApiUserId, [new(afterId == 0 ? 1 : 2, afterId == 0 ? "NApp fixture" : "Other fixture", 151, "SAR")], afterId == 0, afterId == 0 ? 1 : null));
        public Task<OdooCatalog<OdooAccount>> ListAccountsAsync(int companyId, int afterId = 0, CancellationToken ct = default, bool bankOnly = false)
        {
            BankOnly = bankOnly;
            return Task.FromResult(new OdooCatalog<OdooAccount>(ApiUserId, companyId == 2 ? [new(218, "218", "Other company bank", "asset_cash", 151)] :
                afterId == 0 ? [new(118, "118", "Bank A", "asset_cash", null), new(119, "119", "Bank B", "asset_cash", 151)] :
                [new(120, "120", "Foreign bank", "asset_cash", 99)], companyId == 1 && afterId == 0, companyId == 1 && afterId == 0 ? 119 : null));
        }
        public async Task<OdooGlSample> ReadPostedSampleAsync(int companyId, int accountId, DateOnly from, DateOnly to, int limit = 10, CancellationToken ct = default)
        {
            if (BeforeSample != null) await BeforeSample();
            if (FailAccount == accountId) throw new OdooException("http_403", "fixture denied");
            return new(ApiUserId, companyId, accountId, from, to, limit, [new(1, 1, 11, from, 0, 10, -10)], false);
        }
        public void Dispose() { }
    }
    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Dike.Web";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public const string Key = "synthetic-local-key-for-odoo-web-tests";
        public const string BaseUrl = "https://odoo-web.example.test";
        public Guid A { get; } = Guid.NewGuid();
        public Guid B { get; } = Guid.NewGuid();
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "dike-odoo-tests-" + Guid.NewGuid().ToString("N"));
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public CompanyIdentityDbContext Db { get; private set; } = null!;
        public CompanyOdoo Connector { get; private set; } = null!;
        public EnvironmentStub Environment { get; } = new();
        public Factory Factory { get; } = new();
        public IConfigurationRoot Configuration { get; private set; } = null!;
        public CompanyOdooStore Store { get; private set; } = null!;
        public string StateFile(Guid id) => Path.Combine(Root, id.ToString("N"), "state.enc");
        public ClaimsPrincipal Principal(string id, string? stamp = null) => new(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, id), new("AspNet.Identity.SecurityStamp", stamp ?? "stamp-" + id), new(ClaimTypes.Role, "Administrator")], "test"));
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            await f.connection.OpenAsync();
            f.Db = new(new DbContextOptionsBuilder<CompanyIdentityDbContext>().UseSqlite(f.connection).Options);
            await f.Db.Database.EnsureCreatedAsync();
            f.Db.Companies.AddRange(new Company { Id = f.A, Name = "Fixture A", SetupComplete = true, ConnectionProvider = ConnectionProviders.Odoo },
                new Company { Id = f.B, Name = "Fixture B", SetupComplete = true, ConnectionProvider = ConnectionProviders.Odoo });
            f.Db.Roles.Add(new() { Id = "admin", Name = "Administrator", NormalizedName = "ADMINISTRATOR" });
            foreach (var id in new[] { "a", "b", "viewer" })
            {
                f.Db.Users.Add(new() { Id = id, CompanyId = id == "b" ? f.B : f.A, UserName = id, NormalizedEmail = id, SecurityStamp = "stamp-" + id });
                if (id != "viewer") f.Db.UserRoles.Add(new() { UserId = id, RoleId = "admin" });
            }
            await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
            var config = f.Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Odoo:StoragePath"] = f.Root, ["Odoo:LocalCompanyId"] = f.A.ToString(), ["Odoo:ApiKey"] = Key, ["Odoo:BaseUrl"] = BaseUrl }).Build();
            var access = new CompanyAdministrator(f.Db, Options.Create(new IdentityOptions()), new UpperInvariantLookupNormalizer(), TimeProvider.System);
            f.Store = new(new EphemeralDataProtectionProvider(), f.Environment, config);
            f.Connector = new(access, f.Store, f.Factory, config, f.Environment, TimeProvider.System);
            return f;
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync(); await connection.DisposeAsync();
            var path = Path.GetFullPath(Root);
            if (Path.GetDirectoryName(path) == Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) &&
                Path.GetFileName(path).StartsWith("dike-odoo-tests-", StringComparison.Ordinal) && Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
