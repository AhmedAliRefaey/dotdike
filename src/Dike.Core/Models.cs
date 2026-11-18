namespace Dike.Core;

public enum SourceSide { Bank, Book }
public enum ReviewStatus { Draft, Submitted, Approved, Returned, Reopened, Invalidated }
public enum ProposalKind { Proposed, Ambiguous, AmountMismatch, RequiresReview, Unmatched }

public sealed record AccountScope(string Company, string Subsidiary, string AccountingBook, string Account, string Currency);
public sealed record Movement(string Id, AccountScope Scope, SourceSide Side, decimal Amount,
    DateOnly Date, string Reference, string Version, bool Booked = true);
public sealed record GlAccount(string Id, string Name, string Subsidiary, string Currency, bool IsBank);
public sealed record SourceSnapshot(IReadOnlyList<Movement> Rows, string RunId, DateTimeOffset FetchedAt,
    DateTimeOffset? SourceAsOf, bool Complete);
public sealed record BankBalance(string AccountId, string Type, decimal Amount, string Currency, DateTimeOffset AsOf);
public sealed record Proposal(AccountScope Scope, string BankId, IReadOnlyList<string> BookIds, ProposalKind Kind, string Reason);

public interface INetSuiteReader
{
    Task<IReadOnlyList<GlAccount>> GetAccountsAsync(CancellationToken cancellationToken);
    Task<SourceSnapshot> ReadPostedMovementsAsync(AccountScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
public interface INeotekClient
{
    Task<SourceSnapshot> ReadTransactionsAsync(AccountScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<BankBalance>> ReadBalancesAsync(AccountScope scope, CancellationToken cancellationToken);
}
