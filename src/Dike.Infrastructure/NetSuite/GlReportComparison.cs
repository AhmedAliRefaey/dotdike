namespace Dike.Infrastructure.NetSuite;

// Offline evidence only. Scope/completeness are operator declarations, not authorization.
public sealed record GlValidationInput(string CompanyId, string ConnectionId, bool Complete, GlExtract Data);
public sealed record GlValidationIssue(string Code, string Source, int? Row, string Field);
public sealed record GlTotals(int Rows, decimal Debit, decimal Credit, decimal SignedAmount);
public sealed record GlComparisonResult(bool Matches, GlTotals? ExtractTotals, GlTotals? ReferenceTotals,
    IReadOnlyList<GlValidationIssue> Issues)
{
    public string AcceptanceStatus => "AwaitingLiveValidationAndFinanceReview";
}

public static class GlReportComparison
{
    public const decimal MaximumMoney = NetSuiteMoney.MaximumAmount;

    // Structured components avoid delimiter collisions in provisional provider IDs.
    private sealed record LineKey(string Transaction, string Line, string Book, string Account, string Subsidiary);
    private sealed record IndexedLine(GlLine Value, int Row);

    public static GlComparisonResult Compare(GlValidationInput extract, GlValidationInput reference)
    {
        var issues = new List<GlValidationIssue>();
        var actual = Index(extract, "extract", issues, out var extractTotals);
        var expected = Index(reference, "reference", issues, out var referenceTotals);
        if (extract?.Data is null || reference?.Data is null)
            return new(false, extractTotals, referenceTotals, issues);

        Equal(extract.CompanyId, reference.CompanyId, "CompanyId");
        Equal(extract.ConnectionId, reference.ConnectionId, "ConnectionId");
        Equal(extract.Data.AccountId, reference.Data.AccountId, "AccountId");
        Equal(extract.Data.SubsidiaryId, reference.Data.SubsidiaryId, "SubsidiaryId");
        Equal(extract.Data.AccountingBookId, reference.Data.AccountingBookId, "AccountingBookId");
        Equal(extract.Data.Currency, reference.Data.Currency, "Currency");
        if (extract.Data.From != reference.Data.From || extract.Data.To != reference.Data.To)
            issues.Add(new("PERIOD_MISMATCH", "both", null, "From/To"));

        if (extract.Data.QueryVersion != NetSuiteQueryContract.QueryVersion ||
            extract.Data.IdentityVersion != NetSuiteQueryContract.IdentityVersion ||
            extract.Data.QuerySha256 != NetSuiteQueryContract.QuerySha256)
            issues.Add(new("UNVERIFIED_QUERY_PROVENANCE", "extract", null, "QueryVersion/IdentityVersion/QuerySha256"));

        foreach (var (key, line) in actual)
        {
            if (!expected.TryGetValue(key, out var reported))
            {
                issues.Add(new("NOT_IN_REFERENCE", "extract", line.Row, "Identity"));
                continue;
            }
            CompareField(line.Value.Debit == reported.Value.Debit, "Debit");
            CompareField(line.Value.Credit == reported.Value.Credit, "Credit");
            CompareField(line.Value.Date == reported.Value.Date, "Date");
            CompareField(line.Value.Reference == reported.Value.Reference, "Reference");

            void CompareField(bool agrees, string field)
            {
                if (!agrees) issues.Add(new("ROW_MISMATCH", "extract", line.Row, field));
            }
        }
        foreach (var (key, line) in expected)
            if (!actual.ContainsKey(key)) issues.Add(new("MISSING_FROM_EXTRACT", "reference", line.Row, "Identity"));

        if (extractTotals is not null && referenceTotals is not null && extractTotals != referenceTotals)
            issues.Add(new("TOTALS_MISMATCH", "both", null, "Rows/Debit/Credit/SignedAmount"));

        return new(issues.Count == 0, extractTotals, referenceTotals, issues);

        void Equal(string left, string right, string field)
        {
            if (!string.Equals(left, right, StringComparison.Ordinal))
                issues.Add(new("SCOPE_MISMATCH", "both", null, field));
        }
    }

    private static Dictionary<LineKey, IndexedLine> Index(GlValidationInput? input, string source,
        List<GlValidationIssue> issues, out GlTotals? totals)
    {
        var rows = new Dictionary<LineKey, IndexedLine>();
        totals = null;
        if (input?.Data is not { } data)
        {
            issues.Add(new("MISSING_DATA", source, null, "Data"));
            return rows;
        }

        Required(input.CompanyId, "CompanyId");
        Required(input.ConnectionId, "ConnectionId");
        Required(data.RunId, "RunId");
        Required(data.AccountId, "AccountId");
        Required(data.SubsidiaryId, "SubsidiaryId");
        Required(data.AccountingBookId, "AccountingBookId");
        if (!input.Complete) issues.Add(new("INCOMPLETE_INPUT", source, null, "Complete"));
        if (data.Currency != "SAR") issues.Add(new("UNSUPPORTED_CURRENCY", source, null, "Currency"));
        if (data.From == default || data.To == default || data.To < data.From)
            issues.Add(new("INVALID_PERIOD", source, null, "From/To"));
        if (data.FetchedAt == default || data.FetchedAt.Offset != TimeSpan.Zero)
            issues.Add(new("INVALID_FETCH_TIME", source, null, "FetchedAt"));
        if (data.Lines is null)
        {
            issues.Add(new("MISSING_ROWS", source, null, "Lines"));
            return rows;
        }

        decimal debit = 0, credit = 0;
        var validMoney = true;
        for (var index = 0; index < data.Lines.Count; index++)
        {
            var row = index + 1;
            var line = data.Lines[index];
            if (line is null)
            {
                issues.Add(new("MISSING_ROW", source, row, "Lines"));
                validMoney = false;
                continue;
            }
            if (new[] { line.TransactionId, line.LineId, line.BookId, line.AccountId, line.SubsidiaryId }.Any(string.IsNullOrWhiteSpace))
                issues.Add(new("MISSING_IDENTITY", source, row, "Identity"));
            else if (!rows.TryAdd(new(line.TransactionId, line.LineId, line.BookId, line.AccountId, line.SubsidiaryId), new(line, row)))
                issues.Add(new("DUPLICATE_IDENTITY", source, row, "Identity"));
            if (line.AccountId != data.AccountId || line.SubsidiaryId != data.SubsidiaryId || line.BookId != data.AccountingBookId)
                issues.Add(new("ROW_OUTSIDE_SCOPE", source, row, "AccountId/SubsidiaryId/BookId"));
            if (line.Date == default || line.Date < data.From || line.Date > data.To)
                issues.Add(new("ROW_OUTSIDE_PERIOD", source, row, "Date"));
            if (line.Reference is null) issues.Add(new("MISSING_REFERENCE_FIELD", source, row, "Reference"));

            if (!Money(line.Debit) || !Money(line.Credit))
            {
                issues.Add(new("INVALID_MONEY", source, row, "Debit/Credit"));
                validMoney = false;
                continue;
            }
            if (!Money(line.SignedAmount))
            {
                issues.Add(new("INVALID_MONEY", source, row, "SignedAmount"));
                validMoney = false;
            }
            try { debit = checked(debit + line.Debit); credit = checked(credit + line.Credit); }
            catch (OverflowException)
            {
                issues.Add(new("TOTAL_OVERFLOW", source, row, "Debit/Credit"));
                validMoney = false;
            }
        }
        if (validMoney)
        {
            try { totals = new(data.Lines.Count, debit, credit, checked(debit - credit)); }
            catch (OverflowException) { issues.Add(new("TOTAL_OVERFLOW", source, null, "SignedAmount")); }
        }
        return rows;

        void Required(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) issues.Add(new("MISSING_FIELD", source, null, field));
        }
    }

    private static bool Money(decimal value) => NetSuiteMoney.IsValid(value);
}
