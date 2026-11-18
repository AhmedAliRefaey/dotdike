using System.ComponentModel.DataAnnotations;
using Dike.Infrastructure.NetSuite;
using Dike.Web.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Dike.Web.Pages.Accounts;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(65536)]
public sealed class NetSuiteModel(CurrentCompany currentCompany, CompanyNetSuite connector) : PageModel
{
    [BindProperty] public ConnectionInput Input { get; set; } = new();
    public bool Configured { get; private set; }
    public string? AccountId { get; private set; }
    public sealed class ConnectionInput
    {
        [Required(ErrorMessage = "The {0} field is required."), StringLength(80, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name="NetSuite account ID")] public string AccountId { get; set; } = "";
        [Required(ErrorMessage = "The {0} field is required."), Display(Name = "Authentication method")] public string Mode { get; set; } = "TBA";
        [Required(ErrorMessage = "The {0} field is required."), StringLength(512, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name="Consumer key / client ID")] public string ClientId { get; set; } = "";
        [StringLength(512, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Consumer secret")] public string? ClientSecret { get; set; }
        [StringLength(512, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Token ID")] public string? TokenId { get; set; }
        [StringLength(512, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Token secret")] public string? TokenSecret { get; set; }
        [StringLength(512, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "Certificate ID")] public string? CertificateId { get; set; }
        [StringLength(16000, ErrorMessage = "{0} must be {1} characters or fewer."), Display(Name = "RSA private key (PEM)")] public string? PrivateKeyPem { get; set; }
    }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var company = await currentCompany.FindAsync(User); if (company is null) return Forbid();
        if (!company.SetupComplete) return RedirectToPage("/Company/Setup");
        var state = await connector.ReadAsync(company.Id,ct); Configured = state is not null; AccountId = state?.Connection.AccountId;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var company = await currentCompany.FindAsync(User); if (company is null) return Forbid();
        if (!company.SetupComplete) return RedirectToPage("/Company/Setup");
        if (ModelState.IsValid)
        {
            try
            {
                await connector.ConfigureAsync(company.Id,new() { AccountId=Input.AccountId, Mode=Input.Mode, ClientId=Input.ClientId.Trim(),
                    ClientSecret=Input.ClientSecret?.Trim() ?? "", TokenId=Input.TokenId?.Trim() ?? "", TokenSecret=Input.TokenSecret?.Trim() ?? "",
                    CertificateId=Input.CertificateId?.Trim() ?? "", PrivateKeyPem=Input.PrivateKeyPem ?? "" },ct);
                TempData["Notice"] = "Connection saved. Load GL accounts to test NetSuite access.";
                return RedirectToPage("Index");
            }
            catch (NetSuiteException e) { ModelState.AddModelError("",e.Message); }
        }
        // Never reflect secrets back into HTML, including on a failed submission.
        Input.ClientSecret = Input.TokenId = Input.TokenSecret = Input.PrivateKeyPem = null;
        foreach (var key in new[] { "Input.ClientSecret", "Input.TokenId", "Input.TokenSecret", "Input.PrivateKeyPem" }) ModelState.Remove(key);
        return Page();
    }
}
