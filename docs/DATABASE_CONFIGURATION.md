# Database configuration and storage checkpoints

Verified configuration date: 4 November 2026. See [project status](PROJECT_STATUS.md) for delivery gates.

| Data | Current provider/location | State |
| --- | --- | --- |
| Users, roles, company profiles and selected connection provider | SQLite at `src/Dike.Web/App_Data/identity.db` by default | Implemented; provider is constrained to NetSuite or Odoo |
| Normalized finance data | SQL Server via `ConnectionStrings:Dike` | EF model exists; connection currently empty; migrations/publication pending |
| NetSuite credentials, accounts and GL extracts | Encrypted files under `src/Dike.Web/App_Data/NetSuite/<company-id>/` | Implemented; folder created on configuration; no live provider acceptance yet |
| Odoo credentials, loaded catalogs and latest GL preview | Encrypted files under `src/Dike.Web/App_Data/Odoo/<company-id>/` | Implemented; bounded read-only preview, not a complete finance ledger |

## Development

The Identity default is constructed from the web content root:

```text
Data Source=C:\Dike\src\Dike.Web\App_Data\identity.db
```

Override `ConnectionStrings:Identity` / `ConnectionStrings__Identity` to use another SQLite path. This setting still selects SQLite; supplying a SQL Server connection string here does not change the provider.

On 17 November, the local Identity store received the tested additive `Companies.ConnectionProvider` upgrade after backup. Startup applies it only when the column is absent; existing choices survive restart. Original row values/counts were verified unchanged apart from the added provider field. See [company registration and upgrade details](COMPANY_REGISTRATION.md). Switching providers preserves both company-specific connection stores; the selected provider controls which pages/actions are available. This does not introduce finance migrations or change SQL Server configuration.

The checked [application configuration](../src/Dike.Web/appsettings.json) currently contains an empty finance connection:

```json
"ConnectionStrings": { "Dike": "" }
```

Example only, for an installed local SQL Server instance using Windows authentication:

```json
"ConnectionStrings": {
  "Dike": "Server=localhost;Database=Dike;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
}
```

This example has NOT been applied or tested. Replace the server/instance with the actual target. Prefer development user secrets for credentials; do not commit passwords.

## Production

Supply `ConnectionStrings__Dike` through environment configuration or an approved secrets manager. Use encryption and a trusted SQL Server certificate, with `TrustServerCertificate=False`. Configure database permissions for the application. Do not copy the development certificate-bypass setting into production.

Adding this configuration does not create tables, change Identity's provider or import existing NetSuite files. Reviewed finance migrations, database deployment, scoped ingestion persistence and a data migration plan are still required. No database was created or migrated as part of this documentation update.

## Evidence and key storage

`NetSuite:StoragePath` / `NetSuite__StoragePath` overrides the encrypted connector storage root. The root must be private to the application, not served from `wwwroot`. The current store is a single-instance pilot implementation, not a distributed database or managed vault.

Back up Identity consistently with its SQLite WAL state. Preserve Data Protection keys together with encrypted NetSuite state/evidence: copying encrypted files alone is insufficient to guarantee recovery. Production key persistence/encryption, evidence retention and a restore rehearsal remain pending.

## Remaining checklist

- [ ] Select and test SQL Server endpoint and authentication.
- [ ] Complete/review finance schema and migrations.
- [ ] Publish normalized complete snapshots to SQL with company/account scope.
- [ ] Define migration of existing identity/evidence without data loss.
- [ ] Configure production key and secret storage, backups and retention.
- [ ] Rehearse restoring identity, finance data and encrypted evidence.
