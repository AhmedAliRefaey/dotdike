using System.Text;
using System.Text.Json;
using Dike.Infrastructure.NetSuite;

internal static class GlValidationChecks
{
    public static void Run()
    {
        var day = new DateOnly(2026, 11, 1);
        var line = new GlLine("10", "1", "1", "100", "1", day, "R1", 125.50m, 0m);
        var data = new GlExtract("synthetic-run", new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
            "100", "1", "1", "SAR", day, day.AddDays(2), [line, line with { TransactionId = "11", Debit = 0m, Credit = 25.50m }])
        {
            QueryVersion = NetSuiteQueryContract.QueryVersion,
            IdentityVersion = NetSuiteQueryContract.IdentityVersion,
            QuerySha256 = NetSuiteQueryContract.QuerySha256
        };
        var input = new GlValidationInput("synthetic-company", "synthetic-connection", true, data);
        GlValidationInput Rows(params GlLine[] rows) => input with { Data = data with { Lines = rows } };
        void Reject(GlValidationInput other, string code, string name)
        {
            var result = GlReportComparison.Compare(input, other);
            Check(!result.Matches && result.Issues.Any(x => x.Code == code), name);
        }

        var match = GlReportComparison.Compare(input, input with { Data = data with { RunId = "independent-report", Lines = data.Lines.Reverse().ToArray() } });
        Check(match.Matches && match.ExtractTotals == new GlTotals(2, 125.50m, 25.50m, 100m), "GL comparison checks exact signed/debit/credit totals independent of order");
        Check(match.AcceptanceStatus == "AwaitingLiveValidationAndFinanceReview", "Offline agreement never grants finance approval");
        Reject(input with { CompanyId = "other" }, "SCOPE_MISMATCH", "GL comparison rejects foreign company");
        Reject(input with { ConnectionId = "other" }, "SCOPE_MISMATCH", "GL comparison rejects foreign connection");
        Reject(input with { Data = data with { SubsidiaryId = "2" } }, "ROW_OUTSIDE_SCOPE", "GL comparison rejects wrong subsidiary");
        Reject(input with { Data = data with { AccountingBookId = "2" } }, "ROW_OUTSIDE_SCOPE", "GL comparison rejects wrong book");
        Reject(input with { Data = data with { AccountId = "200" } }, "ROW_OUTSIDE_SCOPE", "GL comparison rejects wrong account");
        Reject(input with { Data = data with { From = day.AddDays(-1) } }, "PERIOD_MISMATCH", "GL comparison rejects different coverage");
        Reject(input with { Data = data with { Currency = "USD" } }, "UNSUPPORTED_CURRENCY", "GL comparison rejects non-SAR");
        Reject(input with { Complete = false }, "INCOMPLETE_INPUT", "GL comparison rejects incomplete reference");
        Check(!GlReportComparison.Compare(input with { Complete = false }, input).Matches, "GL comparison rejects incomplete extract");
        Reject(Rows(line, line), "DUPLICATE_IDENTITY", "GL comparison rejects repeated reference key");
        Check(GlReportComparison.Compare(Rows(line, line), input).Issues.Any(x => x.Code == "DUPLICATE_IDENTITY" && x.Source == "extract"), "GL comparison rejects repeated extract key");
        Reject(Rows(line with { TransactionId = "" }), "MISSING_IDENTITY", "GL comparison requires source identity");
        Reject(Rows(line), "NOT_IN_REFERENCE", "GL comparison finds extra extract line");
        Reject(input with { Data = data with { Lines = [.. data.Lines, line with { TransactionId = "12" }] } }, "MISSING_FROM_EXTRACT", "GL comparison finds missing extract line");
        var offsetting = input with { Data = data with { Lines = data.Lines.Select(x => x with { Debit = x.Debit + 50m, Credit = x.Credit + 50m }).ToArray() } };
        var sameNet = GlReportComparison.Compare(input, offsetting);
        Check(!sameNet.Matches && sameNet.ExtractTotals!.SignedAmount == sameNet.ReferenceTotals!.SignedAmount && sameNet.Issues.Any(x => x.Code == "ROW_MISMATCH"), "Equal net amounts do not conceal debit/credit differences");
        Reject(input with { Data = data with { Lines = data.Lines.Select(x => x with { Reference = "changed" }).ToArray() } }, "ROW_MISMATCH", "GL comparison checks references");
        Reject(Rows(line with { Date = day.AddDays(1) }), "ROW_MISMATCH", "GL comparison checks dates inside the same period");
        Reject(Rows(line with { Date = day.AddDays(5) }), "ROW_OUTSIDE_PERIOD", "GL comparison rejects out-of-period rows");
        Reject(Rows(line with { Debit = 1.001m }), "INVALID_MONEY", "GL comparison rejects excess precision");
        Reject(Rows(line with { Debit = 100000000000000000m }), "INVALID_MONEY", "GL comparison rejects SQL money overflow");
        Reject(Rows(line with { Debit = GlReportComparison.MaximumMoney, Credit = -GlReportComparison.MaximumMoney }), "INVALID_MONEY", "GL comparison validates signed amount overflow");
        Check(GlReportComparison.Compare(Rows(), Rows()).Matches, "Explicitly complete empty periods can agree");
        Check(GlReportComparison.Compare(Rows(line, line with { TransactionId = "12" }), Rows(line, line with { TransactionId = "12" })).Matches, "Equal-looking lines with distinct identities survive");
        Check(GlReportComparison.Compare(input with { Data = data with { QueryVersion = null } }, input).Issues.Any(x => x.Code == "UNVERIFIED_QUERY_PROVENANCE"), "Historical missing query provenance cannot pass comparison");
        Check(!GlReportComparison.Compare(input with { Data = data with { QuerySha256 = "wrong" } }, input).Matches, "Changed query hash cannot pass comparison");
        Check(!GlReportComparison.Compare(input with { Data = data with { IdentityVersion = "other" } }, input).Matches, "Changed candidate key version cannot pass comparison");
        Check(!GlReportComparison.Compare(input with { Data = null! }, input).Matches, "Null data yields a failed comparison");
        Check(!GlReportComparison.Compare(input with { Data = data with { Lines = [null!] } }, input).Matches, "Null source row yields a failed comparison");

        var json = JsonSerializer.Serialize(input);
        Check(GlReportComparison.Compare(GlValidationJson.Read(Encoding.UTF8.GetBytes(json)), input).Matches, "Strict validation input round-trips current extract");
        Check(GlReportComparison.Compare(GlValidationJson.Read([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(json)]), input).Matches, "UTF-8 BOM input is accepted");
        RejectJson(json.Replace("\"Debit\":125.50", "\"Debit\":125.50000000000000000000000000000001", StringComparison.Ordinal), "JSON precision cannot round away nonzero trailing digits");
        RejectJson(json.Replace("\"Debit\":125.50", "\"Debit\":1e2", StringComparison.Ordinal), "Validation JSON requires non-exponent decimal amounts");
        RejectJson(json.Replace("\"Complete\":true", "\"Complete\":true,\"Complete\":false", StringComparison.Ordinal), "Duplicate JSON fields are rejected");
        RejectJson(json.Replace("\"Complete\":true,", "", StringComparison.Ordinal), "Missing completeness declaration is rejected");
        RejectJson(json.Replace("\"CompanyId\":\"synthetic-company\"", "\"CompanyId\":null", StringComparison.Ordinal), "Null JSON scope is rejected");
        RejectJson(json.Replace("\"Credit\":0", "\"Credit\":null", StringComparison.Ordinal), "Null money is rejected");
        var oldJson = JsonSerializer.Serialize(new { data.RunId, data.FetchedAt, data.AccountId, data.SubsidiaryId, data.AccountingBookId, data.Currency, data.From, data.To, data.Lines });
        var oldExtract = JsonSerializer.Deserialize<GlExtract>(oldJson)!;
        Check(oldExtract.QueryVersion is null && oldExtract.IdentityVersion is null && oldExtract.QuerySha256 is null, "Historical extracts stay readable without invented provenance");
        Console.WriteLine("NetSuite validation checks passed (synthetic; no live acceptance).");
    }

    private static void RejectJson(string json, string name)
    {
        try { GlValidationJson.Read(Encoding.UTF8.GetBytes(json)); }
        catch (JsonException) { Check(true, name); return; }
        throw new Exception("Expected JSON rejection: " + name);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
