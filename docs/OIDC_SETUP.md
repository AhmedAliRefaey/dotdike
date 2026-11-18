# Company sign-in setup

> Superseded: the application now uses direct company registration and ASP.NET Core Identity. See [company registration](COMPANY_REGISTRATION.md). The OIDC configuration below is historical and inactive.

Status reviewed 4 November 2026. Do not follow the configuration below for the current application, and do not put NetSuite integration credentials in website OIDC settings. This file is retained to explain the baseline design change; see [delivery checkpoints](PROJECT_STATUS.md).

Dike uses a confidential OIDC web client with authorization code flow and PKCE. No development bypass or automatic administrator assignment exists. Account pages require the exact `Administrator` value in the validated `roles` claim. Other defined role policies are `Accountant`, `Controller`, and `Viewer`. Resource-level grants still require implementation before connecting financial data.

## Provider registration

1. Register a web application in your company identity provider. Use a tenant-specific HTTPS authority and client ID.
2. Register the exact callback URL `https://localhost:7080/signin-oidc` for the local HTTPS example below. Register your production HTTPS callback separately.
3. Issue a client secret and store it outside source control. Configure assigned users/groups and the provider's MFA policy.
4. Include a `roles` claim in the ID token. Define and assign Dike app roles; do not assume directory group membership automatically creates app roles. For Entra ID use a tenant-specific authority of the form `https://login.microsoftonline.com/<tenant-id>/v2.0`.

## Configuration

Supply these keys using the hosting secrets manager, injected environment variables, or ASP.NET Core development user secrets:

```text
Authentication__Oidc__Authority
Authentication__Oidc__ClientId
Authentication__Oidc__ClientSecret
```

Never paste a client secret into chat, commit it, or put it in a launch task. Missing or invalid basic configuration leaves the public shell available and protected pages unavailable. Completing these fields is not proof that provider registration is valid.

For local sign-in, trust the ASP.NET Core development certificate and run the web project on HTTPS:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/Dike.Web --no-launch-profile --urls https://localhost:7080
```

The existing HTTP task can preview the public shell, but actual sign-in requires HTTPS because session and correlation cookies are secure. In production configure `AllowedHosts`, HTTPS termination and trusted forwarded headers if behind a proxy. Persist and encrypt ASP.NET Core data-protection keys across replicas and restarts using your deployment's approved store.

## Behavior and validation

- Login is a CSRF-protected POST; return destinations must be local URLs.
- Authenticated sessions expire after one hour without sliding renewal. Tokens are not stored in the session cookie.
- Sign out is a CSRF-protected POST that clears the Dike session only. The company's provider session remains active.
- Provider failures show a generic retry message, not raw exceptions or tokens.
- Verify no configuration, valid sign-in, cancelled sign-in, wrong role (403), administrator access, external return-URL rejection, logout and session expiry against the actual provider.
- Verify account/subsidiary/company grants separately before enabling real provider data.

End-to-end sign-in cannot be validated until the actual authority, registered client, secret and assigned test users are available.
