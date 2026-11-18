# Dike

.NET 10 implementation starter based on [the supplied design](Dike_ASPNET_Core_Design_and_Plan.docx). This is a buildable foundation, not a completed production finance application. Company registration now uses local ASP.NET Core Identity instead of external OIDC; see [registration and setup](docs/COMPANY_REGISTRATION.md).

The [Dike design system](docs/design/README.md) now covers the public and authenticated application in English and Arabic. Use the header language switch for persistent RTL/LTR selection. The design retains truthful readiness states and the existing registration, staff and NetSuite workflows.

## Projects

- `src/Dike.Web`: Areopagus Razor UI, direct company registration/login, company dashboard, scoped NetSuite collection and [Odoo connection/account/GL preview pages](docs/ODOO_WEB.md).
- `src/Dike.Worker`: Alecto background host; imports intentionally disabled until safe durable processing exists.
- `src/Dike.Core`: provider interfaces, normalized contracts, Megaera matching and Tisiphone review policies.
- `src/Dike.Infrastructure`: initial SQL Server EF model, read-only NetSuite SuiteQL connector and bounded Odoo JSON-2 client.
- `tests/Dike.Tests`: executable domain, Identity, NetSuite and Odoo fixture checks without a test framework dependency.
- `tools/Dike.OdooValidation`: local read-only JSON-2 testing with company/account selection and weekly/monthly/custom periods.

## Build and verify

```powershell
dotnet restore Dike.slnx
dotnet build Dike.slnx --no-restore
dotnet run --project tests/Dike.Tests
dotnet run --project src/Dike.Web
```

Register a company at `/Auth/Register`, explicitly choose **NetSuite** or **Odoo**, then complete its profile. The workspace shows only the chosen provider and blocks the other provider's pages/actions. Existing administrators can change the choice in **Company profile** without deleting previously saved connection settings. Identity uses local SQLite persistence; no OIDC credentials are needed. Configure `ConnectionStrings__Dike` separately when connecting the finance database. Set `AllowedHosts` for deployment. Production requires HTTPS; the local Development profile supports HTTP on port 5080. Connection pages require a current company administrator. [Selection, upgrade and verification details](docs/COMPANY_REGISTRATION.md).

No existing NetSuite client or pilot code was supplied, so a new connector was implemented. Configure it through `/Accounts/NetSuite`, load accounts and collect a selected GL period. Company administrators are scoped to their own connection. Fine-grained subsidiary/account grants and real finance acceptance remain pending. See [NetSuite instructions](docs/NETSUITE_CONNECTOR.md); live queries and report agreement have not been validated.

For Odoo, open **[Odoo](http://localhost:5080/Accounts/Odoo)** in the sidebar: connection settings → permitted companies/accounts → posted GL preview. The existing local key is assigned to **T-sab**; choose **Use existing local connection**, load NApp accounts, then select accounts and weekly/monthly/custom dates. [Web instructions and verification](docs/ODOO_WEB.md).

The Odoo server address comes from `Odoo:BaseUrl`; set the environment variable `Odoo__BaseUrl` before starting the app to override Development User Secrets. There is no compiled default host. Changing the address, or upgrading an older saved connection without an endpoint binding, requires connection verification again. [Endpoint setup](docs/ODOO_WEB.md#configure-the-server-address).

## Delivery status

The [complete remaining work plan](docs/delivery/REMAINING_WORK_PLAN.md) defines milestones and task dependencies and estimates 86–133 remaining focused engineer-days before external waits/significant rework, including the added Odoo validation slice. The [acceptance tracker](docs/delivery/ACCEPTANCE_TRACKER.md) maps the original 44 release cases plus eight Odoo cases. The 16 November planning-review checks were a clean isolated serialized six-project build, 158 executable checks and 10 CLI checks; adding the Odoo task on 17 November did not rerun application tests.

The T01–T42 work is tracked in the [delivery task board](docs/delivery/TASKS.md). [T42 — Odoo connection and GL validation](docs/delivery/T42_ODOO_VALIDATION.md) runs parallel to T02. The [Odoo web workflow](docs/ODOO_WEB.md), local secret setup, diagnostic CLI and company provider selection are implemented. Latest provider-selection verification: clean seven-project build, **62 HTTP checks, 38 browser checks and 6 isolated upgrade checks passed**; original Identity data was preserved after backup and upgrade. Earlier live NApp sample checks remain recorded. The separate executable regression runner is blocked by Windows security (`0x800700E1`); previous executable passes remain historical. The previously authorized [synthetic AP/payment batch](docs/delivery/ODOO_AP_TEST_DATA_20261117.md) is posted and verified. Full extraction, independent report agreement and production finance integration remain outstanding. [T01's baseline](docs/delivery/T01_BASELINE.md) preserves original verification history.

Resume from the [latest delivery checkpoint](docs/delivery/CHECKPOINT.md). T02's offline GL validation tool passes its executable and command-line checks; live report agreement remains pending. T03's [CSV contract review pack](docs/delivery/T03_CSV_CONTRACT_REVIEW.md) is ready for a representative bank export and D04/D06 finance decisions.

The earlier [delivery review](docs/delivery/DELIVERY_REVIEW.md) fixed source-money precision/range handling and recorded 81 passing executable checks plus 10 CLI checks. [T04's Neotek review](docs/delivery/T04_NEOTEK_CONTRACT_REVIEW.md) covers the supplied developer documentation and unresolved contract differences.

[T05's Identity, access and operations decision pack](docs/delivery/T05_IDENTITY_SECURITY_OPERATIONS.md) covers deployment choices, scoped grants/revocation, MFA/recovery, existing-user/key migration and evidence retention/restore. Named owners and D01/D02/D09 choices remain open; its 14 acceptance cases are planned, not executed tests.

[T06's revision, concurrency and workpaper pack](docs/delivery/T06_REVIEW_CONCURRENCY_WORKPAPER.md) records the remaining five decisions and verified independent code improvements. Matching now uses sar-v2 with explicit scope and deterministic ordering; matching/review share strict SAR input validation. Latest checks: **126 executable, 10 CLI and 9 disposable SQL mechanism checks**, with a clean build. Persisted finance workflows remain incomplete.

The subsequent [staff directory foundation](docs/delivery/T05_IDENTITY_SECURITY_OPERATIONS.md#implemented-staff-directory-foundation) is available at `/Company/Staff`: current database authorization, company-scoped pagination and account security indicators. Current verification is **158 executable checks and 42 isolated web checks**, with a clean build. Invitations, role changes and finance grants remain pending. The CLI/SQL counts above are prior verification and were not rerun for this change.

See [all checkpoints and original six-week timeline](docs/PROJECT_STATUS.md) for completed, partial and outstanding work, evidence limits and the next implementation order. The app is not production-ready and no planned finance acceptance gate has been signed off.

Current storage: SQLite Identity, encrypted local NetSuite snapshots and Odoo connection/previews, and an unconfigured SQL Server finance model. See [database configuration](docs/DATABASE_CONFIGURATION.md). Local encrypted evidence exists; SQL publication, durable jobs, Neotek/CSV ingestion, review workflows, exports and production operations remain outstanding.

Supporting documents: [implementation plan](docs/IMPLEMENTATION_PLAN.md), [company registration](docs/COMPANY_REGISTRATION.md), [NetSuite connector](docs/NETSUITE_CONNECTOR.md). Historical [OIDC instructions](docs/OIDC_SETUP.md) are superseded.

The [SQL concurrency probe](tests/sql-concurrency-smoke.ps1) tests the proposed guard pattern against local SQL Server using synthetic tables. It requires the installed `MSSQLLocalDB` development instance and uses only a generated database; it does not apply application migrations. Inspect its plan without `-Run`, or execute and automatically remove the test database with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/sql-concurrency-smoke.ps1 -Run
```

To repeat the isolated web smoke checks after changes:

```powershell
dotnet build src/Dike.Web/Dike.Web.csproj -p:UseAppHost=false -o artifacts/registration-check
powershell -NoProfile -ExecutionPolicy Bypass -File tests/registration-smoke.ps1
```

The smoke script starts a temporary server on port 5081 with synthetic users and credentials. It does not call NetSuite. Latest results: clean web build and 42 web smoke checks, including staff isolation, caching, read-only enforcement, HTML encoding, Arabic validation and secure language switching. See the status register for exact coverage and unverified areas.
