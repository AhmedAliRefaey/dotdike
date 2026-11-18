using System.Security.Claims;
using Dike.Infrastructure.Odoo;

namespace Dike.Web.Authentication;

public interface IOdooClientFactory
{
    IOdooReadOnlyClient Create(OdooConnection connection);
}

public sealed class OdooClientFactory : IOdooClientFactory
{
    public IOdooReadOnlyClient Create(OdooConnection connection) => new OdooReadOnlyClient(connection);
}

public sealed class CompanyOdoo(CompanyAdministrator access, CompanyOdooStore store, IOdooClientFactory clients,
    IConfiguration configuration, IWebHostEnvironment environment, TimeProvider clock)
{
    private const int CatalogLimit = 1000;

    public async Task<OdooWorkspace> ReadAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var company = await RequireAsync(user, ct);
        return await store.LockedAsync(company.Id, async () =>
        {
            var state = await store.ReadAsync(company.Id, ct);
            var baseUri = Endpoint();
            var needsReconnect = state != null && state.BaseUrl != baseUri.AbsoluteUri;
            // Old/unbound state is retained, but cannot be reused against a different endpoint.
            if (needsReconnect) state = null;
            await RecheckAsync(user, company.Id, ct);
            return new OdooWorkspace(company.Name, state != null, CanUseLocal(company.Id), state?.ApiUserId,
                state?.VerifiedAt, state?.Companies.ToArray() ?? [], state?.CompanyCursor != null,
                state?.SelectedCompanyId, state?.Accounts.ToArray() ?? [], state?.AccountCursor != null,
                state?.AccountsFetchedAt, state?.CatalogVersion ?? Guid.Empty, state?.Preview,
                baseUri.GetLeftPart(UriPartial.Authority), needsReconnect);
        }, ct);
    }

    public Task ConfigureAsync(ClaimsPrincipal user, string? apiKey, string? database, bool useLocal, CancellationToken ct = default) =>
        MutateAsync(user, async (company, previous, token) =>
        {
            if (useLocal)
            {
                if (!CanUseLocal(company.Id)) throw new OdooException("local_binding", "The local Odoo key is not assigned to this Dike workspace.");
                apiKey = configuration["Odoo:ApiKey"];
                database = configuration["Odoo:Database"];
            }
            var connection = new OdooConnection(configuration["Odoo:BaseUrl"], apiKey, database);
            using var client = clients.Create(connection);
            var companies = await client.ListCompaniesAsync(ct: token);
            if (companies.Rows.Count == 0) throw new OdooException("scope", "No permitted Odoo companies were returned.");
            return new OdooStoredState { BaseUrl = connection.BaseUri.AbsoluteUri, ApiKey = apiKey!, Database = database, ApiUserId = companies.AuthenticatedUserId,
                Companies = companies.Rows.ToList(), CompanyCursor = Cursor(companies), VerifiedAt = clock.GetUtcNow() };
        }, ct);

    public Task MoreCompaniesAsync(ClaimsPrincipal user, CancellationToken ct = default) =>
        MutateAsync(user, async (_, saved, token) =>
        {
            var state = Configured(saved);
            if (state.CompanyCursor is not int cursor) throw Selection();
            using var client = Client(state);
            var page = await client.ListCompaniesAsync(cursor, token);
            CheckCatalog(state.Companies.Select(x => x.Id), page.Rows.Select(x => x.Id), page.AuthenticatedUserId, state.ApiUserId);
            state.Companies.AddRange(page.Rows); state.CompanyCursor = Cursor(page);
            return state;
        }, ct);

    public Task LoadAccountsAsync(ClaimsPrincipal user, int odooCompanyId, bool more = false, CancellationToken ct = default) =>
        MutateAsync(user, async (_, saved, token) =>
        {
            var state = Configured(saved);
            if (!state.Companies.Any(company => company.Id == odooCompanyId)) throw Selection();
            if (more && (state.SelectedCompanyId != odooCompanyId || state.AccountCursor == null)) throw Selection();
            using var client = Client(state);
            var page = await client.ListAccountsAsync(odooCompanyId, more ? state.AccountCursor!.Value : 0, token, bankOnly: true);
            CheckCatalog(more ? state.Accounts.Select(x => x.Id) : [], page.Rows.Select(x => x.Id), page.AuthenticatedUserId, state.ApiUserId);
            state.SelectedCompanyId = odooCompanyId;
            state.Accounts = more ? state.Accounts.Concat(page.Rows).ToList() : page.Rows.ToList();
            state.AccountCursor = Cursor(page); state.AccountsFetchedAt = clock.GetUtcNow();
            state.CatalogVersion = Guid.NewGuid(); state.Preview = null;
            return state;
        }, ct);

    public Task FetchAsync(ClaimsPrincipal user, Guid catalogVersion, IReadOnlyList<int> accountIds, OdooPeriod period,
        int limit, CancellationToken ct = default, string periodMode = "range") =>
        MutateAsync(user, async (_, saved, token) =>
        {
            var state = Configured(saved);
            if (catalogVersion == Guid.Empty || catalogVersion != state.CatalogVersion)
                throw new OdooException("stale_selection", "The loaded accounts changed. Select accounts again.");
            if (state.SelectedCompanyId is not int companyId || accountIds.Count is < 1 or > 5 ||
                accountIds.Distinct().Count() != accountIds.Count || accountIds.Any(id => !state.Accounts.Any(account => account.Id == id)) ||
                limit is < 1 or > OdooReadOnlyClient.MaxSampleRows || period.From > period.To ||
                period.To.DayNumber - period.From.DayNumber >= OdooReadOnlyClient.MaxPeriodDays ||
                periodMode is not ("week" or "month" or "range")) throw Selection();
            var company = state.Companies.Single(item => item.Id == companyId);
            if (accountIds.Any(id => state.Accounts.Any(account => account.Id == id &&
                (account.AccountType != "asset_cash" || account.CurrencyId is int currency && currency != company.CurrencyId)))) throw Selection();
            using var client = Client(state);
            var samples = new List<OdooGlSample>();
            foreach (var accountId in accountIds)
            {
                var sample = await client.ReadPostedSampleAsync(companyId, accountId, period.From, period.To, limit, token);
                if (sample.AuthenticatedUserId != state.ApiUserId || sample.CompanyId != companyId || sample.AccountId != accountId ||
                    sample.From != period.From || sample.To != period.To || sample.Rows.Count > limit) throw Selection();
                samples.Add(sample);
            }
            state.Preview = new(Guid.NewGuid(), clock.GetUtcNow(), state.Companies.Single(company => company.Id == companyId).Name,
                accountIds.Select(id => state.Accounts.Single(account => account.Id == id)).ToArray(), samples.ToArray(), periodMode);
            return state;
        }, ct);

    private async Task MutateAsync(ClaimsPrincipal user, Func<AuthorizedCompany, OdooStoredState?, CancellationToken, Task<OdooStoredState>> update, CancellationToken ct)
    {
        var company = await RequireAsync(user, ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            await store.LockedAsync(company.Id, async () =>
            {
                await RecheckAsync(user, company.Id, deadline.Token);
                var state = await update(company, await store.ReadAsync(company.Id, deadline.Token), deadline.Token);
                // A revoked/moved/locked-out user cannot publish the completed network result.
                await RecheckAsync(user, company.Id, deadline.Token);
                RequireEndpoint(state);
                await store.WriteAsync(company.Id, state, deadline.Token);
                return true;
            }, deadline.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new OdooException("timeout", "The Odoo request timed out. The previous saved preview was retained."); }
    }

    private async Task<AuthorizedCompany> RequireAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var company = await access.RequireAsync(user, ct);
        if (company.ConnectionProvider != ConnectionProviders.Odoo) throw new CompanyAccessDeniedException();
        if (!company.SetupComplete) throw new OdooException("setup", "Complete the company profile first.");
        return company;
    }
    private async Task RecheckAsync(ClaimsPrincipal user, Guid companyId, CancellationToken ct)
    {
        if ((await RequireAsync(user, ct)).Id != companyId) throw new CompanyAccessDeniedException();
    }
    private bool CanUseLocal(Guid company) => environment.IsDevelopment() &&
        Guid.TryParse(configuration["Odoo:LocalCompanyId"], out var bound) && bound == company &&
        !string.IsNullOrWhiteSpace(configuration["Odoo:ApiKey"]);
    private Uri Endpoint() => OdooConnection.ParseBaseUri(configuration["Odoo:BaseUrl"]);
    private void RequireEndpoint(OdooStoredState state)
    {
        if (state.BaseUrl != Endpoint().AbsoluteUri)
            throw new OdooException("endpoint_changed", "Verify the connection again for the configured Odoo server.");
    }
    private IOdooReadOnlyClient Client(OdooStoredState state)
    {
        RequireEndpoint(state);
        return clients.Create(new(state.BaseUrl, state.ApiKey, state.Database));
    }
    private OdooStoredState Configured(OdooStoredState? state)
    {
        if (state == null) throw new OdooException("not_configured", "Connect Odoo before loading accounting data.");
        RequireEndpoint(state);
        return state;
    }
    private static OdooException Selection() => new("selection", "Choose a loaded company and one to five loaded accounts, with valid period and row limits.");
    private static int? Cursor<T>(OdooCatalog<T> page) => page.HasMore ? page.NextAfterId ?? throw Selection() : null;
    private static void CheckCatalog(IEnumerable<int> current, IEnumerable<int> next, int apiUser, int savedUser)
    {
        var ids = current.Concat(next).ToArray();
        if (apiUser != savedUser || ids.Length > CatalogLimit || ids.Distinct().Count() != ids.Length) throw Selection();
    }
}
