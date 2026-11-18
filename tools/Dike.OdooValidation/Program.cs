using System.Globalization;
using System.Text.Json;
using Dike.Infrastructure.Odoo;
using Microsoft.Extensions.Configuration;

if (args is ["--help"])
{
    Usage();
    return 0;
}

try
{
    // Validate command syntax before loading credentials or making requests.
    var command = Parse(args);
    var configuration = new ConfigurationBuilder().AddUserSecrets<LocalConfigurationMarker>(optional: true)
        .AddEnvironmentVariables().Build();
    using var configurationLifetime = configuration as IDisposable;
    var connection = new OdooConnection(configuration["Odoo:BaseUrl"], configuration["Odoo:ApiKey"], configuration["Odoo:Database"]);
    using var client = new OdooReadOnlyClient(connection);
    // Interactive thinking time is unrestricted; each network call still has a 30-second deadline.
    using var deadline = command.Action == "browse" ? new CancellationTokenSource() : new CancellationTokenSource(TimeSpan.FromMinutes(2));
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; deadline.Cancel(); };
    if (command.Action == "browse")
    {
        await Interactive.RunAsync(client, connection.BaseUri, deadline.Token);
        return 0;
    }
    object result = command.Action switch
    {
        "companies" => await client.ListCompaniesAsync(command.AfterId, deadline.Token),
        "accounts" => await client.ListAccountsAsync(command.CompanyId, command.AfterId, deadline.Token),
        "gl" => await client.ReadPostedSampleAsync(command.CompanyId, command.AccountId,
            command.Period!.From, command.Period.To, command.Limit, deadline.Token),
        _ => throw new ArgumentException()
    };
    // JSON escapes terminal control characters in provider-supplied display names.
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Target = connection.BaseUri.GetLeftPart(UriPartial.Authority),
        ReadOnly = true,
        SourceContractVerified = false,
        Result = result
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (OdooException error)
{
    Console.Error.WriteLine($"Odoo test failed ({error.Code}): {error.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Odoo test cancelled or its total time limit was reached. No result was accepted.");
    return 1;
}
catch (ArgumentException)
{
    Usage();
    return 2;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
{
    Console.Error.WriteLine("Odoo test could not read local configuration or complete its operation. No secret or provider error content was displayed.");
    return 1;
}

static Command Parse(string[] args)
{
    if (args.Length == 0 || args is ["browse"]) return new("browse");
    if (args is ["companies"]) return new("companies");
    if (args is ["companies", var after]) return new("companies", AfterId: Number(after, allowZero: true));
    if (args is ["accounts", var company]) return new("accounts", CompanyId: Number(company));
    if (args is ["accounts", var company2, var after2]) return new("accounts", CompanyId: Number(company2), AfterId: Number(after2, allowZero: true));
    if (args.Length >= 5 && args[0] == "gl")
    {
        var extra = args[3] == "range" ? 1 : 0;
        if (args.Length != 5 + extra && args.Length != 6 + extra) throw new ArgumentException();
        var period = OdooPeriod.Parse(args[3], args[4], extra == 1 ? args[5] : null);
        var limit = args.Length == 6 + extra ? Number(args[^1]) : 10;
        if (limit > OdooReadOnlyClient.MaxSampleRows) throw new ArgumentException();
        return new("gl", Number(args[1]), Number(args[2]), Period: period, Limit: limit);
    }
    throw new ArgumentException();
}

static int Number(string text, bool allowZero = false) =>
    int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= (allowZero ? 0 : 1)
        ? value : throw new ArgumentException();

static void Usage()
{
    Console.WriteLine("Dike.OdooValidation [browse]  (interactive company, loaded-account and period selection)");
    Console.WriteLine("Dike.OdooValidation companies [after-id]");
    Console.WriteLine("Dike.OdooValidation accounts <company-id> [after-id]");
    Console.WriteLine("Dike.OdooValidation gl <company-id> <account-id> week <week-start:yyyy-MM-dd> [limit]");
    Console.WriteLine("Dike.OdooValidation gl <company-id> <account-id> month <yyyy-MM> [limit]");
    Console.WriteLine("Dike.OdooValidation gl <company-id> <account-id> range <from:yyyy-MM-dd> <to:yyyy-MM-dd> [limit]");
    Console.WriteLine("Load companies, then accounts; use a listed bank/cash account. Limit defaults to 10, maximum 50; custom ranges allow at most 31 days.");
    Console.WriteLine("Uses the web project's local User Secrets plus Odoo__ environment overrides. Never pass credentials as command arguments.");
}

internal sealed class LocalConfigurationMarker;
internal sealed record Command(string Action, int CompanyId = 0, int AccountId = 0, int AfterId = 0, OdooPeriod? Period = null, int Limit = 10);
