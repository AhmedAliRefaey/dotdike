using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dike.Web.Authentication;

// Additive upgrade for the existing EnsureCreated-based SQLite development store.
public static class IdentitySchema
{
    public static async Task InitializeAsync(CompanyIdentityDbContext db, Guid? legacyOdooCompany, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "PRAGMA table_info(\"Companies\")";
        bool hasProvider;
        await using (var columns = await command.ExecuteReaderAsync(ct))
        {
            hasProvider = false;
            while (await columns.ReadAsync(ct))
                hasProvider |= string.Equals(columns.GetString(1), nameof(Company.ConnectionProvider), StringComparison.OrdinalIgnoreCase);
        }
        if (!hasProvider)
        {
            command.CommandText = "ALTER TABLE \"Companies\" ADD COLUMN \"ConnectionProvider\" TEXT NOT NULL DEFAULT 'NetSuite' CHECK (\"ConnectionProvider\" IN ('NetSuite', 'Odoo'))";
            await command.ExecuteNonQueryAsync(ct);
            // Preserve the explicitly established local Odoo binding when upgrading a legacy database.
            if (legacyOdooCompany is Guid company && company != Guid.Empty)
            {
                command.CommandText = "UPDATE \"Companies\" SET \"ConnectionProvider\" = 'Odoo' WHERE lower(\"Id\") = $company";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "$company"; parameter.Value = company.ToString("D");
                command.Parameters.Add(parameter);
                await command.ExecuteNonQueryAsync(ct);
            }
        }
        await transaction.CommitAsync(ct);
    }
}
