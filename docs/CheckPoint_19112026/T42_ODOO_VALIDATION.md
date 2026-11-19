# T42 — Odoo connection, journal-line identity and GL report validation

Created: **17 November 2026**. Status: **In progress — Dike.Web preview pages, company-scoped encrypted connection, local secrets and read-only JSON-2 sample client implemented and exercised live; full extraction, comparison and finance acceptance remain pending.** This is the user-requested Odoo equivalent of [T02](T02_NETSUITE_VALIDATION.md). It runs alongside T02, depends on T01, and does not wait for T41. Existing NetSuite work remains on the board.

Owner: integration engineer, with a finance reviewer and technical/security review of the connection. Named people remain unassigned. Priority: **P0 source validation**. Provisional effort: **4–6 focused engineer-days plus access/reviewer waits**, including a minimal read-only validation client because no Odoo adapter exists in this workspace.

## Objective and boundary

Establish an authenticated, company-scoped Odoo connection; collect every posted journal item for one selected SAR bank GL account and period; prove the source identity and accounting semantics; and reconcile the complete extract with an independently generated Odoo General Ledger report.

The deliverable is a validated source contract, a reusable read-only client/validation harness, fixture checks, comparison evidence and finance signoff. The user subsequently requested the validation workflow in Dike.Web; those basic preview screens are now implemented. Production deployment controls, durable scheduling/publication, migrations and reconciliation/workpaper integration require the downstream work below. Odoo is not selected as a replacement for NetSuite.

## Required inputs and owner record

| Input | Evidence needed | Current state |
| --- | --- | --- |
| Odoo deployment | Trusted HTTPS base URL, actual version/build, Community/Enterprise edition, Online/Odoo.sh/self-hosted deployment, database identity and relevant accounting/custom modules | User selected **`https://napp.odoo.com/`** as the replacement account and confirmed **Accounting is installed** on 17 November 2026. Connection base URL: `https://napp.odoo.com`. Odoo Online inferred from hostname; Odoo 19 remains the planning version from the earlier conversation. Exact build/edition, database identity and installed accounting schema/custom modules remain unverified. |
| API availability | Supported API mode, applicable hosting/subscription entitlement and model/method access | JSON-2 bearer authentication and required company/account/sample-GL reads succeeded at `https://napp.odoo.com/json/2` on 17 November. Actual build/edition, subscription metadata, trial expiry and stable database identity remain unverified. |
| Service identity | Dedicated integration user, permitted companies and least-privilege read access; approved secret reference and rotation owner | Local User Secrets key was present and used without disclosure. Observed API user ID **2** can access company **1**. Dedicated-service status, least privilege, negative live permission tests and rotation owner remain unverified. |
| Dike scope | Trusted Dike company and stable connection ID; explicit binding to the Odoo database/environment and selected Odoo company/branch | Development key explicitly bound to **T-sab**, Dike company `e2093771-dc16-4a9d-9653-fe0aa3d14792`. Web storage/authorization is company-scoped. Stable upstream database identity and the production connection contract remain open. |
| Accounting scope | Company/base currency, selected bank GL account, bank journal relationship, applicable journal/ledger/report filters and account eligibility | Live samples: company **1 / NApp**, currency **151 / SAR**, bank/cash accounts **118** and **119**. Previously authorized synthetic payments use general journal **11**. Local Dike binding is implemented; full reporting/journal semantics remain unapproved. |
| Period | Inclusive start/end, accounting-date rule, timezone where timestamps are used, report filters and stable capture window | User selected November 2026, then requested October/November test transactions. Monthly reads cover 1–31 October and 1–30 November 2026. Weekly and custom inclusive ranges are implemented; source-stability/full-report evidence remains pending. |
| Independent report | Original full Odoo GL detail export with journal-item IDs or independently proven line linkage, dates/references, debits/credits, row count and totals; protected original and digest | Not supplied |
| Reviewers | Integration engineer, finance owner of the report and technical/security reviewer | Unassigned |

API reachability alone does not prove that all eligible accounting rows are visible. Record rules, field access and company selection must be checked against the independent report.

The selected target is `https://napp.odoo.com/`, with Accounting installed. Odoo 19 and the Custom trial remain user-provided setup context; exact build/entitlement metadata still needs recording. Live JSON-2 authentication and account/journal-item field inspection now work. Keep the API key in local secret configuration, not in chat or tracked files such as `appsettings.json`. Next establish stable database/Dike identity, complete extraction/evidence and an independent GL report. Record trial expiry and named reviewers before finance acceptance.

## Implemented local development configuration

The [local setup guide](../ODOO_LOCAL_SETUP.md) and [masked setup script](../../tools/configure-odoo-local.ps1) store `Odoo:BaseUrl` and `Odoo:ApiKey` in the current user's .NET User Secrets. The web project's `UserSecretsId` enables automatic Development loading. The script sends JSON through stdin, preserves unrelated secrets and provides a `-Check` mode that reports presence only. This development store is outside the project but is not encrypted at rest; production secret services remain a separate decision.

At the earlier setup checkpoint: clean isolated six-project build, zero warnings/errors; **14 synthetic local-configuration checks passed**, covering presence checks, save, rotation, unrelated-secret preservation, invalid input, target mismatch and sanitized corrupt-store errors. The generated web assembly contains the expected UserSecretsId. No live request had been made at that stage. Subsequent implementation and live evidence follow below; no full OD acceptance case is complete.

## Implemented read-only sample and live synthetic data

Endpoint configuration follow-up: `Odoo__BaseUrl` now controls the shared client, Dike.Web and CLI through `Odoo:BaseUrl`, with no compiled hostname fallback. Saved web state is bound to the verified HTTPS endpoint; older unbound state or a changed address requires explicit re-verification. This endpoint binding does not prove stable upstream database identity. Latest isolated build/configuration/browser evidence and the continuing executable-test block are recorded in [the checkpoint](CHECKPOINT.md).

The primary workflow is now [four Dike.Web pages](../ODOO_WEB.md): overview, connection settings, companies/accounts and posted GL preview. The user's request for web pages supersedes the earlier CLI placement. The shared client retains its original read-only boundary. Company-specific Data Protection storage, current database Administrator/security-stamp/lockout checks, explicit local-key binding, stale-selection protection, atomic multi-account preview saves, CSRF and secret-safe responses are implemented. English/Arabic navigation and responsive forms/tables are included.

Web conversion verification: clean final seven-project build, **49 HTTP smoke checks and 44 Odoo browser checks passed**, including the NApp connection and monthly/weekly/custom live samples. The prior 221 executable passes below are historical: Windows now blocks the rebuilt runner with `0x800700E1`, including outside the sandbox. **24 new service fixture cases compile but have not run**. No security settings were changed. This conversion made no new Odoo postings. See [verification details and remaining limits](../ODOO_WEB.md#verification-and-limits).

The [local validation tool](../../tools/Dike.OdooValidation/README.md) authenticates the API user, reads assigned companies, inspects account-company schema and loads permitted accounts. Interactive mode requires selection from loaded company/account IDs before fetching. It supports 1–5 bank/cash accounts, weekly/calendar-month/custom dates, at most 31 inclusive days and 1–50 rows per account. It rechecks access, SAR currency, posted state, scope, dates and exact money. Catalog pages and sample truncation are explicit; redirects and arbitrary RPC methods are disabled.

Verification: **seven-project build with zero warnings/errors and 221 executable checks, including 63 Odoo fixtures**. CLI help, malformed input rejection and the shared UserSecretsId were also checked. Live authentication, company listing and account discovery succeeded. The initially empty November samples led to an explicit user request to create and post synthetic AP data, followed by explicit approval of manual payment entries.

The separate [seed script](../../tools/seed-odoo-ap-test-data.ps1) posted six bills totaling **SAR 5,753.00** and four manual payment entries totaling **SAR 2,201.50**. Paid/partial/unpaid states and **SAR 3,551.50** remaining AP were verified. Four October/November bank samples then returned the expected rows. See the [record IDs, scenarios and evidence](ODOO_AP_TEST_DATA_20261117.md). The read-only client itself has no write methods. These controlled fixtures do not replace independent source-report comparison or finance acceptance.

## API selection and documentation evidence

Odoo's 19.0 documentation describes JSON-2 at `/json/2/<model>/<method>`, bearer API-key authentication, deployment-dependent database routing and database-specific models/methods on `/doc`. It also notes external-API restrictions for its hosted pricing plans. JSON-2 authentication and the required sample schema/read permissions are now verified live; exact build, entitlement metadata, stable database identity and broader permission/completeness evidence remain open. [Official JSON-2 documentation](https://www.odoo.com/documentation/19.0/developer/reference/external_api.html).

For an older deployment, inspect its version-specific RPC support and authentication. The official RPC guide describes model introspection with `fields_get` and combined `search_read`; it warns that separate searches and counts can observe different data. Do not assume one API or response shape works across versions. [Official RPC documentation](https://www.odoo.com/documentation/19.0/developer/reference/external_rpc_api.html).

JSON-2 calls use separate database transactions; a sequence of page requests is not a shared snapshot. Record a stable capture-window or verified consistency mechanism. An upper ID bound cannot by itself freeze edits to existing rows. [Official transaction guidance](https://www.odoo.com/documentation/19.0/developer/reference/external_api.html#transaction).

These official documentation references were consulted on 17 November 2026. Subsequent live requests verified JSON-2 and inspected the selected account/journal-item schema. Other deployment/API generations remain unimplemented.

## Planned deliverables

| Deliverable | Required result |
| --- | --- |
| Connection contract/client | Versioned transport, database/environment identity and secret reference; authenticated read-only operations; trusted destination, TLS/redirect controls, timeout/cancellation and sanitized errors. |
| Scope and schema record | Actual model/field metadata, record-rule limitations, permitted company/account/journal bindings, currency and posted-state semantics. |
| Versioned extraction contract | Model/method, exact selected fields/domain/context/order/page strategy, identity components, monetary/date rules and digest; candidate until validated live. |
| Complete extract/evidence | Every page retained in protected validation storage with run ID, scope, capture times, actual coverage, counts and hashes; failed collection cannot be labeled complete. |
| Offline comparison | Odoo-aware comparison of all line identities, dates/references, debit/credit/signed amounts and totals with exact input hashes; synthetic agreement leaves live acceptance pending. |
| Fixture and live review record | Executed negative/replay/precision/page cases plus accepted actual report comparison, known limitations and named signoff. |

The sample client/contracts now exist in `src/Dike.Infrastructure/Odoo/`, with `tests/Dike.Tests/OdooChecks.cs` and `tools/Dike.OdooValidation/`. A full all-page reader, protected evidence workflow and Odoo comparison envelope are still planned.

The current [NetSuite comparison tool](../../tools/Dike.NetSuiteValidation/README.md) requires NetSuite-specific identity, book/subsidiary fields and query provenance. Extract shared comparison logic or introduce a separate Odoo envelope while preserving existing NetSuite checks. Do not force Odoo data through that tool using fabricated NetSuite fields or candidate query versions. Reuse the established SAR precision rules; no silent rounding is allowed.

## Candidate accounting contract to verify

The following model/field names are inspection candidates, not an approved customer schema. Inspect the installed version and extensions before coding assumptions into the reader.

| Concept | Candidate source / required verification |
| --- | --- |
| Company | `res.company` and journal-line `company_id`; verify branch/company membership, base SAR currency, service-user visibility and explicit company context. A context value does not replace Dike authorization or Odoo record rules. |
| GL account and bank journal | `account.account` and `account.journal`; verify the selected journal's actual bank GL relation and account eligibility. Do not assume account-company fields have the same shape across versions. |
| Posted accounting evidence | `account.move.line` joined to its `account.move`; confirm posted parent state, selected company/account, exact accounting date and report filters. Exclude draft/cancelled items from posted movement totals. |
| Logical line identity | Candidate: Dike company + stable connection/database binding + Odoo source company + journal-item `id`, with provider namespace Odoo. Retain `move_id` as parent evidence; journal-entry ID alone cannot identify multiple lines. |
| Signed movement | Candidate `debit - credit`, checked against `balance` if supplied and confirmed to have the same currency/meaning. Validate original numeric precision/range before conversion. |
| Currency | Confirm company-currency debit/credit and any account restrictions. Preserve `currency_id`/`amount_currency` as source metadata where present; do not substitute foreign-currency amounts for SAR GL movements or add implicit conversion. |
| Date and references | Candidate accounting `date`, journal-item label, parent entry number/reference and applicable transaction references. Record exact comparison fields; do not substitute invoice date or display timezone. |
| Revisions | Preserve material normalized facts and available source modification metadata such as `write_date`. A timestamp alone is not an immutable revision identity or a multi-page consistency guarantee. |
| Corrections/reversals | Verify actual posting/reset/cancellation/reversal behavior and stable-ID changes. Preserve separate reversal postings; never deduplicate by amount/date/reference. |
| Ledger context | Define the actual Odoo reporting context explicitly. Odoo company/journal IDs must not be presented as a verified NetSuite subsidiary/primary-book equivalent. If no book dimension applies, model that explicitly in the reviewed provider contract. |

Credential rotation must preserve canonical source identity. Changing databases, restoring/cloning an environment or pointing a connection at a different source requires explicit lineage review; reused numeric IDs must not merge unrelated financial rows. Remapping a bank account must not create a new identity for an already observed source row.

## Implementation and verification sequence

| Step | Work | Effort |
| --- | --- | --- |
| T42.1 | Confirm version/hosting/API access, service identity, company/account/report scope; inspect schema and record a candidate extraction/identity contract. | 1 day |
| T42.2 | Implement the minimal read-only connection/reader and protected validation output, explicit fields/filters/company context, deterministic full paging and bounded failure behavior. | 1–2 days |
| T42.3 | Implement the Odoo-aware offline comparison and negative fixtures; verify exact money, duplicate-looking rows, replay, scope errors and partial-page rejection. | 1–2 days |
| T42.4 | Run authorized live extraction and independent GL comparison, repeat under the agreed consistency window, resolve differences and record finance acceptance. | 1 day plus external wait |

1. Record the actual destination, database/environment identity and API choice. Use approved secret configuration and a dedicated read identity. Allow only required read/introspection methods; do not expose a browser-supplied arbitrary RPC/model/method bridge. Protect against untrusted destinations/redirects and credentials appearing in logs, URLs or error output.
2. Verify user/company/account access and bank-GL eligibility. Check selected company/base currency, posted-state filters and report scope with finance. Explicitly test wrong-company/account and missing permissions. An empty response without scope/completeness evidence is not a successful zero-activity statement.
3. Capture actual schema and contract versions. Verify line IDs across multiple lines in one journal entry, journals, transfers, reversals and genuine equal-looking rows. Preserve foreign-key IDs as identifiers; never key records by their translated display names.
4. Collect all pages with a stable unique ordering and validated cursor/offset progression. Capture the consistency window and actual filter boundaries. Fail on duplicate IDs, repeated/non-advancing pages, missing required fields, scope drift, cancellation, configured limits or a failed middle page. No fixed provider page cap or cadence is assumed.
5. Preserve raw evidence and exact money digits; validate SAR range/precision and debit-minus-credit before building normalized rows. Retain source timestamps separately from local capture time, including unknown source-as-of.
6. Obtain a separately generated full GL report with identical company/account/posted/date/journal filters. Preserve its original file and transformation digest. Separate opening balance, period movements and closing balance; compare period debit/credit totals and every detail line. The application preview or a re-export of its own extract is not an independent reference.
7. Run offline comparison over the complete files. Missing line IDs require an independently verified linkage; totals alone cannot establish line agreement. An equal net total with different debit/credit or line facts must fail. Report issues without exposing credentials or unnecessary financial text in routine logs.
8. Repeat the extraction in the agreed stable window and compare identities/facts. Detect material changes or unresolved consistency and repeat/reject the run; do not claim a shared source snapshot merely because page counts agree. Unchanged replay must preserve logical rows.
9. Record the verified contract/version/digest, scope/period, report and extract hashes, row/debit/credit/signed totals, negative cases, limitations and reviewer/date. Mark T42 Done only after the implemented client, offline checks and actual finance gate all meet their criteria.

## Acceptance cases

These cases have **partial fixture and small live-sample evidence; their full exit criteria remain open**. They extend the backlog as OD-01–OD-08; the original AC07–AC09 remain NetSuite-specific and are not automatically satisfied by an Odoo result.

| Case | Required result | Evidence / reviewer |
| --- | --- | --- |
| OD-01 | Selected version/API/database authenticates with permitted read access; wrong/expired credentials, foreign destination and redirect fail without secret leakage. | Fixture + live connection; integration/security |
| OD-02 | Dike-company/Odoo-company/account bindings, SAR currency and posted-state filters are enforced; foreign company/account, draft/cancelled or unsupported scope cannot appear in accepted output. | Fixture + actual metadata/negative scope evidence; integration/finance |
| OD-03 | Journal-item IDs are unique in verified source scope; multiple lines per entry and equal-looking transactions survive; unchanged replay and database/environment changes cannot cause identity collisions. | Fixture + representative live replay; integration |
| OD-04 | Complete deterministic paging succeeds; missing/duplicate/non-advancing/failed pages, caps and mid-read changes cannot produce a falsely complete accepted extract. | Fixture/fault + recorded source-consistency evidence; engineer |
| OD-05 | Debit, credit and signed company-currency amounts retain exact valid precision/range; dates/references match the approved contract; foreign amount currency is not substituted. | Fixture + finance examples; engineer/finance |
| OD-06 | All independent report identities and row facts, counts, debit/credit/signed period totals agree; opening/closing balances are distinguished; input hashes and transformation lineage retained. | Offline comparison + real GL report; finance |
| OD-07 | Every complete run retains scoped page/contract/digest/time evidence; partial collection fails visibly and preserves prior accepted validation evidence; retrieval cannot disclose another company's data or credentials. | Fixture + validation-store inspection; engineer/security |
| OD-08 | Verified connection/contract, repeat extraction, limitations and signed worksheet are complete; fixture success is clearly separate from live acceptance. | Named integration and finance signoff |

## Live review worksheet

| Record | Value / evidence |
| --- | --- |
| Odoo version/edition/hosting / API transport / entitlement | User context: Odoo 19 / Custom trial. JSON-2 sample access verified live; exact build/edition, subscription metadata and expiry pending. |
| Trusted endpoint / stable database/environment binding | JSON-2 works at `https://napp.odoo.com/` without a database header; stable database/environment identity pending verification |
| Dike company/connection / Odoo company / bank GL / journal context | Dike company T-sab (`e2093771-dc16-4a9d-9653-fe0aa3d14792`) is explicitly bound to the development connection; saved web state is company-scoped. Odoo company 1 / NApp, bank accounts 118 and 119, synthetic payment journal 11. Stable upstream database/environment identity remains pending. |
| Currency / posted/date/report filters / inclusive period | SAR; posted state; explicit company/account and inclusive October/November 2026 sample filters; independent report scope pending |
| Source schema / query and identity versions / contract digest | Pending |
| Permission/record-rule evidence / negative scope results | Pending |
| Full original report / reviewed transformation / SHA-256 | Pending |
| Extract run IDs / full-page evidence / capture window / SHA-256 | Pending |
| Report and extract row counts / debit / credit / signed totals | Pending |
| Differences / replay / correction and consistency limitations | Pending |
| OD-01–OD-08 results | 63 fixtures and small live synthetic samples provide partial evidence; full gates remain open |
| Integration engineer / finance reviewer / review date | Unassigned / unassigned / pending |
| T42 exit | Incomplete — implementation, actual source inputs and acceptance required |

## Dependencies and downstream integration

T42 starts after T01 and can proceed independently of NetSuite validation. Access, report and reviewer availability govern live steps. Existing D01–D11 choices remain open; creating this task does not select them.

- **T07/T08, if Odoo is included:** incorporate the verified provider namespace, stable database/company/journal-item identity and actual ledger context. T42 is a conditional prerequisite to finalizing Odoo-specific schema, not a new blocker for the existing NetSuite/CSV path.
- **T12/T16:** use shared protected evidence and current scoped authorization for production connection screens, imports and downloads. A validation harness is not full production access enforcement.
- **T17–T19 and a scoped adapter-integration follow-up:** route Odoo collection through durable jobs, all-page evidence and fenced publication. NetSuite-specific T20/T21 do not implement Odoo by implication.
- **T26/T30/T32–T35:** consume verified normalized Odoo snapshots, invalidate changed evidence and retain provider-specific provenance in UI/workpapers. A future production integration needs an explicit estimate and tests at these seams.
- **T37/T41:** add actual Odoo service race/fault and end-to-end finance evidence if Odoo is selected for the release. Odoo validation does not waive required NetSuite or Neotek gates without an explicit release-scope decision.

The 4–6-day estimate covers T42's connection/validation slice. It excludes a custom Odoo server module, supporting several API generations, production UI/worker integration and live-access waiting time. Re-estimate after the actual deployment/contract is known.

## Preparation record

Reviewed T02, the current delivery records and the existing NetSuite-specific comparison shape; searched the workspace and found no Odoo implementation. Added this task definition and its planning/acceptance links. Documentation checks passed: 15 Markdown files, 202 local links/anchors, 42 unique task rows, 41 remaining-task plans, 44 original plus eight Odoo acceptance cases, matching 86–133-day totals and no dependency cycles. No Odoo connection, API call, implemented-client test or finance approval is claimed.
