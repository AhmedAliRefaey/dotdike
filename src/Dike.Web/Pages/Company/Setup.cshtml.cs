using System.ComponentModel.DataAnnotations;
using Dike.Web.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dike.Web.Pages.Company;
public sealed class SetupModel(CurrentCompany currentCompany, CompanyIdentityDbContext db, CompanyAdministrator access) : PageModel
{
    [BindProperty] public CompanyProfile Input { get; set; } = new();
    public sealed class CompanyProfile
    {
        [Required(ErrorMessage = "The {0} field is required."), StringLength(160, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Company legal name")] public string Name { get; set; } = "";
        [Required(ErrorMessage = "The {0} field is required."), StringLength(80, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Country")] public string Country { get; set; } = "Saudi Arabia";
        [Required(ErrorMessage = "Choose NetSuite or Odoo."), RegularExpression(ConnectionProviders.Pattern, ErrorMessage = "Choose NetSuite or Odoo."), Display(Name = "Connection provider")]
        public string ConnectionProvider { get; set; } = "";
        [StringLength(80, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Commercial registration number (optional)")] public string? RegistrationNumber { get; set; }
        [StringLength(500, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Business address (optional)")] public string? Address { get; set; }
    }
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CanEditAsync()) return Forbid();
        var company = await currentCompany.FindAsync(User);
        if (company is null) return Forbid();
        Input = new() { Name = company.Name, Country = company.Country, ConnectionProvider = company.ConnectionProvider,
            RegistrationNumber = company.RegistrationNumber, Address = company.Address };
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!await CanEditAsync()) return Forbid();
        if (!ModelState.IsValid) return Page();
        var company = await currentCompany.FindAsync(User);
        if (company is null) return Forbid();
        company.Name = Input.Name.Trim();
        company.Country = Input.Country.Trim();
        company.ConnectionProvider = Input.ConnectionProvider;
        company.RegistrationNumber = Input.RegistrationNumber?.Trim() ?? "";
        company.Address = Input.Address?.Trim() ?? "";
        company.SetupComplete = true;
        await db.SaveChangesAsync();
        TempData["Notice"] = "Company profile saved. Your workspace is ready.";
        return RedirectToPage("Index");
    }
    private async Task<bool> CanEditAsync()
    {
        try { await access.RequireAsync(User, HttpContext.RequestAborted); return true; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
