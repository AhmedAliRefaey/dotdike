using Dike.Infrastructure.Odoo;

namespace Dike.Web.Authentication;

public sealed record OdooPreview(Guid RunId, DateTimeOffset FetchedAt, string CompanyName,
    IReadOnlyList<OdooAccount> Accounts, IReadOnlyList<OdooGlSample> Samples, string PeriodMode = "range");

// Only this credential-free projection is exposed to Razor pages.
public sealed record OdooWorkspace(string DikeCompanyName, bool Configured, bool CanUseLocalConnection,
    int? ApiUserId, DateTimeOffset? VerifiedAt, IReadOnlyList<OdooCompany> Companies, bool MoreCompanies,
    int? SelectedCompanyId, IReadOnlyList<OdooAccount> Accounts, bool MoreAccounts,
    DateTimeOffset? AccountsFetchedAt, Guid CatalogVersion, OdooPreview? Preview,
    string? BaseUrl = null, bool NeedsReconnect = false)
{
    public OdooCompany? SelectedCompany => Companies.FirstOrDefault(company => company.Id == SelectedCompanyId);
    public static OdooWorkspace Empty { get; } = new("", false, false, null, null, [], false, null, [], false, null, Guid.Empty, null);
}

// Never return this object to page models, TempData, logs, or API responses.
internal sealed class OdooStoredState
{
    public string? BaseUrl { get; set; }
    public string ApiKey { get; set; } = "";
    public string? Database { get; set; }
    public int ApiUserId { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
    public List<OdooCompany> Companies { get; set; } = [];
    public int? CompanyCursor { get; set; }
    public int? SelectedCompanyId { get; set; }
    public List<OdooAccount> Accounts { get; set; } = [];
    public int? AccountCursor { get; set; }
    public DateTimeOffset? AccountsFetchedAt { get; set; }
    public Guid CatalogVersion { get; set; } = Guid.NewGuid();
    public OdooPreview? Preview { get; set; }
}
