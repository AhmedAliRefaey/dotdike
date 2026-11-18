# T01 — implementation and migration inventory

Completed: 16 November 2026. Scope: local source inspection, build, existing executable checks and isolated web smoke tests. Source: T01 in the supplied `dike_diagrams_and_tasks.md`, derived from [design draft v0.1](../../design_draft.md). See the [task board](TASKS.md) for subsequent work.

This is a baseline before T02 changes. No provider request, finance migration, production data change or finance approval was performed. The user confirmed that T02's finance reference report and scope/reviewer details are not available. No AGENTS.md was found in the workspace or checked parent. The workspace has no Git repository; [source fingerprints](T01_SOURCE_MANIFEST.json) identify the inspected baseline instead of a commit.

## Implemented versus missing

| Area | Inspected implementation | Remaining requirements / tasks |
| --- | --- | --- |
| Solution | Five projects target .NET 10 with nullable analysis and warnings as errors. EF/Identity packages are 10.0.14. Installed SDK used: 10.0.401. | No pinned SDK, real SQL integration project or discovered unit-test framework. T37 adds SQL evidence. |
| Identity | [Identity context](../../src/Dike.Web/Authentication/CompanyIdentityDbContext.cs), [startup](../../src/Dike.Web/Program.cs) and [registration](../../src/Dike.Web/Pages/Auth/Register.cshtml.cs): company/user/admin transaction, globally unique normalized email, restrictive company FK, cookies, password rules, lockout configuration and server-resolved company membership. | T13–T16: verification/recovery delivery, invitations, staff lifecycle, grants, MFA, current-role/revocation enforcement and full access matrix. The [staff service file](../../src/Dike.Web/Authentication/StaffService.cs) is empty. Role policies alone do not implement staff management. |
| Finance data | [Finance context](../../src/Dike.Infrastructure/DikeDbContext.cs) has six entities, three rowversions, precision and selected indexes/FKs. Registered in web startup. | No finance reads/writes, migrations or SQL tests. T07–T12/T37 must establish the real persistence model. |
| NetSuite | [Reader](../../src/Dike.Infrastructure/NetSuite/NetSuiteReader.cs) and [authentication](../../src/Dike.Infrastructure/NetSuite/NetSuiteConnection.cs): TBA/PS256, derived HTTPS host, account listing, primary-book/SAR guards, posted GL query, full paging, scoped rows, duplicate rejection, decimal parsing and bounded query retries. | T02 live query/key/report verification; T20–T21 run/page evidence, decimal(19,2) overflow checks, source hardening and partitions. Existing two-decimal parsing does not enforce the SQL precision bound. |
| Connector storage | [Company connector](../../src/Dike.Web/Authentication/CompanyNetSuite.cs): encrypted company-purpose state and completed extracts, atomic state replacement, old configuration archive, in-process company gate, last-success retention on handled failures. | No canonical connection ID/version, evidence digest/access service, distributed transaction, complete raw-page history or SQL publication. T10–T12/T19/T20/T39. The globally registered [fallback reader](../../src/Dike.Infrastructure/UnconfiguredNetSuiteReader.cs) is not the UI's active connector. |
| Bank data | [Core contracts](../../src/Dike.Core/Models.cs) define an INeotekClient port, snapshots and balances. | No CSV or Neotek implementation. Balance type is a string and as-of cannot currently be unknown. D04–D06, T03–T04/T22–T25. |
| Matching | [Matcher](../../src/Dike.Core/MatchingEngine.cs): sar-v1 proposals, exact reference/amount, competing candidates, pending exclusion and Friday/Saturday calendar. | No persisted proposals, snapshot pins or full date-boundary evidence. T26 and AC19/20. |
| Review | [Policy](../../src/Dike.Core/ReviewPolicy.cs): reason, same scope, whole-row 1:1/1:N/N:1, nonzero same sign, balance, duplicate/pending rejection and different approver. | No role authorization, current-source checks, revisions, transactional reservation/idempotency/approval/return/reopen/invalidation or exceptions. T27–T31. |
| Worker | [Worker host](../../src/Dike.Worker/Program.cs) logs that processing is disabled, then waits for shutdown. | No actual provider execution, claim, lease owner/renewal/fencing, retries, cancellation or scheduler. T17–T19. |
| UI/export | Registration/company/connection/account screens; full saved GL totals with only 500 preview rows. Health endpoint reports liveness. | T32–T36 overview, grouping/review, exceptions, consistent full exports and accessibility. T38 readiness/monitoring. No current endpoint establishes finance readiness. |
| Operations | Private local file encryption and defensive connector error messages are present. | D09/T38–T41: approved storage/key topology, log audit, complete restore, pilot migration, benchmark and signed two-account acceptance. |

## Stores and migration inventory

| Store | Observed source/configuration | Required migration treatment |
| --- | --- | --- |
| Identity SQLite | Default [App_Data](../../src/Dike.Web/App_Data/) contains an existing `identity.db` (106,496 bytes at inspection). Startup uses EnsureCreatedAsync. No Identity migrations or model snapshots exist. Database rows were not read. | Select D01 first. Do not apply an initial create migration to this populated store. Inventory the live schema and reconcile a reviewed baseline; preserve company/user IDs, hashes, security stamps, roles and tokens. Use a WAL-consistent backup and restore rehearsal. |
| Finance SQL Server | [Committed configuration](../../src/Dike.Web/appsettings.json) has an empty Dike connection string. Source contains UseSqlServer registration but no finance caller or migrations. Runtime environment/secret overrides and remote databases were not inspected. | Decide D03/D09 and build the tenant-safe model before a disposable SQL migration. Do not assume an existing production database is empty. No SQL connection or migration was attempted. |
| NetSuite encrypted files | Default root is below web App_Data, overridable by NetSuite:StoragePath. No default NetSuite subdirectory was present before smoke testing. Existing alternate stores were not searched. | Preserve original state/run/archive files and key lineage. Validate scope/key/coverage before any pilot import. Do not infer absent source timestamps, missing provider pages or prior finance signoff. |
| Data Protection | Company-specific protector purpose `Dike.NetSuite.v1`; application code does not explicitly configure production key persistence/protection. | Inventory the actual deployment key ring/application identity under D09; preserve keys alongside encrypted evidence. Encryption without a complete restore is not acceptance. |
| Test output | [Artifacts](../../artifacts/) holds compiled output and isolated synthetic smoke databases/evidence. | Keep separate from pilot/production data. These files are test evidence, not migration inputs. |

The six finance entities need substantial replacement or extension:

| Entity | Existing constraints | Gaps to resolve before baseline migration |
| --- | --- | --- |
| AccountMapping | Unique Company/Connection/ExternalAccount; rowversion | String company only on this entity, no canonical bank/GL entities, active revision/pointer or distinct GL guarantee. |
| StoredMovement | decimal(19,2); unique MappingId/Side/SourceId; restrictive mapping FK | Identity depends on replaceable mapping. No CompanyId, logical row/revision separation, booked history, snapshot membership or import-run FK. Cannot safely represent A→B→A. |
| SyncJob | Status/NextRunAt index; rowversion; lease generation/expiry fields | No tenant FK, mapping FK, owner, DB-time claim/fence, cancellation or retry contract. |
| MatchGroup | Rowversion, status, preparer, reason and rule version | No tenant/mapping FK, immutable revision/member history, snapshot pins or transition log. |
| MatchMember | Restrictive group/movement FKs; filtered unique MovementId when Active | No composite tenant/revision consistency; no transactional lifecycle. This is not proof of stable logical-row reservations. |
| AuditEvent | Unique MappingId/IdempotencyKey | No tenant/mapping FK or typed targets. Does not implement actor/action/resource/payload-bound replay, reauthorization or atomic transition results. |

Missing model families include grants/invitations, connections/revisions, bank/ledger accounts, canonical source state, import runs/pages/coverage, movements/revisions, snapshots/memberships, balances, proposals/candidates, group revisions/transitions/reservations, action requests, exceptions/events/links, and evidence/export manifests. The diagram's proposed supporting entities still need D03/D11 review.

## Fresh verification

All commands below ran on 16 November 2026 from the workspace with .NET SDK 10.0.401.

| Command / check | Actual result | Limit |
| --- | --- | --- |
| `dotnet build Dike.slnx --no-restore -p:UseAppHost=false` | Passed; 0 warnings, 0 errors; all five projects | Uses existing restored dependencies; no SQL/provider verification. |
| Editor test runner on the two check sources | No tests discovered | Expected: this is a plain executable, not VSTest/MTP. Not counted as a test pass. |
| `dotnet tests/Dike.Tests/bin/Debug/net10.0/Dike.Tests.dll` | 31 PASS assertions: 11 domain + 20 connector, including repeated per-page header assertions | Fake HTTP only; no live query semantics or SQL races. A preliminary `dotnet run --no-build` failed because the build disabled the native app host; direct DLL execution is the verified command. |
| `dotnet build src/Dike.Web/Dike.Web.csproj --no-restore -p:UseAppHost=false -o artifacts/registration-check` | Passed; 0 warnings, 0 errors | Isolated web output. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tests/registration-smoke.ps1` | 19 checks passed; temporary server stopped by script | Initial sandbox launch failed on duplicate Path/PATH environment entries. Approved execution outside the sandbox succeeded. Synthetic users/credentials only; no NetSuite request. |

Smoke coverage includes CSRF rejection, registration, duplicate email, company profile isolation, connection separation/secret non-redisplay, arbitrary host rejection, logout, login return-URL safety and password change. It does not exercise concurrent registration/orphan assertions, lockout duration, session expiry, current-role revocation, finance grants or complete authorization matrices. Historical documentation claims are not expanded into new evidence.

## Acceptance coverage and gaps

“Partial” means relevant code/checks exist; it does not mean the acceptance case is complete.

| Acceptance cases | Baseline evidence | Missing gate |
| --- | --- | --- |
| AC01–02 | Partial: transaction/unique index and web registration/login/password checks | Concurrent rollback/no-orphan, full session/lockout/security cases. |
| AC03–04 | Not implemented | Verification/recovery/invitations/MFA and approved staff controls. |
| AC05–06 | Partial: two-company profile/configuration isolation only | Finance SQL, account grants, jobs, exports and role/revocation matrix. |
| AC07 | Partial: host/signature fixtures | Broader negative mode/redirect/assertion claim cases; live auth is AC08. |
| AC08–09 | Open external gate; SAR guard has a fixture | Account-specific Records Catalog/query/key/scope and report agreement. |
| AC10–11 | Partial AC10: all account pages, page failure, duplicates and cap | Full malformed paging matrix, persisted publication and partition tests; AC11 absent. |
| AC12–13 | Partial AC13: debit-minus-credit and precision fixture | Persistent replay, all providers, overflow/zero/sign edge cases. |
| AC14–18 | Not implemented | Neotek, typed balances, approved CSV, revisions/removal/coverage. |
| AC19–20 | Partial: pure matcher/calendar checks | Persisted inputs plus all Friday/Saturday/reversed/3-vs-4-day boundaries. |
| AC21–22 | Partial: pure balance/duplicate/cardinality/direction/approver policy | Complete rule fixtures, SQL freshness and authorized distinct staff workflow. |
| AC23–31 | Not implemented as transactional workflows | SQL reservation/publication/lease races, source invalidation and payload-bound retries. Existing connector retry code is only part of AC30. |
| AC32 | Partial: encrypted previous-success retention in code | Failure injection, coverage/current SQL rows and overview evidence. |
| AC33 | Partial: +5,000 vs +5,050 remains mismatch | D08, mixed-sign rejection fixture and accepted end-to-end workpaper. |
| AC34–37 | Not implemented | Exceptions, reproducible full exports, concurrent capture and text safety. |
| AC38 | Historical limited public mobile inspection only | Representative authenticated keyboard/screen-reader/mobile audit. |
| AC39–41 | Not implemented/verified | Restore, capacity targets, stable reservation identity and cross-period workpapers. |
| AC42 | Partial: sanitized connector errors, HTTP loggers disabled, liveness | Readiness and complete secrets/raw-body logging audit. |
| AC43–44 | Not performed | Pilot migration rehearsal and named two-account finance/operations signoff. |

## T01 exit

The baseline, existing checks, store boundaries, migration gaps and acceptance coverage are recorded. No planned capability is counted as delivered. T01 is complete. T02 can prepare candidate query/version evidence and an offline comparison, but its live/finance exit remains open. D01–D11 remain unapproved; dependent migrations and financial workflows must not silently choose these decisions.
