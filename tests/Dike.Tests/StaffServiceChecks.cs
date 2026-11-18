using System.Security.Claims;
using System.Text.Json;
using Dike.Web.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

internal static class StaffServiceChecks
{
    public static async Task RunAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        var staff = fixture.Scope.ServiceProvider.GetRequiredService<StaffService>();
        var db = fixture.Scope.ServiceProvider.GetRequiredService<CompanyIdentityDbContext>();
        var owner = await fixture.PrincipalAsync("a-owner");
        var viewer = await fixture.PrincipalAsync("a-viewer");
        var passed = 0;
        async Task Check(string name, Func<Task> action)
        {
            await action();
            passed++;
            Console.WriteLine($"PASS Staff: {name}");
        }

        await Check("anonymous principals cannot read staff", async () =>
            await Reject<UnauthorizedAccessException>(() => staff.GetPageAsync(new ClaimsPrincipal())));
        await Check("claims without authentication do not authorize a directory", async () =>
            await Reject<UnauthorizedAccessException>(() => staff.GetPageAsync(new ClaimsPrincipal(new ClaimsIdentity(owner.Claims)))));
        await Check("missing user identity fails closed", async () =>
            await Reject<UnauthorizedAccessException>(() => staff.GetPageAsync(Without(owner, ClaimTypes.NameIdentifier))));
        await Check("missing security stamp fails closed", async () =>
            await Reject<UnauthorizedAccessException>(() => staff.GetPageAsync(Without(owner, "AspNet.Identity.SecurityStamp"))));
        await Check("unknown authenticated user fails closed", async () =>
            await Reject<UnauthorizedAccessException>(() => staff.GetPageAsync(With(owner, ClaimTypes.NameIdentifier, "unknown"))));
        await Check("invalid session stamp fails closed", async () =>
            await Reject<UnauthorizedAccessException>(() => staff.GetPageAsync(With(owner, "AspNet.Identity.SecurityStamp", "invalid-stamp"))));
        await Check("forged Administrator role cannot elevate a Viewer", async () =>
            await Reject<UnauthorizedAccessException>(() => staff.GetPageAsync(With(viewer, ClaimTypes.Role, "Administrator"))));
        await Check("database role authorizes without a cookie role", async () =>
            Require((await staff.GetPageAsync(Without(owner, ClaimTypes.Role))).TotalCount == 32));
        await Check("caller company claims cannot change count or returned members", async () =>
        {
            var principal = With(owner, "CompanyId", fixture.CompanyB.ToString());
            var result = await staff.GetPageAsync(principal, pageSize: 100);
            Require(result.TotalCount == 32 && result.Members.Count == 32);
            Require(result.Members.All(member => member.Id.StartsWith("a-", StringComparison.Ordinal)));
        });
        await Check("second company sees only its own staff", async () =>
        {
            var result = await staff.GetPageAsync(await fixture.PrincipalAsync("b-owner"));
            Require(result.TotalCount == 2 && result.Members.All(member => member.Id.StartsWith("b-", StringComparison.Ordinal)));
        });
        await Check("default pages are bounded and stable for duplicate display names", async () =>
        {
            var first = await staff.GetPageAsync(owner);
            var second = await staff.GetPageAsync(owner, 2);
            var repeated = await staff.GetPageAsync(owner);
            Require(first.Members.Count == 25 && second.Members.Count == 7);
            Require(!first.HasPreviousPage && first.HasNextPage && second.HasPreviousPage && !second.HasNextPage);
            Require(first.TotalPages == 2 && second.TotalCount == 32);
            Require(first.Members.Select(x => x.Id).SequenceEqual(repeated.Members.Select(x => x.Id)));
            Require(first.Members.Concat(second.Members).Select(x => x.Id).Distinct().Count() == 32);
            Require(first.Members.Where(x => x.FullName == "Member").Select(x => x.Id)
                .SequenceEqual(Enumerable.Range(0, 24).Select(i => $"a-{i:00}")));
        });
        await Check("roles are current, sorted, and do not imply extra roles", async () =>
        {
            var result = await staff.GetPageAsync(owner, pageSize: 100);
            Require(result.Members.Single(x => x.Id == "a-security").Roles.SequenceEqual(new[] { "Accountant", "Controller" }));
            Require(result.Members.Single(x => x.Id == "a-owner").Roles.SequenceEqual(new[] { "Administrator" }));
            Require(result.Members.Single(x => x.Id == "a-unassigned").Roles.Count == 0);
        });
        await Check("security indicators include active lockout without filtering staff", async () =>
        {
            var result = await staff.GetPageAsync(owner, pageSize: 100);
            var member = result.Members.Single(x => x.Id == "a-security");
            Require(member.EmailConfirmed && member.TwoFactorEnabled && member.IsLockedOut);
            var unverified = result.Members.Single(x => x.Id == "a-owner");
            Require(!unverified.EmailConfirmed && !unverified.TwoFactorEnabled && !unverified.IsLockedOut);
        });
        await Check("results expose no Identity secrets or token records", async () =>
        {
            var json = JsonSerializer.Serialize(await staff.GetPageAsync(owner, pageSize: 100));
            foreach (var secret in new[] { "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "AuthenticatorKey", "RecoveryCodes", "secret-fixture" })
                Require(!json.Contains(secret, StringComparison.Ordinal));
        });
        await Check("directory reads leave the Identity change tracker clean", () =>
        {
            Require(!db.ChangeTracker.HasChanges() && !db.ChangeTracker.Entries().Any());
            return Task.CompletedTask;
        });
        await Check("empty later page retains only company-scoped totals", async () =>
        {
            var result = await staff.GetPageAsync(owner, 20);
            Require(result.Members.Count == 0 && result.TotalCount == 32 && !result.HasNextPage);
        });
        foreach (var size in new[] { -1, 0, 101, int.MaxValue })
            await Check($"page size {size} is rejected", async () =>
                await Reject<ArgumentOutOfRangeException>(() => staff.GetPageAsync(owner, pageSize: size)));
        foreach (var page in new[] { -1, 0, int.MaxValue })
            await Check($"page number {page} is rejected", async () =>
                await Reject<ArgumentOutOfRangeException>(() => staff.GetPageAsync(owner, page)));
        await Check("cancelled requests propagate cancellation", async () =>
            await Reject<OperationCanceledException>(() => staff.GetPageAsync(owner, cancellationToken: new CancellationToken(true))));

        // Hold stale tracked entities while another scope updates the real relational store.
        _ = await db.Users.SingleAsync(x => x.Id == "a-owner");
        _ = await db.UserRoles.Where(x => x.UserId == "a-owner").ToListAsync();
        await Check("role removal denies an already issued Administrator principal", async () =>
        {
            await fixture.ChangeAsync(async (store, users) =>
                Succeeded(await users.RemoveFromRoleAsync(await store.Users.SingleAsync(x => x.Id == "a-owner"), "Administrator")));
            await Reject<UnauthorizedAccessException>(() => staff.GetPageAsync(owner));
            await fixture.ChangeAsync(async (store, users) =>
                Succeeded(await users.AddToRoleAsync(await store.Users.SingleAsync(x => x.Id == "a-owner"), "Administrator")));
        });
        await Check("stamp rotation rejects stale sessions despite a tracked user", async () =>
        {
            await fixture.ChangeAsync(async (store, users) =>
                Succeeded(await users.UpdateSecurityStampAsync(await store.Users.SingleAsync(x => x.Id == "a-owner"))));
            await Reject<UnauthorizedAccessException>(() => staff.GetPageAsync(owner));
            owner = await fixture.PrincipalAsync("a-owner");
            Require((await staff.GetPageAsync(owner)).TotalCount == 32);
        });
        await Check("active sign-in lockout denies an existing session", async () =>
        {
            await fixture.SetOwnerLockoutAsync(fixture.Clock.GetUtcNow().AddMinutes(10));
            await Reject<UnauthorizedAccessException>(() => staff.GetPageAsync(owner));
        });
        await Check("expired lockout allows the current session at its exact boundary", async () =>
        {
            await fixture.SetOwnerLockoutAsync(fixture.Clock.GetUtcNow());
            Require((await staff.GetPageAsync(owner)).TotalCount == 32);
        });
        await Check("disabled lockout does not mislabel or deny an account", async () =>
        {
            await fixture.SetOwnerLockoutAsync(fixture.Clock.GetUtcNow().AddMinutes(10), enabled: false);
            Require(!(await staff.GetPageAsync(owner, pageSize: 100)).Members.Single(x => x.Id == "a-owner").IsLockedOut);
            await fixture.SetOwnerLockoutAsync(null);
        });
        await Check("membership is resolved afresh from the database", async () =>
        {
            await fixture.ChangeAsync(async (store, _) =>
            {
                var user = await store.Users.SingleAsync(x => x.Id == "a-owner");
                user.CompanyId = fixture.CompanyB;
                await store.SaveChangesAsync();
            });
            var result = await staff.GetPageAsync(owner);
            Require(result.TotalCount == 3 && result.Members.All(x => x.Id == "a-owner" || x.Id.StartsWith("b-", StringComparison.Ordinal)));
            await fixture.ChangeAsync(async (store, _) =>
            {
                (await store.Users.SingleAsync(x => x.Id == "a-owner")).CompanyId = fixture.CompanyA;
                await store.SaveChangesAsync();
            });
        });
        await Check("configured Identity claim names are respected", async () =>
        {
            var options = new IdentityOptions();
            options.ClaimsIdentity.UserIdClaimType = "test-user";
            options.ClaimsIdentity.SecurityStampClaimType = "test-stamp";
            var configured = new StaffService(db, Options.Create(options), new UpperInvariantLookupNormalizer(), fixture.Clock);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim("test-user", "a-owner"), new Claim("test-stamp", owner.FindFirst("AspNet.Identity.SecurityStamp")!.Value) }, "test"));
            Require((await configured.GetPageAsync(principal)).TotalCount == 32);
            await Reject<UnauthorizedAccessException>(() => configured.GetPageAsync(owner));
        });
        await Check("deletion denies a stale principal and tracked membership", async () =>
        {
            await fixture.ChangeAsync(async (store, users) =>
                Succeeded(await users.DeleteAsync(await store.Users.SingleAsync(x => x.Id == "a-owner"))));
            await Reject<UnauthorizedAccessException>(() => staff.GetPageAsync(owner));
        });

        Console.WriteLine($"Staff service checks: {passed} passed against isolated SQLite Identity.");
    }

    private static ClaimsPrincipal Without(ClaimsPrincipal principal, string claimType) =>
        new(new ClaimsIdentity(principal.Claims.Where(claim => claim.Type != claimType), "test"));

    private static ClaimsPrincipal With(ClaimsPrincipal principal, string claimType, string value) =>
        new(new ClaimsIdentity(principal.Claims.Where(claim => claim.Type != claimType).Append(new Claim(claimType, value)), "test"));

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected staff authorization or directory result was not observed.");
    }

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name} was not observed.");
    }

    private static void Succeeded(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Code)));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private ServiceProvider services = null!;
        public AsyncServiceScope Scope { get; private set; }
        public FixedClock Clock { get; } = new();
        public Guid CompanyA { get; } = Guid.NewGuid();
        public Guid CompanyB { get; } = Guid.NewGuid();

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            try { await fixture.InitializeAsync(); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }

        private async Task InitializeAsync()
        {
            await connection.OpenAsync();
            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddSingleton<TimeProvider>(Clock);
            collection.AddDbContext<CompanyIdentityDbContext>(options => options.UseSqlite(connection));
            collection.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<CompanyIdentityDbContext>();
            collection.AddScoped<StaffService>();
            services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            await using var setup = services.CreateAsyncScope();
            var store = setup.ServiceProvider.GetRequiredService<CompanyIdentityDbContext>();
            await store.Database.EnsureCreatedAsync();
            store.Companies.AddRange(new Company { Id = CompanyA, Name = "Staff fixture A" }, new Company { Id = CompanyB, Name = "Staff fixture B" });
            await store.SaveChangesAsync();
            var roles = setup.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var name in new[] { "Administrator", "Accountant", "Controller", "Viewer" })
                Succeeded(await roles.CreateAsync(new IdentityRole(name)));
            var users = setup.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            async Task Add(string id, Guid company, string name, params string[] assignments)
            {
                var user = new ApplicationUser { Id = id, CompanyId = company, FullName = name,
                    Email = id + "@example.test", UserName = id + "@example.test" };
                Succeeded(await users.CreateAsync(user));
                if (assignments.Length > 0) Succeeded(await users.AddToRolesAsync(user, assignments));
            }
            await Add("a-owner", CompanyA, "Owner A", "Administrator");
            await Add("a-viewer", CompanyA, "Viewer A", "Viewer");
            await Add("a-security", CompanyA, "<script>alert(1)</script>", "Controller", "Accountant");
            await Add("a-unassigned", CompanyA, "Unassigned A");
            for (var i = 0; i < 28; i++) await Add($"a-{i:00}", CompanyA, "Member", "Viewer");
            await Add("b-owner", CompanyB, "Owner B", "Administrator");
            await Add("b-staff", CompanyB, "Private B", "Controller");
            var secured = await store.Users.SingleAsync(x => x.Id == "a-security");
            secured.EmailConfirmed = true;
            secured.TwoFactorEnabled = true;
            secured.LockoutEnabled = true;
            secured.LockoutEnd = Clock.GetUtcNow().AddHours(1);
            secured.PasswordHash = "secret-fixture-password";
            secured.SecurityStamp = "secret-fixture-stamp";
            store.UserTokens.Add(new IdentityUserToken<string> { UserId = secured.Id, LoginProvider = "[AspNetUserStore]",
                Name = "AuthenticatorKey", Value = "secret-fixture-authenticator" });
            await store.SaveChangesAsync();
            Scope = services.CreateAsyncScope();
        }

        public async Task<ClaimsPrincipal> PrincipalAsync(string id)
        {
            await using var scope = services.CreateAsyncScope();
            var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id)
                ?? throw new InvalidOperationException("Missing fixture member.");
            return await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
        }

        public async Task ChangeAsync(Func<CompanyIdentityDbContext, UserManager<ApplicationUser>, Task> action)
        {
            await using var scope = services.CreateAsyncScope();
            await action(scope.ServiceProvider.GetRequiredService<CompanyIdentityDbContext>(),
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
        }

        public Task SetOwnerLockoutAsync(DateTimeOffset? until, bool enabled = true) => ChangeAsync(async (store, _) =>
        {
            var owner = await store.Users.SingleAsync(x => x.Id == "a-owner");
            owner.LockoutEnabled = enabled;
            owner.LockoutEnd = until;
            await store.SaveChangesAsync();
        });

        public async ValueTask DisposeAsync()
        {
            if (Scope.ServiceProvider is not null) await Scope.DisposeAsync();
            if (services is not null) await services.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
