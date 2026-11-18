using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dike.Infrastructure;
using Dike.Infrastructure.NetSuite;
using Dike.Web.Authentication;
using Dike.Web.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class HostedStorageChecks
{
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }

    public static async Task RunAsync()
    {
        // This opt-in test can only create/drop a uniquely named database on a local test server.
        var testConnection = Environment.GetEnvironmentVariable("DIKE_TEST_POSTGRES")
            ?? throw new InvalidOperationException("Set DIKE_TEST_POSTGRES to a local PostgreSQL test server with permission to create databases.");
        var server = new NpgsqlConnectionStringBuilder(PostgresConnection.Parse(testConnection, hosted: false));
        if (server.Host is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException("Hosted storage checks require a loopback PostgreSQL test server.");
        var database = "dike_hosted_checks_" + Guid.NewGuid().ToString("N");
        server.Database = "postgres";
        await using var admin = new NpgsqlConnection(server.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();
        server.Database = database;
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Dike isolated storage checks", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        const string certificatePassword = "Isolated-test-only!";
        var config = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Identity"] = server.ConnectionString,
            ["DataProtection:CertificateBase64"] = Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, certificatePassword)),
            ["DataProtection:CertificatePassword"] = certificatePassword
        };
        try
        {
            VerifyConfigurationGuards(config);
            await using (var setup = new NpgsqlConnection(server.ConnectionString))
            {
                await setup.OpenAsync();
                await using var unrelated = new NpgsqlCommand("CREATE TABLE public.other_app(id integer PRIMARY KEY)", setup);
                await unrelated.ExecuteNonQueryAsync();
            }
            var company = Guid.NewGuid();
            var otherCompany = Guid.NewGuid();
            string protectedValue;
            await using (var first = CreateServices(config))
            await using (var second = CreateServices(config))
            {
                await Task.WhenAll(InitializeAsync(first), InitializeAsync(second));
                Check(true, "Parallel cold starts migrate both PostgreSQL schemas alongside existing tables");
                await VerifyFinanceAsync(first, second);
                await using (var scope = first.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<CompanyIdentityDbContext>();
                    Check(await db.Roles.CountAsync() == 4, "Concurrent startup creates each role once");
                    db.Companies.AddRange(new Company { Id = company, Name = "Hosted fixture" },
                        new Company { Id = otherCompany, Name = "Other fixture" });
                    await db.SaveChangesAsync();
                    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var result = await users.CreateAsync(new ApplicationUser
                    {
                        CompanyId = company, UserName = "hosted@example.test", Email = "hosted@example.test", FullName = "Fixture"
                    }, "Only-A-Test-Password!492");
                    Check(result.Succeeded, "Hosted Identity creates a user");
                }
                var protector = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("Dike.Odoo.Web.v1", company.ToString("N"));
                protectedValue = protector.Protect("synthetic-connector-secret");
                var storage = first.GetRequiredService<ICompanyDocumentStorage>();
                await using (var session = await storage.OpenAsync("Odoo", company, default))
                {
                    await session.WriteAsync("state.enc", protectedValue, default);
                    await session.CompleteAsync(default);
                }

                var otherStorage = second.GetRequiredService<ICompanyDocumentStorage>();
                var limited = new NpgsqlConnectionStringBuilder(server.ConnectionString) { MaxPoolSize = 1 };
                await using (var single = CreateServices(new(config) { ["ConnectionStrings:Identity"] = limited.ConnectionString }))
                await using (var identityScope = single.CreateAsyncScope())
                await using (var document = await single.GetRequiredService<ICompanyDocumentStorage>().OpenAsync("Odoo", company, default))
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var identity = identityScope.ServiceProvider.GetRequiredService<CompanyIdentityDbContext>();
                    Check(await identity.Companies.AnyAsync(x => x.Id == company, timeout.Token),
                        "Identity remains available while a connector holds the last document connection");
                }
                await using (var session = await otherStorage.OpenAsync("Odoo", company, default))
                    Check(await session.ReadAsync("state.enc", default) == protectedValue, "Separate instances read the same persisted document");
                await using (var session = await otherStorage.OpenAsync("Odoo", otherCompany, default))
                    Check(await session.ReadAsync("state.enc", default) is null, "Company storage remains isolated");
                await using (var session = await otherStorage.OpenAsync("NetSuite", company, default))
                    Check(await session.ReadAsync("state.enc", default) is null, "Provider storage remains isolated");

                await using (var held = await storage.OpenAsync("Odoo", company, default))
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var waiting = otherStorage.OpenAsync("Odoo", company, deadline.Token);
                    await Task.Delay(150);
                    Check(!waiting.IsCompleted, "PostgreSQL transaction lock serializes writes across instances");
                    await held.WriteAsync("state.enc", protectedValue, default);
                    await held.CompleteAsync(default);
                    await using var acquired = await waiting;
                    Check(await acquired.ReadAsync("state.enc", default) == protectedValue, "Waiting instance sees the completed write");
                }
                await using (var held = await storage.OpenAsync("Odoo", company, default))
                {
                    await held.WriteAsync("state.enc", "uncommitted", default);
                    await held.WriteAsync("aborted.enc", "uncommitted", default);
                    using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                    try { await using var ignored = await otherStorage.OpenAsync("Odoo", company, cancel.Token); throw new Exception("Expected lock cancellation"); }
                    catch (OperationCanceledException) { Check(true, "Waiting PostgreSQL lock can be cancelled"); }
                }
                await using (var session = await otherStorage.OpenAsync("Odoo", company, default))
                {
                    Check(await session.ReadAsync("state.enc", default) == protectedValue &&
                        await session.ReadAsync("aborted.enc", default) is null, "Interrupted multi-document updates roll back and release their lock");
                    try { await session.ReadAsync("state.enc", default, 1); throw new Exception("Expected size rejection"); }
                    catch (IOException) { Check(true, "Stored document size limit is enforced"); }
                    try { await session.WriteAsync("../escape.enc", "x", default); throw new Exception("Expected invalid document rejection"); }
                    catch (ArgumentException) { Check(true, "Document names cannot escape their scope"); }
                }

                var connector = first.GetRequiredService<CompanyNetSuite>();
                var connection = new NetSuiteConnection { AccountId = "1234567", ClientId = "test-client", ClientSecret = "test-secret", TokenId = "test-token", TokenSecret = "test-token-secret" };
                await connector.ConfigureAsync(company, connection, default);
                connection.ClientSecret = "updated-test-secret";
                await connector.ConfigureAsync(company, connection, default);
                Check((await second.GetRequiredService<CompanyNetSuite>().ReadAsync(company, default))?.Connection.ClientSecret == "updated-test-secret",
                    "NetSuite credentials persist across instances");
                var odoo = first.GetRequiredService<CompanyOdooStore>();
                await odoo.LockedAsync(company, async session =>
                {
                    await odoo.WriteAsync(session, company, new OdooStoredState { ApiKey = "synthetic-odoo-key", BaseUrl = "https://odoo.example.test/" }, default);
                    return true;
                }, default);
                var otherOdoo = second.GetRequiredService<CompanyOdooStore>();
                Check(await otherOdoo.LockedAsync(company, async session =>
                    (await otherOdoo.ReadAsync(session, company, default))?.ApiKey == "synthetic-odoo-key", default),
                    "Odoo credentials persist across instances");
                await using var inspect = first.CreateAsyncScope();
                var inspectDb = inspect.ServiceProvider.GetRequiredService<CompanyIdentityDbContext>();
                Check(await inspectDb.CompanyDocuments.AnyAsync(x => x.CompanyId == company && x.Provider == "NetSuite" && x.Name.StartsWith("previous-")),
                    "Replacing NetSuite settings preserves encrypted history");
                var documents = await inspectDb.CompanyDocuments.Select(x => x.Ciphertext).ToArrayAsync();
                Check(documents.All(x => !x.Contains("test-secret") && !x.Contains("synthetic-odoo-key")), "Connector secrets are encrypted in SQL");
                var keys = await inspectDb.DataProtectionKeys.Select(x => x.Xml).ToArrayAsync();
                Check(keys.Length > 0 && keys.All(x => x is not null && x.Contains("EncryptedData")), "Shared encryption keys are certificate-protected in SQL");
            }

            await using (var restarted = CreateServices(config))
            {
                await InitializeAsync(restarted);
                var protector = restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("Dike.Odoo.Web.v1", company.ToString("N"));
                Check(protector.Unprotect(protectedValue) == "synthetic-connector-secret", "Encrypted data remains readable after all app instances restart");
                await using var scope = restarted.CreateAsyncScope();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByEmailAsync("hosted@example.test");
                Check(user is not null && await users.CheckPasswordAsync(user, "Only-A-Test-Password!492"), "User credentials survive app restarts");
                try
                {
                    restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("Dike.Odoo.Web.v1", otherCompany.ToString("N")).Unprotect(protectedValue);
                    throw new Exception("Expected company-key rejection");
                }
                catch (CryptographicException) { Check(true, "Encryption remains company-scoped"); }
            }
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static WebApplicationBuilder Builder(Dictionary<string, string?> values)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.GetTempPath(), EnvironmentName = "Development",
            ApplicationName = typeof(DeploymentConfiguration).Assembly.FullName
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(values);
        builder.Logging.ClearProviders();
        return builder;
    }

    private static ServiceProvider CreateServices(Dictionary<string, string?> values)
    {
        var builder = Builder(values);
        builder.ConfigureDikeDeployment();
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<CompanyIdentityDbContext>();
        builder.Services.AddSingleton<CompanyOdooStore>();
        builder.Services.AddSingleton<CompanyNetSuite>();
        builder.Services.AddHttpClient("NetSuite");
        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task InitializeAsync(ServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanyIdentityDbContext>();
        await IdentitySchema.InitializeAsync(db, null);
        await scope.ServiceProvider.GetRequiredService<DikeDbContext>().Database.MigrateAsync();
        await IdentitySchema.InitializeRolesAsync(db, scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    }

    private static async Task VerifyFinanceAsync(ServiceProvider first, ServiceProvider second)
    {
        await using var firstScope = first.CreateAsyncScope();
        await using var secondScope = second.CreateAsyncScope();
        var db = firstScope.ServiceProvider.GetRequiredService<DikeDbContext>();
        var other = secondScope.ServiceProvider.GetRequiredService<DikeDbContext>();
        var mapping = new AccountMapping { Company = "fixture", Connection = "test", ExternalAccount = "test" };
        db.AccountMappings.Add(mapping);
        await db.SaveChangesAsync();
        var stale = await other.AccountMappings.FindAsync(mapping.Id) ?? throw new Exception("Missing finance mapping.");
        mapping.Currency = "USD";
        await db.SaveChangesAsync();
        stale.Currency = "EUR";
        try { await other.SaveChangesAsync(); throw new Exception("Expected a concurrency conflict."); }
        catch (DbUpdateConcurrencyException) { Check(true, "PostgreSQL row versions prevent stale finance updates"); }

        var movement = new StoredMovement { MappingId = mapping.Id, SourceId = "row", Date = new(2026, 1, 1), Amount = 12.34m };
        var group = new MatchGroup { MappingId = mapping.Id };
        db.AddRange(movement, group);
        await db.SaveChangesAsync();
        MatchMember Member(bool active) => new() { GroupId = group.Id, MovementId = movement.Id, Active = active };
        db.MatchMembers.AddRange(Member(false), Member(false), Member(true));
        await db.SaveChangesAsync();
        db.MatchMembers.Add(Member(true));
        try { await db.SaveChangesAsync(); throw new Exception("Expected duplicate active membership rejection."); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { Check(true, "Finance history allows inactive matches and enforces one active match per movement"); }
    }

    private static void VerifyConfigurationGuards(Dictionary<string, string?> valid)
    {
        void Reject(Dictionary<string, string?> values, string expected, string name)
        {
            try { Builder(values).ConfigureDikeDeployment(); }
            catch (InvalidOperationException e) when (e.Message.Contains(expected)) { Check(true, name); return; }
            throw new Exception(name);
        }
        Reject(new() { ["Hosting:Provider"] = "Vercel", ["Storage:Provider"] = "Sqlite" }, "require PostgreSQL", "Vercel requires persistent PostgreSQL storage");
        Reject(new() { ["Hosting:Provider"] = "Vercel" }, "ConnectionStrings__Identity", "Missing hosted database produces an actionable error");
        Reject(new(valid) { ["Hosting:Provider"] = "Vercel", ["ConnectionStrings:Identity"] = "postgres://db.example/dike?sslmode=disable" }, "VerifyFull", "Vercel rejects insecure database connections");
        Reject(new(valid) { ["Hosting:Provider"] = "Vercel", ["ConnectionStrings:Identity"] = "Host=db.example", ["DataProtection:CertificateBase64"] = null }, "CertificateBase64", "Hosted keys require a persistent encryption certificate");
        var url = "postgresql://app%40tenant:a%2Fb%3A%2B%25@db.example:6543/dike%20test?sslmode=require&channel_binding=require";
        var parsed = new NpgsqlConnectionStringBuilder(PostgresConnection.Parse(url, hosted: true));
        Check(parsed.Username == "app@tenant" && parsed.Password == "a/b:+%" && parsed.Database == "dike test" && parsed.Port == 6543 &&
            parsed.SslMode == SslMode.VerifyFull && parsed.ChannelBinding == ChannelBinding.Require && parsed.MaxPoolSize == 10,
            "Vercel URLs preserve escaped credentials and channel binding and enforce certificate verification");
        var builder = Builder(new(valid) { ["ConnectionStrings:Identity"] = null, ["DATABASE_URL"] = url, ["Hosting:Provider"] = "Vercel" });
        Check(builder.ConfigureDikeDeployment(), "A Vercel integration DATABASE_URL configures the application");
    }
}
