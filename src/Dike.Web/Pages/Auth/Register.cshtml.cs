using System.ComponentModel.DataAnnotations;
using Dike.Web.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Dike.Web.Pages.Auth;
public sealed class RegisterModel(CompanyIdentityDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn) : PageModel
{
    [BindProperty] public Registration Input { get; set; } = new();
    public sealed class Registration
    {
        [Required(ErrorMessage = "The {0} field is required."), StringLength(160, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Company name")] public string CompanyName { get; set; } = "";
        [Required(ErrorMessage = "Choose NetSuite or Odoo."), RegularExpression(ConnectionProviders.Pattern, ErrorMessage = "Choose NetSuite or Odoo."), Display(Name = "Connection provider")]
        public string ConnectionProvider { get; set; } = "";
        [Required(ErrorMessage = "The {0} field is required."), StringLength(100, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Your full name")] public string FullName { get; set; } = "";
        [Required(ErrorMessage = "The {0} field is required."), EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(254, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Work email")] public string Email { get; set; } = "";
        [Required(ErrorMessage = "The {0} field is required."), StringLength(128, MinimumLength = 12, ErrorMessage = "Use between {2} and {1} characters for {0}."), DataType(DataType.Password), Display(Name = "Password")] public string Password { get; set; } = "";
        [Required(ErrorMessage = "The {0} field is required."), Compare(nameof(Password), ErrorMessage = "{0} and {1} do not match."), DataType(DataType.Password), Display(Name = "Confirm password")] public string ConfirmPassword { get; set; } = "";
        [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm that you are authorized to register this company.")]
        public bool Authorized { get; set; }
    }
    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Company/Index") : Page();
    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Company/Index");
        if (!ModelState.IsValid) return Page();
        var company = new Dike.Web.Authentication.Company { Name = Input.CompanyName.Trim(), ConnectionProvider = Input.ConnectionProvider };
        var user = new ApplicationUser { UserName = Input.Email.Trim(), Email = Input.Email.Trim(), FullName = Input.FullName.Trim(), CompanyId = company.Id };
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            var created = await users.CreateAsync(user, Input.Password);
            if (!created.Succeeded)
            {
                foreach (var error in created.Errors)
                    ModelState.AddModelError("", error.Code.StartsWith("Duplicate", StringComparison.Ordinal)
                        ? "Registration could not be completed. If you already have an account, sign in." : error.Description);
                return Page();
            }
            var assigned = await users.AddToRoleAsync(user, "Administrator");
            if (!assigned.Succeeded) { ModelState.AddModelError("", "Registration could not be completed. Please try again."); return Page(); }
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Registration could not be completed. If you already have an account, sign in.");
            return Page();
        }
        await signIn.SignInAsync(user, isPersistent: false);
        return RedirectToPage("/Company/Setup");
    }
}
