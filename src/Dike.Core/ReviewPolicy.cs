namespace Dike.Core;

public static class ReviewPolicy
{
    public static void ValidateSubmission(IReadOnlyList<Movement> rows, string reason)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A review reason is required.");
        foreach (var row in rows) MovementRules.Validate(row);
        if (rows.Count < 2 || rows.Select(x => x.Scope).Distinct().Count() != 1)
            throw new InvalidOperationException("Select bank and book rows from one account scope.");
        if (rows.Select(x => (x.Side, x.Id)).Distinct().Count() != rows.Count || rows.Any(x => !x.Booked))
            throw new InvalidOperationException("Duplicate or pending rows are not eligible.");
        var bank = rows.Where(x => x.Side == SourceSide.Bank).ToArray();
        var book = rows.Where(x => x.Side == SourceSide.Book).ToArray();
        if (bank.Length == 0 || book.Length == 0 || (bank.Length > 1 && book.Length > 1))
            throw new InvalidOperationException("Only one-to-one, one-to-many and many-to-one groups are supported.");
        if (rows.Any(x => x.Amount == 0) || rows.Select(x => Math.Sign(x.Amount)).Distinct().Count() != 1)
            throw new InvalidOperationException("All rows must have a consistent nonzero cash direction.");
        if (bank.Sum(x => x.Amount) != book.Sum(x => x.Amount))
            throw new InvalidOperationException("Groups must balance exactly; monetary tolerance is zero.");
    }

    public static void ValidateApproval(string preparer, string approver, ReviewStatus status)
    {
        if (status != ReviewStatus.Submitted) throw new InvalidOperationException("Only submitted groups can be approved.");
        if (string.IsNullOrWhiteSpace(preparer) || string.IsNullOrWhiteSpace(approver) ||
            string.Equals(preparer, approver, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A different controller must approve the group.");
    }
}
