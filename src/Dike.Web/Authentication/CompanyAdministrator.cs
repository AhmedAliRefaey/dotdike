using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dike.Web.Authentication;

public sealed record AuthorizedCompany(Guid Id, string Name, bool SetupComplete, string ConnectionProvider);

public sealed class CompanyAdministrator(CompanyIdentityDbContext db, IOptions<IdentityOptions> options,
    ILookupNormalizer normalizer, TimeProvider clock)
{
    public async Task<AuthorizedCompany> RequireAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity) throw Denied();
        var claims = options.Value.ClaimsIdentity;
        var userId = identity.FindFirst(claims.UserIdClaimType)?.Value;
        var stamp = identity.FindFirst(claims.SecurityStampClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(stamp)) throw Denied();
        var administrator = normalizer.NormalizeName("Administrator");
        var actor = await (from user in db.Users.AsNoTracking()
                           join company in db.Companies.AsNoTracking() on user.CompanyId equals company.Id
                           where user.Id == userId && company.Id != Guid.Empty &&
                               db.UserRoles.Any(member => member.UserId == user.Id && db.Roles.Any(role =>
                                   role.Id == member.RoleId && role.NormalizedName == administrator))
                           select new { company.Id, company.Name, company.SetupComplete, company.ConnectionProvider, user.SecurityStamp, user.LockoutEnabled, user.LockoutEnd })
            .SingleOrDefaultAsync(ct);
        if (actor is null || !string.Equals(actor.SecurityStamp, stamp, StringComparison.Ordinal) ||
            actor.LockoutEnabled && actor.LockoutEnd > clock.GetUtcNow()) throw Denied();
        return new(actor.Id, actor.Name, actor.SetupComplete, actor.ConnectionProvider);
    }

    private static UnauthorizedAccessException Denied() => new CompanyAccessDeniedException();
}

internal sealed class CompanyAccessDeniedException : UnauthorizedAccessException
{
    internal CompanyAccessDeniedException() : base("Current company administrator access is required.") { }
}
