using Dike.Core;
using Microsoft.EntityFrameworkCore;

namespace Dike.Infrastructure;

public sealed class AccountMapping
{
    public Guid Id { get; set; }
    public string Company { get; set; } = "";
    public string Subsidiary { get; set; } = "";
    public string AccountingBook { get; set; } = "";
    public string GlAccount { get; set; } = "";
    public string Currency { get; set; } = "SAR";
    public string Connection { get; set; } = "";
    public string ExternalAccount { get; set; } = "";
    public byte[] Version { get; set; } = [];
}
public sealed class StoredMovement
{
    public Guid Id { get; set; }
    public Guid MappingId { get; set; }
    public SourceSide Side { get; set; }
    public string SourceId { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string Reference { get; set; } = "";
    public bool Booked { get; set; }
    public Guid ImportRunId { get; set; }
}
public sealed class SyncJob
{
    public Guid Id { get; set; }
    public Guid MappingId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string Status { get; set; } = "Queued";
    public int Attempts { get; set; }
    public long LeaseGeneration { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset NextRunAt { get; set; }
    public byte[] Version { get; set; } = [];
}
public sealed class MatchGroup
{
    public Guid Id { get; set; }
    public Guid MappingId { get; set; }
    public ReviewStatus Status { get; set; }
    public string Preparer { get; set; } = "";
    public string Reason { get; set; } = "";
    public string RuleVersion { get; set; } = MatchingEngine.RuleVersion;
    public byte[] Version { get; set; } = [];
}
public sealed class MatchMember
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid MovementId { get; set; }
    public string EvidenceVersion { get; set; } = "";
    public bool Active { get; set; }
}
public sealed class AuditEvent
{
    public Guid Id { get; set; }
    public Guid MappingId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public DateTimeOffset At { get; set; }
}
public sealed class DikeDbContext(DbContextOptions<DikeDbContext> options) : DbContext(options)
{
    public DbSet<AccountMapping> AccountMappings => Set<AccountMapping>();
    public DbSet<StoredMovement> Movements => Set<StoredMovement>();
    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();
    public DbSet<MatchGroup> MatchGroups => Set<MatchGroup>();
    public DbSet<MatchMember> MatchMembers => Set<MatchMember>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AccountMapping>().Property(x => x.Version).IsRowVersion();
        model.Entity<AccountMapping>().Property(x => x.Connection).HasMaxLength(100);
        model.Entity<AccountMapping>().Property(x => x.Company).HasMaxLength(100);
        model.Entity<AccountMapping>().Property(x => x.ExternalAccount).HasMaxLength(200);
        model.Entity<AccountMapping>().HasIndex(x => new { x.Company, x.Connection, x.ExternalAccount }).IsUnique();
        model.Entity<StoredMovement>().Property(x => x.Amount).HasPrecision(19, 2);
        model.Entity<StoredMovement>().Property(x => x.SourceId).HasMaxLength(300);
        model.Entity<StoredMovement>().HasIndex(x => new { x.MappingId, x.Side, x.SourceId }).IsUnique();
        model.Entity<StoredMovement>().HasOne<AccountMapping>().WithMany().HasForeignKey(x => x.MappingId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<SyncJob>().Property(x => x.Version).IsRowVersion();
        model.Entity<SyncJob>().HasIndex(x => new { x.Status, x.NextRunAt });
        model.Entity<MatchGroup>().Property(x => x.Version).IsRowVersion();
        model.Entity<MatchMember>().HasIndex(x => x.MovementId).IsUnique().HasFilter("[Active] = 1");
        model.Entity<MatchMember>().HasOne<MatchGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<MatchMember>().HasOne<StoredMovement>().WithMany().HasForeignKey(x => x.MovementId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AuditEvent>().Property(x => x.IdempotencyKey).HasMaxLength(200);
        model.Entity<AuditEvent>().HasIndex(x => new { x.MappingId, x.IdempotencyKey }).IsUnique();
    }
}
