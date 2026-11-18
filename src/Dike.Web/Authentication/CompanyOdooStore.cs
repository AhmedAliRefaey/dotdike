using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dike.Infrastructure.Odoo;
using Microsoft.AspNetCore.DataProtection;

namespace Dike.Web.Authentication;

// Company-scoped, encrypted single-instance preview storage; not the production finance ledger.
public sealed class CompanyOdooStore(IDataProtectionProvider protection, IWebHostEnvironment environment, IConfiguration configuration)
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> gates = new();
    private string Folder(Guid company) => Path.Combine(configuration["Odoo:StoragePath"] ??
        Path.Combine(environment.ContentRootPath, "App_Data", "Odoo"), company.ToString("N"));
    private IDataProtector Protector(Guid company) => protection.CreateProtector("Dike.Odoo.Web.v1", company.ToString("N"));

    internal async Task<T> LockedAsync<T>(Guid company, Func<Task<T>> action, CancellationToken ct)
    {
        var gate = gates.GetOrAdd(company, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try { return await action(); }
        catch (Exception e) when (e is IOException or CryptographicException or JsonException ||
            e is UnauthorizedAccessException and not CompanyAccessDeniedException)
        {
            throw new OdooException("storage", "Odoo preview storage is unavailable. Existing data was not replaced.");
        }
        finally { gate.Release(); }
    }

    internal async Task<OdooStoredState?> ReadAsync(Guid company, CancellationToken ct)
    {
        var path = Path.Combine(Folder(company), "state.enc");
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new IOException();
        return JsonSerializer.Deserialize<OdooStoredState>(Protector(company).Unprotect(await File.ReadAllTextAsync(path, ct)))
            ?? throw new JsonException();
    }

    internal async Task WriteAsync(Guid company, OdooStoredState state, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(state);
        if (Encoding.UTF8.GetByteCount(json) > 4 * 1024 * 1024) throw new IOException();
        Directory.CreateDirectory(Folder(company));
        var path = Path.Combine(Folder(company), "state.enc");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, Protector(company).Protect(json), ct);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
