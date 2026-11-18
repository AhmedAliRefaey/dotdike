# Odoo read-only validation tool

The primary user workflow now lives in [Dike.Web](../../docs/ODOO_WEB.md) at `/Accounts/Odoo`, with connection, company/account and period-filter pages. This CLI remains available for diagnostics and scripted read-only checks.

Uses the local Odoo API key configured by [the masked setup script](../../docs/ODOO_LOCAL_SETUP.md). It calls JSON-2 at the HTTPS root from `Odoo:BaseUrl`; set `Odoo__BaseUrl` to override the Development User Secrets value. There is no compiled default hostname. Redirects, cookies and arbitrary RPC methods are disabled. API credentials are never CLI arguments. Command and interactive results report the configured target.

## Interactive company, account and period selection

From `C:\Dike`:

```powershell
$env:Odoo__BaseUrl = 'https://napp.odoo.com' # replace with your Odoo HTTPS root
dotnet run --project tools/Dike.OdooValidation -- browse
```

1. Choose a company from the loaded list. It is intersected with the API user's assigned companies.
2. Load eligible bank/cash accounts for that company. Choose 1–5 loaded IDs separated by commas. Enter `more` if another catalog page is available.
3. Choose `week`, `month`, or `range`. A week covers the selected starting date and six following days. A calendar month uses `YYYY-MM`. Custom from/to dates use `YYYY-MM-DD`, inclusive, with a 31-day maximum for this first test.
4. Choose the sample size: 10 rows per account by default, maximum 50.

No GL request occurs before selection. Each fetch rechecks the integration user's company membership, actual account-company schema, bank/cash account type, company SAR currency and account currency restriction. These are Odoo API-user permissions; separate per-user account assignments inside Dike remain future work.

The client requests posted journal items with explicit company/account/date filters, `allowed_company_ids`, selected fields and deterministic ID ordering. It validates every returned row's scope, state, date, exact SAR precision/range and debit/credit/balance consistency. Unsupported schemas or negative/storno debit/credit conventions fail closed.

## Individual commands

```powershell
dotnet run --project tools/Dike.OdooValidation -- companies
dotnet run --project tools/Dike.OdooValidation -- accounts 1
dotnet run --project tools/Dike.OdooValidation -- gl 1 118 month 2026-11 10
dotnet run --project tools/Dike.OdooValidation -- gl 1 119 week 2026-11-08 10
dotnet run --project tools/Dike.OdooValidation -- gl 1 119 range 2026-11-01 2026-11-17 10
```

`companies` and `accounts` return up to 100 rows and support an optional `after-id` argument. Follow `NextAfterId` when `HasMore` is true. The unfiltered `accounts` command lists all permitted active accounts; the interactive GL selector narrows to bank/cash accounts for this validation slice.

The tool shares `UserSecretsId` with `Dike.Web` and explicitly loads that development store. Standard `Odoo__BaseUrl`, `Odoo__ApiKey` and optional `Odoo__Database` environment variables can override it. Never guess the database name. The single selected host currently routes successfully without the database header.

## Result limits

- `SampleOnly` remains true, even for an empty result or `HasMore: false`. An extra row detects truncation; a bounded sample is not a complete independently verified GL report.
- Calls are separate Odoo transactions. Catalog pages and multi-account samples do not share a database snapshot.
- Each HTTP request, including body reading, has a 30-second deadline and a 2 MiB response limit. Noninteractive commands have a two-minute total deadline. Ctrl+C cancels a run.
- Errors omit credentials, raw response content and provider tracebacks. Multi-account output is buffered until every selected sample succeeds.
- The CLI does not publish finance data, schedule jobs or change Odoo records. The separately authorized [AP test-data script](../seed-odoo-ap-test-data.ps1) is the only write tool added for the synthetic fixture batch.

On 17 November 2026, live authentication, company/account listing and posted bank GL samples succeeded. The user-authorized [test-data report](../../docs/delivery/ODOO_AP_TEST_DATA_20261117.md) records the actual synthetic postings. Full extraction, independent report comparison and finance acceptance remain under [T42](../../docs/delivery/T42_ODOO_VALIDATION.md).
