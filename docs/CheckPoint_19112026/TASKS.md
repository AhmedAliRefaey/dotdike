# Dike ordered delivery tasks

Updated: 17 November 2026. This board preserves T01–T41 from the user's supplied `C:/Users/ahmed/Documents/Codex/2026-11-05/re/outputs/dike_diagrams_and_tasks.md` and adds the user-requested **T42 Odoo connection/GL validation**. The original backlog's SHA-256 is recorded in the [baseline manifest](T01_SOURCE_MANIFEST.json). Requirements and D01–D11 remain in the [design draft](../../design_draft.md); Odoo's added scope is defined in [T42](T42_ODOO_VALIDATION.md). The supplied task file preserves original estimates and full done criteria; the [remaining work plan](REMAINING_WORK_PLAN.md) supplies revised estimates and execution prerequisites.

For the latest verified state and continuation steps, read the [resume checkpoint](CHECKPOINT.md). [Company provider selection](../COMPANY_REGISTRATION.md) now scopes the existing NetSuite/Odoo pages and extends the bilingual resources to **405 keys**. Current evidence: 62 HTTP checks, 38 browser checks and 6 isolated upgrade checks passed; the executable regression runner remains blocked by local Windows security. This does not complete T32, T33, T35 or T36, whose finance workflows and broader acceptance remain pending.

The [remaining work plan](REMAINING_WORK_PLAN.md) defines milestones, responsibilities, task-level scope, revised estimates and immediate work. The [acceptance tracker](ACCEPTANCE_TRACKER.md) covers AC01–AC44 plus OD-01–OD-08 for Odoo. Current totals: **42 tasks: 1 done, 10 in progress, 31 pending**. Dependencies include the planning review's additional evidence, authority and current-source prerequisites; the external original backlog is preserved unchanged.

The [delivery review](DELIVERY_REVIEW.md) covers every file present before T04 preparation, including contract/source fingerprints and the reader money fix.

Status: **Done** = deliverable verified to its stated scope; **In progress** = work started, exit not met; **Pending** = not started. External finance/provider approval is recorded separately and is never inferred from fixture passes. Follow dependency order rather than strict numeric order; independent preparation may proceed while a gate waits. For example, T12 evidence support precedes T11 publication, and T37 progresses alongside the services it verifies.

| ID | Deliverable | Depends on | Status / evidence or gate |
| --- | --- | --- | --- |
| T01 | Existing models, tests, stores and migration inventory | — | **Done** — [baseline and verification](T01_BASELINE.md). |
| T02 | NetSuite query, line identity and selected GL period validation | T01 | **In progress** — [offline comparison, candidate contract and live evidence checklist](T02_NETSUITE_VALIDATION.md) prepared and locally tested. Live reference report, scope and reviewer unavailable (user confirmed 16 Nov). AC08/09 not accepted. |
| T03 | Approved CSV identity, completeness and comparison date | T01 | **In progress** — [review pack with identity, replacement and date examples](T03_CSV_CONTRACT_REVIEW.md) prepared. Representative bank export, named reviewers and D04/D06 decisions remain pending. |
| T04 | Neotek product/consent/pagination/correction contract | T01 | **In progress** — [user-supplied developer documentation and contract review](T04_NEOTEK_CONTRACT_REVIEW.md) inspected; candidate endpoints and five documentation differences recorded. Actual subscription/version, corporate-bank and correction/coverage evidence plus D05 decision remain pending. |
| T05 | Identity/grants/MFA/hosting/secrets/evidence policies | T01 | **In progress** — [implementation review and decision pack](T05_IDENTITY_SECURITY_OPERATIONS.md) prepared with deployment alternatives, scoped authority/revocation, MFA/recovery, legacy migration and 14 planned acceptance cases. Named owners and D01/D02/D09 choices remain open. |
| T06 | Revision/concurrency/review/fee/workpaper/capacity decisions | T01 | **In progress** — [decision pack and verified independent work](T06_REVIEW_CONCURRENCY_WORKPAPER.md) prepared: 45 new domain checks and 9 disposable SQL mechanism checks pass. Named owners and D03/D07/D08/D10/D11 choices remain open. |
| T07 | Scoped entities, canonical keys and typed contracts | T02,T03,T05,T06; T42 for Odoo-specific contracts | Pending — include approved bank identity/date contracts; do not promote candidate provider keys or diagram proposals to approved schema. Odoo does not block the existing NetSuite/CSV scope. |
| T08 | Finance EF mappings and reviewed baseline migration | T07 | Pending — approved model and reviewed finance migration. LocalDB is available for disposable validation; the production SQL target remains open. |
| T09 | Scoped queries and account concurrency guard | T08 | Pending. |
| T10 | Immutable revisions/membership and unchanged replay | T03,T08 | Pending. |
| T11 | Atomic complete-snapshot publication and removal rules | T09,T10,T12 | Pending — verified evidence precedes publication; exposing active review also requires T30 invalidation. |
| T12 | Protected evidence/digests/authorized retrieval | T05,T08 | Pending. |
| T13 | Verification/recovery delivery and expiring tokens | T05 | Pending. |
| T14 | Invitations, staff roles/grants and revocation | T05,T07,T09,T13 | **In progress** — [read-only staff directory foundation](T05_IDENTITY_SECURITY_OPERATIONS.md#implemented-staff-directory-foundation) implemented with fresh database authorization and company-scoped pagination; 32 SQLite service checks and 10 added web checks pass. Invitations, assignment policy, grants, audit and revocation coordination remain pending. |
| T15 | MFA, recovery, last-admin and abuse controls | T13,T14 | Pending. |
| T16 | Finance endpoint/job/download authorization | T09,T12,T14 | Pending. |
| T17 | Atomic job claim/reclaim/renew and generation | T08,T09 | Pending. |
| T18 | Retry/reschedule, authorization pause and cancellation | T16,T17 | Pending. |
| T19 | Fenced worker publication and serialization | T11,T12,T16,T17,T18 | Pending. |
| T20 | Harden verified NetSuite reader and persist all-page evidence | T02,T12,T19 | Pending — existing reader money precision/range gaps fixed independently during [delivery review](DELIVERY_REVIEW.md); live contract, durable evidence/publication and full hardening remain gated. |
| T21 | Disjoint partitions and parent completeness | T20 | Pending. |
| T22 | Approved CSV parser/diagnostics/upload checks | T03,T12 | Pending. |
| T23 | CSV replay/replacement publication | T10,T11,T16,T22 | Pending. |
| T24 | Actual-contract Neotek discovery/authorization/balances | T04,T12,T16 | Pending. |
| T25 | All-page Neotek transactions and publication | T19,T24 | Pending. |
| T26 | Persist deterministic pinned proposals | T20,T23 | **In progress** — independent sar-v2 matching hardening is tested: scope-bearing results, scoped competition, deterministic ordering and invalid-input rejection. Persisted proposals and snapshot/revision pins remain pending; see [T06 evidence](T06_REVIEW_CONCURRENCY_WORKPAPER.md). |
| T27 | Draft group revisions and membership validation | T06,T08,T09,T10 | **In progress** — independent pure validation now rejects invalid scope/identity/revision/currency/side/money; balanced group and SAR 50 cases tested. Durable draft/group revisions and authoritative current-member checks remain pending. |
| T28 | Transactional submit/reservations/idempotency | T09,T16,T27 | Pending. |
| T29 | Distinct-controller approve/return/reopen/history | T28 | Pending. |
| T30 | Publication invalidation and stale drafts | T11,T29 | Pending. |
| T31 | Exception assignment/notes/resolution/links | T14,T30 | Pending. |
| T32 | Account balances/coverage/job overview | T16,T19,T20,T23; T24 for provider balances | Pending — CSV/GL overview may precede T24; full typed Neotek balance integration follows it. |
| T33 | Reconciliation grids/grouping/controller screens | T26,T29,T31 | Pending. |
| T34 | Consistent export inputs and immutable manifest | T06,T12,T16,T30 | Pending. |
| T35 | Approved safe CSV/JSON full workpapers | T34 | Pending. |
| T36 | Keyboard/screen-reader/mobile audit | T32,T33,T35 | Pending. |
| T37 | Real-SQL integration/race/fault runner | T08; extend through T35 | **In progress** — [LocalDB mechanism probe](../../tests/sql-concurrency-smoke.ps1) passes 9 checks with actual concurrent connections and removes its generated database. Application EF schema/service integration and fault cases remain gated on T08 and subsequent services. |
| T38 | Sanitized readiness/logging/monitoring | T05,T19 | Pending. |
| T39 | Pilot migration and full-store/key restore | T12,T15,T30,T35,T38 | Pending. |
| T40 | Benchmark agreed workload and remediate | T06,T36,T37 | Pending. |
| T41 | Two-account acceptance/runbooks/signoffs | T02,T15,T21,T25,T36–T40 | Pending — AC01–44 evidence and required live approvals. |
| T42 | Odoo connection, journal-line identity and selected GL period validation | T01 | In progress — [Dike.Web workflow](../ODOO_WEB.md), explicit T-sab local-key binding, encrypted company-scoped connection/preview, permitted accounts and weekly/monthly/custom periods implemented. 49 HTTP and 44 Odoo browser checks pass, including live reads; earlier 63 connector fixtures remain historical, with the current executable runner blocked by Windows security. [Previously approved AP/payment postings](ODOO_AP_TEST_DATA_20261117.md) verified. Full extraction, stable database/source identity, independent report and [OD-01–OD-08 acceptance](T42_ODOO_VALIDATION.md) remain open. Runs alongside T02. |

## Outstanding decisions and evidence owners

Owner roles below come from the draft. Named individuals have not been assigned.

| Gate | Responsible role | Needed evidence / choice | Affected work |
| --- | --- | --- | --- |
| NetSuite validation | Integration engineer + finance reviewer | Account-specific Records Catalog, selected account/subsidiary/primary book/period, independent GL detail report, line-key and debit/credit comparison | T02,T07,T20,T41 |
| Odoo validation | Integration engineer + finance reviewer | Actual version/hosting/API entitlement, trusted endpoint/database, service identity, company/bank GL/period, installed schema and independent full GL report | T42; T07 and production integration if Odoo is included |
| D01 | Technical/security | Identity topology, populated-store migration and membership/finance consistency | T05,T13–T16, production hosting |
| D02 | Finance/security | Role/grant authority, MFA/recovery/last-admin controls | T05,T14–T16 |
| D03 | Technical | Revision model and shared SQL approval/publication concurrency mechanism | T06–T11,T17,T27–T30,T37 |
| D04/D06 | Finance/provider | Representative CSV, duplicate-looking rows, stable identities/replacement coverage, booking/value-date and timezone rule | T03,T07,T10,T22–T23,T26 |
| D05 | Integration | Actual Neotek access/consent/pages/IDs/corrections/history/quotas | T04,T24–T25 and scheduling |
| D07/D08 | Finance/technical | Reservation release/hold lifetime, valid SAR 50 outcome under same-direction rule | T06,T27–T30,T41 |
| D09 | Operations/security | SQL target, hosting, email/secret/evidence/key services, retention and restore targets | T05,T08,T12–T13,T38–T39 |
| D10 | Product/operations | Workload, hardware, concurrency and numeric performance targets | T06,T40 |
| D11 | Finance | Workpaper schema, full-group/period totals, immutable review/audit pins and signoff | T06,T07,T34–T35,T41 |

## Session record

- 17 Nov company provider choice: Registration now requires NetSuite or Odoo. Company profile can change the persisted choice; navigation, overview and provider pages show only the selection, and direct cross-provider GET/POST requests are rejected. Previously saved connections remain intact. Tested and applied an additive SQLite upgrade with a private local backup and original row hashes/counts verified unchanged. Seven-project build, 62 HTTP checks, 38 browser checks and 6 isolated upgrade checks pass. The executable regression runner remains blocked by Windows security. [Behavior and verification](../COMPANY_REGISTRATION.md). No new upstream provider operations or finance acceptance changes.

- 17 Nov Odoo web conversion: User replaced the CLI placement with Dike.Web pages. Implemented overview, connection settings, companies/accounts and GL preview, with English/Arabic navigation and weekly/monthly/custom filters. Existing local key is explicitly bound to T-sab; saved connection/preview is encrypted per company. Final seven-project build is clean; 49 HTTP and 44 Odoo browser checks pass. Windows blocks the standalone executable runner with `0x800700E1`; 24 new service cases are compiled but unexecuted. No new Odoo postings, security exclusions or real Identity test users. [Usage and verification](../ODOO_WEB.md). T42 remains in progress.

- 17 Nov Odoo read-only test and AP fixtures: Implemented the local JSON-2 validation tool and requested company/account/period selectors. Seven-project build and 221 executable checks pass, including 63 Odoo fixtures. Live authentication found NApp company 1 and SAR bank accounts 118/119. On explicit user instruction, separately created and posted six synthetic AP bills (SAR 5,753.00) and four approved manual payment entries (SAR 2,201.50); verified SAR 3,551.50 remaining AP and all four October/November bank GL samples. See the [posting report](ODOO_AP_TEST_DATA_20261117.md). T42 remains in progress; no finance gate is complete.

- 17 Nov Odoo local secrets: Added the web UserSecretsId, masked setup script and redacted presence check. Verified a clean isolated six-project build and 14 synthetic configuration checks. No real credentials or Odoo calls; user enters the real key locally. T42 moves to In progress; no acceptance case is complete.

- 17 Nov Odoo Accounting: User confirmed Accounting is installed on `https://napp.odoo.com/`. Recorded this setup input in T42 and the checkpoint. Next prepare the integration user and API key securely, then verify read-only access and company/accounting scope. No connector implementation or API test has been completed.

- 17 Nov Odoo account replacement: User selected `https://napp.odoo.com/` as the current T42 target. Updated the task, checkpoint and API base to `https://napp.odoo.com/json/2`. The earlier Odoo 19 / Custom-trial setup remains planning context; replacement-account metadata and API access have not been tested.

- 17 Nov earlier Odoo inputs: User supplied an initial Apps URL, reported creating a company and clarified that the account uses a Custom-plan trial; subsequently confirmed Odoo 19. That initial endpoint has been superseded by the replacement above. T42 implementation and live acceptance remain pending.

- 17 Nov Odoo task: User requested a new task equivalent to T02 for an Odoo connection. Added T42 with a 4–6-day connection/validation slice and OD-01–OD-08 acceptance cases; updated remaining totals to 86–133 focused engineer-days. T42 is Pending; no Odoo implementation, live call, finance signoff or replacement of NetSuite was performed. Existing localization/browser results are preserved.

- 16 Nov project review/planning: Read all 11 delivery files, verified the original backlog fingerprint, inspected current source including recent localization, and created the [remaining work plan](REMAINING_WORK_PLAN.md) and [AC01–AC44 tracker](ACCEPTANCE_TRACKER.md). Fresh isolated serial build is clean; 158 executable and 10 CLI checks pass. Earlier 29 web/9 SQL checks were not rerun. Strengthened execution prerequisites; no application code, task completion, owner decision or finance approval changed. Revised remaining estimate: 82–127 focused engineer-days plus external waits/significant rework.

- 16 Nov staff continuation: User selected StaffService.cs. Implemented the company-scoped read-only staff service and Razor directory without changing the Identity model. Six-project build, 158 executable checks and 29 web smoke checks pass. Browser pagination and desktop/mobile DOM checks pass; integrated screenshot viewport emulation is unreliable. T14 remains partial; no authority-assignment decision or real-user change was inferred.

- 16 Nov: Read the full supplied backlog; inspected the actual code and stores; completed T01 with a clean build, 31 executable assertions and 19 isolated smoke checks.
- 16 Nov: User confirmed T02's reference inputs are not available. Continue its offline preparation; do not mark live validation complete or activate an unverified finance mapping.
- 16 Nov: Added candidate query/key provenance, offline full-row GL comparison, synthetic examples and live review worksheet. Six-project build and 71 executable assertions pass. T02 remains open for the actual account/report and reviewer.
- 16 Nov continuation: Rechecked the six-project build (zero warnings/errors), all 71 executable assertions and all 10 NetSuite validation CLI smoke checks. T02 offline preparation is verified; live/finance gates remain open.
- 16 Nov continuation: No CSV sample was found in the workspace; requested a sample path. Prepared T03's format/identity/coverage/date review worksheet and 18 synthetic cases. D04/D06 remain open and the T22 importer has not started. Added the resume checkpoint for the next session.
- 16 Nov review: Reviewed all seven delivery files, historical source fingerprints and the candidate contract. Reproduced reader rounding loss, then shared strict monetary validation and added ten connector checks. Build, 81 executable checks and 10 CLI checks pass; query/key versions unchanged.
- 16 Nov review: User supplied the Neotek ReadMe developer portal. Inspected its guides and API schemas; prepared T04's source/contract review, recorded documentation differences and kept D05/live acceptance open. See the delivery review and resume checkpoint.
- 16 Nov next task: User requested another task. Prepared T05 against the current Identity/account/connector/worker implementation and Microsoft guidance. Recorded D01/D02/D09 alternatives, authorization/revocation and recovery policies, legacy user/key migration, service/retention worksheets and 14 planned acceptance cases. Deployment preference was requested; no choice or named owner has been supplied. Documentation checks only; application verification remains the prior 81 executable and 10 CLI checks.
- 16 Nov remaining tasks: Prepared T06's five decision records, concurrency/fee/workpaper examples and capacity worksheet. Reproduced 25 failures in 45 new domain cases, corrected matching/review guards and versioned the matcher as sar-v2. Build passes with zero warnings/errors; 126 executable checks and 10 CLI checks pass. Found LocalDB 2019 and added a disposable SQL probe: 9 mechanism checks pass, including observed blocking, with successful database cleanup. T26/T27/T37 have verified independent portions; production schema/services and owner decisions remain incomplete.
