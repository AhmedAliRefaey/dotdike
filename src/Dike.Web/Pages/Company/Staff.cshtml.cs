using Dike.Web.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dike.Web.Pages.Company;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class StaffModel(StaffService staff) : PageModel
{
    public StaffPage Directory { get; private set; } = null!;

    public IActionResult OnPost() => StatusCode(StatusCodes.Status405MethodNotAllowed);

    public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
    {
        if (!ModelState.IsValid) return BadRequest();
        try
        {
            Directory = await staff.GetPageAsync(User, pageNumber, cancellationToken: HttpContext.RequestAborted);
            return pageNumber > 1 && Directory.Members.Count == 0 ? NotFound() : Page();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentOutOfRangeException) { return BadRequest(); }
    }
}
