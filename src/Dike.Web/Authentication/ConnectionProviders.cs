namespace Dike.Web.Authentication;

public static class ConnectionProviders
{
    public const string NetSuite = "NetSuite";
    public const string Odoo = "Odoo";
    public const string Pattern = "\\A(?:NetSuite|Odoo)\\z";
    public static bool IsSupported(string? provider) => provider is NetSuite or Odoo;
    public static string Page(string provider) => provider == Odoo ? "/Accounts/Odoo/Index" : "/Accounts/Index";
}
