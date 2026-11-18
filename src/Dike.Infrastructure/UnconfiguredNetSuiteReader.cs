using Dike.Core;

namespace Dike.Infrastructure;

// Replace registration with an adapter around the existing authenticated client.
// Never return fabricated finance data when a provider has not been configured.
public sealed class UnconfiguredNetSuiteReader : INetSuiteReader
{
    public Task<IReadOnlyList<GlAccount>> GetAccountsAsync(CancellationToken cancellationToken)
        => throw new InvalidOperationException("NetSuite is not configured. Register the existing authenticated NetSuite client adapter.");
    public Task<SourceSnapshot> ReadPostedMovementsAsync(AccountScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => throw new InvalidOperationException("NetSuite posted-movement integration has not been configured.");
}
