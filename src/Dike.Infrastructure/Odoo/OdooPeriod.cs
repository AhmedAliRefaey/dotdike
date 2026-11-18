using System.Globalization;

namespace Dike.Infrastructure.Odoo;

public sealed record OdooPeriod(DateOnly From, DateOnly To)
{
    // A week starts on the explicitly selected date; no fiscal-week convention is assumed.
    public static OdooPeriod Parse(string mode, string start, string? end = null)
    {
        try
        {
            if (mode == "month" && end == null &&
                DateOnly.TryParseExact(start + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
                return new(month, new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month)));
            if (!DateOnly.TryParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)) throw Invalid();
            if (mode == "week" && end == null) return new(from, from.AddDays(6));
            if (mode == "range" && DateOnly.TryParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) &&
                from <= to && to.DayNumber - from.DayNumber < OdooReadOnlyClient.MaxPeriodDays) return new(from, to);
        }
        catch (ArgumentOutOfRangeException) { throw Invalid(); }
        throw Invalid();
    }

    private static OdooException Invalid() => new("period", "Choose week YYYY-MM-DD, month YYYY-MM, or range YYYY-MM-DD YYYY-MM-DD (inclusive, at most 31 days).");
}
