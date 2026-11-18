using System.Collections.Concurrent;

namespace Dike.Web.Authentication;

// Every read/modify/write operation owns a company/provider session until it completes.
public interface ICompanyDocumentStorage
{
    Task<ICompanyDocumentSession> OpenAsync(string provider, Guid company, CancellationToken ct);
}

public interface ICompanyDocumentSession : IAsyncDisposable
{
    Task<string?> ReadAsync(string name, CancellationToken ct, int maxBytes = 32 * 1024 * 1024);
    Task WriteAsync(string name, string ciphertext, CancellationToken ct);
    Task CompleteAsync(CancellationToken ct);
}

public sealed class FileCompanyDocumentStorage(IWebHostEnvironment environment, IConfiguration configuration) : ICompanyDocumentStorage
{
    private readonly ConcurrentDictionary<(string, Guid), SemaphoreSlim> gates = new();

    public async Task<ICompanyDocumentSession> OpenAsync(string provider, Guid company, CancellationToken ct)
    {
        StorageNames.ValidateScope(provider, company);
        var gate = gates.GetOrAdd((provider, company), _ => new(1, 1));
        await gate.WaitAsync(ct);
        var folder = Path.Combine(configuration[$"{provider}:StoragePath"] ??
            Path.Combine(environment.ContentRootPath, "App_Data", provider), company.ToString("N"));
        return new Session(folder, gate);
    }

    private sealed class Session(string folder, SemaphoreSlim gate) : ICompanyDocumentSession
    {
        private bool disposed;
        public async Task<string?> ReadAsync(string name, CancellationToken ct, int maxBytes = 32 * 1024 * 1024)
        {
            StorageNames.ValidateName(name);
            var path = Path.Combine(folder, name);
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > maxBytes) throw new IOException("Stored document exceeds the size limit.");
            return await File.ReadAllTextAsync(path, ct);
        }

        public async Task WriteAsync(string name, string ciphertext, CancellationToken ct)
        {
            StorageNames.ValidateName(name);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, name);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, ciphertext, ct);
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public Task CompleteAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            if (!disposed) { disposed = true; gate.Release(); }
            return ValueTask.CompletedTask;
        }
    }
}

internal static class StorageNames
{
    internal static void ValidateScope(string provider, Guid company)
    {
        if (provider is not (ConnectionProviders.Odoo or ConnectionProviders.NetSuite) || company == Guid.Empty)
            throw new ArgumentException("Invalid company storage scope.");
    }

    internal static void ValidateName(string name)
    {
        if (name.Length > 100 || !name.EndsWith(".enc", StringComparison.Ordinal) ||
            name[..^4].Length == 0 || name[..^4].Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Invalid company document name.");
    }
}
