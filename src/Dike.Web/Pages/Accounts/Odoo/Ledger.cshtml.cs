using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Dike.Infrastructure.Odoo;
using Dike.Web.Authentication;
using Dike.Web.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Dike.Web.Pages.Accounts.Odoo;

public sealed class LedgerModel(CompanyOdoo connector, IStringLocalizer<UiText> text, TimeProvider clock) : OdooPageModel(connector, text)
{
    [BindProperty] public FilterInput Filter { get; set; } = new();
    public sealed class FilterInput
    {
        public Guid CatalogVersion { get; set; }
        public List<int> AccountIds { get; set; } = [];
        public string Mode { get; set; } = "month";
        public string? WeekStart { get; set; }
        public string? Month { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        [Range(1, 50, ErrorMessage = "{0} must be between {1} and {2}."), Display(Name = "Rows per account")]
        public int Limit { get; set; } = 10;
    }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await ShowAsync(ct);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(3)).DateTime);
        Filter.CatalogVersion = Workspace.CatalogVersion;
        Filter.Month = today.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        Filter.WeekStart = today.AddDays(-(int)today.DayOfWeek).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Filter.From = today.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Filter.To = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (Workspace.Preview is { Samples.Count: > 0 } preview)
        {
            var sample = preview.Samples[0];
            Filter.AccountIds = preview.Accounts.Select(account => account.Id).ToList();
            Filter.Mode = preview.PeriodMode;
            Filter.Limit = sample.Limit;
            Filter.Month = sample.From.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            Filter.WeekStart = Filter.From = sample.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            Filter.To = sample.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        return result;
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        IActionResult result;
        if (!ModelState.IsValid) result = await ShowAsync(ct);
        else
        {
            try
            {
                var start = Filter.Mode switch { "week" => Filter.WeekStart, "month" => Filter.Month, "range" => Filter.From, _ => null };
                var period = OdooPeriod.Parse(Filter.Mode, start ?? "", Filter.Mode == "range" ? Filter.To : null);
                result = await SubmitAsync(() => Connector.FetchAsync(User, Filter.CatalogVersion, Filter.AccountIds, period, Filter.Limit, ct, Filter.Mode),
                    "Posted GL preview loaded.", "Ledger", ct);
            }
            catch (OdooException error) { AddError(error); result = await ShowAsync(ct); }
        }
        Filter.CatalogVersion = Workspace.CatalogVersion;
        ModelState.Remove("Filter.CatalogVersion");
        return result;
    }
}
