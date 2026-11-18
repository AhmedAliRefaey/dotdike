# Dike implementation plan

Updated 4 November 2026. See [delivery checkpoints and six-week status](PROJECT_STATUS.md) for the authoritative achieved/partial/pending register. The timeline is the original planning baseline, not a claim that six weeks have elapsed.

**Current planning, 17 November 2026:** the [complete remaining work plan](delivery/REMAINING_WORK_PLAN.md) supersedes this historical document for sequencing and estimates. Use the [T01–T42 board](delivery/TASKS.md) for task status and the [acceptance tracker](delivery/ACCEPTANCE_TRACKER.md) for AC01–AC44 plus OD-01–OD-08. New [T42](delivery/T42_ODOO_VALIDATION.md) defines Odoo connection/GL validation alongside NetSuite T02. The conditional refresh example below is not a selected provider cadence.

## Source and boundaries

Source: [Dike ASP NET Core Design and Plan](../Dike_ASPNET_Core_Design_and_Plan.docx), 4 November 2026. First release: one company, subsidiary and accounting book, two SAR bank accounts, one agreed statement period. Payment initiation, automatic journals, official reconciliation writeback, partial allocations and multicurrency consolidation are excluded.

## Created foundation

| Project | Files and current responsibility |
| --- | --- |
| Dike.Core | Models.cs defines scopes, movement/snapshot/balance contracts and provider ports; MatchingEngine.cs implements deterministic proposals; ReviewPolicy.cs validates whole-row submissions and distinct approvers |
| Dike.Infrastructure | Initial finance EF model plus new NetSuite TBA/RSA M2M authentication and paged SuiteQL account/GL reader; the old unconfigured adapter remains a placeholder registration, not the company connector used by the UI |
| Dike.Web | Direct Identity company registration/login, company-scoped setup/dashboard, redesigned Razor UI, encrypted NetSuite configuration and manual account/GL collection |
| Dike.Worker | Program.cs provides the hosted service lifecycle with synchronization disabled |
| Dike.Tests | Domain policy checks and NetSuite authentication/paging/GL fixtures; separate PowerShell smoke script verifies registration and connection isolation |

## Ordered implementation backlog

1. **Accounting input — implemented locally, live gate open:** the new NetSuite reader, authentication, paging, account selection and manual GL collection are implemented. Configure real access and validate Records Catalog fields, primary-book/SAR scope, stable line identity and debit-minus-credit totals against the GL report. Automatic partitioning and persisted verified bank-to-GL mappings remain pending. Confirm the pilot CSV contract.
2. **Persistence and security:** add Connection, ImportRun, immutable source versions, BalanceSnapshot, Exception and evidence records. Create reviewed EF migrations. Implement company/subsidiary/account grants for all reads, writes, jobs and exports. Add Administrator, Accountant, Controller and Viewer policies and hosting secrets-store integration.
3. **Ingestion:** add CsvImporter only after confirming pilot field contract. Implement Neotek accounts, balances and transaction paging using subscribed product contracts and confirmed PSU authorization. Stage every page before publishing complete snapshots. Failed/partial imports preserve last successful coverage. Confirm pending transitions, IDs, correction semantics, quotas and consent expiry.
4. **Durable processing:** implement SQL job claim with locking, lease expiry/generation, attempt count, bounded backoff, rate-limit handling and serialization per connection/account. Fence snapshot publication with the lease generation inside the committing transaction. Schedule a configurable 15-minute refresh only after provider approval. Add overlapping windows and periodic equivalent-scope full refreshes.
5. **Review workflow:** implement submit/approve/return/reopen services in SQL transactions. Validate source versions, group rowversion, exact balance and unique active row reservations. Bind idempotency keys to action and payload. Store immutable memberships and audit events. Require separate preparer/controller. Source changes invalidate affected groups and release reservations; missing rows count as deletions only after a complete equivalent-scope refresh.
6. **Screens and evidence — partial:** redesigned company/dashboard/connection screens, a GL period form and extract totals/rows are implemented. Add bank cash overview, reconciliation grids, proposal explanations, exception ownership and account-scoped CSV/JSON workpapers. Local encrypted extracts with run IDs exist; reviewed exports, audit history and approved production evidence storage remain pending.
7. **Pilot acceptance:** verify two-account isolation and unauthorized read/export denial; provider failed pages, rate limits, expired consent, duplicate replay and worker restart; concurrent reservations and stale approval attempts; corrections/reversals and reopening; approved statement/GL workpaper comparison; backup restoration and agreed volume benchmark.

## Integration dependencies

- Existing NetSuite client/authentication code and Records Catalog field access were not supplied.
- No provider credentials, Neotek subscription contract, authorized bank links or sample payloads were supplied.
- Finance SQL Server connection remains empty. Role policy names exist, but detailed grants and production storage/retention decisions remain pending. Company OIDC was superseded by direct registration and is no longer a setup dependency.
- No source CSV schema or representative pilot fixtures were supplied.

These dependencies are not replaced with mock financial facts. The application must not claim live synchronization, validated balances or production readiness until the relevant acceptance gates pass.

## Supporting runbooks

- [Company registration and outstanding identity work](COMPANY_REGISTRATION.md)
- [NetSuite setup and live validation requirements](NETSUITE_CONNECTOR.md)
- [Database configuration and storage boundaries](DATABASE_CONFIGURATION.md)
- [Original OIDC instructions, superseded](OIDC_SETUP.md)
