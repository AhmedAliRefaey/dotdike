using System.Text.RegularExpressions;

namespace Dike.Infrastructure.Odoo;

// An immutable class, not a record: ToString must never serialize the credential.
public sealed class OdooConnection
{
    public Uri BaseUri { get; }
    internal string ApiKey { get; }
    internal string? Database { get; }

    public OdooConnection(string? baseUrl, string? apiKey, string? database = null)
    {
        BaseUri = ParseBaseUri(baseUrl);
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length > 512 || apiKey.Any(c => c < '!' || c > '~'))
            throw new OdooException("configuration", "A valid local Odoo:ApiKey is required. Run the masked setup script.");
        if (!string.IsNullOrEmpty(database) && !Regex.IsMatch(database, @"\A[A-Za-z0-9_.-]{1,128}\z"))
            throw new OdooException("configuration", "Odoo:Database has an unsupported format; use the verified database name only.");
        ApiKey = apiKey;
        Database = string.IsNullOrEmpty(database) ? null : database;
    }

    // The destination comes from trusted server configuration, never a browser form.
    public static Uri ParseBaseUri(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '\\') ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            uri.Host.Length == 0 || uri.Port <= 0 || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new OdooException("endpoint_configuration", "Set Odoo__BaseUrl (Odoo:BaseUrl) to the Odoo HTTPS root, without credentials, a path, query or fragment.");
        return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/");
    }
}

public sealed class OdooException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
