using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dike.Web.Pages;

public sealed class LanguageModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public IActionResult OnPost(string culture, string? returnUrl)
    {
        if (culture is not ("en" or "ar")) return BadRequest();
        // Keep accounting numbers and date binding Gregorian in both interfaces.
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture("en", culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Path = Request.PathBase.HasValue ? Request.PathBase.Value : "/"
            });
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToPage("/Index");
    }
}
