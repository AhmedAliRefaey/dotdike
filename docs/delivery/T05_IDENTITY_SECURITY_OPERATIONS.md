# T05 — Identity, access and operations decisions

Status: **In progress; decision pack prepared and read-only staff-directory foundation implemented. D01/D02/D09 remain open.** Updated 16 November 2026. The user subsequently selected StaffService.cs for continuation. No deployment option, named decision owner, service provider or retention target has been selected.

T05 is complete when D01/D02/D09 have named owners and recorded choices. The choices below are proposals for review, not approvals or implemented controls. Requirements come from the [design draft](../../design_draft.md), sections 3, 5.2, 12, 13.2 and AC01–AC06. Implementation dependencies remain on the [task board](TASKS.md).

## Decision record

| Decision | Accountable roles | Proposed choice and tradeoff | Recorded choice / named owners |
| --- | --- | --- | --- |
| D01 — Identity topology and authorization consistency | Technical + security | **B for production:** migrate populated Identity to the approved SQL Server target; prefer one physical database with separately managed Identity/finance tables and an explicit shared transaction boundary. **A for a restricted pilot:** retain local SQLite on one web instance with consistent backup and the split-store revocation protocol below. A avoids immediate migration but adds coordination and recovery work. | **Open / unassigned.** Also record physical database layout, migration reviewer and revocation boundary. |
| D02 — Grants, MFA and recovery | Finance + security | **B if independent staff are available:** sensitive assignments need a different authorized reviewer. **A otherwise:** audited administrator assignment within an explicit delegation ceiling. Both retain distinct preparer/controller, scoped grants and mandatory MFA for privileged/finance access. B needs an operational bootstrap and recovery route; A accepts greater administrator risk. | **Open / unassigned.** Record assignment model, staff availability, MFA population, token/step-up durations and recovery custodians. |
| D09 — Hosting, secrets, evidence and recovery | Operations + security | **A for a restricted single-instance pilot:** private encrypted durable storage and protected persistent keys. **B for multiple instances:** approved shared evidence/secret services, separate service identities and coordinated SQL processing. Neither option is ready merely by choosing a host. | **Open / unassigned.** Record service selections, region, SQL authentication, email delivery, retention, RPO/RTO and restore/alert ownership. |

Direct ASP.NET Core Identity registration remains the user's established choice; external OIDC is superseded. Choosing a pilot does not waive the draft's public-launch controls or permit additional replicas against local files. A later move from A to B needs a recorded migration decision.

## Current implementation and gaps

| Area | Observed implementation | Consequence for T05 |
| --- | --- | --- |
| Identity persistence | [Web startup](../../src/Dike.Web/Program.cs) always selects SQLite and uses EnsureCreated. [Identity model](../../src/Dike.Web/Authentication/CompanyIdentityDbContext.cs) has one company per user and globally unique normalized email. | A connection-string change cannot select SQL Server. Preserve populated users/companies when introducing migrations. |
| Registration | [Registration handler](../../src/Dike.Web/Pages/Auth/Register.cshtml.cs) creates company/user/Administrator in a transaction and signs the user in immediately. Authority is self-attested. | Email ownership verification and controlled company bootstrap remain to be implemented; this is not independent company verification. |
| Authentication | [Web startup](../../src/Dike.Web/Program.cs) sets 12-character minimum passwords, five-attempt/15-minute lockout and a non-sliding one-hour cookie. [Login](../../src/Dike.Web/Pages/Auth/Login.cshtml.cs) uses local return URLs. | No delivered confirmation, forgotten-password, invitation, MFA challenge or registration throttling flow. Default token providers alone do not implement these features. |
| Authorization | [Web startup](../../src/Dike.Web/Program.cs) protects company/account pages by Administrator role. [CurrentCompany](../../src/Dike.Web/Authentication/CompanyIdentityDbContext.cs) resolves membership from the database. | Database-derived company scope exists; explicit active-membership status, subsidiary/account grants and transactional revocation do not. Cookie roles are not sufficient authority for future finance operations. |
| Existing account access | [Account page handler](../../src/Dike.Web/Pages/Accounts/Index.cshtml.cs) permits the company Administrator to load account metadata and collect/display eligible GL extracts. [Staff service](../../src/Dike.Web/Authentication/StaffService.cs) now supplies a company-scoped read-only directory with fresh database authorization. | The proposed role/grant matrix is stricter than today's pilot. Directory checks do not retrofit existing routes or implement finance grants. |
| Credentials and extracts | [CompanyNetSuite](../../src/Dike.Web/Authentication/CompanyNetSuite.cs) protects state, previous configuration and run files under one company-specific purpose using the web Data Protection provider. Its semaphore is process-local. | State and previous-configuration archives contain credentials as well as financial data. Treat them as secret-bearing; separate future credential references from retained evidence. This store does not coordinate replicas. |
| Deployment and keys | [Configuration](../../src/Dike.Web/appsettings.json) has localhost AllowedHosts and an empty finance connection. [Web startup](../../src/Dike.Web/Program.cs) has production HTTPS/HSTS/cookie controls but no explicit key repository, key wrapping or trusted-proxy configuration. | Actual host, proxy, key custody and restart/restore behavior need deployment evidence. This review did not inspect private keys, credentials or user database rows. |
| Jobs and health | [Worker](../../src/Dike.Worker/Program.cs) deliberately does not run imports. [Web startup](../../src/Dike.Web/Program.cs) exposes process liveness only. | No service-job authorization, distributed processing or dependency readiness is delivered. T17–T19/T38 remain separate work. |

## Implemented staff directory foundation

The [staff service](../../src/Dike.Web/Authentication/StaffService.cs), [page handler](../../src/Dike.Web/Pages/Company/Staff.cshtml.cs) and [Staff page](../../src/Dike.Web/Pages/Company/Staff.cshtml) deliver a read-only portion of T14. Administrators can open Staff from company navigation to view names, emails, current Identity roles and email/MFA/lockout indicators. The page shows 25 members at a time, rejects POST, disables response caching and encodes member text.

The service requires an authenticated Identity principal, then queries current database membership and Administrator role without trusting cookie roles or a submitted company ID. It checks the configured security-stamp claim against the current stored stamp and rejects active sign-in lockout. Both counts and paginated rows use the derived company; roles are fetched in one bounded query. Returned DTOs contain no password hashes, stamps, authenticator material or recovery tokens. The claim name comes from Identity options rather than a hard-coded default. [Microsoft claim configuration](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.claimsidentityoptions.securitystampclaimtype?view=aspnetcore-10.0).

No Identity columns/tables, real users, email verification state or role assignments were changed. Existing unconfirmed administrators retain directory access under the current pilot policy. Displayed flags describe stored settings; they do not establish MFA enforcement, recovery readiness or finance permission. Authorization is checked at the start of this read; this is not the cross-store transaction fence proposed below. Other routes retain their existing authorization behavior.

Verification: [32 real-SQLite service checks](../../tests/Dike.Tests/StaffServiceChecks.cs) pass, including stale tracked-user/cookie scenarios after changes from a separate context. [Web smoke coverage](../../tests/registration-smoke.ps1) is now 29 checks, including 10 staff checks. The full custom runner passes 158 checks. Browser Next/Previous and 1440px/390px DOM checks pass on a synthetic 28-member directory; screenshot viewport drift limits exact visual evidence. See the [checkpoint](CHECKPOINT.md) for commands and remaining browser QA. Invitations, delivery, assignments, grants, audit, last-admin protection and finance revocation remain unimplemented, so T14 is not complete.

## D01 — Persistence, migration and revocation

For A, keep exactly one web writer and a persistent local SQLite location. Use a SQLite-consistent backup, including the WAL state where applicable; a casual copy of a live database is not the restore procedure. Do not use a network-shared SQLite file as the multi-instance solution. SQL finance permissions still require the explicit cross-store protocol below.

For B, select the SQL database and authentication with D09, then build a reviewed migration against a disposable copy. Preserve company/user/role IDs, password hashes, security and concurrency stamps, memberships, claims, logins and token records. Compare normalized-email uniqueness and row relationships before cutover. Require a working credential-based sign-in and recovery test against the migrated copy. Sharing a SQL server or database does not automatically enlist two EF contexts in one transaction.

The migration sequence is: consistent backup and key inventory; disposable baseline reconciliation; validation and reviewer record; bounded write pause; final transfer and validation; explicit application cutover. Preserve originals and a schema-compatible rollback path. Do not run destructive recreation or blindly apply an initial migration to the populated store. Microsoft documents that EnsureCreated and migrations are different schema-management paths with no seamless transition. [EF Core schema guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created).

The following authorization contract is proposed for either topology:

1. Resolve the authenticated user to current active company membership, approved roles and grants on protected operations. Submitted company/account IDs only identify candidates; they never confer access. Reject unknown authority or unavailable required stores. Do not grant access from stale cookie roles, session caches or queued browser claims.
2. Associate finance authority with a persisted version and a company/user gate. Validate that version while holding the database coordination boundary through the protected mutation's commit. Revocation and grant changes must use the same boundary. D03/T09 select the SQL locking implementation; T05 defines the outcome it must guarantee.
3. For B with a shared physical database, use one explicit transaction for the authority change and its finance gate. If Identity is on a separate database, treat it as a split-store design even when both databases run on SQL Server.
4. For A or any split-store design, close the finance gate and advance its version durably **before** changing Identity membership/roles. Then apply the Identity change and record completion. Reactivation requires verified current membership and reviewed grants. A crash or uncertain outcome leaves the gate closed for idempotent recovery. All administrative mutation paths must use this protocol; direct out-of-band edits require a coordinated maintenance procedure.
5. Define revocation ordering: a finance mutation that already holds the gate can finish before revocation is acknowledged; a later mutation cannot commit under the old version. Return successful revocation only after the fence is durable and the Identity outcome is known. A partial failure is visibly pending and denies finance access. These are acceptance requirements, not behavior the current app already provides.
6. User-requested jobs capture immutable scope and requester identity, then recheck authority at enqueue, before execution/provider reads and before publication. Revoked or changed authority pauses/rejects publication. Recurring service jobs need an explicitly approved, scoped service policy plus active company, connection, mapping and bank consent; they do not inherit the creator's cookie indefinitely.
7. Recheck permissions when delivering an export or raw artifact, including previously generated output. Authorization at the start of delivery is the proposed boundary; a file already delivered cannot be recalled. Decide whether active streaming must be cancelled on revocation before supporting long-running downloads.

No split-store coordinator, authorization version or shared transaction is implemented by this pack. Keep dependent finance actions disabled until T14/T16 and their race/failure tests establish the selected contract.

## D02 — Proposed role and grant policy

Role and resource permission are cumulative requirements. A grant applies to an explicit company/subsidiary/account mapping; cross-company access is never implicit. Inheritance or wildcard scope requires a separately documented rule. Scope must cover list counts, detail traversal, job state, mapping changes, downloads and error responses as well as mutation endpoints.

| Action | Required role | Required scope / additional rule |
| --- | --- | --- |
| Company profile, connection and staff administration | Administrator | Current company and approved delegation authority; connection maintenance does not grant access to financial extracts. |
| Account overview and approved reports | Administrator, Accountant, Controller or Viewer | Explicit read grant for each account represented, including for an Administrator. |
| Start an import/sync | Administrator, Accountant or Controller | Explicit sync grant and active approved connection/mapping; Viewer cannot sync. |
| Prepare, investigate and submit reconciliation | Accountant | Account write grant. Administrator/Controller must separately hold Accountant. |
| Approve, return or reopen | Controller | Account review grant and a different user from the preparer; Administrator must separately hold Controller. Dual roles do not permit self-approval. |
| Export a workpaper | Any authenticated business role above | Explicit export grant for every included account; reauthorize at delivery. |
| Read/download raw source evidence | Explicitly authorized staff | Separate evidence-read permission; report/export permission does not imply raw evidence access or secret access. |

For assignment model A, an administrator may assign only within a recorded delegation ceiling, with before/after scope, actor, reason and audit event. Proposed rule: no self-expansion of sensitive authority. For B, Administrator/Controller assignments, delegation changes and raw-evidence access require an authorized reviewer different from the requester and beneficiary. Revocations may close access immediately without waiting for a second reviewer. Ordinary invitation acceptance never expands the invitation's approved scope.

Bootstrap must be explicit: the first verified company owner gets a restricted setup path and no automatic finance grants. A named bootstrap reviewer authorizes the initial delegation ceiling and second administrator through a recorded one-time procedure. Subsequent staff changes use A or B. If independent reviewers are unavailable, record that limitation and select A explicitly or defer the sensitive assignment; do not silently bypass B.

### MFA, email and recovery proposal

| Control | Proposed policy to accept or amend | Implementation / decision still required |
| --- | --- | --- |
| Email confirmation | Require verified email before operational access; offer a restricted resend/confirmation route. Existing unconfirmed users complete verification through a staged rollout. | T13 delivery adapter, sender/domain, trusted public origin, token expiry and controlled rollout. Never bulk-mark emails confirmed as a substitute for evidence. |
| MFA population | Require authenticator MFA for all Administrators and anyone with finance, export or raw-evidence permissions. | D02 records the exact population. T15 implements enrollment, challenge and enforcement on every relevant entry point. |
| Enrollment/challenge | Use built-in Identity TOTP support; show setup material only to the authenticated enrollee and activate only after proof. A pending MFA session cannot reach operational pages. | The current login treats every non-success generically; implement the two-factor continuation explicitly. Generate QR material locally without sending the seed to a third party. |
| Replay and throttling | Accept each successful authenticator time step only once per user; atomically consume recovery codes once. Apply bounded challenge/enrollment/recovery attempt limits. | T15 needs concurrent replay tests. Built-in TOTP support alone is not proof of this property. |
| Sensitive actions | Require recent password/MFA proof for sensitive assignments, credential changes and authenticator reset. Keep the existing one-hour non-sliding session as the starting policy. | D02 selects step-up age, challenge lifetime and any remembered-device policy; no numeric values are silently approved here. |
| Password recovery | Use expiring, user/purpose-bound tokens and generic responses. Reset does not disable MFA or restore revoked membership/grants. Invalidate prior sessions through the selected authority/stamp mechanism. | T13/T15 test replay, cross-user use, delivery failure and security-state changes; choose explicit reset/verification/invitation lifetimes. |
| Lost authenticator | Prefer a single-use recovery code. With no code, use a recorded identity-check and separate authorized recovery reviewer; revoke old sessions and require re-enrollment. | Record recovery custodians and acceptable proof. Administrators must not view passwords, TOTP seeds or another user's recovery codes. |
| Last administrator | Prevent normal removal/demotion of the last active, verified, MFA-ready administrator; serialize competing changes. Establish a second eligible administrator before operational rollout. | T14/T15 enforce the invariant. Suspected compromise can disable all access through an audited emergency procedure; it must not preserve a compromised account merely to satisfy the count. |
| Emergency recovery | Named operations/security custodians authorize identity recovery with independent review and an audit record. Restore account control only; keep finance scopes blocked until reviewed. | Record identities, escalation route and rehearsal evidence. No permanent bypass account, shared password or self-approval exception is proposed. |
| Abuse and enumeration | Generic login/reset/invitation outcomes; account and origin-aware limits for registration, delivery, login and MFA. Forwarded-client identity comes only from trusted proxies. | Numeric thresholds and operational monitoring require D02/D09. Existing login lockout remains useful but does not cover these other flows. |

ASP.NET Core documents that enabling confirmation for existing unconfirmed accounts blocks their sign-in; Dike therefore needs the staged verification path before enforcement. [Account confirmation and recovery](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm?view=aspnetcore-10.0).

ASP.NET Core supports authenticator TOTP, but its documentation also states that a code can authenticate repeatedly before expiry. Dike's proposed replay restriction therefore needs explicit implementation and concurrency verification. [Authenticator guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-enable-qrcodes?view=aspnetcore-10.0).

## D09 — Deployment and service selection

| Selection | A — restricted single-instance pilot | B — shared deployment | Recorded value |
| --- | --- | --- | --- |
| Host and network | One controlled web instance; durable private volumes; restricted access | Approved web replicas and worker identities; shared SQL/storage and coordinated jobs | **Open:** platform, region, instance count, operators and access boundary |
| SQL | Approved finance SQL target; SQLite Identity only if D01 A | SQL target and Identity layout selected under D01 | **Open:** server/database references, identity/authentication and migration principal; no credentials in this document |
| TLS and ingress | Trusted certificate, explicit public host and proxy trust | Same, consistent across replicas | **Open:** origin, hosts, TLS termination, trusted proxies/networks and client-address handling |
| Transactional email | Approved delivery provider and verified sender | Same, with durable retry and bounded delivery permissions | **Open:** provider, sender, public link origin, delivery owner and secret reference |
| Secrets | Approved restricted protected store; rotation/revocation and backup controls | Approved shared secret service using least-privilege service identities | **Open:** provider/location reference, access principals, rotation owner and emergency revocation route |
| Source evidence and exports | Private encrypted durable storage outside the web root | Private shared encrypted storage with immutable object/version semantics | **Open:** storage service, region, access identities, retention/hold capabilities and restore route |
| Identity keys | Explicit persistent, protected web key ring and recovery material | Shared web key ring for intended web instances only, with protected key material | **Open:** repository, wrapping mechanism, app identity, custodians and backup/unwrapping procedure |
| Worker protection | Worker remains disabled until durable job/evidence controls exist | Separate evidence/secret permissions and protection boundary | **Open:** identities and key access; no access to web cookie/token keys solely to decrypt evidence |
| Monitoring | Sanitized dependency readiness, failed-job/key/backup alerts | Same with replica/lease monitoring | **Open:** alert channel, named responder, escalation and thresholds; capacity targets belong to D10 |

These are service requirements, not a cloud-vendor selection. Selecting B does not enable replicas or the worker before shared evidence, job fencing and authorization are implemented and tested.

### Key and legacy-evidence migration

Record the existing Data Protection application discriminator, key-ring location, wrapping/unwrapping dependency and company purpose chain before changing deployment. The current connector uses `Dike.NetSuite.v1` followed by the company GUID in `N` format. A new application name, purpose, machine identity or key location must not strand the old ciphertext.

Persisting keys and protecting them at rest are separate steps. Microsoft warns that overriding the key persistence location disables automatic key encryption; it also treats all applications sharing a key ring as able to access its key material. Distinct purpose strings alone are not a boundary against a process with those keys. [Data Protection configuration and isolation](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).

Proposed migration: verify a protected backup and legacy decryption in isolation; split credential references from financial payloads; re-protect evidence through the selected dedicated boundary; record old/new object references and integrity digests; compare run IDs, scope, rows and totals; then cut over. Preserve historical lineage and required old key versions until the approved retention/migration policy permits retirement. A digest checks consistency but does not independently establish source authenticity or prevent privileged tampering.

Previous-configuration archives need secret-specific custody and rotation handling. Do not publish them as ordinary raw evidence, copy credentials into manifests/logs, or delete them automatically during this review. Retained legacy extracts remain unaccepted provider evidence until T02 validation; a successful decryption does not approve their source contract.

### Retention and restore worksheet

No retention period, backup interval, RPO, RTO or hosting geography has been approved. Record a numeric duration and accountable owner for each applicable row before enabling automated purge. Holds override deletion; preserve referenced evidence needed to explain retained decisions and exports.

| Data class | Required decision | Recovery proof |
| --- | --- | --- |
| Identity, memberships and grants | Backup frequency, point-in-time target, encrypted retention, suspension/deletion rules | Preserved IDs, password/stamp behavior, membership/grants and usable verified recovery |
| Finance snapshots, mappings and review/audit history | Backup/log schedule, retained history and legal holds | Counts/totals, source/version references, decisions and audit continuity |
| Raw provider/CSV evidence | Retention per source/contract and holds; immutable object policy | Every referenced object decrypts and matches its recorded digest/length |
| Export artifacts and manifests | Retention plus references to pinned evidence and decisions | Reproducible pinned totals and delivery authorization |
| Failed staging/import data | Diagnostic retention and controlled cleanup | Failed runs remain explainable without activating incomplete snapshots |
| Routine logs | Sanitized fields, retention, reader permissions | Useful correlation/state/timing without secrets or raw financial payloads |
| Credentials and key versions | Rotation, revocation, custody and retirement linked to dependent ciphertext | Restored service access through approved references and decryption of required historical objects |

The restore rehearsal must use an isolated environment with outbound provider reads and schedules disabled. Restore a consistent set of Identity, finance, evidence, exports and key material; reapply any later security revocations before allowing access. Compare scope/counts/totals and audit links, verify decryption, then exercise safe job restart/replay. Record achieved recovery point/time against approved RPO/RTO and the named reviewer. Missing keys or unresolved authority must fail closed. No restore, migration, credential rotation or purge was performed in this continuation.

## Acceptance cases for implementation

These are planned cases, **not executed tests or new acceptance evidence**. T01's web smoke checks and the 81 executable/10 CLI checks available during T05 did not establish these controls. Later domain/SQL mechanism checks in [T06](T06_REVIEW_CONCURRENCY_WORKPAPER.md) also do not establish Identity/access acceptance; see the [checkpoint](CHECKPOINT.md) for current counts.

| Case | Scenario and expected outcome | Delivery task / design acceptance |
| --- | --- | --- |
| S01 | Populated Identity copy migrates without changing IDs/hashes/stamps or losing relationships; duplicate-email and rollback cases still hold | T13/T39; AC01–AC03 |
| S02 | Existing unconfirmed user can complete a real verification flow; unrestricted access remains blocked until verified; no blanket confirmation | T13; AC03 |
| S03 | Expired/replayed/cross-user reset and invitation tokens fail; unknown-address responses do not disclose account existence; failed delivery does not activate an account | T13/T14; AC03 |
| S04 | Unenrolled or partially challenged user cannot use protected finance/admin routes; concurrent reuse of one TOTP step or recovery code has at most one success | T15; AC04 |
| S05 | Two administrators concurrently remove/demote each other; the selected last-admin invariant holds. Emergency compromise recovery leaves finance access blocked pending review | T14/T15; AC04 |
| S06 | Cross-company and same-company/different-account users cannot list, traverse, mutate, queue, read totals, inspect jobs or retrieve artifacts outside grants | T16; AC05/AC06 |
| S07 | Administrator without finance grants cannot read existing GL extracts, prepare, approve or export; dual-role preparer cannot approve the same group | T16/T29; AC06 |
| S08 | Revoke while a mutation is in flight: it commits before the acknowledged fence or fails, never commits later with stale authority. Crash between split-store steps leaves access denied | T14/T16/T37; AC04/AC06 |
| S09 | Revoke requester/grant after enqueue or provider fetch: job cannot publish. Expired/revoked bank consent blocks the corresponding service job | T18/T19/T25; AC04/AC05 |
| S10 | Revoke export/evidence grant after generation: later delivery fails; missing/foreign IDs disclose no extra resource data | T12/T16/T34; AC05/AC06 |
| S11 | Restart/redeploy and rotate keys; existing eligible ciphertext remains readable. Wrong-company context, missing key and corrupted artifact fail safely | T12/T39; evidence/restore gates |
| S12 | Worker identity can access only approved evidence/secrets and cannot read web cookie/token key material; raw-evidence users cannot retrieve secret-bearing archives | T12/T19; least-privilege gate |
| S13 | Restore all stores and historical keys in isolation; compare continuity, enforce later revocations and record measured RPO/RTO | T39; restore gate |
| S14 | Untrusted forwarded headers cannot change trusted origin/client identity; production responses/logs/health reveal no secrets or raw financial bodies | T15/T38; abuse/operations gate |

## Completion and next work

To close T05, record the named technical/security, finance/security and operations/security owners; their D01/D02/D09 choices; the selected deployment/service/retention values; explicit exceptions; decision date; and a reference to each review. A recommendation or an unanswered worksheet is not a decision.

After those choices, T13 can implement verification/recovery delivery; T14/T15 add invitations, grants and MFA in dependency order; T12 adds protected evidence once T08 is ready. T07 also requires T02 and T06. Until those inputs arrive, T06's concurrency/review/fee/workpaper/capacity decision preparation can continue independently.

Original pack verification inspected the Identity, account, connector-store and worker implementation, checked Microsoft guidance and validated document links. The later staff-directory continuation added the bounded implementation and tests recorded above. No live provider check or production acceptance is claimed.
