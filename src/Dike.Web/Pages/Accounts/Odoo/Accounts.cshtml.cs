using Dike.Web.Authentication;
using Dike.Web.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Dike.Web.Pages.Accounts.Odoo;

public sealed class AccountsModel(CompanyOdoo connector, IStringLocalizer<UiText> text) : OdooPageModel(connector, text)
{
    [BindProperty] public int CompanyId { get; set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await ShowAsync(ct);
        CompanyId = Workspace.SelectedCompanyId ?? 0;
        return result;
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ShowAsync(ct);
        return await SubmitAsync(() => Connector.LoadAccountsAsync(User, CompanyId, ct: ct),
            "Odoo accounts loaded. Choose accounts and a period for your preview.", "Accounts", ct);
    }
    public Task<IActionResult> OnPostMoreCompaniesAsync(CancellationToken ct) => SubmitAsync(
        () => Connector.MoreCompaniesAsync(User, ct), "More Odoo companies loaded.", "Accounts", ct);
    public Task<IActionResult> OnPostMoreAccountsAsync(CancellationToken ct) => SubmitAsync(
        () => Connector.LoadAccountsAsync(User, CompanyId, more: true, ct: ct), "More Odoo accounts loaded.", "Accounts", ct);
}
