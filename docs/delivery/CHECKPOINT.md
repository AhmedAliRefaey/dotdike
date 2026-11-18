# Delivery resume checkpoint

For the concise conversation handoff and recommended next task, read [compact project context](CONTEXT_SUMMARY.md), reviewed 17 November 2026.

Updated after applying the approved finance frontend design: **17 November 2026**. Workspace: `C:/Dike`, PowerShell, .NET 10. No Git repository metadata. Resume from this file, the [T01–T42 task board](TASKS.md) and the [remaining work plan](REMAINING_WORK_PLAN.md). Latest verification: clean isolated **seven-project build**, **62 HTTP/security smoke checks and 173 browser checks passed** (160 broad checks plus 13 final-build checks). The app is running on port 5080 from `artifacts/finance-design-final-20261117/`, PID 36104; health, home and registration were verified. The prior executable regression-runner block `0x800700E1` remains unresolved; this frontend slice used build, HTTP and browser checks and did not claim new executable-suite passes.

## Latest completed work

Applied the user's approved [finance frontend design](../design/FRONTEND_REDESIGN_PROPOSAL.md), slices F1/F2, in Dike.Web. Shared 224px navy navigation, fixed company header, teal actions, cool page surface, white financial tables and compact native forms now cover the public, identity, company/staff, NetSuite and Odoo pages. The overview displays actual saved Odoo accounts with limited-sample/missing-bank coverage, unavailable differences, capture times and an explicit foreign-currency restriction. Weekly/monthly/custom selection and existing limits remain intact; preview totals now include signed sample movement. No unavailable review/workpaper links were added.

English/Arabic parity is **434 keys**. The Browser plugin is absent, so installed Playwright/Chrome checked English/Arabic at widths 1536/1280/768/390/320, with desktop 1536×1024 and phone 390×844 captures. Concepts and final screenshots were inspected with `view_image`; layout, palette, typography, icons, copy and RTL/mobile behavior are recorded in [the design record](../design/README.md). Browser checks covered provider isolation, saved synthetic account/preview rendering, date-mode changes, invalid date preservation, secret clearing, native mobile navigation and the final live public pages. Main QA recorded no console/page errors or failed resources.

QA used separate synthetic companies and encrypted fixture previews at `%TEMP%/dike-finance-design-20261117`; no upstream calls, accounting postings, live company/user/credential changes or database migrations were performed. The isolated 5087 server was stopped. The normal app uses its existing company data and configuration. F3–F5 and all existing finance acceptance gates remain pending; T42 complete extraction is still the next backend task. Task-board counts were not changed by this visual slice.

### Previous endpoint configuration checkpoint

Removed the compiled NApp hostname from the shared Odoo connection, Dike.Web service/pages and CLI output. All read-only workflows now use `Odoo:BaseUrl` from configuration; the environment variable is **`Odoo__BaseUrl`**, which overrides Development User Secrets. There is no compiled host fallback. HTTPS root validation accepts an explicit port and rejects credentials, paths, queries and fragments; redirects/cookies remain disabled. The local setup script accepts the environment value or `-BaseUrl`. The separately authorized NApp posting script retains its original scope restriction and was not run.

Encrypted web state now records the verified endpoint. A changed endpoint or older state without an endpoint requires explicit connection verification before saved credentials or catalogs can be used. Old encrypted files remain intact until verification succeeds. Overview/connection pages display the configured address and a localized re-verification notice. Resources contain **407 matching English/Arabic keys**. No finance schema, existing user/company data, upstream records or stored credentials were changed by this update.

QA used synthetic users and an alternate `https://configured-erp.example.test:8443` environment URL on isolated port 5087. Covered environment precedence, overview → connection navigation, invalid settings, key clearing, invalid HTTP endpoint rejection, English/Arabic and 1536×1024/390×844/320×844 layouts. No console errors or failed resources; screenshots inspected. Browser plugin was unavailable, so installed Playwright 1.62.1/Chrome were used. Evidence is under `%TEMP%/dike-odoo-config-20261117`; the QA server is stopped. The real app was restarted with `Odoo__BaseUrl=https://napp.odoo.com`. No live Odoo calls were required for these checks. Added endpoint and state-binding regression fixtures compile; **29 Odoo web service cases remain unexecuted** because Windows blocks the test runner. [Configuration and verification details](../ODOO_WEB.md).

### Previous provider-selection checkpoint

The provider-choice slice passed a clean seven-project build, **62 HTTP smoke checks, 38 browser checks and 6 isolated database-upgrade checks**. Details: [provider registration and upgrade verification](../COMPANY_REGISTRATION.md).

Registration now requires an explicit **NetSuite** or **Odoo** choice, persisted in `Companies.ConnectionProvider`. Navigation, overview content, status reads, footers and connection pages show only that provider. Direct GET/POST access to the other provider is blocked before its page handlers execute; `/Accounts` routes Odoo companies to `/Accounts/Odoo`. The provider is resolved from the authenticated user's database company, not browser-supplied values or cookie claims. Administrators can change the provider in Company profile; previous encrypted connection settings remain intact for switching back. Profile/overview/connection operations verify current Administrator membership, security stamp and lockout. Resources now contain **405 matching English/Arabic keys**.

The additive SQLite upgrade was tested for old-data preservation, provider constraints and repeat startup. Before applying it to the real app, the current two-company Identity database was backed up to `src/Dike.Web/App_Data/Backups/provider-choice-2026-11-17T13-10-29-812Z/identity.db`. All original table row values/counts matched their pre-upgrade hashes. **T-sab** retains **Odoo** through its existing explicit local binding. **TestOddo**, created through the older registration flow without a provider field, retains the legacy **NetSuite** default and can choose Odoo in Company profile. No passwords, user roles, saved provider credentials or financial data were changed. No upstream provider requests were made for this update.

Browser QA covered both registration choices, provider-specific navigation/content, direct URL denial, spoofed selection, company isolation, switching back with retained settings, and English/Arabic desktop/mobile rendering. Installed Chrome/Playwright were used because the Browser plugin was not available. Viewports: 1536×1024, 390×844 and 320×844; screenshots inspected, no console errors or failed resources. Evidence/scripts are outside the repo at `%TEMP%/dike-provider-choice-20261117`. The isolated test server is stopped after verification. Production migration/finance authorization work and T42 full extraction/report acceptance remain open.

### Previous Odoo web conversion checkpoint

The user's preference is **web pages in Dike.Web**, superseding the earlier local-tool placement. Four administrator pages exist at `/Accounts/Odoo`: overview, connection settings, companies/accounts and posted GL preview. Sidebar, company overview and connections link to them for Odoo companies. Loaded account selection and weekly/monthly/custom filters use the same read-only client. Selection persists after success; invalid dates, foreign/stale selections and failed fetches preserve the prior saved result. At that checkpoint, resources contained **393 English/Arabic keys**. Responsive English/Arabic screens, scrolling account tables, error states and live monthly/weekly/custom samples were checked in Chrome at 1536×1024, 390×844 and 320×844.

The current Dike company **T-sab**, ID `e2093771-dc16-4a9d-9653-fe0aa3d14792`, is explicitly bound to the existing development key via `Odoo:LocalCompanyId` in User Secrets. After signing in, choose **Use existing local connection**, select NApp and load bank accounts 118/119. The app is running at `http://localhost:5080/Accounts/Odoo`; the normal launch profile also uses port 5080. Key import, saved credentials, catalogs and previews are scoped to the current database-authorized company. Fresh Administrator membership/security stamp/lockout checks, antiforgery, no-store responses, company-specific encryption and safe error/secret handling are implemented. No key is returned to the browser. No new Odoo postings were performed for this conversion.

Web verification used isolated temporary Identity/Odoo stores, not synthetic users in the real company. The existing 42 HTTP cases plus seven Odoo cases passed. Browser isolation checks rejected another workspace's local-key import, copied encrypted state, revoked security stamps and removed Administrator roles. The service fixture suite adds 24 cases for additional fault/pagination/revocation behavior; rerun it after local Windows security resolves the executable block. Screenshots and temporary QA files live outside the repository under `%TEMP%/dike-odoo-web-20261117`. The final clean build is `artifacts/odoo-web-final-20261117/`.

### Previous CLI and synthetic posting checkpoint

Implemented the [T42 local read-only validation tool](../../tools/Dike.OdooValidation/README.md): authenticate; list assigned companies and permitted accounts; choose 1–5 loaded bank/cash accounts; select weekly, calendar-month or custom inclusive dates; fetch a bounded posted SAR GL sample. Company/account access and returned row scope are rechecked. Samples are capped at 31 days and 50 rows per account, with explicit truncation. JSON-2 only targets `napp.odoo.com`; redirects and arbitrary methods are disabled. Full extraction, independent GL comparison, production integration and finance acceptance remain pending.

Live authentication succeeded as API user **2**, company **1 / NApp**, currency **151 / SAR**. The user's initial November selections were bank accounts **118** and **119**, both empty. The user then explicitly requested and approved test AP postings and manual payment entries. Created six posted bills (**SAR 5,753.00**) and four posted manual payment entries (**SAR 2,201.50**); verified paid/partial/unpaid states and remaining AP **SAR 3,551.50**. Four October/November bank samples returned the expected rows. [Exact IDs, dates, scenarios and audit evidence](ODOO_AP_TEST_DATA_20261117.md). These are synthetic fixtures; no independent finance acceptance is claimed.

The earlier [local setup](../ODOO_LOCAL_SETUP.md) remains: shared UserSecretsId, masked prompt, stdin transfer to Secret Manager, unrelated-setting preservation and redacted presence check. Its 14 synthetic checks passed at the setup checkpoint. The real stored key was subsequently used locally without disclosure or copying into project files. `appsettings.json` is unchanged. User Secrets is development-only and not encrypted at rest.

The [plan](REMAINING_WORK_PLAN.md), [board](TASKS.md) and [acceptance tracker](ACCEPTANCE_TRACKER.md) now include T42 and OD-01–OD-08. Current totals: **42 tasks: 1 done, 10 in progress, 31 pending**; **86–133 remaining focused engineer-days**, excluding external waits/significant rework. This includes the Odoo validation slice, not full production Odoo UI/worker integration. Existing NetSuite/Neotek gates and D01–D11 decisions remain open; no provider was replaced. Recent design/Arabic/RTL implementation and its verification are preserved below.

T42 target: **`https://napp.odoo.com/`**, Accounting installed, JSON-2 authentication and required sample reads verified. Exact build/edition, subscription metadata, trial expiry, stable database identity and least-privilege service-user status remain unverified. The local Dike company binding and preview pages are implemented. Next implement full paging/protected extraction and comparison, obtain the independent report and named reviewers, and formalize stable source/connection identity for production. Preserve provider-specific ledger semantics. Odoo-specific T07 contracts still wait for T42 acceptance.

## Most recent application deliverable

The user requested a name-and-purpose-led application redesign, then Arabic translation and RTL. The new [design system and ImageGen references](../design/README.md) are implemented across the homepage, overview, connections, staff, setup, security, authentication and error pages.

- Ink navy, warm paper and bronze; native wordmark/icons, balance photograph and clear release readiness.
- 281 English/Arabic resource keys, persistent CSRF-protected language switch, translated validation and right-to-left navigation/forms/tables.
- Gregorian date/number binding remains stable; identifiers and financial values are isolated LTR.
- Mobile menu, keyboard Escape/focus handling, scrollable staff tables and authentication-method-specific credential fields verified.
- No live provider calls, finance policy changes or real company/user/credential data mutations were made for this work.

The previous staff-directory foundation is preserved:

The user previously selected [StaffService.cs](../../src/Dike.Web/Authentication/StaffService.cs). Implemented a read-only, company-scoped staff directory and [Razor page](../../src/Dike.Web/Pages/Company/Staff.cshtml) at `/Company/Staff`, linked from company navigation.

- Resolves the current company and Administrator role from the database; ignores caller-supplied company/role claims.
- Checks current security stamp and lockout, including when the EF context holds stale tracked users.
- Returns bounded, deterministic pages (25 by default; maximum 100), company-scoped totals and batched roles. DTOs exclude Identity secrets.
- Displays email/MFA/lockout indicators; rejects POST, disables response caching and HTML-encodes member text.
- Uses the existing Identity schema. No real users, roles, email-confirmation state or stored credentials were changed.

**T14 remains in progress:** invitations, delivery, controlled assignments, finance grants, audit, last-admin protection and coordinated revocation are not implemented. These directory checks do not retrofit other routes or provide the transactional finance authorization fence. Details: [T05 implementation and limits](T05_IDENTITY_SECURITY_OPERATIONS.md#implemented-staff-directory-foundation).

## Verified state

| Check | Last result |
| --- | --- |
| Six-project build | Pass; zero warnings/errors using a separate output directory and serial build |
| Custom executable runner | **158 passed:** 56 domain, 31 NetSuite fixtures, 39 offline GL validation, 32 SQLite staff checks |
| Isolated web smoke suite | **42 passed**, including 13 language/RTL validation, cookie, CSRF and redirect checks |
| Browser QA | **44 checks passed** in headless Chrome through temporary Playwright tooling; no console errors or failed resources |
| Viewports | 1536×1024 and 390×844 across nine current pages in English and Arabic; additional RTL staff checks at widths 320, 768 and 1024 |
| Interactions | Registration → profile → overview; language persistence; 28-member staff pagination (25/3); mobile menu/Escape/focus; credential method switching; Arabic validation |
| Visual evidence | Three ImageGen concepts and current browser screenshots inspected with view_image, including native 1536×1024 comparisons |
| Coverage limit | No live NetSuite report acceptance, loaded-extract browser fixture, screen-reader certification or cross-browser audit |
| Earlier probes | 10 NetSuite CLI and 9 disposable SQL mechanism checks passed previously; not rerun for the visual/localization change |

The Browser plugin is absent. The shared original tab was unavailable; a new integrated tab verified the public Arabic switch. Independent Playwright/Chrome provided stable screenshot dimensions after prior integrated-browser emulation problems.

Fidelity ledger:

| Comparison | Evidence and result |
| --- | --- |
| Copy | Homepage/overview headings, CTAs and planned-feature statements match the concepts; requested language switch and real company/user values are intentional additions |
| Layout | 268px sidebar, evidence band, ruled readiness rows and open homepage columns retained; gutters and first-screen spacing refined |
| Typography | Georgia English display and deliberate UI sizes; Arabic uses Segoe UI/Tahoma with readable line height; control scale refined |
| Palette | Navy/paper/bronze/sage retained; generated texture replaced by flat native surfaces intentionally |
| Asset/icons | Standalone generated balance photo; native matching outline icons and directional arrows; no raster UI |
| RTL/mobile | Mirrored sidebar, reading order and arrows; isolated LTR values; responsive menu and contained table scrolling verified |

[Staff service checks](../../tests/Dike.Tests/StaffServiceChecks.cs) still cover current database authorization and company isolation. [Web checks](../../tests/registration-smoke.ps1) now cover localization without provider calls.

Commands for the current state:

```powershell
dotnet build Dike.slnx --no-restore -p:UseAppHost=false -p:BaseOutputPath=C:/Dike/artifacts/redesign-build/ -m:1
dotnet artifacts/redesign-build/Debug/net10.0/Dike.Tests.dll
dotnet build src/Dike.Web/Dike.Web.csproj --no-restore -p:UseAppHost=false -o artifacts/registration-check
powershell -NoProfile -ExecutionPolicy Bypass -File tests/registration-smoke.ps1
```

An existing Dike.Web process locks the normal build output. The separate output avoids stopping that instance. The test project is a custom executable; a zero-test editor discovery run is not validation.


## Backlog and missing inputs

| Work | Current position / next requirement |
| --- | --- |
| T01 | [Baseline](T01_BASELINE.md) complete to its inventory scope; preserve its historical source manifest |
| T02 | [Candidate query and offline comparison](T02_NETSUITE_VALIDATION.md) implemented; actual scope, Records Catalog, independent GL report and finance reviewer unavailable, as confirmed by the user |
| T42 | [Odoo validation](T42_ODOO_VALIDATION.md) in progress: local secrets, read-only sample client, company/account selection and weekly/monthly/custom web filters implemented; the Dike company binding is in place. 63 Odoo fixtures passed at the earlier CLI checkpoint; live reads and explicitly authorized synthetic AP/payment postings were verified. Full extraction, stable upstream database/source identity, independent report and OD-01–OD-08 signoff remain open. Current executable verification is blocked as noted above. |
| T03 | [CSV contract review](T03_CSV_CONTRACT_REVIEW.md) prepared; representative export and D04/D06 choices still missing |
| T04 | [Neotek documentation review](T04_NEOTEK_CONTRACT_REVIEW.md) prepared from the supplied portal; actual subscription/version, bank consent, contract differences and D05 still open |
| T05 | [Identity/security/operations decisions](T05_IDENTITY_SECURITY_OPERATIONS.md) prepared; D01/D02/D09, named owners and delivery/hosting/recovery values remain open |
| T06 | [Concurrency/review/workpaper decisions](T06_REVIEW_CONCURRENCY_WORKPAPER.md) prepared; D03/D07/D08/D10/D11 and owners remain open |
| T14 | Staff directory foundation implemented; next implement T13 delivery/token prerequisites, then invitations and approved authority changes |
| T26/T27 | Pure matching/review hardening verified; sar-v2 adds scope and deterministic ordering. Persisted proposals/revisions and transactional workflows remain pending |
| T37 | Nine real LocalDB mechanism checks pass; actual EF/service races and faults await the reviewed finance schema |
| Other tasks | Follow dependencies on the task board; application finance persistence and production operations remain incomplete |

Deployment preference and the proposed T06 defaults were asked about but not answered. “Continue” does not select those decisions or provide finance approval. Provider documentation and synthetic passes are not live acceptance. No independent EGM review is claimed.

## Resume safeguards

- Preserve populated SQLite Identity and existing encrypted NetSuite evidence/keys. Startup uses EnsureCreated; do not blindly apply an initial migration or recreate the store.
- Keep existing unconfirmed owners usable until staged verification delivery is ready. Do not bulk-confirm users or introduce sensitive role/grant assignments without the selected policy.
- The worker remains disabled; no application finance migration or live provider publication occurred.
- LocalDB 2019 is available for disposable checks. The prior probe dropped its generated database; it did not deploy the application schema.
- The previous staff-test server on port 5082 was stopped at that checkpoint. This review did not inspect current browser state; an existing process held a normal web build DLL, so verification used isolated output. No existing application process was stopped.
- Synthetic test output is under the ignored artifacts folder. No Git commit or checkout was created.
- Continue independent authorized implementation while decisions wait; consult existing context before asking again. Update this checkpoint and task board after the next verified deliverable.
