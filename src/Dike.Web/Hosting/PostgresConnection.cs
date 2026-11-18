using System.Web;
using Npgsql;

namespace Dike.Web.Hosting;

internal static class PostgresConnection
{
    internal static string Parse(string value, bool hosted)
    {
        NpgsqlConnectionStringBuilder connection;
        try
        {
            connection = value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                         value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                ? FromUrl(new Uri(value))
                : new NpgsqlConnectionStringBuilder(value);
            if (string.IsNullOrWhiteSpace(connection.Host)) throw new FormatException();
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or KeyNotFoundException)
        {
            // Parser errors can contain credentials from the supplied URL.
            throw new InvalidOperationException("Invalid PostgreSQL connection. Supply a PostgreSQL URL or an Npgsql connection string.");
        }
        if (hosted)
        {
            if (connection.ShouldSerialize("SSL Mode") && connection.SslMode is SslMode.Disable or SslMode.Allow or SslMode.Prefer)
                throw new InvalidOperationException("Hosted PostgreSQL requires TLS. Use SSL Mode=VerifyFull or sslmode=verify-full.");
            connection.SslMode = SslMode.VerifyFull;
            connection.GssEncryptionMode = GssEncryptionMode.Disable;
            if (!connection.ShouldSerialize("Maximum Pool Size")) connection.MaxPoolSize = 10;
        }
        return connection.ConnectionString;
    }

    private static NpgsqlConnectionStringBuilder FromUrl(Uri uri)
    {
        if (uri.Host.Length == 0 || uri.AbsolutePath.Length < 2 || uri.AbsolutePath[1..].Contains('/') ||
            uri.Fragment.Length != 0) throw new FormatException();
        var credentials = uri.UserInfo.Split(':', 2);
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = uri.IdnHost.Trim('[', ']'),
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath[1..])
        };
        if (credentials[0].Length > 0) connection.Username = Uri.UnescapeDataString(credentials[0]);
        if (credentials.Length == 2) connection.Password = Uri.UnescapeDataString(credentials[1]);
        var query = HttpUtility.ParseQueryString(uri.Query);
        foreach (var name in query.AllKeys)
        {
            if (name is null || query.GetValues(name) is not [var setting]) throw new FormatException();
            switch (name.ToLowerInvariant())
            {
                case "sslmode": connection["SSL Mode"] = setting.Replace("-", ""); break;
                case "channel_binding": connection["Channel Binding"] = setting; break;
                case "sslrootcert": connection.RootCertificate = setting; break;
                case "connect_timeout": connection["Timeout"] = setting; break;
                case "application_name": connection.ApplicationName = setting; break;
                case "options": connection.Options = setting; break;
                default: throw new FormatException();
            }
        }
        return connection;
    }
}
