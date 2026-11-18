using System.Data;
using Npgsql;

namespace Dike.Web.Authentication;

public sealed class PostgresCompanyDocumentStorage(NpgsqlDataSource source) : ICompanyDocumentStorage
{
    public async Task<ICompanyDocumentSession> OpenAsync(string provider, Guid company, CancellationToken ct)
    {
        StorageNames.ValidateScope(provider, company);
        NpgsqlConnection? connection = null;
        try
        {
            connection = await source.OpenConnectionAsync(ct);
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await PostgresStorageLock.AcquireAsync(connection, transaction, $"Dike:Company:{provider}:{company:N}", ct);
            return new Session(connection, transaction, provider, company);
        }
        catch (Exception e)
        {
            if (connection is not null) await connection.DisposeAsync();
            ct.ThrowIfCancellationRequested();
            if (e is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable })
                throw new IOException("Company storage is busy. Try again shortly.", e);
            if (e is NpgsqlException) throw new IOException("Shared company storage is unavailable.", e);
            throw;
        }
    }

    private sealed class Session(NpgsqlConnection connection, NpgsqlTransaction transaction, string provider, Guid company) : ICompanyDocumentSession
    {
        private NpgsqlCommand Command(string sql, string name)
        {
            StorageNames.ValidateName(name);
            var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(company);
            command.Parameters.AddWithValue(provider);
            command.Parameters.AddWithValue(name);
            return command;
        }

        public async Task<string?> ReadAsync(string name, CancellationToken ct, int maxBytes = 32 * 1024 * 1024)
        {
            await using var command = Command("""
                SELECT octet_length("Ciphertext"), "Ciphertext" FROM dike."CompanyDocuments"
                WHERE "CompanyId" = $1 AND "Provider" = $2 AND "Name" = $3
                """, name);
            try
            {
                await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
                if (!await reader.ReadAsync(ct)) return null;
                if (reader.GetInt32(0) > maxBytes) throw new IOException("Stored document exceeds the size limit.");
                return await reader.GetFieldValueAsync<string>(1, ct);
            }
            catch (NpgsqlException e) { ct.ThrowIfCancellationRequested(); throw new IOException("Shared company storage is unavailable.", e); }
        }

        public async Task WriteAsync(string name, string ciphertext, CancellationToken ct)
        {
            await using var command = Command("""
                INSERT INTO dike."CompanyDocuments" ("CompanyId", "Provider", "Name", "Ciphertext", "UpdatedAt")
                VALUES ($1, $2, $3, $4, statement_timestamp())
                ON CONFLICT ("CompanyId", "Provider", "Name") DO UPDATE
                SET "Ciphertext" = EXCLUDED."Ciphertext", "UpdatedAt" = EXCLUDED."UpdatedAt"
                """, name);
            command.Parameters.AddWithValue(ciphertext);
            try { await command.ExecuteNonQueryAsync(ct); }
            catch (NpgsqlException e) { ct.ThrowIfCancellationRequested(); throw new IOException("Shared company storage is unavailable.", e); }
        }

        public async Task CompleteAsync(CancellationToken ct)
        {
            try { await transaction.CommitAsync(ct); }
            catch (NpgsqlException e) { ct.ThrowIfCancellationRequested(); throw new IOException("Shared company storage is unavailable.", e); }
        }

        public async ValueTask DisposeAsync()
        {
            try { await transaction.DisposeAsync(); }
            finally { await connection.DisposeAsync(); }
        }
    }
}

internal static class PostgresStorageLock
{
    internal static async Task AcquireAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string resource, CancellationToken ct)
    {
        await using var timeout = new NpgsqlCommand("SET LOCAL lock_timeout = '30s'", connection, transaction);
        await timeout.ExecuteNonQueryAsync(ct);
        await using var acquire = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1, 0))", connection, transaction)
        {
            CommandTimeout = 35
        };
        acquire.Parameters.AddWithValue(resource);
        await acquire.ExecuteNonQueryAsync(ct);
    }
}
