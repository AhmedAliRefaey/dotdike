namespace Dike.Core;

// Megaera: deterministic whole-row proposals, with no external I/O.
public static class MatchingEngine
{
    public const string RuleVersion = "sar-v2";
    public static IReadOnlyList<Proposal> Propose(IEnumerable<Movement> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var rows = source.ToArray();
        foreach (var row in rows) MovementRules.Validate(row);
        if (rows.Select(x => (x.Scope, x.Side, x.Id)).Distinct().Count() != rows.Length)
            throw new InvalidOperationException("Only one current revision per scoped source identity may be proposed.");

        // Ordinal scope/ID ordering makes both proposals and their candidates reproducible.
        rows = rows.OrderBy(x => x.Scope.Company, StringComparer.Ordinal)
            .ThenBy(x => x.Scope.Subsidiary, StringComparer.Ordinal)
            .ThenBy(x => x.Scope.AccountingBook, StringComparer.Ordinal)
            .ThenBy(x => x.Scope.Account, StringComparer.Ordinal)
            .ThenBy(x => x.Scope.Currency, StringComparer.Ordinal)
            .ThenBy(x => x.Side)
            .ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        var books = rows.Where(x => x.Side == SourceSide.Book && x.Booked && x.Amount != 0).ToLookup(x => x.Scope);
        var result = new List<Proposal>();
        foreach (var bank in rows.Where(x => x.Side == SourceSide.Bank && x.Booked))
        {
            if (bank.Amount == 0)
            {
                result.Add(new(bank.Scope, bank.Id, [], ProposalKind.Unmatched, "Zero-value evidence is not eligible for reconciliation."));
                continue;
            }
            var nearby = books[bank.Scope].Where(x => BusinessDaysBetween(x.Date, bank.Date) <= 3).ToArray();
            var exact = nearby.Where(x => x.Amount == bank.Amount && SameReference(x.Reference, bank.Reference)).ToArray();
            var reference = nearby.Where(x => SameReference(x.Reference, bank.Reference)).ToArray();
            var amounts = nearby.Where(x => x.Amount == bank.Amount).ToArray();
            var candidates = exact.Length > 0 ? exact : reference.Length > 0 ? reference : amounts;
            var kind = exact.Length == 1 ? ProposalKind.Proposed : exact.Length > 1 ? ProposalKind.Ambiguous
                : reference.Length > 0 ? ProposalKind.AmountMismatch : amounts.Length > 0 ? ProposalKind.RequiresReview : ProposalKind.Unmatched;
            result.Add(new(bank.Scope, bank.Id, candidates.Select(x => x.Id).ToArray(), kind, kind switch
            {
                ProposalKind.Proposed => "Unique signed amount and reference within three business days.",
                ProposalKind.AmountMismatch => "Reference agrees but amount differs; supporting evidence is required.",
                ProposalKind.RequiresReview => "Amount and date alone require human review.",
                ProposalKind.Ambiguous => "Multiple matching ledger rows.",
                _ => "No eligible ledger candidate."
            }));
        }
        var contested = result.Where(x => x.Kind == ProposalKind.Proposed)
            .SelectMany(x => x.BookIds.Select(id => (x.Scope, Id: id)))
            .GroupBy(x => x).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
        return result.Select(x => x.Kind == ProposalKind.Proposed && x.BookIds.Any(id => contested.Contains((x.Scope, id)))
            ? x with { Kind = ProposalKind.Ambiguous, Reason = "Competing bank rows claim the same ledger row." } : x).ToArray();
    }
    private static bool SameReference(string a, string b) => !string.IsNullOrWhiteSpace(a) &&
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    public static int BusinessDaysBetween(DateOnly a, DateOnly b)
    {
        if (a > b) (a, b) = (b, a);
        var days = b.DayNumber - a.DayNumber;
        var count = days / 7 * 5;
        // Count at most six remaining days without stepping beyond DateOnly.MaxValue.
        for (var offset = 1; offset <= days % 7; offset++)
        {
            var day = (DayOfWeek)(((int)a.DayOfWeek + offset) % 7);
            if (day is not DayOfWeek.Friday and not DayOfWeek.Saturday) count++;
        }
        return count;
    }
}
