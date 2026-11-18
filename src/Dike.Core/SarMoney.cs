using System.Globalization;

namespace Dike.Core;

// Accepted SAR movement values must fit SQL decimal(19,2) without rounding.
public static class SarMoney
{
    public const decimal MaximumAmount = 99999999999999999.99m;

    public static bool IsValid(decimal value) =>
        value >= -MaximumAmount && value <= MaximumAmount && decimal.Round(value, 2) == value;

    public static bool TryParseExact(string raw, out decimal value)
    {
        value = default;
        var dot = raw.IndexOf('.');
        // Validate the source digits before decimal parsing can round them away.
        if (raw.Contains('e') || raw.Contains('E') ||
            (dot >= 0 && raw[(dot + 1)..].TrimEnd('0').Length > 2)) return false;
        return decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value) && IsValid(value);
    }
}
