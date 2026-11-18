# PostgreSQL configuration

Dike.Web uses PostgreSQL by default in Development and Production. Point it at a database created for Dike. The database role needs to create the `dike` and `dike_finance` schemas on first startup and to apply subsequent EF Core migrations. A database owner can also create the two schemas in advance and assign ownership to the application role.

| Schema | Stored data |
| --- | --- |
| `dike` | Companies, ASP.NET Core Identity users and roles, encrypted Odoo and NetSuite documents, and the hosted Data Protection key ring |
| `dike_finance` | Account mappings, movements, jobs, match groups and members, and audit events |

Both schemas use the same database by default. Set `ConnectionStrings__Dike` only when using a separate PostgreSQL database for finance. Each schema has its own migration history. The finance tables support the existing EF model. Publishing connector extracts into the ledger and processing durable jobs remain separate application work.

## Local development

With PostgreSQL 17 running locally, create a database named `dike` owned by your application role. Set its URL in the same PowerShell session as the web app. Replace the example credentials with your own and percent-encode special characters in URL credentials.

```powershell
$env:DATABASE_URL = 'postgresql://dike_user:<password>@localhost:5432/dike'
dotnet run --project src/Dike.Web
```

Alternatively store the connection in Development User Secrets for `src/Dike.Web`. Configuration is checked in this order, skipping empty values.

1. `ConnectionStrings__Identity` (or `ConnectionStrings:Identity` in User Secrets)
2. `DATABASE_URL`
3. `POSTGRES_URL`

Each setting accepts a PostgreSQL URL or an Npgsql connection string. Development uses the ASP.NET Core key ring on the local machine unless a Data Protection certificate is supplied. With a certificate, the encrypted key ring is stored in PostgreSQL and shared across instances. Hosted connections validate both the server certificate and its hostname. [Npgsql connection parameters](https://www.npgsql.org/doc/connection-string-parameters.html).

## Existing local data

To open the existing SQLite workspace, set `$env:Storage__Provider = 'Sqlite'` and run Dike.Web in Development. It uses `src/Dike.Web/App_Data/identity.db` and the company-specific Odoo and NetSuite folders. A SQLite-specific `ConnectionStrings__Identity` can override the local path. Clear any PostgreSQL override for that setting before opening this workspace.

A new PostgreSQL database starts with the schema and roles. Transferring an existing workspace requires copying companies, Identity records including their IDs and password hashes, and encrypted connector documents with a compatible key ring. Preserve the source database, connector files, and original Data Protection keys together until sign-in and decryption have been verified on the destination.

## Hosted deployment and verification

Configure the persistent certificate and database in your hosting environment. Back up PostgreSQL and the certificate and its password separately. [Vercel deployment steps](VERCEL_DEPLOYMENT.md).

The PostgreSQL integration checks use synthetic data and automatically create and drop a uniquely named database. Supply a loopback test server with a role that can create databases.

```powershell
$env:DIKE_TEST_POSTGRES = 'postgresql://postgres:<password>@127.0.0.1:5432/postgres'
dotnet run --project tests/Dike.Tests -- --hosted-storage
```

To run the registration and connector HTTP checks against the official PostgreSQL Docker image, pass the name of a local test container published on IPv4 loopback. The script uses its own temporary database.

```powershell
dotnet publish src/Dike.Web/Dike.Web.csproj -c Release -p:UseAppHost=false -o artifacts/registration-check
powershell -NoProfile -File tests/registration-smoke.ps1 -PostgresContainer <container-name>
```
