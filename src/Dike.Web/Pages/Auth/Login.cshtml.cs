using Dike.Web.Authentication;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dike.Web.Pages.Auth;

public sealed class LoginModel(SignInManager<ApplicationUser> signIn, CurrentCompany currentCompany) : PageModel
{
    [BindProperty] public Credentials Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public sealed class Credentials
    {
        [Required(ErrorMessage = "The {0} field is required."), EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(254, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Work email")] public string Email { get; set; } = "";
        [Required(ErrorMessage = "The {0} field is required."), StringLength(128, ErrorMessage = "{0} must be {1} characters or fewer."), DataType(DataType.Password), Display(Name = "Password")] public string Password { get; set; } = "";
    }
    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Company/Index") : Page();
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await signIn.PasswordSignInAsync(Input.Email.Trim(), Input.Password, false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Unable to sign in. Check your credentials, or wait 15 minutes if you have tried repeatedly.");
            return Page();
        }
        var user = await signIn.UserManager.FindByEmailAsync(Input.Email.Trim());
        var principal = await signIn.CreateUserPrincipalAsync(user!);
        var company = await currentCompany.FindAsync(principal);
        if (company?.SetupComplete != true) return RedirectToPage("/Company/Setup");
        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl!) : RedirectToPage("/Company/Index");
    }
}
