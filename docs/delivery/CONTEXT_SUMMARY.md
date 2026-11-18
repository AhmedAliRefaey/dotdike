# Compact project context

Updated: **17 November 2026**, after applying the approved finance frontend design. Workspace: `C:/Dike`; PowerShell; .NET 10. This handoff condenses the conversation and current Markdown status. Latest build/HTTP/browser verification is recorded below; no new live extraction or posting was performed for this change.

## Read first

- [Checkpoint](CHECKPOINT.md): latest completed implementation and verification.
- [Task board](TASKS.md) and [remaining work plan](REMAINING_WORK_PLAN.md): dependencies and delivery scope.
- [T42 Odoo validation](T42_ODOO_VALIDATION.md) and [acceptance tracker](ACCEPTANCE_TRACKER.md): the next Odoo deliverable and its exit criteria.

Treat dated baseline/design records as historical where the latest checkpoint records a later implementation. Older NetSuite-first implementation lists do not remove the subsequently added Odoo work or waive other provider gates.

## User decisions to retain

- Use **Odoo 19**, user-reported **Custom trial**, Accounting installed, at **https://napp.odoo.com/**. This supersedes the earlier `tsab.odoo.com` target.
- The Odoo endpoint must come from environment configuration, not a compiled constant. Use `Odoo__BaseUrl` (`Odoo:BaseUrl`); the deployed local process currently uses NApp.
- Put the workflow in **Dike.Web**. The earlier local CLI remains an optional validation tool.
- Let users load permitted companies/accounts, select accounts before fetching, and use weekly, calendar-month or custom inclusive dates.
- Company registration requires **NetSuite or Odoo**. Show only the chosen provider throughout that company's connection workflow.
- The user explicitly approved the recorded synthetic AP bills and manual payment entries. That batch is already posted; do not rerun it as a setup step.
- The user approved the [finance frontend design](../design/FRONTEND_REDESIGN_PROPOSAL.md). F1/F2 are applied to current Razor pages; future source/review/workpaper pages remain dependent on their backend services.

## Current implementation

| Area | Current state |
| --- | --- |
| Local application | Last verified at `http://localhost:5080`, using `artifacts/finance-design-final-20261117/`, PID 36104. Check runtime state before assuming it is still running. |
| Frontend | Approved navy/teal finance design applied to existing public, identity, company/staff and connector pages; real saved-account overview, explicit sample coverage, signed sample totals, responsive EN/AR and 434 matching resource keys. |
| Provider selection | Persisted on the company; selected-provider navigation/content; other provider's direct GET/POST requests blocked before handlers. Administrators can change the choice while retaining saved connection settings. |
| Odoo pages | Overview, Connection, Companies/Accounts and posted GL preview at `/Accounts/Odoo`. English/Arabic, desktop/mobile and current database authorization are implemented. |
| Endpoint configuration | Shared client, web labels and CLI use `Odoo:BaseUrl`; `Odoo__BaseUrl` overrides User Secrets. No compiled host fallback. Saved state is bound to its verified endpoint; older unbound state or an endpoint change requires re-verification. |
| Odoo scope | Live API user 2, Odoo company 1 / NApp, SAR; accounts 118 and 119 are the selected test bank accounts. |
| Current preview limits | 1–5 loaded bank/cash accounts, at most 31 inclusive days, 1–50 rows per account. `SampleOnly` remains true even when no further row is detected. |
| Local company binding | T-sab uses Odoo and may import its explicitly bound development connection. TestOddo retains the older NetSuite default; its administrator can select Odoo in Company profile. |
| Protection | Company-specific encrypted saved connection/state, secret redaction, CSRF protection, no-store responses and fresh Administrator/security-stamp/lockout checks. Development User Secrets are not encrypted at rest. |
| Finance backend | Complete durable snapshots, production finance migrations, jobs, bank ingestion and persisted reconciliation/review/workpapers remain unfinished. The worker is disabled. |

No Git metadata is present. Preserve source/build fingerprints and existing populated Identity/evidence stores when implementing later changes. The provider-choice upgrade backup and verification are recorded in the checkpoint.

## Verification: current versus historical

- Latest frontend slice: clean seven-project build, **62 HTTP/security checks and 173 browser checks passed** (160 main + 13 final-build). English/Arabic at widths 1536/1280/768/390/320; accepted concept and final captures visually inspected. Synthetic isolated users/encrypted previews only; no upstream requests. Live health/home/registration verified. QA server stopped; evidence at `%TEMP%/dike-finance-design-20261117`.

- Latest endpoint configuration slice: clean seven-project build; **21 configuration checks, 24 browser checks and one CLI invalid-endpoint check passed**. Test users/stores were isolated; no upstream Odoo requests. Real app health and registration returned HTTP 200 after restart.
- Earlier provider-choice slice: **62 HTTP smoke checks, 38 browser checks and 6 isolated database-upgrade checks passed**.
- Windows blocks the rebuilt executable regression runner with **`0x800700E1`**. Resolve the local security finding through the normal review process and rerun the suite; do not bypass protection or add exclusions.
- The earlier **221 executable checks**, including 63 Odoo fixtures, are historical passes. The expanded endpoint fixtures and **29 Odoo web service cases compile but have not executed successfully**. Do not report them as passing.
- Earlier live monthly/weekly/custom samples and browser checks remain scoped evidence, not proof of a complete GL extract or finance acceptance.

## Existing Odoo test data

Six October/November 2026 bills total **SAR 5,753.00**. Four posted manual payment entries total **SAR 2,201.50**, leaving **SAR 3,551.50** AP outstanding across paid, partially paid and unpaid examples. These are accounting test entries; no external bank transfer was performed.

The four recorded bank samples are account 118: October credit 451.50 and November credit 900.00; account 119: October credit 450.00 and November credit 400.00. These are historical fixture observations, not current exhaustive totals. Exact IDs and evidence are in [the posting record](ODOO_AP_TEST_DATA_20261117.md).

## Recommended next task: finish T42 extraction and comparison

1. **Establish the source contract.** Verify stable upstream database/environment identity, actual schema, permitted read scope and journal-item identity. The Dike company binding already exists; upstream identity and completeness still need proof.
2. **Implement complete posted GL extraction.** Add deterministic paging behind the existing web workflow. Validate scope, exact SAR amounts and progress on every page. Reject duplicate/non-advancing/failed pages, exceeded bounds and unresolved source changes; never label a truncated or inconsistent run complete.
3. **Retain protected run evidence.** Preserve run ID, contract version/digest, company/account/date scope, original pages, capture times, row counts and hashes. A failed run must preserve prior valid evidence. Keep credentials out of evidence and routine logs.
4. **Add Odoo-aware offline comparison and fault/replay tests.** Compare every journal-item identity and row fact, plus debit, credit and signed movement totals. Keep opening/closing balances separate. Do not fabricate NetSuite book/subsidiary fields to reuse its envelope.
5. **Validate against an independent Odoo GL export.** Start with NApp, accounts 118 and 119, October and November 2026 as separate monthly scopes. Match company, posted-state, dates and journal filters; retain the original report and transformation lineage. Repeat reads in an agreed stable window and obtain a named finance review.

The first implementation slice is steps 1–3 with fixture coverage. Resolve the executable-test block alongside this work. The independent report and named reviewer are required for final acceptance; their absence does not prevent offline engineering preparation.

**T42 exit:** complete extraction and fault/replay evidence, independent row-level and total agreement, recorded limitations and OD-01–OD-08 acceptance. A bounded preview, matching totals alone or synthetic data alone cannot close it.

## What follows and what remains open

- Finalize Odoo-specific T07 identities/schema only after its source contract is verified; T08 migrations, protected evidence, scoped publication and durable jobs follow the recorded dependencies. T42 does not block independent NetSuite/CSV preparation.
- T02 still needs NetSuite scope/catalog/reference-report evidence. T03 needs representative bank CSVs and identity/date choices. T04 needs the actual Neotek subscription/version, bank access and correction/paging evidence.
- T05/T06 still need named owners and D01–D11 decisions covering production identity, grants, hosting/storage, concurrency, review, retention, workpapers and capacity. Do not infer these choices from a request to continue.
- The board remains **42 tasks: 1 done, 10 in progress, 31 pending**. Its **86–133 engineer-day** figure is the retained planning estimate, not a new forecast. No full finance release acceptance is recorded.

## Resume prompt

> Read docs/delivery/CONTEXT_SUMMARY.md, CHECKPOINT.md and T42_ODOO_VALIDATION.md. Continue T42 with the complete read-only Odoo GL reader and protected run evidence in Dike.Web, preserving provider selection, company isolation and existing data. Keep independent-report/finance acceptance explicit, and do not rerun the already posted seed batch. Recheck the documented executable-test block before claiming regression passes.
