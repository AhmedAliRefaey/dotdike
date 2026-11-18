# Odoo in Dike.Web

Implemented 17 November 2026. The primary validation workflow is now inside Dike.Web. The read-only JSON-2 endpoint comes from `Odoo:BaseUrl`, with company-scoped connection storage and English/Arabic pages. The CLI remains available for diagnostics.

## Configure the server address

Set the environment variable before launching the web app:

```powershell
$env:Odoo__BaseUrl = 'https://napp.odoo.com'
dotnet run --project src/Dike.Web --launch-profile Dike.Web
```

Replace the example with your Odoo HTTPS root. `Odoo__BaseUrl` maps to `Odoo:BaseUrl` through ASP.NET Core configuration and overrides the Development User Secrets value. The application has no built-in host fallback. HTTPS roots with an explicit port are supported; credentials, application paths, queries and fragments are rejected. The overview and connection pages show the configured address. Restart the process after changing its environment.

The endpoint is deployment-wide server configuration; it cannot be changed by submitting a browser form. Each company's verified encrypted connection records its endpoint. Changing the configured endpoint, or upgrading an older saved connection that did not record an endpoint, requires **Verify and save connection** or the explicitly bound **Use existing local connection** action. Existing encrypted state remains intact until verification succeeds; old catalogs/previews and saved credentials are not reused against another server.

## Open the workflow

Start Dike.Web with its Development launch profile, then sign in as your Dike company administrator:

```powershell
dotnet run --project src/Dike.Web --launch-profile Dike.Web
```

Choose **Odoo** during company registration, or select it in **Company profile** for an existing company. Open `http://localhost:5080/Accounts/Odoo`, or choose **Odoo** in the sidebar. Only the company's selected provider is visible and accessible; a NetSuite company must change its provider before opening Odoo pages. Complete the Dike company profile first if prompted. [Provider selection and upgrade behavior](COMPANY_REGISTRATION.md).

| Page | Route | Purpose |
| --- | --- | --- |
| Overview | `/Accounts/Odoo` | Connection status, workflow links and latest saved preview summary |
| Connection settings | `/Accounts/Odoo/Connection` | Verify the existing local connection or supply a replacement API key |
| Companies and accounts | `/Accounts/Odoo/Accounts` | Choose a permitted Odoo company and load its bank/cash accounts |
| GL preview | `/Accounts/Odoo/Ledger` | Select loaded accounts, dates and row limit; inspect posted SAR entries |

1. On **Connection settings**, choose **Use existing local connection**. The existing local key is bound to **T-sab** through the nonsecret `Odoo:LocalCompanyId` setting. The key is never displayed. Another Dike workspace must configure its own connection.
2. Select **NApp (SAR)** and choose **Load bank accounts**. Lists follow the connected Odoo API user's permissions and Odoo record rules. Dike's current Administrator membership is checked separately.
3. Open **GL preview**, select accounts **118** and **119**, choose **Monthly → November 2026**, and select **Fetch posted GL**. The verified synthetic sample is one credit of SAR 900.00 for account 118 on 4 November and one credit of SAR 400.00 for account 119 on 12 November.
4. Use **Weekly** for the selected start date plus six days, or **Custom dates** for an inclusive from/to range. October 1–31 returns the previously authorized SAR 451.50 and SAR 450.00 payment entries. [Posting evidence](delivery/ODOO_AP_TEST_DATA_20261117.md).

The preview supports one to five loaded bank/cash accounts, at most 31 inclusive days, and 1–50 rows per account. It shows debit, credit, signed SAR, journal, entry and journal-item IDs. A truncated sample is clearly labeled. Empty accounts are explicit. These sample totals do not establish full-period completeness, independent report agreement or finance acceptance.

## Connection and state behavior

- Credentials, loaded catalogs and the last successful preview are protected using ASP.NET Core Data Protection in `src/Dike.Web/App_Data/Odoo/<Dike-company-id>/state.enc`. The protection purpose includes the company ID; copying the encrypted file to another company's folder does not make it readable there. Preserve the application's Data Protection keys when backing up this local state.
- The stored state never becomes a page model, browser response, cookie or log entry. Pages receive a credential-free projection. Errors are translated safe messages; submitted keys are not reflected into the password field.
- Every read and action resolves the Dike company and current Administrator membership from Identity, checks the security stamp and lockout, and ignores browser-supplied company/role claims. Mutations recheck membership before saving the network result. This is a local preview boundary, not the future transactional finance authorization fence.
- Local-key import is enabled only in Development and only for the explicitly bound Dike company. New web connections can use a separately entered key. [Local configuration and rotation](ODOO_LOCAL_SETUP.md).
- A successful replacement connection clears old company/account selection and preview. Reloading or switching the selected Odoo company clears its account selection and preview. A version token rejects stale account selections from older tabs.
- Results from multiple selected accounts are saved together only after all reads succeed. Failed requests preserve the previous successful state. Odoo company/account access and posted SAR row scope are rechecked by the shared client for every fetch.
- POST forms require antiforgery tokens; responses prohibit caching. The destination comes only from server configuration, redirects are disabled, and the web client exposes no posting methods. The separately authorized NApp AP seed script retains its original destination restriction and has no web route.
- Storage is for one application instance, one connection per Dike workspace, with a bounded catalog (1,000 companies/accounts), request deadlines and file sizes. Durable jobs, immutable extraction history, shared-instance locking, fine-grained Dike account grants, production vault/retention/restore and full GL publication remain separate delivery work.

Rotating the local User Secrets key does not automatically replace a saved web connection. After rotation, choose **Use existing local connection** again to verify and save it. Local secret deletion also does not revoke the upstream Odoo key or erase an already saved web connection.

## Verification and limits

Latest endpoint configuration change: clean seven-project build at `artifacts/odoo-config-20261117/`, **21 setup-script checks, 24 browser checks and one CLI invalid-endpoint check passed**. The running app on port 5080 was restarted with `Odoo__BaseUrl=https://napp.odoo.com`; health and registration returned 200. Isolated browser QA on port 5087 used a different environment hostname and an invalid HTTP URL, with no upstream provider requests. Overview/connection navigation, endpoint labels, English/Arabic, invalid-input handling and 1536×1024/390×844/320×844 layouts passed; no console errors or failed resources. Screenshots were inspected. Installed Playwright 1.62.1/Chrome were used because the Browser plugin was unavailable; evidence is in `%TEMP%/dike-odoo-config-20261117`, and its server is stopped. The expanded endpoint fixtures and 29 Odoo web service cases compile, but the executable runner remains blocked by Windows `0x800700E1`.

The following results describe the earlier web-conversion checkpoint:

The final seven-project build is clean, with zero warnings/errors: `artifacts/odoo-web-final-20261117/`. The app is running locally on port 5080. The existing real Identity database was not used for synthetic registrations; live browser QA ran on port 5087 with temporary Identity and Odoo stores.

- **49 HTTP smoke checks passed**, including the existing registration, staff, NetSuite and localization cases plus seven Odoo authorization/CSRF/secret-redaction/empty-state cases in `tests/registration-smoke.ps1`.
- **44 distinct Odoo browser checks passed** across the live-flow and final UI passes. Covered real NApp connection, permitted account loading, monthly/weekly/custom samples, empty results, invalid ranges, unloaded/stale selections, row limits, persisted selections, company isolation, copied encrypted-state rejection, revoked security stamps/Administrator roles, and English/Arabic rendering. Installed Playwright 1.62.1 and Chrome were used because the Browser plugin was not available. Viewports: 1536×1024, 390×844 and 320×844. No browser errors, failed resources, blank pages or framework error pages were observed. Screenshots were visually inspected; mobile account tables scroll within their panels.
- **24 new service fixture cases compile** in `tests/Dike.Tests/OdooWebChecks.cs`. Execution of the rebuilt `Dike.Tests.dll` is blocked by Windows with `0x800700E1` (file reported as virus or potentially unwanted software), both inside and outside the sandbox. No security settings, exclusions or quarantined files were changed. These 24 cases have **not** passed yet. The previous **221 passing executable checks** remain historical evidence, not a fresh result for this web change. Rerun the full executable suite after the local security block is resolved.
- Screenshots and temporary QA scripts are retained under `%TEMP%/dike-odoo-web-20261117`, outside the repository. After testing, the isolated QA server was stopped and its encrypted credential copy, saved login cookies and synthetic Identity database were removed. The real application remains running on port 5080. No API key was printed, sent to the browser or saved in tracked configuration. No new Odoo transactions were posted during this conversion.

The next T42 work remains complete extraction/evidence, stable database/source identity, independent GL report comparison and named finance acceptance. Cross-browser, screen-reader and production deployment/restore verification are still open.
