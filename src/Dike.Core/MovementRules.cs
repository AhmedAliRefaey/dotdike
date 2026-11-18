namespace Dike.Core;

internal static class MovementRules
{
    internal static void Validate(Movement row)
    {
        if (row is null || row.Scope is null ||
            string.IsNullOrWhiteSpace(row.Scope.Company) || string.IsNullOrWhiteSpace(row.Scope.Subsidiary) ||
            string.IsNullOrWhiteSpace(row.Scope.AccountingBook) || string.IsNullOrWhiteSpace(row.Scope.Account))
            throw new InvalidOperationException("A complete account scope is required.");
        if (!string.Equals(row.Scope.Currency, "SAR", StringComparison.Ordinal))
            throw new InvalidOperationException("Only SAR movements are supported.");
        if (row.Side is not SourceSide.Bank and not SourceSide.Book)
            throw new InvalidOperationException("A supported bank or book source side is required.");
        if (string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.Version))
            throw new InvalidOperationException("Source identity and revision evidence are required.");
        if (row.Reference is null)
            throw new InvalidOperationException("Reference text must be supplied, using an empty string when absent.");
        if (!SarMoney.IsValid(row.Amount))
            throw new InvalidOperationException("SAR movement amounts must fit decimal(19,2) without rounding.");
    }
}
