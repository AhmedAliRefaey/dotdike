# Dike delivery checkpoints

Current delivery work is tracked in the [T01–T42 task board](delivery/TASKS.md). The [16 November inspected baseline](delivery/T01_BASELINE.md) and later checkpoint preserve verification history.

The [remaining work plan](delivery/REMAINING_WORK_PLAN.md), updated 17 November, contains **42 tasks: 1 done, 10 in progress and 31 pending**, seven milestone gates and the retained **86–133 focused engineer-day** planning estimate excluding external waits/significant rework. [T42 Odoo validation](delivery/T42_ODOO_VALIDATION.md) now includes [four Dike.Web pages](ODOO_WEB.md), company-scoped encrypted credentials/previews, the explicit T-sab local-key binding and company/account/weekly/monthly/custom selection. Live NApp reads succeeded; the previously authorized [six synthetic AP bills and four manual payments](delivery/ODOO_AP_TEST_DATA_20261117.md) are posted and verified. Full extraction, independent report agreement and production finance integration remain open. The [acceptance tracker](delivery/ACCEPTANCE_TRACKER.md) preserves AC01–AC44 and OD-01–OD-08; no finance gate was approved.

The [latest resume checkpoint](delivery/CHECKPOINT.md) adds [provider selection during registration](COMPANY_REGISTRATION.md): each company chooses NetSuite or Odoo and sees only that provider's navigation/pages. Company profile can change the choice while retaining saved settings; direct cross-provider URLs/actions are blocked. Latest verification: clean **seven-project build**, **62 HTTP smoke checks, 38 browser checks and 6 isolated upgrade checks passed**, with 405 bilingual resource keys. The real Identity store was backed up and its original data verified unchanged after the additive provider-column upgrade. Windows still blocks the executable runner with `0x800700E1`; its previous 221 passes remain historical. Prior live Odoo, secret, CLI and SQL evidence remains in the checkpoint. Staff invitations/assignments/grants, finance persistence and named acceptance decisions remain incomplete.

Status date: 4 November 2026. Baseline: [original design](../Dike_ASPNET_Core_Design_and_Plan.docx). This is the consolidated delivery status; the week numbers below are planned phases, not elapsed time or completed calendar weeks. Work has crossed phase boundaries. No finance acceptance gate has yet been signed off.

The 16 November [design and Arabic/RTL update](design/README.md) adds 281 bilingual resource keys, responsive navigation and consistent public/authenticated screens. All 44 browser checks passed in Chrome at desktop and mobile sizes; no console errors or failed resources were observed. This does not close T36 for future finance screens, screen-reader testing or other browsers.

## Overall position

The application runs locally with company registration, profile setup, password login and a redesigned Razor Pages frontend. A company-scoped NetSuite connector can be configured and its account/GL collection paths are implemented and fixture-tested. Live NetSuite authentication, source identities and agreement with a GL report remain unverified. Bank ingestion, end-to-end reconciliation, durable processing, finance SQL persistence and production readiness remain incomplete.

Status meanings:

- **Completed:** implemented and checked locally to the stated scope; not equivalent to live finance acceptance.
- **Partial:** some code exists, but required workflow or validation remains.
- **Pending:** no working end-to-end implementation.
- **Awaiting live validation:** implementation exists but requires real provider access, data or finance review.
- **Superseded:** an earlier design choice was replaced; it is not an outstanding requirement unless reinstated.

## Original six-week timeline

| Phase | Original objective | Current status | Achieved | Remaining exit condition |
| --- | --- | --- | --- | --- |
| 1 / Week 1 | Define mappings/contracts, wrap NetSuite client, validate posted GL, confirm Neotek sandbox | Partial; awaiting live validation | Five projects; domain/provider contracts; new NetSuite TBA and RSA M2M clients; account listing; scoped GL extraction UI | Obtain provider access; validate Records Catalog joins, line keys, mapping and signed totals against GL report; confirm Neotek access |
| 2 / Week 2 | SQL schema, OIDC/access, Razor shell, CSV, durable jobs, matching | Partial | Initial EF finance model; local Identity registration instead of OIDC; Razor UI; pure matching/review policies; job entity and idle worker host | SQL migrations and persistence; resource grants; CSV ingestion; leased job processor; reproducible bank/book reconciliation flow |
| 3 / Week 3 | Neotek account/balance/transaction reads, authorization, pagination and replay | Pending | Provider interface and balance/source contracts only | Authorized bank account completes a full sync; replay produces no duplicate source rows; provider failures/consent states handled |
| 4 / Week 4 | Review groups, reservations, controller approval, invalidation and exceptions | Partial at domain/schema level only | Balance/direction/duplicate/pending checks; distinct-approver rule; initial group/member/audit models; filtered reservation index definition | Transactional submit/approve/return/reopen workflows; idempotency; source version checks; SQL concurrency proof; correction invalidation; exception queue |
| 5 / Week 5 | Cash overview, exports, scheduled sync, recovery and second bank | Partial UI only | Company dashboard, truthful empty states, connection status, GL extract totals and encrypted local evidence | Bank balances and period overview; CSV/JSON workpapers; scheduled jobs; failure/restart recovery; second-bank mapping and isolation |
| 6 / Week 6 | Finance review, access tests, restore rehearsal and volume measures | Pending; local checks available | Domain/connector fixtures and registration/connection web smoke tests | End-to-end finance acceptance; all access/export tests; restore rehearsal; agreed performance benchmark; operational handover |

Original estimate: six weeks for one experienced .NET developer plus finance review, contingent on provider onboarding and access. No revised completion date is established. Do not interpret local scaffolding or fixture passes as a completed phase exit condition.

## Checkpoint register

### Foundation and frontend

- [x] .NET 10 solution with Web, Worker, Core, Infrastructure and Tests projects.
- [x] Razor Pages application starts locally on port 5080.
- [x] Registration/login, company setup, dashboard, account security and connection-page styling.
- [x] Responsive public pages checked at 390px; no horizontal overflow observed.
- [x] Signed-in sidebar, setup status and explicit unimplemented-feature states.
- [x] Current public/authenticated pages visually checked in English/Arabic at 1536px and 390px, including menu focus and staff pagination (16 November).
- [ ] Full screen-reader, cross-browser and future finance-screen accessibility audit.
- [ ] Reconciliation grid, review actions, exception queue and workpaper screens.
- [ ] Product Design plugin integration. Suggested but not installed/used; redesign used native Razor/CSS.

### Company identity and authorization

- [x] Company and first administrator created in one database transaction.
- [x] Email/password login, logout and password change through ASP.NET Core Identity.
- [x] Unique emails, password rules and configured login lockout.
- [x] CSRF-protected actions and local-only login return URLs.
- [x] Company identity resolved from authenticated database membership, not a submitted company ID.
- [x] Administrator protection for company/account/connection pages.
- [x] Administrator, Accountant, Controller and Viewer policy definitions.
- [x] Read-only staff directory: current database Administrator/stamp/lockout checks, company-scoped counts/rows, bounded pagination and secret-free results; 32 service checks and 10 added web checks pass (16 November).
- [ ] Staff invitations, membership management and role assignment UI.
- [ ] Account/subsidiary-specific grants for all future data actions, jobs and exports.
- [ ] Email ownership verification and forgotten-password delivery.
- [ ] MFA, registration throttling and production identity operations.
- [ ] Legal company verification; current registration authority is self-attested.
- **Superseded:** external company OIDC login. User requested direct website registration instead; old OIDC guidance is historical.

### NetSuite

- [x] Per-company configuration UI, encrypted server-side credential storage and no secret redisplay.
- [x] TBA HMAC-SHA256 and OAuth 2.0 RSA/PS256 certificate authentication implementations.
- [x] Derived HTTPS SuiteTalk destination, redirect rejection and disabled HTTP client logging.
- [x] Active accessible GL account metadata collection.
- [x] Selection of SAR bank account, subsidiary/book internal IDs and date range.
- [x] Bound-parameter posted GL query, debit/credit retention and debit-minus-credit totals.
- [x] Pagination validation and rejection at the REST result cap; no silent truncation.
- [x] Candidate source identity checks, duplicate rejection and monetary precision checks.
- [x] Completed extract files, run IDs, fetch timestamp and latest snapshot display.
- [x] Failure status and retention of the last completed snapshot in implementation.
- [x] Retry handling for query 429/5xx responses in implementation.
- [ ] Live authentication test for the selected customer integration.
- [ ] Actual Records Catalog verification of fields, joins, scope and line uniqueness.
- [ ] Finance agreement of one period's row identities, debit/credit totals and signed total with GL report.
- [ ] Live rate-limit, expired-token/certificate and replay/correction testing.
- [ ] Persisted verified bank-to-GL mappings with version history.
- [ ] Finance SQL publication and source-change invalidation of review decisions.
- [ ] Automatic large-query partitioning; currently users must narrow the period.
- [ ] Interactive OAuth authorization-code flow and ECDSA certificate support.

Current restriction: movement imports require a SAR bank account, SAR subsidiary base currency and the primary accounting book. Metadata can include other GL accounts, but detailed non-bank imports and secondary-book currency mapping are not enabled.

### Storage and jobs

- [x] SQLite persistence for users, companies and Identity data.
- [x] Initial SQL Server EF mappings for finance entities, rowversions and reservation uniqueness.
- [x] Encrypted, company-separated local NetSuite state and evidence; atomic state-file replacement.
- [x] In-process per-company serialization for manual collection.
- [ ] SQL Server endpoint and nonempty `ConnectionStrings:Dike` configuration.
- [ ] Reviewed migrations and complete finance schema, including imports, balances and exceptions.
- [ ] Ingestion services writing normalized data to the finance database.
- [ ] Durable SQL job claims, lease generation checks, expiry/reclaim, retries and worker execution.
- [ ] Approved refresh schedule, overlap windows and full-period reconciliation checks.
- [ ] Distributed coordination and multi-instance storage support.
- [ ] Production secrets/evidence storage, retention policy and protected persistent Data Protection keys.
- [ ] Backup and restore rehearsal covering identity, finance, evidence and encryption keys.

The worker intentionally does not process imports. The NetSuite UI currently collects synchronously. A connection string alone neither creates the finance tables nor moves encrypted extracts into SQL Server.

### Bank ingestion and reconciliation

- [x] Bank/source/balance interfaces and normalized movement contracts.
- [x] Pure matching for reference/amount/date, ambiguity, pending exclusion and account scope.
- [x] Sunday-through-Thursday business-day window; holiday calendar remains deferred.
- [x] Whole-row group balance/direction checks and distinct-preparer/approver policy.
- [ ] Confirm pilot CSV field contract and implement CSV importer.
- [ ] Neotek authorization, account linking, balances, booked/pending transactions and all-page import.
- [ ] Replay deduplication, corrections, removals and equivalent-scope refresh handling.
- [ ] Persist/run matching proposals from real imported bank and book rows.
- [ ] Transactional reservations and source-version-aware review transitions.
- [ ] Exception ownership, reasons, audit history, reopening and invalidation.
- [ ] SAR 50 scenario validated end-to-end without inferring an unsupported fee.
- [ ] Two real bank accounts mapped to distinct GL accounts and independently reconciled.
- [ ] Account-scoped CSV/JSON workpapers with totals, revisions, run IDs and review history.

## Verification evidence

These are the last observed results from implementation work, not new test runs performed for this documentation update.

| Check | Observed result | Limit |
| --- | --- | --- |
| Web build after connector integration | Passed; zero warnings/errors | Does not prove provider connectivity or deployed SQL schema |
| Domain executable checks | 11 passed | Pure C# policies, not SQL transactions |
| NetSuite fixture checks | Passed | Fake HTTP responses; signing, paging, scope, signs, IDs, precision and caps; no live NetSuite access |
| Registration/connection smoke checks | 19 passed | Synthetic accounts/credentials, isolated local test database; no provider requests |
| Public mobile layout | Landing, login and registration checked at 390px without horizontal overflow | Not a complete accessibility or authenticated visual audit |
| Live finance/operations acceptance | Not performed | No signed-off statement/GL workpaper, restore rehearsal or load benchmark |

Reproduction instructions are in [README](../README.md). Test sources: [domain checks](../tests/Dike.Tests/Program.cs), [connector fixtures](../tests/Dike.Tests/NetSuiteChecks.cs), [web smoke script](../tests/registration-smoke.ps1).

## Decisions and deviations from the baseline

| Baseline | Current decision or implementation | Consequence |
| --- | --- | --- |
| Company OIDC sign-in | Direct company registration requested by user | OIDC authority/client settings no longer apply to website login |
| Reuse working NetSuite client | No existing client supplied; new read-only adapter implemented | Requires account-specific live validation before acceptance |
| Shared SQL Server persistence | Identity SQLite plus encrypted local NetSuite files; finance EF model only | SQL migrations/publication and production storage remain outstanding |
| Background synchronization | Manual synchronous collection with single-instance lock | No restart-safe job execution or scheduled refresh yet |
| One company pilot | Company-separated registration and credentials supported | Fine-grained account grants and real multi-company finance testing still required |
| One accounting book/SAR | Primary-book/SAR-only movement guard | Secondary-book mapping requires explicit additional implementation |

## Next implementation order

1. Configure a permitted NetSuite integration in the website and complete one live account/period GL comparison. Record verified fields, scope, row keys, totals and finance approval; do not store secrets in these documents.
2. Select the development/production SQL Server instance, configure its finance connection string, create reviewed migrations, complete missing entities and implement scoped snapshot publication. Preserve current Identity and encrypted evidence during any migration.
3. Implement resource grants and durable leased jobs before enabling unattended/multi-instance synchronization.
4. Confirm the CSV contract and deliver bank CSV ingestion plus a real bank/book comparison workflow; confirm Neotek sandbox/product access in parallel.
5. Implement transactional review, exceptions, invalidation and workpaper export, then integrate Neotek and the second bank account.
6. Complete identity production requirements, operations/restore testing, volume checks and finance acceptance.

## Required external inputs

- NetSuite integration credentials entered in the app, record permissions, primary book/subsidiary IDs and a reference GL report.
- SQL Server target/authentication and approved secrets, backup and evidence retention arrangements.
- Bank statement CSV contract/sample and Neotek subscription, bank support, authorization and sandbox details.
- Finance reviewer, agreed statement period, two account mappings, volume dataset and response-time target.

## Still outside the initial release

Payment initiation, automatic journals, official NetSuite reconciliation writeback, partial allocations, multicurrency consolidation, forecasting and detailed invoice/bill application chains. Slack/email notifications and holiday calendars remain later work. None is marked as achieved.
