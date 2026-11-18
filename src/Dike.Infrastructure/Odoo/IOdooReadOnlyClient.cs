namespace Dike.Infrastructure.Odoo;

public interface IOdooReadOnlyClient : IDisposable
{
    Task<OdooAccess> AuthenticateAsync(CancellationToken ct = default);
    Task<OdooCatalog<OdooCompany>> ListCompaniesAsync(int afterId = 0, CancellationToken ct = default);
    Task<OdooCatalog<OdooAccount>> ListAccountsAsync(int companyId, int afterId = 0, CancellationToken ct = default, bool bankOnly = false);
    Task<OdooGlSample> ReadPostedSampleAsync(int companyId, int accountId, DateOnly from, DateOnly to, int limit = 10, CancellationToken ct = default);
}
