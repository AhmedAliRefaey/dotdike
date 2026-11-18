# Direct company registration

Users register directly on Dike with local Identity. Dike passwords are separate from provider credentials.

Updated 17 November 2026. Registration now requires **NetSuite** or **Odoo** as the company's connection provider. Only the selected provider appears in navigation, overview, connection settings and GL pages. See [full timeline and checkpoints](PROJECT_STATUS.md); policy definitions are not a completed staff-management or fine-grained authorization workflow.

## Language and layout

The header offers English and Arabic. Selection persists across pages through a protected culture cookie. Arabic navigation, forms, role/status labels and validation are translated and laid out right-to-left. Email addresses, IDs, passwords and accounting/date values retain left-to-right isolation. See the [design and localization notes](design/README.md).

## Flow

1. Open `/Auth/Register`, supply the company name, select **NetSuite** or **Odoo**, enter owner name/email/password, and confirm registration authority. The provider has no preselected default; missing or unsupported choices are rejected.
2. Company, owner and Administrator membership are created in one database transaction. Duplicate emails cannot create another owner or leave orphan companies.
3. Complete `/Company/Setup`. The selected provider is carried into the profile. The profile is resolved from the signed-in user's database membership, never a browser-supplied company ID.
4. Open `/Company` to view only the selected provider's status and connection link. NetSuite opens `/Accounts`; Odoo opens `/Accounts/Odoo`. The generic `/Accounts` entry redirects Odoo companies to their workspace. Direct GET/POST requests for the other provider return 404 before provider handlers run. Selection is read from Identity, not URL/form overrides or stale cookie claims.
5. Sign out through the POST action and sign in again with email/password. Incomplete setup returns to onboarding.
6. Administrators can open Staff at `/Company/Staff` to view company members, roles and email/MFA/lockout indicators. This is a paginated, read-only directory; invitations and role changes remain pending. Its service verifies current database membership, role and session stamp on each request. See the [staff implementation and limits](delivery/T05_IDENTITY_SECURITY_OPERATIONS.md#implemented-staff-directory-foundation).

Existing companies can change **Connection provider** in **Company profile**. The next request uses the new selection, including other signed-in sessions. Previously saved provider credentials/data are retained under the same company's protected store and become accessible again if the company switches back. Switching providers does not import data, call an upstream provider or transfer another company's connection. Profile changes, overview and connection access check current Administrator membership, security stamp and lockout. Signed-in visits to `/` return to the company overview.

## Storage and operation

ASP.NET Core Identity hashes passwords and handles cookies, anti-forgery, security stamps and lockout (five failed attempts, fifteen minutes). Passwords require twelve characters, uppercase, lowercase, digit and symbol. Sessions last one hour. Emails are unique across workspaces in this initial one-company-per-user model. Registration authority is self-attested, not legal company verification.

Identity/company data persists in ignored `src/Dike.Web/App_Data/identity.db` SQLite storage. Override `ConnectionStrings:Identity` for another SQLite location. SQL Server finance storage remains separate. Initial schema creation uses EnsureCreated. `IdentitySchema.InitializeAsync` applies one additive, transactional SQLite upgrade when `Companies.ConnectionProvider` is absent: add the constrained provider column, retain NetSuite for legacy companies, and preserve the explicitly bound Development Odoo company. It does not rewrite a provider on subsequent starts. Fresh databases receive the field and constraint directly from the EF model. This local upgrade is not a general production migration framework.

The real database was backed up under `src/Dike.Web/App_Data/Backups/provider-choice-2026-11-17T13-10-29-812Z/` before the update. All previous row values and counts across its Identity/company tables matched their pre-upgrade hashes afterward. T-sab retains Odoo through its existing local binding. Other legacy companies can select Odoo in Company profile. Backups contain private Identity data; keep them local and protected with the application data. Production still requires reviewed migrations, backup/restore and persistent Data Protection keys.

Verification: clean seven-project build; **62 HTTP smoke checks**, **38 browser checks** in Chrome through Playwright, and **6 isolated schema/upgrade checks** passed. Covered both registration choices, provider-specific content, blocked cross-provider GET/POST actions, browser override rejection, company isolation, switching and retained settings, unchanged legacy data, database constraints and idempotent restart. English/Arabic layouts were inspected at 1536×1024, 390×844 and 320×844 with no console errors, failed resources or page overflow. Browser plugin was not available. Temporary QA scripts/screenshots remain outside the repository. The executable regression runner remains blocked by local Windows security (`0x800700E1`); its previous passes are historical. No upstream NetSuite/Odoo operations were made for this update.

Run `dotnet run --project src/Dike.Web` for the Development profile at http://localhost:5080. Production requires HTTPS and secure cookies. No OIDC registration or secrets are required.

## Remaining production requirements

Email delivery is not configured: email ownership verification, forgotten-password recovery and invitations are not implemented. Registration signs the owner in without claiming the email is verified. Add verification and reset delivery before public deployment. Also add registration throttling, MFA and company verification where required. Company-scoped NetSuite configuration/collection is now implemented; finer subsidiary/account grants remain pending. Account pages use the company connector service rather than the global placeholder reader.

The previous OIDC setup instructions are historical and inactive.
