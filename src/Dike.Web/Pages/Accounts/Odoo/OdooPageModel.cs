using Dike.Infrastructure.Odoo;
using Dike.Web.Authentication;
using Dike.Web.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Dike.Web.Pages.Accounts.Odoo;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(65536)]
public abstract class OdooPageModel(CompanyOdoo connector, IStringLocalizer<UiText> text) : PageModel
{
    protected CompanyOdoo Connector { get; } = connector;
    protected IStringLocalizer<UiText> Text { get; } = text;
    public OdooWorkspace Workspace { get; protected set; } = OdooWorkspace.Empty;

    protected async Task<IActionResult> ShowAsync(CancellationToken ct)
    {
        try { Workspace = await Connector.ReadAsync(User, ct); return Page(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (OdooException error) when (error.Code == "setup") { return RedirectToPage("/Company/Setup"); }
        catch (OdooException error) { AddError(error); return Page(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return StatusCode(499); }
        catch (Exception) { ModelState.AddModelError("", Text["Odoo is temporarily unavailable. Please try again."]); return Page(); }
    }

    protected async Task<IActionResult> SubmitAsync(Func<Task> action, string notice, string destination, CancellationToken ct)
    {
        try { await action(); TempData["Notice"] = notice; return RedirectToPage(destination); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (OdooException error) when (error.Code == "setup") { return RedirectToPage("/Company/Setup"); }
        catch (OdooException error) { AddError(error); return await ShowAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return StatusCode(499); }
        catch (Exception) { ModelState.AddModelError("", Text["Odoo is temporarily unavailable. Please try again."]); return await ShowAsync(ct); }
    }

    protected void AddError(OdooException error) => ModelState.AddModelError("", Text[error.Code switch
    {
        "configuration" => "Check the API key and database settings.",
        "endpoint_configuration" => "Ask your administrator to configure a valid HTTPS Odoo server address.",
        "endpoint_changed" => "Verify the connection again for the configured Odoo server.",
        "http_401" => "Odoo rejected the API key. Check its expiry and permissions.",
        "http_403" => "Odoo denied access. Check the integration user's permissions.",
        "local_binding" => "The local connection is not assigned to this Dike workspace.",
        "not_configured" => "Connect Odoo before loading accounting data.",
        "selection" => "Select one to five loaded accounts and a valid period of up to 31 days.",
        "stale_selection" => "The account list changed. Select accounts again.",
        "period" => "Enter a valid week, calendar month, or date range of up to 31 days.",
        "scope" or "currency" => "The company or accounts are no longer accessible or eligible for this SAR preview.",
        "schema" or "money" or "response" => "Odoo returned data that could not be validated. The previous preview is retained.",
        "storage" => "The saved Odoo connection could not be read or updated. Please try again.",
        _ => "The Odoo request did not complete. The previous preview is retained."
    }]);
}
