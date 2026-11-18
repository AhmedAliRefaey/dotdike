using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dike.Web.Authentication;

public sealed record StaffMember(string Id, string FullName, string Email, IReadOnlyList<string> Roles,
    bool EmailConfirmed, bool TwoFactorEnabled, bool IsLockedOut);

public sealed record StaffPage(int PageNumber, int PageSize, int TotalCount, IReadOnlyList<StaffMember> Members)
{
    public int TotalPages => (int)Math.Max(1, ((long)TotalCount + PageSize - 1) / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

/// <summary>
/// Reads the current company's staff. Roles here describe Identity membership, not finance grants.
/// Invitations and authority changes require the separate approval, audit and revocation workflow.
/// </summary>
public sealed class StaffService(CompanyIdentityDbContext db, IOptions<IdentityOptions> identityOptions,
    ILookupNormalizer normalizer, TimeProvider clock)
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;

    public async Task<StaffPage> GetPageAsync(ClaimsPrincipal principal, int pageNumber = 1,
        int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (pageSize is < 1 or > MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (pageNumber < 1 || (long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        cancellationToken.ThrowIfCancellationRequested();

        // The application supplies an authenticated Identity principal. Browser-supplied company
        // or role claims are never authority for this query, including before cookie refresh.
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
            throw AccessDenied();
        var claims = identityOptions.Value.ClaimsIdentity;
        var userId = identity.FindFirst(claims.UserIdClaimType)?.Value;
        var stamp = identity.FindFirst(claims.SecurityStampClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(stamp))
            throw AccessDenied();

        var administrator = normalizer.NormalizeName("Administrator");
        var actor = await db.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.CompanyId != Guid.Empty &&
                db.Companies.Any(company => company.Id == user.CompanyId) &&
                db.UserRoles.Any(membership => membership.UserId == user.Id &&
                    db.Roles.Any(role => role.Id == membership.RoleId && role.NormalizedName == administrator)))
            .Select(user => new { user.CompanyId, user.SecurityStamp, user.LockoutEnabled, user.LockoutEnd })
            .SingleOrDefaultAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (actor is null || !string.Equals(actor.SecurityStamp, stamp, StringComparison.Ordinal) ||
            actor.LockoutEnabled && actor.LockoutEnd > now)
            throw AccessDenied();

        var members = db.Users.AsNoTracking().Where(user => user.CompanyId == actor.CompanyId);
        var total = await members.CountAsync(cancellationToken);
        var rows = await members.OrderBy(user => user.FullName).ThenBy(user => user.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(user => new { user.Id, user.FullName, user.Email, user.EmailConfirmed,
                user.TwoFactorEnabled, user.LockoutEnabled, user.LockoutEnd })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(user => user.Id).ToArray();
        // Load roles for this page in one bounded query, retaining the company predicate.
        var roles = await (from user in members
                           join membership in db.UserRoles on user.Id equals membership.UserId
                           join role in db.Roles on membership.RoleId equals role.Id
                           where ids.Contains(user.Id) && role.Name != null
                           select new { user.Id, Name = role.Name! }).ToListAsync(cancellationToken);
        var rolesByUser = roles.ToLookup(role => role.Id, role => role.Name, StringComparer.Ordinal);
        var result = rows.Select(user => new StaffMember(user.Id, user.FullName, user.Email ?? "",
            Array.AsReadOnly(rolesByUser[user.Id].Order(StringComparer.Ordinal).ToArray()),
            user.EmailConfirmed, user.TwoFactorEnabled, user.LockoutEnabled && user.LockoutEnd > now)).ToArray();
        return new StaffPage(pageNumber, pageSize, total, Array.AsReadOnly(result));
    }

    private static UnauthorizedAccessException AccessDenied() => new("Staff directory access is not authorized.");
}
