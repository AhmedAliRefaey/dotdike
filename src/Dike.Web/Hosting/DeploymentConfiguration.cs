using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using Dike.Infrastructure;
using Dike.Web.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dike.Web.Hosting;

public static class DeploymentConfiguration
{
    public static bool ConfigureDikeDeployment(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var vercel = configuration["VERCEL"] == "1" ||
            string.Equals(configuration["Hosting:Provider"], "Vercel", StringComparison.OrdinalIgnoreCase);
        var provider = configuration["Storage:Provider"] ?? "PostgreSql";
        var postgres = string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase);
        var hosted = vercel || !builder.Environment.IsDevelopment();
        if (!postgres && !string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage:Provider must be PostgreSql or Sqlite.");
        if (hosted && !postgres)
            throw new InvalidOperationException("Hosted deployments require PostgreSQL. SQLite is available only in Development.");

        if (postgres)
        {
            var value = new[] { configuration.GetConnectionString("Identity"), configuration["DATABASE_URL"], configuration["POSTGRES_URL"] }
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                ?? throw new InvalidOperationException("Set DATABASE_URL or ConnectionStrings__Identity to your PostgreSQL database connection.");
            var connection = PostgresConnection.Parse(value, hosted);
            // A connector can recheck Identity while holding a document transaction.
            // Give documents their own pool so waiting writers cannot exhaust Identity's connections.
            builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connection));
            builder.Services.AddDbContext<CompanyIdentityDbContext>(options =>
                options.UseNpgsql(connection, pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "dike")));
            builder.Services.AddSingleton<ICompanyDocumentStorage, PostgresCompanyDocumentStorage>();

            var finance = configuration.GetConnectionString("Dike");
            var financeConnection = string.IsNullOrWhiteSpace(finance) ? connection : PostgresConnection.Parse(finance, hosted);
            builder.Services.AddDbContext<DikeDbContext>(options =>
                options.UseNpgsql(financeConnection, pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "dike_finance")));

            var protection = builder.Services.AddDataProtection().SetApplicationName("Dike.Web");
            var certificateValue = configuration["DataProtection:CertificateBase64"];
            if (hosted && string.IsNullOrWhiteSpace(certificateValue))
                throw new InvalidOperationException("Set DataProtection__CertificateBase64 to the persistent PFX certificate for encrypting stored keys.");
            if (!string.IsNullOrWhiteSpace(certificateValue))
            {
                var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(certificateValue),
                    configuration["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
                if (!certificate.HasPrivateKey) { certificate.Dispose(); throw new InvalidOperationException("The Data Protection certificate must contain its private key."); }
                builder.Services.AddSingleton(_ => certificate);
                protection.PersistKeysToDbContext<CompanyIdentityDbContext>().ProtectKeysWithCertificate(certificate);
            }
        }
        else
        {
            var connectionString = configuration.GetConnectionString("Identity");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                var path = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
                Directory.CreateDirectory(path);
                connectionString = $"Data Source={Path.Combine(path, "identity.db")}";
            }
            builder.Services.AddDbContext<CompanyIdentityDbContext>(options => options.UseSqlite(connectionString));
            builder.Services.AddSingleton<ICompanyDocumentStorage, FileCompanyDocumentStorage>();
        }

        if (!vercel) return false;

        var portValue = configuration["PORT"] ?? "80";
        if (!int.TryParse(portValue, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
            throw new InvalidOperationException("PORT must be an integer between 1 and 65535.");
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        // Vercel's generated deployment and production hostnames are supplied as system variables.
        var hosts = (configuration["AllowedHosts"] ?? "localhost").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var key in new[] { "VERCEL_URL", "VERCEL_PROJECT_PRODUCTION_URL" })
            if (configuration[key] is { Length: > 0 } host && Uri.CheckHostName(host) == UriHostNameType.Dns)
                hosts.Add(host);
        configuration["AllowedHosts"] = string.Join(';', hosts.Distinct(StringComparer.OrdinalIgnoreCase));

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Only Vercel's ingress can reach the function. Trust its single scheme header;
            // do not accept forwarded hosts or client IPs from arbitrary request headers.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        return true;
    }
}
