# Dike documentation and implementation review — 19 November 2026

Reviewed all 29 project Markdown files outside generated artifacts, dependencies and build output against the current source. Reference commit: `7e6e49a212ccf71aeabc3f6a0b09236105ee236e`, with the existing staged `src/Dike.Web/appsettings.json` change retained. This is a direct review, not an independent reviewer signoff.

Fresh verification: isolated seven-project build, zero warnings/errors; **259 executable checks passed**: 56 domain, 31 NetSuite fixtures, 39 offline GL comparisons, 32 SQLite staff checks, 72 Odoo client fixtures and 29 Odoo web service checks. [Executable log](artifacts/progress-review-20261119/executable-checks.log). The historical Windows runner block did not reproduce. PostgreSQL integration, HTTP/browser suites, hosted deployment, live providers and finance acceptance were not rerun in this review. No application code or database was changed. Historical records below are assessed within their dated scope; planned gaps are distinguished from inaccurate current-status claims.

## README.md

- The statement that the executable runner remains blocked is outdated for this checkout: the full runner now passes 259 checks, including the previously unexecuted Odoo web cases. Add a dated current verification entry while retaining older results as history.
- The linked delivery board and checkpoint omit parts of the 18 November PostgreSQL implementation. Their old task counts and 86–133-day estimate should not be presented as a freshly assessed completion forecast.

## design_draft.md

- **Architecture drift:** sections 6 and 13 still specify SQL Server finance and SQLite-only current Identity. [DeploymentConfiguration](src/Dike.Web/Hosting/DeploymentConfiguration.cs) now selects PostgreSQL, with separate Identity/finance schemas and optional Development SQLite. [Initial finance migration](src/Dike.Infrastructure/Migrations/Finance/20261118113239_InitialPostgresFinance.cs) and [Identity migration](src/Dike.Web/Migrations/Identity/20261118113143_InitialPostgresIdentity.cs) exist. Record this implementation choice and update provider-specific concurrency assumptions; production operational approval remains separate.
- **Scope drift:** the core specification remains NetSuite-centric. Odoo/provider selection is implemented and documented in T42, but its source namespace, stable database binding and actual ledger dimensions are not integrated into the core contracts. [AccountScope](src/Dike.Core/Models.cs) still requires subsidiary/book dimensions; an Odoo adapter must not fabricate those values.
- **Planned schema gaps:** [DikeDbContext](src/Dike.Infrastructure/DikeDbContext.cs) still has six finance entities. ImportRun/Page, immutable MovementRevision/SnapshotMember, mapping/group history, balances, grants, action requests, exceptions and export manifests remain absent. `StoredMovement.ImportRunId` has no corresponding entity/FK; movement uniqueness still depends on replaceable `MappingId`. Existing migrations therefore do not complete the specified finance model.
- **Planned workflow gaps:** no normalized snapshot publisher, CSV/Neotek implementation, persisted proposal/review service, workpaper exporter or functioning worker exists. Matching and review are pure policies; their passing tests do not establish the transactional workflows in sections 9–10.
- **Transaction-boundary difference:** [CompanyOdoo.MutateAsync](src/Dike.Web/Authentication/CompanyOdoo.cs) and [CompanyNetSuite.CollectAsync](src/Dike.Web/Authentication/CompanyNetSuite.cs) perform provider HTTP calls inside a document session. The [PostgreSQL session](src/Dike.Web/Authentication/PostgresCompanyDocumentStorage.cs) already holds a transaction and advisory lock at that point. Same-company/provider reads and writes wait on that lock. Full extraction should stage network results outside the transaction, then recheck authority and connection/catalog versions during a short commit.
- The planned file manifest needs reconciliation with actual locations: Staff pages already exist; migrations live under `Migrations/Identity` and `Migrations/Finance`; PostgreSQL integration checks are in the existing test executable. These location differences should not be mistaken for entirely absent implementations.

## docs/PROJECT_STATUS.md

- The opening PostgreSQL update and storage checklist are newer than the remaining narrative. The status date, five-project description, older verification table and Week 2 instruction to create migrations no longer describe the current checkout consistently.
- The next implementation order remains NetSuite-first, while the later Odoo handoff prioritizes T42 and records unavailable NetSuite reference inputs. Make the Odoo extraction slice the immediate engineering task and keep NetSuite validation as a separate pending gate.
- Existing migrations need to be distinguished from the missing complete finance model and application publication services. PostgreSQL availability does not imply that connector results reach `dike_finance.Movements`.

## docs/IMPLEMENTATION_PLAN.md

- This is explicitly historical, but its setup dependency still says finance SQL Server is empty/unconfigured. Add a clear reference to the PostgreSQL implementation when using this document for current work.
- Its five-project inventory omits both validation tools and the later Odoo workflow. Preserve the original estimate as history; use the actual seven-project solution for implementation planning.

## docs/DATABASE_CONFIGURATION.md

No drift detected.

## docs/VERCEL_DEPLOYMENT.md

No drift detected.

## docs/COMPANY_REGISTRATION.md

- The current runner-block statement needs a dated update: all 259 executable checks now pass. Keep the 17 November HTTP/browser/upgrade results explicitly historical; they were not rerun today.

## docs/OIDC_SETUP.md

No drift detected.

## docs/NETSUITE_CONNECTOR.md

- Collection is described as serialized only within one application instance. PostgreSQL mode now serializes company/provider sessions across instances using an advisory transaction lock; single-instance behavior applies to the legacy file implementation. Durable leased jobs remain absent.
- The current synchronous collection holds the PostgreSQL document transaction across remote extraction. Record this bounded implementation limitation and replace it with staged collection plus a short version-checked commit before expanding import duration/concurrency.

## docs/ODOO_WEB.md

- The single-instance/shared-locking limitation is stale for PostgreSQL document storage. Shared locking exists; complete extraction history, durable jobs and finance authorization still do not.
- The statement that 29 Odoo web cases are compiled but unexecuted is superseded by today's passing run. Earlier browser/live observations remain historical.
- **Planned gap:** [OdooStoredState](src/Dike.Web/Authentication/OdooWorkspace.cs) retains one latest preview, not immutable extraction runs, original pages, contract digests or persistent failed-attempt history. Failed requests preserve the preview and show a request error, but there is no durable import-attempt record.

## docs/ODOO_LOCAL_SETUP.md

No drift detected.

## docs/design/README.md

- The current implementation section still presents a dated PID/output directory as the running application. Treat those as last-observed details and verify runtime state before using them as a resume instruction. Current runtime/browser state was not checked here.

## docs/design/FRONTEND_REDESIGN_PROPOSAL.md

- Section 9 still recommends starting F1 then F2, although the opening and design record mark both implemented. The next UI slice is F3 after complete source/evidence services; F4/F5 still depend on finance workflows.
- The backend caveat still calls out SQL Server topology. Update it to the implemented PostgreSQL foundation and the unresolved finance model, authority, restore and operational decisions.
- The proposed persistent latest-failure/import-history experience has no Odoo run/attempt model yet. Incorporate it into T42/F3, preserving separate last-success and latest-attempt states.

## docs/delivery/TASKS.md

- T08 is marked Pending despite initial PostgreSQL finance mappings/migration being implemented. Recommend **In progress**, with its exit still requiring the expanded reviewed model, tenant/revision constraints and upgrade evidence.
- T12 has an encrypted shared-document foundation, but no immutable page/run evidence manifest or authorized finance artifact retrieval. Record that partial foundation without marking T12 complete.
- T37 describes only the LocalDB probe. [HostedStorageChecks](tests/Dike.Tests/HostedStorageChecks.cs) now covers actual PostgreSQL migrations, cross-instance documents, rollback, key persistence, finance concurrency and active-member uniqueness. These checks are opt-in and were not included in today's 259-check run; application review/publication races remain missing.
- The current runner block and 405-key UI summary are stale. Today's runner passes; English/Arabic resource sets both contain 434 matching keys.
- The published 1 done / 10 in progress / 31 pending count is the old board state. Reconcile partial work before recalculating it; task counts are not a measure of percentage complete.

## docs/delivery/CHECKPOINT.md

- The latest handoff predates the PostgreSQL changes. Its current claims of no Git metadata, EnsureCreated-only startup and no finance migration are stale. Git exists; [IdentitySchema](src/Dike.Web/Authentication/IdentitySchema.cs) uses migrations for PostgreSQL and the legacy additive path for SQLite.
- The active runner-block statement is superseded by the fresh 259-check pass. Old results and security observations should stay in historical entries.
- Refresh the current handoff with PostgreSQL schemas/shared documents and the remaining full-extraction gap. Retain T42 as the next backend slice; a dated PID is not evidence of a currently running process.

## docs/delivery/CONTEXT_SUMMARY.md

- “No Git metadata” is incorrect for the current repository. Record the commit and local changes as the current source baseline.
- The finance summary needs to credit existing initial migrations and shared document storage while retaining the missing complete model/publication workflows.
- The runner-block and unexecuted 29-case statements are no longer current. The recommendation to resolve that block should become a requirement to preserve the now-passing regression baseline.

## docs/delivery/REMAINING_WORK_PLAN.md

- Current implementation/decision sections retain SQL Server, missing migration, no-Git and absent Odoo-adapter assumptions. Reconcile these with PostgreSQL, the existing Odoo client/web workflow and the current commit history.
- WP1/T08 and T37 need credit for the initial migrations and PostgreSQL integration harness. WP0/T42 should estimate the remaining full reader/evidence/comparison work, rather than the already implemented minimal client.
- The 86–133-day estimate is historical. Re-estimate after reconciling implemented slices and confirming release scope; this review supplies no new delivery-date commitment.

## docs/delivery/ACCEPTANCE_TRACKER.md

- OD-07's unexecuted-service-test statement is stale: the 29 Odoo web cases now pass. This improves bounded-preview evidence and does not complete full-extract retention/access acceptance.
- Current implementation evidence should include the PostgreSQL harness and shared encrypted storage, rather than describing only local files/LocalDB mechanisms. Keep actual service-race, restore and live finance gates open; source inspection is not a fresh PostgreSQL test result.
- AC38's old screenshot limitation should be contextualized with the subsequent design record. Screen-reader and cross-browser acceptance remain unverified; no browser audit was performed today.

## docs/delivery/T01_BASELINE.md

No drift detected.

## docs/delivery/T02_NETSUITE_VALIDATION.md

No drift detected.

## docs/delivery/T03_CSV_CONTRACT_REVIEW.md

No drift detected.

## docs/delivery/T04_NEOTEK_CONTRACT_REVIEW.md

No drift detected.

## docs/delivery/T05_IDENTITY_SECURITY_OPERATIONS.md

- Its implementation table says Identity always uses SQLite, the connector has only a process-local semaphore, and no shared key repository/wrapping/proxy configuration exists. [DeploymentConfiguration](src/Dike.Web/Hosting/DeploymentConfiguration.cs), [PostgresCompanyDocumentStorage](src/Dike.Web/Authentication/PostgresCompanyDocumentStorage.cs) and the migrations supersede those claims for PostgreSQL/hosted mode.
- D01/D09 proposals still center on SQL Server without recording the implemented PostgreSQL/Vercel path. Reconcile the technical record; delivery provider, grants/revocation, MFA, retention, recovery targets and named operational approval remain unresolved.
- The directory's read-only status remains a real delivery boundary: [StaffModel.OnPost](src/Dike.Web/Pages/Company/Staff.cshtml.cs) returns 405. Invitations, permission changes and two-person finance access cannot be treated as implemented staff management.

## docs/delivery/T06_REVIEW_CONCURRENCY_WORKPAPER.md

- The proposed transaction mechanism and nine historical race checks use SQL Server `UPDLOCK,HOLDLOCK`/LocalDB. PostgreSQL is now the application's relational provider. Prove the selected guard, lease and review/publication ordering through PostgreSQL application services; the old mechanism tests cannot establish those semantics on the new provider.
- Initial PostgreSQL `xmin` concurrency and active-member uniqueness cover a smaller boundary than immutable revisions, publication/approval ordering and payload-bound action replay. Those planned services remain absent.

## docs/delivery/T42_ODOO_VALIDATION.md

- The effort introduction still says no Odoo adapter exists. A client and web workflow now exist; the remaining estimate should cover complete extraction, evidence, comparison and live acceptance.
- **Priority implementation gap:** [IOdooReadOnlyClient](src/Dike.Infrastructure/Odoo/IOdooReadOnlyClient.cs) exposes only a GL sample operation. [OdooReadOnlyClient](src/Dike.Infrastructure/Odoo/OdooReadOnlyClient.cs) makes one `limit + 1` GL query, allows at most 50 rows and 31 days, and [OdooGlSample](src/Dike.Infrastructure/Odoo/OdooModels.cs) always reports `SampleOnly = true`. Catalog pagination does not implement GL pagination.
- **Contract gap:** `OdooGlLine` has item/entry/journal IDs, date and money, but no reference text, source modification evidence or stable connection/database identity. The matcher requires reference/version-bearing movements. Define the Odoo-specific extraction contract before connecting these samples to reconciliation.
- **Evidence gap:** only the latest encrypted preview is retained. Add immutable run/page evidence with source/contract versions, capture boundaries, counts, exact totals and digests, separate from stored credentials. Failed/cancelled/inconsistent runs must preserve the previous successful result.
- **Comparison gap:** no Odoo report-comparison implementation exists; the current comparison tool requires NetSuite-specific fields. Add an Odoo envelope and row-level comparison without invented book/subsidiary values. A complete independent GL export and named finance review remain missing.
- **Recommended next slice:** version the candidate source contract; implement deterministic complete GL paging with duplicate/non-advancing/scope/money/change detection; stage protected run evidence outside long-lived SQL transactions; recheck authority/configuration before committing. Prove more-than-50-row extraction, failed middle pages, replay, cancellation and configuration/revocation races with fixtures. Follow with offline comparison and independent-report acceptance before marking T42 Done.
- The old runner block is superseded by 72 Odoo client and 29 Odoo web fixture passes today. Full OD-01–OD-08 acceptance remains open.

## docs/delivery/ODOO_AP_TEST_DATA_20261117.md

No drift detected.

## docs/delivery/DELIVERY_REVIEW.md

No drift detected.

## tools/Dike.NetSuiteValidation/README.md

No drift detected.

## tools/Dike.OdooValidation/README.md

No drift detected.
