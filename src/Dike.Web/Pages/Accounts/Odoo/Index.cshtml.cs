using Dike.Web.Authentication;
using Dike.Web.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Dike.Web.Pages.Accounts.Odoo;

public sealed class IndexModel(CompanyOdoo connector, IStringLocalizer<UiText> text) : OdooPageModel(connector, text)
{
    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ShowAsync(ct);
}
