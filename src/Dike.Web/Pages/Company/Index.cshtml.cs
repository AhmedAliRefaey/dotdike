using Dike.Web.Authentication;
using Dike.Infrastructure.Odoo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dike.Web.Pages.Company;
public sealed class IndexModel(CurrentCompany currentCompany, CompanyNetSuite connector, CompanyOdoo odoo, CompanyAdministrator access) : PageModel
{
    public string ConnectionStatus { get; private set; } = "Not configured";
    public string ConnectionDetail { get; private set; } = "No NetSuite data imported";
    public int ConnectionCount { get; private set; }
    public OdooWorkspace? OdooWorkspace { get; private set; }
    public Dike.Web.Authentication.Company Company { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync()
    {
        try { await access.RequireAsync(User, HttpContext.RequestAborted); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        var company = await currentCompany.FindAsync(User);
        if (company is null) return Forbid();
        if (!company.SetupComplete) return RedirectToPage("Setup");
        Company = company;
        if (company.ConnectionProvider == ConnectionProviders.Odoo)
        {
            ConnectionDetail = "Connect Odoo to load company accounts.";
            try
            {
                var saved = await odoo.ReadAsync(User, HttpContext.RequestAborted);
                OdooWorkspace = saved;
                ConnectionStatus = saved.NeedsReconnect ? "Needs attention" : saved.Preview != null ? "Preview returned" : saved.AccountsFetchedAt != null ? "Accounts loaded" : saved.Configured ? "Connected" : "Not configured";
                if (saved.NeedsReconnect) ConnectionDetail = "Verify the connection again for the configured Odoo server.";
                if (saved.Configured)
                {
                    ConnectionDetail = saved.Preview != null ? "{0} rows in latest GL preview" : "{0} accounts in saved snapshot";
                    ConnectionCount = saved.Preview?.Samples.Sum(sample => sample.Rows.Count) ?? saved.Accounts.Count;
                }
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (OdooException) { ConnectionStatus = "Needs attention"; ConnectionDetail = "Review your Odoo connection settings."; }
            return Page();
        }
        var state = await connector.ReadAsync(company.Id,HttpContext.RequestAborted);
        if (state is not null)
        {
            ConnectionStatus = state.LastError is not null ? "Needs attention" : state.AccountsFetchedAt is null ? "Not tested" : "Accounts loaded";
            ConnectionDetail = state.LatestExtract is not null ? "{0} rows in latest GL extract" : "{0} accounts in saved snapshot";
            ConnectionCount = state.LatestExtract?.Lines.Count ?? state.Accounts.Count;
        }
        return Page();
    }
}
