using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace Dike.Web.Localization;

public static partial class UiMessages
{
    private const string QueryFailure = "NetSuite query failed (HTTP {0}). Check Records Catalog fields and permissions. Previous data is unchanged.";

    public static LocalizedString Localize(IStringLocalizer<UiText> text, string message)
    {
        var direct = text[message];
        if (!direct.ResourceNotFound) return direct;
        var match = QueryFailurePattern().Match(message);
        return match.Success ? text[QueryFailure, match.Groups[1].Value] : direct;
    }

    [GeneratedRegex(@"\ANetSuite query failed \(HTTP ([0-9]{3})\)\. Check Records Catalog fields and permissions\. Previous data is unchanged\.\z", RegexOptions.CultureInvariant)]
    private static partial Regex QueryFailurePattern();
}
