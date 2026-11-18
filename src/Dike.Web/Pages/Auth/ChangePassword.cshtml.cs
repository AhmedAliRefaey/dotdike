using System.ComponentModel.DataAnnotations;
using Dike.Web.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dike.Web.Pages.Auth;
public sealed class ChangePasswordModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : PageModel
{
    [BindProperty] public PasswordInput Input { get; set; } = new();
    public sealed class PasswordInput
    {
        [Required(ErrorMessage = "The {0} field is required."), DataType(DataType.Password), Display(Name = "Current password")] public string Current { get; set; } = "";
        [Required(ErrorMessage = "The {0} field is required."), StringLength(128, MinimumLength = 12, ErrorMessage = "Use between {2} and {1} characters for {0}."), DataType(DataType.Password), Display(Name = "New password")] public string New { get; set; } = "";
        [Required(ErrorMessage = "The {0} field is required."), Compare(nameof(New), ErrorMessage = "{0} and {1} do not match."), DataType(DataType.Password), Display(Name = "Confirm new password")] public string Confirm { get; set; } = "";
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        var result = await users.ChangePasswordAsync(user, Input.Current, Input.New);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return Page();
        }
        await signIn.RefreshSignInAsync(user);
        TempData["Notice"] = "Your password has been updated.";
        return RedirectToPage("/Company/Index");
    }
}
