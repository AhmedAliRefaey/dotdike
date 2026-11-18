using System.ComponentModel.DataAnnotations;
using Dike.Web.Authentication;
using Dike.Web.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Dike.Web.Pages.Accounts.Odoo;

public sealed class ConnectionModel(CompanyOdoo connector, IStringLocalizer<UiText> text) : OdooPageModel(connector, text)
{
    [BindProperty] public ConnectionInput Input { get; set; } = new();
    public sealed class ConnectionInput
    {
        [StringLength(512, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "API key")]
        public string? ApiKey { get; set; }
        [StringLength(128, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Database name")]
        public string? Database { get; set; }
    }
    public Task<IActionResult> OnGetAsync(CancellationToken ct) => ShowAsync(ct);
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid) return await ShowAsync(ct);
            return await SubmitAsync(() => Connector.ConfigureAsync(User, Input.ApiKey, Input.Database, false, ct),
                "Odoo connection verified and saved.", "Accounts", ct);
        }
        finally
        {
            if (ModelState.TryGetValue("Input.ApiKey", out var keyState) && keyState.Errors.Count > 0)
                ModelState.AddModelError("", Text["Enter an API key with at most 512 characters."]);
            Input.ApiKey = null;
            ModelState.Remove("Input.ApiKey");
        }
    }
    public Task<IActionResult> OnPostLocalAsync(CancellationToken ct) => SubmitAsync(
        () => Connector.ConfigureAsync(User, null, null, true, ct), "Odoo connection verified and saved.", "Accounts", ct);
}
