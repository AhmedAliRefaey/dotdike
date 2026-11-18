# Odoo local development configuration

The current target is `https://napp.odoo.com`. Accounting is installed. This guide prepares local credentials for the [Odoo web pages](ODOO_WEB.md) and optional [CLI diagnostic tool](../tools/Dike.OdooValidation/README.md). Authentication, company/account listing and posted GL previews succeeded on 17 November 2026. Full extraction, independent report agreement and finance acceptance remain pending under [T42](delivery/T42_ODOO_VALIDATION.md).

## Save the API key

Use the API key belonging to the dedicated Odoo integration user. Open a local PowerShell terminal in `C:\Dike` and run:

```powershell
$env:Odoo__BaseUrl = 'https://napp.odoo.com'
powershell -NoProfile -File .\tools\configure-odoo-local.ps1
```

Replace the example endpoint with your Odoo HTTPS root. The script reads `Odoo__BaseUrl` or an explicit `-BaseUrl` argument; it has no hard-coded target. Paste the key only into the masked prompt and press Enter. The script saves `Odoo:BaseUrl` and `Odoo:ApiKey` through .NET User Secrets. It preserves unrelated secrets, passes the key through the child process's standard input, and prints no secret values. It makes no network requests. No database name or company ID is guessed.

Both Dike.Web and the CLI read `Odoo:BaseUrl` through .NET configuration. `Odoo__BaseUrl` takes precedence over Development User Secrets; the application's compiled code contains no default hostname. HTTPS roots, including explicit ports, are accepted; credentials, paths, queries and fragments are rejected. Restart the app after changing its process environment. Web overview/connection labels show the effective endpoint. An older unbound saved connection or a changed endpoint requires explicit connection verification before saved credentials or accounting data can be used.

The project identifier is declared in [Dike.Web.csproj](../src/Dike.Web/Dike.Web.csproj) and shared by the local Odoo validation tool. ASP.NET Core automatically loads this store in **Development**; the existing `Dike.Web` launch profile selects that environment. The CLI explicitly loads the same store for its JSON-2 client. No settings were added to `appsettings.json`. The web preview workflow is implemented; production finance publication and deployment controls remain future work.

The local key is explicitly assigned to Dike workspace **T-sab** (`e2093771-dc16-4a9d-9653-fe0aa3d14792`) using `Odoo:LocalCompanyId` in User Secrets. This nonsecret binding enables **Use existing local connection** only for that workspace in Development. Sign in, open `/Accounts/Odoo/Connection`, and use that button to verify and save the company's encrypted web connection. Other companies cannot import this shared local key. Do not reassign this setting to an unrelated company.

User Secrets stores values in the current user's profile outside the repository, **without encryption at rest**. This is development configuration, not a production vault. Do not put the API key in chat, tracked configuration, command arguments, a PowerShell transcript or screenshots. Do not run `dotnet user-secrets list` in shared output; it displays secret values. See [Microsoft's Secret Manager guidance](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0).

## Check and load

Check only whether the correct endpoint and a key are stored:

```powershell
powershell -NoProfile -File .\tools\configure-odoo-local.ps1 -Check
```

A successful check shows:

```text
Stored endpoint valid and matches requested target: yes
API key stored: yes
Local configuration is present. This check does not test Odoo authentication.
```

Missing settings produce a nonzero exit. With `Odoo__BaseUrl` or `-BaseUrl`, the check also compares the stored endpoint with the requested target; without either, it validates the stored HTTPS root. The check confirms the local store, not key validity, company binding, effective API-key overrides or Odoo permissions. Environment variables and command-line configuration can override User Secrets; check for stale `Odoo__BaseUrl` / `Odoo__ApiKey` / `Odoo__LocalCompanyId` overrides before testing.

Restart the app when ready:

```powershell
dotnet run --project src/Dike.Web --launch-profile Dike.Web
```

Only run the setup command in your own interactive terminal. The API key was subsequently verified present and used locally for the authorized live test; it was not printed or copied into project files. Synthetic configuration checks use an isolated store under `artifacts`.

## Replace or remove a key

To rotate the local key, rerun the setup script and enter the new key. Revoke the old key in Odoo after verifying the replacement. To remove only this application's local Odoo key:

```powershell
dotnet user-secrets remove Odoo:ApiKey --project src/Dike.Web
```

Local removal does not revoke the key in Odoo or erase a saved web connection. After rotating the local key, use **Use existing local connection** again to replace the verified web copy. Use [the web workflow](ODOO_WEB.md) for company/account selection and weekly/monthly/custom period filters; `dotnet run --project tools/Dike.OdooValidation -- browse` remains an optional diagnostic. See the [live synthetic posting and GL test report](delivery/ODOO_AP_TEST_DATA_20261117.md). Local key presence or a small sample does not complete the OD-01–OD-08 acceptance gates.
