using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dike.Infrastructure.Odoo;
using Microsoft.AspNetCore.DataProtection;

namespace Dike.Web.Authentication;

// Company-scoped encrypted previews; the storage session serializes each update.
public sealed class CompanyOdooStore(IDataProtectionProvider protection, ICompanyDocumentStorage storage)
{
    private IDataProtector Protector(Guid company) => protection.CreateProtector("Dike.Odoo.Web.v1", company.ToString("N"));

    internal async Task<T> LockedAsync<T>(Guid company, Func<ICompanyDocumentSession, Task<T>> action, CancellationToken ct)
    {
        try
        {
            await using var session = await storage.OpenAsync(ConnectionProviders.Odoo, company, ct);
            var result = await action(session);
            await session.CompleteAsync(ct);
            return result;
        }
        catch (Exception e) when (e is IOException or CryptographicException or JsonException ||
            e is UnauthorizedAccessException and not CompanyAccessDeniedException)
        {
            throw new OdooException("storage", "Odoo preview storage is unavailable. Existing data was not replaced.");
        }
    }

    internal async Task<OdooStoredState?> ReadAsync(ICompanyDocumentSession session, Guid company, CancellationToken ct)
    {
        var ciphertext = await session.ReadAsync("state.enc", ct, 8 * 1024 * 1024);
        if (ciphertext is null) return null;
        return JsonSerializer.Deserialize<OdooStoredState>(Protector(company).Unprotect(ciphertext))
            ?? throw new JsonException();
    }

    internal async Task WriteAsync(ICompanyDocumentSession session, Guid company, OdooStoredState state, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(state);
        if (Encoding.UTF8.GetByteCount(json) > 4 * 1024 * 1024) throw new IOException();
        await session.WriteAsync("state.enc", Protector(company).Protect(json), ct);
    }
}
