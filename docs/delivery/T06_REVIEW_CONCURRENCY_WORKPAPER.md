# T06 — Revisions, review, concurrency and workpapers

Status: **In progress; decision pack, domain hardening and SQL mechanism checks prepared.** Updated 16 November 2026. D03/D07/D08/D10/D11 still require named owners and recorded choices. The user requested continuation through the remaining tasks; no finance decision or production topology was inferred from that request.

Requirements are in the [design draft](../../design_draft.md), sections 7, 9–11, 13.2 and 15. [T05](T05_IDENTITY_SECURITY_OPERATIONS.md) covers authority, deployment, keys and recovery; [the task board](TASKS.md) records dependencies. The recommendations below do not change stored finance data or establish finance acceptance.

## Decisions for review

| Decision | Recommendation and alternative | Named owner / recorded choice |
| --- | --- | --- |
| D03 — Immutable revisions and concurrency | **Recommend A:** immutable source/group revisions with an explicit SQL guard shared by review and publication. This gives a clear ordering and has now been exercised with synthetic concurrent SQL connections. **B:** serializable/conditional generation updates with proved conflict handling; potentially more concurrency, but requires equally strong reservation/phantom/publication tests. | Technical owner **unassigned; choice open**. Select the physical revision layout, guard identity/order and retry contract before the finance migration. |
| D07 — Return/reopen reservations | **Recommend A:** release on Return/Reopen in the same transaction as the transition, preserving history. Fresh submission must reacquire every reservation. **B:** retain an explicit review hold; select expiry, owner, withdrawal and takeover rules to prevent indefinite blocking. | Finance + technical owners **unassigned; choice open**. |
| D08 — SAR 50 difference | **Recommend A for the existing release rules:** retain an unresolved documented exception when actual source rows cannot form a valid same-direction group. **B:** accept only a verified source representation/correction that satisfies those rules. **C:** mixed-direction allocation is a separate scope/design change. | Finance owner **unassigned; choice open**. No fee or source correction has been inferred. |
| D10 — Capacity and targets | Agree the workload, host, provider limits and numeric acceptance thresholds before benchmarking. Use the measurement worksheet below. | Product + operations owners **unassigned; workload/targets open**. No production SLA is asserted. |
| D11 — Workpaper package | **Recommend B:** separate CSV tables plus one structured JSON package and manifest. **A:** one flat CSV with explicit record types. Both need approved fields, masking, period/group arithmetic and immutable evidence pins. | Finance owner **unassigned; choice open**. No export schema is approved yet. |

## Implemented independent hardening

Review of the existing pure domain code found defects that do not depend on the open choices. [Domain regression checks](../../tests/Dike.Tests/DomainPolicyChecks.cs) first reproduced **25 failures** in 45 new cases; all 45 pass after the fixes.

| Area | Corrected behavior | Limit |
| --- | --- | --- |
| [Matching engine](../../src/Dike.Core/MatchingEngine.cs) | Competition includes full existing account scope, so repeated provider IDs in different accounts/companies do not falsely compete. Proposals and candidate IDs use deterministic ordinal ordering. | The existing AccountScope is not the full future connection/mapping revision contract. T07/T26 still need canonical identities and snapshot/version pins. |
| [Proposal contract](../../src/Dike.Core/Models.cs) | Every proposal now carries its AccountScope, allowing repeated raw IDs in different scopes to be distinguished. | This is an API shape change; callers constructing Proposal must supply scope. No persisted proposal store exists to migrate. |
| [Movement validation](../../src/Dike.Core/MovementRules.cs) | Both matching and submission reject incomplete scope/identity/revision, unsupported sides/currency, null reference text and invalid SAR money. Matching rejects multiple revisions of one scoped side/identity. | Existence, current revision, removed status, grants and reservation ownership still require authoritative persisted checks. |
| [SAR monetary bounds](../../src/Dike.Core/SarMoney.cs) | One decimal(19,2) range/precision rule is shared by domain checks and [NetSuite money handling](../../src/Dike.Infrastructure/NetSuite/NetSuiteMoney.cs). Exact trailing zeros remain valid. | Original-digit parsing remains at the ingestion boundary; already-rounded decimal input cannot recover lost source digits. |
| Matching eligibility/calendar | Zero-value evidence remains unmatched; pending rows cannot become candidates. Business-day counting preserves the existing Sunday–Thursday, earlier-exclusive/later-inclusive rule and handles DateOnly boundaries without overflow. | Holidays remain unmodeled. Source comparison-date selection remains D06. |
| [Review policy](../../src/Dike.Core/ReviewPolicy.cs) | Valid positive/negative 1:1, 1:N and N:1 groups remain eligible. Unsupported/invalid rows cannot hide outside bank/book totals. Approval requires both preparer and approver identities. | The pure guard does not authorize a Controller or perform a financial transition. T27–T30 remain incomplete. |

The corrected matcher is **sar-v2**. Reference normalization, exact signed equality, classification priority and the three-business-day window remain as documented. New ordering, scoped competition, malformed-input rejection, zero-row eligibility and scope-bearing results are explicitly versioned. Historical sar-v1 evidence must retain its original version; no historical records were rewritten.

The six-project build passes with zero warnings/errors. The executable runner now reports **126 checks: 56 domain (11 original + 45 new), 31 connector and 39 offline GL validation**. All **10 NetSuite CLI checks** also pass. T01's historical baseline and source manifest remain unchanged.

## D03 — Proposed SQL transaction boundary

Keep stable logical source identity separate from immutable financial revisions. Snapshot membership and group membership pin both the logical row and the accepted revision. Reserve the logical row, not a revision or mapping ID; a changed mapping must not make an already reserved source row available again.

For option A, provision guard rows for the canonical company/connection/source-account resources before accepting work. A financial group may involve both bank and book resources. Acquire all affected guards in a documented deterministic order, together with T05's authority boundary. Mapping changes acquire the relevant old and new resource guards. Do not use a newly generated mapping ID as the only serialization identity.

Proposed commit sequence:

1. Begin an explicit SQL transaction and acquire the required guards. Revalidate current authority, mapping, source generations, group rowversion and, for a worker, lease owner/generation/expiry/cancellation.
2. Load the exact current revisions. Apply domain rules and distinct-approver policy. Insert or verify every reservation with a unique logical-row constraint.
3. Append immutable membership/revision/transition evidence, the request's payload-bound result and audit events. Publication also invalidates affected active groups and releases their reservations before exposing the new snapshot.
4. Change current pointers/status and commit all effects together. No provider HTTP request or remote artifact upload occurs while holding the SQL transaction; immutable uploads precede publication and are verified before they are referenced.
5. On a stale version, return a recoverable conflict. A retry of the same committed command returns its recorded result only after current authorization. A changed payload/action conflicts. Never reload newer source rows and silently retry an approval against changed evidence.

The synthetic probe uses `UPDLOCK,HOLDLOCK` on an existing guard row to test this serialization pattern. SQL Server documents the lock behavior and limits hints to their specified table; they do not automatically protect unrelated application reads. [SQL Server table hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver17). The production design must ensure every conflicting path participates, including mapping changes, exports and authorization revocation.

EF rowversion checks detect competing updates to the tracked row; they do not by themselves establish Dike's cross-entity source/approval ordering. [EF concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency). Where multiple contexts share a transaction, explicitly share both connection and transaction; a common connection string is insufficient. [EF transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).

| Ordering / failure | Required committed result |
| --- | --- |
| Approval obtains guard before changed-source publication | Approval may commit first; later publication invalidates it and releases reservations atomically with the new current snapshot. |
| Publication obtains guard first | Approval sees the newer generation and fails its expected-version/state checks. |
| Two groups claim one logical row | At most one owns the reservation; the loser has no partial group transition or audit success. |
| Worker expires/reclaims while old attempt continues | Every final write is fenced by owner/generation and database-clock expiry; old work cannot renew or publish after losing authority. |
| A transaction fails after inserting some members | Group/member/reservation/status/audit/result changes roll back together. |
| Commit succeeds but response is lost | Same-key/same-payload replay returns the prior committed result without a second transition. |
| Mapping changes | Old mapping runs cannot become current; historical evidence remains addressable and logical reservations remain unique. |

### Executed LocalDB mechanism checks

[SQL concurrency smoke script](../../tests/sql-concurrency-smoke.ps1) ran against the existing SQL Server 2019 LocalDB instance, version 15.0.4382.1. It fixes the target to local development, generates a unique `DikeSqlChecks_<guid>` database, places files under the ignored workspace artifacts directory and drops only that database in cleanup. It accepts no existing database name or production connection string.

Run without `-Run` to print its plan. Run with `-Run` to execute:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/sql-concurrency-smoke.ps1 -Run
```

The instance required local IPC without TLS for this fixture client. That choice is confined to the fixed LocalDB target; production SQL still requires an approved encrypted connection. The script may auto-start LocalDB. It does not apply the application EF schema or inspect user financial data.

**Nine checks passed:** atomic rollback; competing reservations; both approval/publication orderings; reservation uniqueness across remapping; lease expiry/reclaim; identical-command replay; changed-payload conflict; and a consistent guarded export capture. Concurrent cases verify the actual blocking session through SQL's request metadata before releasing the first transaction. The test database was removed successfully.

These prove the exercised SQL mechanisms on a synthetic schema. They do **not** establish application-service integration, immutable production history, full authorization, simultaneous idempotency retries, complete worker publication fencing, or AC23–AC29/AC36 acceptance. T37 must rerun equivalent races through the actual services and reviewed migration, with failure injection at their commit boundaries.

## D07 — Return, reopen and holds

Under recommended A, Returned/Reopened groups retain their old immutable membership and decision history but release reservations atomically. A later edit creates a new revision; resubmission reloads current evidence and reacquires all members. If another group has acquired any member, the resubmission conflicts rather than stealing it.

Under B, distinguish a review hold from active Approved coverage. Record its owner, reason, start, expiry/renewal rule and authorized release action. Returned/Reopened must no longer count as an active approval even when a hold retains rows. Expiry/release runs under the same guard and cannot silently approve or reinstate a group. No default hold duration has been selected.

For either choice, material correction/removal invalidates active Submitted/Approved groups, releases their reservations and preserves history. Invalidated evidence cannot be reinstated merely by clearing a status flag. Draft/Returned/Reopened evidence becomes stale and must be refreshed before submission.

## D08 — SAR 50 examples

All rows below are synthetic arithmetic examples, not accounting evidence or source corrections.

| Bank rows | Book rows | Arithmetic | Current-rule outcome |
| --- | --- | --- | --- |
| +5,000 | +5,050 | Bank minus book = -50 | AmountMismatch; retain a 50 difference and investigate. |
| +5,000 | +5,050 and -50 | Both sides net to +5,000 | Reject submission: mixed cash directions. A net-zero difference is insufficient. |
| +5,000 | A genuinely verified corrected source row of +5,000 | Equal signed totals | Pure grouping can pass; source-correction lineage, current versions, permissions and distinct review still must pass. Do not manufacture this row. |
| -100 | -60 and -40 | Both sides total -100 | Valid same-direction 1:N arithmetic. |

The executable domain checks cover the mismatch and mixed-direction rejection. D08 A would accept an honestly unresolved exception as this scenario's release outcome; only the finance owner can record that acceptance. No automatic fee or journal creation is proposed.

## D11 — Candidate workpaper contract

Recommended B would package separate summary, movement, group, membership, exception and audit CSV tables, plus structured JSON and a manifest. This is a proposed layout, not a delivered exporter. Finance still selects column names/order, masking and presentation. Candidate contents:

| Record set | Required evidence |
| --- | --- |
| Manifest/summary | Schema version, export ID/actor/UTC time, company/account labels and masked external identifiers, mapping revision, subsidiary/book/SAR, inclusive period/date rule, capture boundary, all artifact digests and limitations |
| Source metadata | Bank/book snapshot and run IDs, requested/actual coverage, completeness/status filters, query/schema versions, fetched times and nullable source-as-of |
| Movements | Stable logical and immutable revision IDs, source side, signed amount, book debit/credit where supplied, original/comparison dates, reference, status, in-period flag and source evidence reference |
| Groups/members | Immutable group revision, status as captured, complete member set, reason, preparer/controller/time, matcher rule version, full-group totals and per-member period flags |
| Exceptions/audit | Category, scope, ownership and immutable notes/transitions/reasons; source changes, invalidations and resolution history |

Capture source snapshots and review state from one consistent database boundary, then serialize the captured immutable data outside the guard. Reauthorize artifact delivery, including for an older export. Include the full captured dataset, not the 500-row UI preview. Quote CSV delimiters/newlines and neutralize formula-like **text** without altering signed numeric money; JSON retains original values. No raw credentials or secret-bearing configuration archives belong in this package.

### Period and full-group arithmetic

Period totals use only rows whose approved comparison dates lie inside the inclusive report window. Bank booked, book posted and pending bank populations remain distinct. `PeriodDifference = BankBookedTotal - BookPostedTotal`. Available/current/closing provider balances are separate typed observations, not substitutes for movement totals.

Candidate cross-period treatment: include complete membership for each relevant group, flag out-of-period members and show full-group balance separately from period totals. Example: a report for 4 November contains bank +100 dated 4 November and a group counterpart book +100 dated 5 November. Full-group difference is zero; report-period bank total is +100, book total is zero and period difference is +100. Do not add the 5 November book row into the 4 November total to make it appear balanced.

Count each logical movement once in active approved coverage. Show Submitted, Approved, Invalidated and unresolved populations explicitly; a proposal count is not reconciled money. Reproducibility requires pinned immutable review history as well as pinned source snapshots. These period/full-group rules remain proposed until D11 review.

## D10 — Capacity measurement worksheet

| Input / metric | Required recorded value before acceptance |
| --- | --- |
| Dataset | Bank/book rows per account/period, account count, history depth, correction/duplicate/ambiguity distributions, maximum group and export size, representative evidence hashes |
| Concurrency | Active viewers/preparers/controllers, simultaneous imports/exports, hot-account contention and web/worker instance count |
| Environment | SQL version/compatibility and isolation settings, CPU/RAM/storage/network, app build/configuration, evidence service and region |
| Provider constraints | Approved quotas, page/history caps, expected latency, permitted cadence and retry limits from T02/T04 |
| Latency | Named endpoint/command, percentile (including p95), target in milliseconds, measurement window and error/conflict treatment |
| Throughput | Complete import/export duration, row/page volume, queue wait, sustained run count and provider wait separated from local processing |
| Resources | Peak memory/CPU, database/log/storage growth, lock waits/deadlocks and evidence I/O limits |
| Recovery | RPO/RTO from D09, restart/reclaim/publication recovery duration and fault scenario |

No numeric target has been supplied. The fast completion of nine small SQL probes is not a capacity benchmark. T40 needs a reproducible agreed dataset, repeat runs and measured results before any service-level claim.

## Remaining implementation sequence

| Work | Next dependency / evidence |
| --- | --- |
| T07–T12: canonical entities, schema, publication and evidence | T02 account-specific identity proof; T05 decisions; D03 and reviewed schema; actual SQL/evidence deployment choices. LocalDB is now available for disposable checks but is not the selected production target. |
| T13–T16: identity and scoped access | T05 choices, email service and approved grant/recovery rules; then authoritative finance scope from T07. |
| T17–T25: durable jobs and provider ingestion | Reviewed finance storage/authorization/evidence services; actual NetSuite, CSV and Neotek contracts. Mechanism probes do not enable the worker. |
| T26–T30: proposals and review | Pure matching/submission/approval guards are improved. Persisted snapshot pins, group revisions, transactional reservations, D07 transitions and invalidation remain. |
| T31–T36: exceptions, screens and exports | Working scoped workflow, D11 contract and representative data for accessibility/finance checks. |
| T37–T41: integration, operations and acceptance | Extend the SQL probe through real service APIs/migrations; add fault tests, approved-host restore, D10 benchmark and named two-account finance/operations signoff. |

T06 closes only after the five decision records contain actual choices, named owners, date and supporting review. This continuation advances independent code and validation; it does not mark the remaining backlog complete.
