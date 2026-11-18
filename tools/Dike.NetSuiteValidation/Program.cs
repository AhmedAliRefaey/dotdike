using System.Security.Cryptography;
using System.Text.Json;
using Dike.Infrastructure.NetSuite;

try
{
    if (args is ["contract", var contractPath])
    {
        WriteNew(contractPath, new
        {
            Status = "CandidateUnverified",
            NetSuiteQueryContract.QueryVersion,
            NetSuiteQueryContract.IdentityVersion,
            NetSuiteQueryContract.QuerySha256,
            NetSuiteQueryContract.ScopeSql,
            ScopeParameters = new[] { "account", "subsidiary", "book" },
            NetSuiteQueryContract.PostedGlSql,
            PostedGlParameters = new[] { "account", "subsidiary", "book", "from (inclusive)", "to (inclusive)" },
            IdentityFields = new[] { "TransactionId", "LineId", "BookId", "AccountId" },
            RequiredOuterScope = new[] { "CompanyId", "ConnectionId", "SubsidiaryId" }
        });
        Console.WriteLine("Candidate query contract written. Records Catalog and finance review are still required.");
        return 0;
    }
    if (args is ["compare", var extractPath, var referencePath, var outputPath])
    {
        var extractBytes = ReadInput(extractPath);
        var referenceBytes = ReadInput(referencePath);
        var result = GlReportComparison.Compare(GlValidationJson.Read(extractBytes), GlValidationJson.Read(referenceBytes));
        WriteNew(outputPath, new
        {
            SchemaVersion = "dike-gl-comparison-v1",
            ComparedAtUtc = DateTimeOffset.UtcNow,
            ExtractFileSha256 = Convert.ToHexString(SHA256.HashData(extractBytes)),
            ReferenceFileSha256 = Convert.ToHexString(SHA256.HashData(referenceBytes)),
            NetSuiteQueryContract.QueryVersion,
            NetSuiteQueryContract.IdentityVersion,
            NetSuiteQueryContract.QuerySha256,
            Result = result
        });
        Console.WriteLine(result.Matches
            ? "Inputs agree. Live contract validation and finance approval remain pending."
            : $"Comparison failed with {result.Issues.Count} issue(s). See the local report.");
        return result.Matches ? 0 : 1;
    }
    Console.Error.WriteLine("Usage: Dike.NetSuiteValidation contract <new-contract.json>");
    Console.Error.WriteLine("       Dike.NetSuiteValidation compare <extract.json> <reference.json> <new-report.json>");
    return 2;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
{
    // Input can contain financial data. Do not echo JSON values or file contents into console logs.
    Console.Error.WriteLine("Validation could not finish. Check the input schema, 64 MiB limit, file permissions and that the output file does not already exist.");
    return 2;
}

static byte[] ReadInput(string path)
{
    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    const int maxBytes = 64 * 1024 * 1024;
    if (stream.Length == 0 || stream.Length > maxBytes) throw new IOException("Invalid input length.");
    var bytes = new byte[(int)stream.Length];
    stream.ReadExactly(bytes);
    // Read UTF-8 with or without a BOM; retain the original bytes for the evidence hash.
    return bytes;
}

static void WriteNew<T>(string path, T value)
{
    var json = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions { WriteIndented = true });
    using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    stream.Write(json);
}
