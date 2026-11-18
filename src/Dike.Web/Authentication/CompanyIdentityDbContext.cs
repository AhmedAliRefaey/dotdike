using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dike.Web.Authentication;

public sealed class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Country { get; set; } = "Saudi Arabia";
    public string Currency { get; set; } = "SAR";
    public string ConnectionProvider { get; set; } = ConnectionProviders.NetSuite;
    public string RegistrationNumber { get; set; } = "";
    public string Address { get; set; } = "";
    public bool SetupComplete { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class ApplicationUser : IdentityUser
{
    public Guid CompanyId { get; set; }
    public string FullName { get; set; } = "";
}
public sealed class CompanyIdentityDbContext(DbContextOptions<CompanyIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<CompanyDocument> CompanyDocuments => Set<CompanyDocument>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        if (Database.IsNpgsql()) builder.HasDefaultSchema("dike");
        builder.Entity<ApplicationUser>().HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.Entity<Company>().Property(x => x.Name).HasMaxLength(160);
        builder.Entity<Company>().Property(x => x.ConnectionProvider).HasMaxLength(16).HasDefaultValue(ConnectionProviders.NetSuite);
        builder.Entity<Company>().ToTable(table => table.HasCheckConstraint("CK_Companies_ConnectionProvider",
            "\"ConnectionProvider\" IN ('NetSuite', 'Odoo')"));
        builder.Entity<CompanyDocument>().HasKey(x => new { x.CompanyId, x.Provider, x.Name });
        builder.Entity<CompanyDocument>().Property(x => x.Provider).HasMaxLength(16);
        builder.Entity<CompanyDocument>().Property(x => x.Name).HasMaxLength(100);
        builder.Entity<CompanyDocument>().HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CompanyDocument
{
    public Guid CompanyId { get; set; }
    public string Provider { get; set; } = "";
    public string Name { get; set; } = "";
    public string Ciphertext { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
// Resolve tenancy from the authenticated database user, never a browser-supplied company ID.
public sealed class CurrentCompany(UserManager<ApplicationUser> users, CompanyIdentityDbContext db)
{
    public async Task<Company?> FindAsync(ClaimsPrincipal principal)
    {
        var user = await users.GetUserAsync(principal);
        return user is null ? null : await db.Companies.SingleOrDefaultAsync(x => x.Id == user.CompanyId);
    }
}
