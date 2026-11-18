# Deploy Dike.Web to Vercel

The root Dockerfile publishes and starts the ASP.NET Core web project, `src/Dike.Web/Dike.Web.csproj`. The build needs the repository root to resolve the referenced projects. [Vercel Container runtime](https://vercel.com/docs/functions/runtimes/container).

## Project settings

| Setting | Value |
| --- | --- |
| Git repository and production branch | `AhmedAliRefaey/dotdike` on `main` |
| Root Directory | Repository root (leave the override empty) |
| Framework Preset | `Container`, as set in `vercel.json` |
| Build, install and output overrides | Leave at the defaults for the Dockerfile |

## Database and environment

1. Create a hosted PostgreSQL database for Dike, or connect a PostgreSQL integration to the Vercel project. PostgreSQL 17 is the version used for local integration testing. Give Dike a role that can create and own its two schemas and apply migrations. Ensure the database accepts connections from the deployment region.
2. Open **Project → Settings → Environment Variables**. Set the production values below. Use a separate database and certificate for preview deployments.
3. Keep the same Data Protection certificate across redeployments. To generate the pair for a new workspace, run `powershell -NoProfile -File tools/new-data-protection-certificate.ps1`. It writes a private settings file under the ignored `artifacts/vercel-secrets/` directory.

| Variable | Production value |
| --- | --- |
| `PORT` | `8080`. Set this in the Vercel project so incoming requests are routed to the port Dike listens on. [Vercel port resolution](https://vercel.com/docs/functions/runtimes/container#port-resolution). |
| `DATABASE_URL` | The provider's PostgreSQL connection URL, stored as a sensitive value. The app also accepts `POSTGRES_URL`. |
| `DataProtection__CertificateBase64` | Base64 PFX containing the persistent private key |
| `DataProtection__CertificatePassword` | Password for that PFX |
| `AllowedHosts` | `project-ktsi8.vercel.app` and any custom domains separated by `;` |
| `Odoo__BaseUrl` | Your Odoo HTTPS endpoint, if using Odoo |

Example URL with placeholders only.

```text
postgresql://<user>:<encoded-password>@<host>:5432/<database>?sslmode=verify-full
```

Use the provider's host and port when connecting through a pooler. Dike verifies the server certificate and hostname, including for integration URLs containing `sslmode=require`. For a private certificate authority, supply its trusted root through the connection configuration. The image sets `PORT=8080`, `Hosting__Provider=Vercel` and `Storage__Provider=PostgreSql`. Replace or remove an older `Storage__Provider=SqlServer` override in Vercel.

An explicit `ConnectionStrings__Identity` takes precedence over the integration URL. Remove an old SQL Server value or replace it with a PostgreSQL URL or Npgsql connection string. Both schemas share this database by default. Remove a stale `ConnectionStrings__Dike` override or set it to a separate PostgreSQL finance database. [Database configuration and existing-data notes](DATABASE_CONFIGURATION.md).

## Deploy and verify

1. Push the PostgreSQL changes to `main` and redeploy that commit after saving the settings. The build log should show a Docker build and publication of Dike.Web.
2. On first startup, EF Core applies migrations to `dike` and `dike_finance` and creates the four application roles. A new database starts with an empty workspace.
3. Open `/health` and check for HTTP 200 with `{"status":"alive"}`. Open `/Auth/Login`, register a test company, then confirm you can sign in again after a redeployment.

A Vercel `NOT_FOUND` response with no app startup logs points to the project root, framework, deployment or domain assignment. For an application startup error, check runtime logs for database connectivity, certificate settings and schema permissions. Keep the PostgreSQL backup and the matching Data Protection certificate so encrypted records can be restored.
