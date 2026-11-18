using Dike.Web.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dike.Web.Pages.Auth;

public sealed class LogoutModel(SignInManager<ApplicationUser> signIn) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");
    public async Task<IActionResult> OnPostAsync()
    {
        await signIn.SignOutAsync();
        return RedirectToPage("/Index");
    }
}
