# Dike frontend design proposal

Prepared and approved 17 November 2026. **Status: visual direction accepted; F1/F2 implemented in Dike.Web. F3–F5 remain planned with the dependencies below.** See the [current implementation record](README.md) for scope and verification.

This proposal applies the [product specification](../../design_draft.md) to the current [project checkpoint](../delivery/CHECKPOINT.md) and [Odoo workflow](../ODOO_WEB.md). Later user decisions take precedence over the original NetSuite-first specification: Odoo 19, provider selection at registration, environment-configured Odoo endpoint, permitted company/account selection, weekly/monthly/custom periods, and English/Arabic interfaces.

The recommended product experience is a **finance reconciliation and evidence workspace**: establish the source scope, compare bank and ledger movements, investigate differences, obtain independent review, and retain reproducible workpapers. AP test bills are source fixtures; they do not create a requirement for an AP purchasing or payment-entry module in Dike.

The existing .NET 10 Razor Pages application remains the implementation target. Lovable was used for design planning in [this project](https://lovable.dev/projects/0015c01a-02bb-4822-a897-69ab66d27e59). Its refined `.lovable/plan.md` is awaiting review through `plan--show`; no approval to build was submitted. That link is a planning workspace, not a completed frontend preview.

## 1. Finance references and the resulting design

These are primary product and design-system references researched on 17 November 2026. The Dike adaptations below are recommendations based on those sources, not claims that Dike implements their capabilities. Product screenshots and marketing figures are not reused as Dike assets or evidence.

| Reference | Useful pattern | Application to Dike |
| --- | --- | --- |
| [Xero: reconcile a bank account](https://central.xero.com/0/article/Reconcile-your-bank-account) and [reconciliation details](https://central.xero.com/s/article/View-bank-reconciliation-details) | Bank statement lines and accounting transactions can be inspected together, with candidates and transaction details. | Put bank activity and posted GL beside one another; keep date/reference/amount filters and candidate explanations close to the rows. Dike actions prepare internal review; they do not create transactions, transfers or write-offs. |
| [FloQast: account reconciliations](https://www.floqast.com/close-management/products/account-reconciliations) | Account worklists connect reconciling items, ownership, support, review notes and sign-off. | Use a controller review queue and exceptions with owner/age/history. Keep review evidence beside the decision. Dike retains exact-zero validation and explicit human approval; materiality thresholds and automatic sign-off are outside this proposal. |
| [BlackLine: transaction matching](https://www.blackline.com/products/financial-close/transaction-matching/) | Transaction matching, exception visibility and audit history form the main operational workflow. | Make unresolved items and explainable matching proposals central. Preserve Dike's deterministic rules and restricted group shapes; do not introduce AI confidence scores, adjusting journals or vendor performance claims. |
| [IBM Carbon: data table](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines) | Tables combine clear toolbars, selection, expansion, density and pagination. | Implement semantic Razor tables with stable headers, explicit selection scope, row details, server paging and full-scope counts. Use the interaction principles without adding a React component dependency. |
| [SAP Fiori: dynamic side content](https://www.sap.com/design-system/fiori-design-web/v1-96/ui-elements/dynamic-side-content) and [flexible column layout](https://www.sap.com/design-system/fiori-design-web/v1-154/page-types/page-layouts/flexible-column-layout) | Supporting context can accompany a task; list/detail layouts serve inspection workflows. | Use an evidence inspector for supporting details and a list/detail review inbox. Keep essential amounts, differences and eligibility visible in the main task. The dual-ledger comparison remains its own workbench, not a literal Fiori flexible-column implementation. |

The resulting direction combines clear transaction comparison, accountable review and restrained enterprise tables. A generic revenue dashboard would require metrics outside Dike's current data and purpose. The opening screen should answer **which account needs work, what evidence exists, and what the user can do next**.

## 2. Visual direction

Retain the lowercase Dike identity and the idea of balance. Move the authenticated workspace toward a compact operational layout. Preserve the existing brand assets until a deliberate brand decision is made; the generated monograms are illustrative.

| Element | Proposed specification |
| --- | --- |
| Navigation | Flat ink navy `#142B3D`; 224px desktop rail; active item uses teal plus a visible label/current-page marker. |
| Main background | Cool neutral `#F6F8FB`; white `#FFFFFF` forms and tables. |
| Primary action | Teal `#127A71`; one dominant action per task area. |
| Brand accent | Bronze `#A57545`, used sparingly in the wordmark or a decorative rule. |
| Text | Main `#162C3B`; secondary `#526171`; muted text must remain readable. |
| Borders | `#DCE3EB`; stronger visible outlines for inputs and focused controls where needed. |
| Status | Label + icon + color. Proposed/neutral, review-needed/amber, approved/green, invalidated/error/red. Green never means a source is complete unless its contract proves completeness. |
| Typography | Proposed Inter for English UI and Noto Sans Arabic for Arabic, self-hosted if adopted; existing Segoe UI/Tahoma fallbacks remain valid. Use one coherent sans-serif family per language. |
| Type scale | Page title 28px/34px, section title 20px/28px, body 14px/22px, controls 14px/20px, helper text 12px/18px. Arabic line heights increase as needed. |
| Numbers | Tabular digits, two decimal places for SAR, explicit currency, visible signed movement. Keep source precision and validation separate from formatting. |
| Spacing and shape | 4/8/12/16/24/32px rhythm; 8px corners; 24px desktop gutters; 16px small-screen gutters. |
| Table density | 40px default rows, 48px comfortable option. Dense views must retain usable selection targets and readable financial values. |
| Icons | Reuse the local outline SVG family; 20–24px; consistent stroke and alignment. Mirror directional icons only. |
| Motion | Short focus/expansion feedback; no decorative animation. Respect reduced-motion preferences. |

No decorative financial charts are required for the first redesign. Useful later charts must answer a defined question using complete, correctly scoped data. Do not introduce portfolio value, revenue growth, cash forecasts or savings claims from the existing GL samples.

### Concept images

All three are generated, synthetic design studies, visually inspected at 1536 × 1024. They illustrate the target experience after its backend prerequisites exist.

| Concept | Main design decision |
| --- | --- |
| [Account overview](concepts-20261117/accounts-overview.png) | Period selector, account rows, separate source coverage, expandable movement summary, attention list and source history. |
| [Reconciliation workspace](concepts-20261117/reconciliation.png) | Paired ledgers, supporting explanation panel and persistent selected-total validation. |
| [Arabic review page](concepts-20261117/review-ar.png) | Mirrored shell, readable Arabic, isolated Latin references/amounts, distinct preparer and reviewer, explicit review action. |

These are accepted visual-direction references, with the following corrections and current-feature adaptations governing implementation:

- Provider and currency are read-only scope facts on the reconciliation page. Generated dropdown chevrons must not imply the user can switch their company's provider there.
- Use a single flat palette and the existing approved SVG/wordmark treatment; do not reproduce generated texture, shadows or inconsistent icon variants.
- All screen copy comes from reviewed English/Arabic resources. The Arabic reviewer timestamp should distinguish assignment/viewing from an actual decision; a submitted group has no completed approval event.
- The full-November totals are a synthetic fixture. A live current-month capture must show its capture time and that the period is still in progress. Complete enumeration at capture time does not mean the month has closed.
- Selected-group totals and period totals are separate. The sample reconciliation is `+7,550.00` bank versus `+7,600.00` book for the full fixture, while the selected pair is `+5,000.00` versus `+5,050.00`.
- The mockup's complete GL coverage is a future-state example. Today's Odoo preview remains a limited sample even when it returns fewer than the row limit.

Exact image prompts and provenance are retained in [the concept manifest](concepts-20261117/manifest.json). No customer financial data or credentials appear in these concepts.

## 3. Navigation and page architecture

The top bar shows the current Dike company, page context, language control and user menu. Company identity is fixed by the user's membership; there is no global company switcher. Choosing an authorized upstream Odoo company is a separate connector step and must not change Dike tenancy.

Target navigation:

| Area | Destinations | Visibility |
| --- | --- | --- |
| Daily work | Overview, Accounts, Reconciliation, Review, Exceptions, Workpapers | Only enabled features and authorized scopes. Viewer access is read-only; raw evidence export requires its own grant. |
| Setup and operations | Connections, Imports, Team & access, Company | Connections and sensitive administration remain Administrator-only. Import visibility and actions follow the eventual approved grant policy. |
| User menu | Account security, language, sign out | Available within current identity capabilities. Future MFA controls appear only when implemented. |

The full navigation shown in the concepts describes the target product. During staged delivery, do not add dead links or pretend that persisted review already exists. A concise overview readiness list may explain upcoming capabilities.

Retain existing URLs for implemented pages. Labels can improve without breaking bookmarks. New route names below are proposed, aligned with the product draft where practical.

## 4. Complete screen plan

“Existing” means a working route exists, not that all future business capabilities on that page are complete. “Planned” means the screen requires additional services and acceptance evidence.

| Screen / route | Proposed content and primary action | State and authority requirements |
| --- | --- | --- |
| Public home `/` — existing | Concise purpose: compare bank and accounting evidence, investigate differences and retain review reasoning. Show current provider support and available capabilities. Create workspace / Sign in. | Keep future capabilities clearly described as planned until enabled. Existing balance photograph can remain; operational screens use native tables. |
| Registration `/Auth/Register` — existing | Group company/owner details, provider choice and authority acknowledgment in one clear form. NetSuite and Odoo are mutually exclusive choices. | Preserve submitted values and accessible validation. Provider choice determines the subsequent connection flow; registration does not ask for ERP secrets. |
| Sign in `/Auth/Login` — existing | Focused form with language selection, validation and safe local return. | Preserve generic invalid-credential responses. Add recovery links only when recovery is available. |
| Verification, recovery and invitation acceptance — planned | Small focused forms with token state, next action and success confirmation. | Requires identity delivery/token services; expired, replayed and invalid states must be understandable without exposing account existence. |
| Overview `/Company` — existing, future finance view | Readiness and next actions now; later an account worklist, pending review and unresolved items based on authorized real data. | No fabricated zero balances, completion percentages or synthetic operational metrics. Account-based totals require complete scope. |
| Company profile `/Company/Setup` — existing | Company details, chosen provider and setup state. Explicit save action. | Administrator can change provider using the existing controlled flow. Retain stored settings and re-evaluate navigation; expose only the selected provider. |
| Team & access `/Company/Staff` — existing directory | Search/paged staff directory now. Later invitations, membership state, approved role/account grants and audit history. | Invitations, assignment and revocation require the unresolved identity/grant policy and services. Do not present future controls as working. |
| Account security `/Auth/ChangePassword` — existing | Current password/change form; clear confirmation. Future MFA enrollment/recovery sections when supported. | Keep current validation, antiforgery and session security. No fabricated MFA-enabled indicator. |
| Connections `/Accounts` — existing entry | One selected accounting-provider journey plus the future bank-source area. Distinguish saved, verified, reconnect required and unavailable. | Credential management remains Administrator-only. No second accounting-provider configuration form for a company that selected the other provider. |
| Odoo overview `/Accounts/Odoo` — existing | Connection → permitted company/accounts → posted GL preview, with real readiness at each step. | A successful login is not proof of full account coverage or completed reconciliation. |
| Odoo connection `/Accounts/Odoo/Connection` — existing | Read-only configured endpoint, database/login fields, write-only credential entry, Verify and save. | Endpoint comes from environment configuration. If absent/invalid/changed, show an actionable safe state; changed endpoint requires re-verification before account data can be reused. Never redisplay secrets. |
| Odoo company/accounts `/Accounts/Odoo/Accounts` — existing | Load permitted upstream companies, choose one, load permitted bank/cash accounts, inspect name/code/currency. | Do not label API-user permissions as completed Dike staff account grants. Selected accounts must belong to the loaded catalog and chosen company. Explain foreign-currency restriction. |
| Odoo GL preview `/Accounts/Odoo/Ledger` — existing | Select loaded accounts first; weekly/monthly/custom dates; row cap; Fetch posted GL. Present per-account debit/credit/signed totals and posted rows. | Preserve 1–5 accounts, at most 31 inclusive days and 1–50 rows per account. Always identify a limited sample and sample totals. Show last successful preview separately from the latest failed attempt. |
| NetSuite `/Accounts/NetSuite` — existing | Provider-specific connection, supported account discovery, scope and manual extraction in the same visual family. | Keep actual NetSuite subsidiary/book fields and credential modes. Show only for NetSuite companies; source/report acceptance still applies. |
| Mappings `/Accounts/Mappings` — planned | Bank account ↔ permitted GL mapping, currency, provider-specific scope, verification evidence and version history. Activate verified mapping. | Only valid, authorized scope can activate. Odoo uses its actual company/journal/account semantics, not invented NetSuite book identifiers. |
| Account overview `/Accounts/Overview` — planned | Period control; account table; bank and book coverage; typed balances with as-of where known; movement sums and next action. | Unknown values stay unknown. Distinguish period movement from balance and full coverage from preview. Default to assigned/authorized accounts. |
| Bank link / CSV import `/Accounts/BankLink`, `/Accounts/ImportCsv` — planned | Explain bank-source choice; connection/consent flow where supported; CSV contract, file/period/completeness, validation preview and import result. | CSV identity/date contract and Neotek provider evidence are prerequisites. Do not advertise a working bank connection or accept a file as complete merely because it parses. |
| Imports `/Jobs/Index`, run detail `/Jobs/Detail` — planned | Account/source/period, queued/running/completed/failed state, attempts, last successful publication, rows and safe retry. | A queued request is not an imported snapshot. Do not invent percentage progress when the total is unknown. Preserve prior successful evidence after failure. |
| Reconciliation `/Reconciliation/Index` — planned | Scoped dual tables, proposal/candidate explanations, row eligibility, selection totals, draft and submit actions. | Requires durable snapshots, grants, publication/version checks and persisted review services. Exact arithmetic and reservations remain server authoritative. |
| Review inbox `/Reconciliation/Inbox` — proposed extension | Submitted groups in a compact list, filters by account/preparer/age/status, selected group detail. Review group. | Counts include only authorized groups. Assignment/due dates are optional later fields, not invented current facts or policy. |
| Review detail `/Reconciliation/Review` — planned | Full group membership/revisions, signed totals/difference, reason, preparer, reviewer and immutable history. Approve / Return / Reopen when eligible. | A different person must approve; roles alone do not establish independence. Source changes/conflicts require fresh review. Return/reopen policy follows the final review contract. |
| Exceptions `/Exceptions/Index`, `/Exceptions/Detail` — planned | Category, owner, age, linked source rows/groups, notes and history. Assign, resolve or reopen as authorized. | Resolving a case does not post a correction or approve a group. A difference is not automatically classified as a fee. |
| Workpapers `/Workpapers/Index` — planned | Scope and captured revision, coverage, counts, unresolved items, CSV/JSON generation and download history. Generate workpaper. | Explicit export grant; consistent captured data; all relevant rows and history. Open exceptions are disclosed and retained, not silently dropped or a blanket reason to prohibit export. |
| Audit/evidence detail — planned contextual views | Timeline and evidence links within account/import/group/workpaper detail; raw payload access only with appropriate authorization. | Avoid an extra global “Audit” module until its user need and access policy are agreed. No credentials or raw provider errors in ordinary UI. |
| Shared denied, error, not found and conflict states — existing foundation / extend | Clear explanation, safe next action and support correlation ID where useful. | Never disclose another company's existence or data. Preserve appropriate filter context without silently replaying a financial command. |

## 5. Core workflows and interactions

### Registration and connection

1. Create the company and owner account with exactly one accounting provider selected.
2. Show the chosen provider's setup journey. Keep company identity and selected provider visible.
3. Verify the connection and load permitted source companies/accounts.
4. Choose accounts before requesting a period. Keep the fetched scope visible above results.
5. Clearly distinguish a diagnostic preview from a future complete published extract.

For Odoo, the environment variable is `Odoo__BaseUrl` (configuration key `Odoo:BaseUrl`). An authorized settings page displays the configured origin; the user does not edit the host in the browser. Missing/invalid configuration points the user toward the deployment administrator. A changed origin shows “Verify this connection again” and prevents stale catalog reuse. Credentials stay in protected server-side storage.

### Period control

Use the same control family in preview, accounts, reconciliation and workpapers, with screen-specific validated limits:

- **Weekly:** choose the starting date; include it and the following six days, matching the current tool. Do not silently force a different week start.
- **Monthly:** choose a calendar month; show the resolved first and last dates.
- **Custom dates:** From and To, both inclusive; show the resolved interval and validation next to the fields.
- Apply filters explicitly; preserve scope in navigation. Changing account/period with unsaved selections must explain whether that draft is retained or discarded.
- The existing preview's 31-day limit remains visible. Future full-extract limits follow the accepted provider/capacity contract rather than an arbitrary frontend limit.

The working calendar used for matching is separate from the user's period selector. Current matching counts Sunday–Thursday business days; the interface explains the counted distance without claiming holiday support.

### Reconciliation workbench

The page has five layers:

1. **Scope strip:** mapped account, period, provider, currency and source status. Provider/currency are read-only facts. Show source capture time and coverage separately for bank and book.
2. **Two ledgers:** Bank activity and posted GL, each with date, reference, signed SAR, status and row details. Add source identifiers and debit/credit details where useful without overwhelming the default columns.
3. **Candidate explanation:** reference comparison, counted business-day distance, amount comparison, scope and source revision. Alternative candidates are expandable; an ambiguous result never looks pre-approved.
4. **Supporting inspector:** evidence links, original reference, journal/move details, notes and history. It can collapse without hiding the selected amounts or eligibility explanation.
5. **Selection bar:** selected bank/book counts, group shape, signed totals, difference, reason and eligible next action. This bar remains visible while inspecting rows.

Selection is deliberate. “Select this page” states its scope. Do not silently select all pages or drop selected rows when paginating. Maintain a visible list of selected members; the server validates their exact revisions before saving or submitting. Use meaningful empty states instead of an empty grid with zero totals.

Proposal labels are **Proposed, Ambiguous, Amount mismatch, Requires review and Unmatched**. Group lifecycle labels are **Draft, Submitted, Approved, Returned, Reopened and Invalidated**. Keep the two kinds of status visually and semantically separate.

Submission accepts only whole, nonzero, same-direction rows forming balanced 1:1, 1:N or N:1 groups, with a nonempty trimmed reason. Tolerance is exactly zero. Partial allocations, N:N groups, mixed-sign netting, pending/unposted rows, reserved members and stale revisions are ineligible. A disabled button has a visible explanation; client validation supplements the authoritative server check.

The essential mismatch example remains explicit: bank `+5,000.00 SAR`, book `+5,050.00 SAR`, Bank minus Book `−50.00 SAR`. Label it **Amount mismatch**, disable submission, and allow investigation/exception handling. Do not infer a fee or suggest adding a negative row to evade the same-direction rule.

### Independent review

The inbox uses list/detail navigation; review detail remains a usable standalone route. Present the exact group members and source revisions before the decision. Keep the original preparation reason, reviewer notes, state changes and actor/time visible.

Approve is available only for an eligible Controller with the relevant grant who is a different person from the preparer. An Administrator has no implicit financial approval bypass. A user holding both preparer and controller roles still cannot approve their own group. The final detailed Return/Reopen permissions and reservation behavior remain policy decisions, not visual-design assumptions.

An approval conflict says what changed and offers Reload current revision. Never automatically retry approval against different evidence. Approved history remains readable after reopening or invalidation. “Approved in Dike” describes internal review and must not imply an ERP reconciliation writeback.

### Workpapers

The generation page summarizes company/account/period, source runs, coverage, captured review state, counts and unresolved cases before export. It must make incomplete evidence visible while allowing authorized evidence export where the workpaper contract permits it.

Export all captured relevant rows, not the displayed page. Period movement totals remain strictly period-filtered. Where a review group crosses a boundary, retain its complete membership with out-of-period members marked and a separate full-group total; this remains subject to the workpaper contract decision. Show capture and generation times distinctly. Recheck download authorization and retain version/digest/history metadata.

## 6. States that must be designed explicitly

| State | User-facing treatment |
| --- | --- |
| Never connected / never imported | Describe what is missing and link to the authorized setup/import action. No zero balance. |
| Complete empty period | State that the source was completely enumerated for the scope at capture time and returned no movements. A zero period movement is meaningful here. |
| Empty limited preview | “Limited sample returned 0 rows.” Do not promote it to complete evidence. |
| Filter has no results | “No rows match these filters” with Clear filters; preserve the source and period context. |
| Loading / queued | Show the requested scope and actual phase; use a count only when known. Keep prior valid results identified as previous. |
| Latest import failed | Show safe failure class/time and last successful snapshot separately. Retrying cannot erase valid evidence. |
| Partial / capped / unknown coverage | Persistent plain-language coverage statement beside totals; no completed reconciliation claim. |
| Current period in progress | Label the capture boundary and open period; future dates do not imply final completeness. |
| Source-as-of unknown | Say Unknown; fetched-at time does not substitute for it. |
| Configuration changed | Re-verification action; stale source catalog is unavailable until it is rebound safely. |
| Permission revoked | Stop the action and show safe access feedback. Do not leak previously authorized data through errors or cached downloads. |
| Row reserved / draft stale | Explain the conflict; preserve the user's reason when safe; reload exact current members before continuing. |
| Source revision changed | Mark affected groups Invalidated with linked history and a re-review path. Never silently retain an old approval as current. |
| Export with open items | Explicit unresolved-item and coverage statement in preview and artifact; export state is distinct from finance sign-off. |

Internal decision codes, SQL details, exception stack traces and connector payloads belong in engineering/operations records. Product messages explain the user consequence and available action.

## 7. English, Arabic, accessibility and responsive behavior

- Keep existing `en` / `ar` localization and culture persistence. Every new label, status, validation and help message gets matching resource keys. Do not translate stored evidence or alter source references.
- Set `lang` and `dir`, use CSS logical properties, mirror sidebar/order/directional icons, and preserve the Latin Dike wordmark. Use `bdi`/explicit LTR isolation for identifiers, email, dates and signed amounts so minus signs and decimal punctuation remain attached.
- Posting dates remain Gregorian date-only values. Display operational timestamps with the specified timezone, normally Asia/Riyadh in this deployment; provider date-basis decisions remain separate.
- Use semantic tables, row/column headers, associated form labels, native checkboxes/buttons, visible focus, error summaries and restrained live announcements. Color never carries status alone. Verify contrast for the actual token combinations before adoption.
- Tab follows native document order. Escape closes a modal/drawer and restores focus; a nonmodal evidence region does not trap keyboard focus. Existing actions remain usable without custom keyboard shortcuts.
- At approximately 1280px and above, show the expanded rail and both ledgers; show the inspector when sufficient width exists. At narrower desktop/tablet widths, move supporting evidence below or to a drawer, keeping selected totals and eligibility visible.
- On phones, collapse navigation and stack or tab the bank/book views with persistent counts and selection summary. Keep table scrolling inside its labeled region. Viewing, selecting and reviewing must remain possible without whole-page overflow; no silent loss of source context when switching tabs.
- Validate 1536×1024, 1280×800, 768px and 390×844, plus a 320px/zoom reflow check. Arabic has the same workflow coverage as English. Record screen-reader and cross-browser limits honestly.

## 8. Implementation approach in Dike.Web

Start with shared CSS and existing Razor partials rather than replacing the app. The design does not require React, Tailwind, shadcn, Supabase, a public REST API or an SPA rewrite.

| Shared element | Responsibility |
| --- | --- |
| App shell and navigation | Role/feature-aware links, fixed company context, mobile navigation, language and user controls. |
| Page heading and scope strip | Consistent page title/action hierarchy; current account/provider/period/currency. |
| Period filter | Current weekly/monthly/custom behavior and validated screen-specific bounds. |
| Source status | Coverage, source-as-of, fetched-at, latest attempt and last success as distinct facts. |
| Money display | Signed SAR, tabular digits, numeric alignment and bidi isolation; no monetary arithmetic in JavaScript. |
| Data table pattern | Semantic headers, server paging/sorting, selected-member scope, empty state and optional expansion. Avoid a generic abstraction that obscures domain semantics. |
| Review selection summary | Counts, shape, totals, eligibility reasons and action states from authoritative service results. |
| Evidence and history | Supporting detail region, exact member revisions, immutable events and safe artifact links. |
| Notices, validation and state labels | Shared appearance, translated text, correct focus/live-region behavior. |

Extend `_Layout.cshtml`, `_Icons.cshtml`, `dike.css`, `odoo.css`, `dike.js` and the existing localization resources in small, reviewable slices. Add partials/tag helpers only when repeated usage warrants them. Keep native JavaScript for progressive interaction; choose no arbitrary new bundle budget or dependency in this proposal.

The existing `/Accounts` authorization boundary is Administrator-only. Future finance read/review pages require action-specific policies backed by actual grants. A CSS/navigation change cannot grant accountants access or weaken credential settings authorization.

Keep mutations as antiforgery-protected POST handlers and authorized reads as GET. Preserve company checks, endpoint binding, secret redaction and version/concurrency checks. New screens consume application service outcomes; they do not manufacture completion from the shape of a returned row list.

## 9. Delivery sequence and backend dependencies

This is an ordered frontend work plan, not a replacement for the existing 42-task delivery board or a new engineering-day commitment. Unresolved backend decisions remain in their original work packages.

| Slice | Concrete work | Prerequisites / completion evidence |
| --- | --- | --- |
| F0 — Design baseline | Review the visual direction, navigation, bilingual terminology and three concepts; settle generated-copy corrections; extract final tokens and component states. | Confirmed design reference and screen/state inventory; current versus future behavior recorded. |
| F1 — Shared shell and current forms | Apply tokens, typography, navigation, notices and form/table patterns to public home, registration, login, company profile, directory and security. | Existing functionality preserved; provider choice and direct-route protection verified; desktop/mobile/RTL review. No finance services needed for this visual slice. |
| F2 — Current connector workflow | Restyle Odoo overview/connection/company-account selector/GL preview and NetSuite connection using the same components. | Permitted account selection, inclusive dates, current limits, sample-only labels, missing/changed endpoint, no-secret rendering and failure preservation all verified. |
| F3 — Complete source and account views | Add actual import history, full-extract progress/coverage, mapping and account overview when their services are ready. | T42 Odoo full extraction/evidence and relevant T02/T03 contracts; T07–T12 finance foundation and job/grant dependencies. Do not make a bounded preview look like this milestone. |
| F4 — Reconciliation and independent review | Implement dual tables, candidate explanations, drafts, submit/review, exceptions and source-change states. | Persisted authorized snapshots/proposals, transactional review/reservations, concurrency and invalidation services, including T30. Finance-approved policy and account grants. |
| F5 — Workpapers and operational completion | Consistent capture, CSV/JSON export/history, source/run/audit details, complete keyboard/RTL/mobile pass and operational states. | T34–T36, workpaper contract, export authorization, reproducibility and SQL integration evidence; later provider/release gates remain in force. |

**Recommended next design implementation:** F1, followed by F2, using the actual current routes. Continue T42 full extraction and the reviewed SQL finance foundation alongside that work. The visual refresh can begin before SQL integration; live reconciliation, approval and workpaper claims cannot.

Do not migrate the populated Identity store merely to begin a frontend redesign. The SQL Server topology, source identities, migrations, restore path and grants remain explicit backend decisions.

## 10. Acceptance and handoff

Future implementation verification should cover the following meaningful scenarios. These are proposed checks, not passes recorded by this design task.

| Scenario | Expected result / link to existing acceptance |
| --- | --- |
| Register one Odoo company and one NetSuite company | Only each selected provider appears; direct access to the other provider is rejected. Company isolation remains intact (AC05). |
| Load permitted accounts, then request weekly/monthly/custom samples | Only selected valid accounts and inclusive dates are requested; current preview limits and sample status remain visible (T42 OD-01–OD-08). |
| Endpoint missing or changed | Safe guidance and required re-verification; no stale catalog/secret reuse disguised as a verified connection. |
| Unknown, empty, sampled, failed and complete source states | Distinct labels and correct totals; balance is never substituted for movement (AC16–AC18). |
| Amount mismatch and invalid group shapes | `5,000` versus `5,050` stays a mismatch; no fee inference or mixed-sign workaround; submit remains ineligible (AC21, AC33). |
| Preparer tries to approve; member becomes reserved; source changes during review | Server denies invalid action; clear conflict/reload state; immutable history and source invalidation retained (AC21–AC26). |
| Export with unresolved items, paging and boundary-crossing members | Artifact contains full captured evidence and explicit limitations; period/full-group totals stay separate; revoked download denied (AC35–AC36, AC41). |
| English/Arabic desktop, mobile and keyboard walkthrough | Accurate signed numbers and references, readable forms/tables, focus recovery, no page overflow, translated validation. |
| Visual comparison | Compare the accepted concept and browser screenshots at matching dimensions; document intentional adaptations and fix inconsistent typography, spacing, icons and colors. |

Open engineering decisions remain D01–D11 in the product/delivery documents, notably authority/grants, finance persistence, CSV identity/date basis, review transitions, fee treatment, retention/capacity and workpaper semantics. This proposal does not mark them resolved, expose their codes as product navigation, or alter task-board completion counts.

The original research task produced this proposal and three inspected concept images. Following the user's approval, the F1/F2 implementation and actual build/browser evidence were recorded in [the design implementation record](README.md). Full source publication, reconciliation, review and workpaper acceptance remain open; visual implementation does not close those gates.
