using Dike.Core;
using Dike.Infrastructure.NetSuite;
using System.ComponentModel.DataAnnotations;
using Dike.Web.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dike.Web.Pages.Accounts;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel(CurrentCompany currentCompany, CompanyNetSuite connector) : PageModel
{
    public NetSuiteState? State { get; private set; }
    [BindProperty] public PeriodInput Period { get; set; } = new();
    public sealed class PeriodInput
    {
        [Required(ErrorMessage = "The {0} field is required."), RegularExpression("^[0-9]+$", ErrorMessage = "{0} must contain digits only."), Display(Name = "Account")] public string Account { get; set; } = "";
        [Required(ErrorMessage = "The {0} field is required."), RegularExpression("^[0-9]+$", ErrorMessage = "{0} must contain digits only."), Display(Name = "Subsidiary")] public string Subsidiary { get; set; } = "";
        [Required(ErrorMessage = "The {0} field is required."), RegularExpression("^[0-9]+$", ErrorMessage = "{0} must contain digits only."), Display(Name = "Book")] public string Book { get; set; } = "";
        [DataType(DataType.Date), Display(Name = "From")] public DateOnly From { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        [DataType(DataType.Date), Display(Name = "To")] public DateOnly To { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    }
    public IReadOnlyList<GlAccount> Accounts { get; private set; } = [];
    public string? SetupMessage { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var company = await currentCompany.FindAsync(User);
        if (company is null) return Forbid();
        if (!company.SetupComplete) return RedirectToPage("/Company/Setup");
        State = await connector.ReadAsync(company.Id, ct);
        Accounts = State?.Accounts ?? [];
        SetupMessage = State is null ? "Configure your company NetSuite connection to begin." : State.AccountsFetchedAt is null ? "Connection saved. Load GL accounts to verify access." : null;
        return Page();
    }
    public async Task<IActionResult> OnPostLoadAccountsAsync(CancellationToken ct)
    {
        ModelState.Clear();
        return await CollectAsync(true,ct);
    }
    public async Task<IActionResult> OnPostImportAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await OnGetAsync(ct);
        return await CollectAsync(false,ct);
    }
    private async Task<IActionResult> CollectAsync(bool accountsOnly, CancellationToken ct)
    {
        var company = await currentCompany.FindAsync(User); if (company is null) return Forbid();
        if (!company.SetupComplete) return RedirectToPage("/Company/Setup");
        try
        {
            await connector.CollectAsync(company.Id,accountsOnly,Period.Account,Period.Subsidiary,Period.Book,Period.From,Period.To,ct);
            TempData["Notice"] = accountsOnly ? "Complete GL account list loaded." : "Complete GL extract saved. Validate the signed total against the NetSuite report.";
            return RedirectToPage();
        }
        catch (NetSuiteException e) { ModelState.AddModelError("",e.Message); return await OnGetAsync(ct); }
    }
}
