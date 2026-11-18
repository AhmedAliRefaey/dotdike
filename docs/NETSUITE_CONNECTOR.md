# NetSuite GL connector

Status checkpoint: 4 November 2026. Code and fixture/web checks completed; live credentials, Records Catalog verification and GL-report acceptance remain outstanding. This is partial completion of Phase 1, not a signed-off accounting input. See [project timeline and checkpoints](PROJECT_STATUS.md) and [storage configuration](DATABASE_CONFIGURATION.md).

## Use

Sign into Dike as your company's administrator. Open Connections → Configure NetSuite. Save integration credentials, then Load / refresh GL accounts. Select an eligible SAR bank account, subsidiary internal ID, primary accounting book internal ID and date range, then Collect GL movements. Every operation resolves the company from the signed-in user; no posted company ID is trusted.

The connector performs read-only SuiteQL POST requests. It never posts journals, changes NetSuite records or marks transactions reconciled. No supplied credentials or working existing client were present; this is a new adapter behind INetSuiteReader.

## Authentication

- TBA: account ID, consumer key, consumer secret, token ID and token secret. The integration/token role must permit REST Web Services and the required accounting records. Requests use OAuth 1.0 HMAC-SHA256 including URL paging parameters.
- OAuth 2.0 M2M: account ID, client ID, mapped certificate ID and RSA private key PEM. This implementation signs PS256 assertions for `rest_webservices`. Configure the certificate/application/entity/role mapping in NetSuite. A client secret alone is insufficient. ECDSA and interactive authorization-code flows are not implemented.

Use the account ID, e.g. `1234567_SB1`, not an arbitrary URL. HTTPS destinations are derived from that ID and restricted to NetSuite's SuiteTalk host; redirects are disabled. Credentials never appear in URLs or HTTP logs and are not redisplayed in forms. Never paste secrets into chat or source control. Use HTTPS for deployed credential entry.

## Accounting contract

Active account metadata comes from `account`, with currency symbols resolved from `currency`. Subsidiary metadata may contain multiple subsidiary IDs; it is not represented as a single verified mapping. GL retrieval joins transaction, transaction line and accounting line using both transaction and line IDs; it filters posting flags, account, subsidiary, book and inclusive posting-date range using bound parameters.

Initial imports require an active Bank account in SAR, a subsidiary with SAR base currency and the primary accounting book. This is deliberately narrower than all possible books/currencies: no foreign-currency conversion or secondary-book currency mapping is inferred. Verify the account/subsidiary mapping and accessible fields in your NetSuite Records Catalog. `transaction:transactionline:accountingbook:account` is a candidate identity; repeated identities fail the entire extraction rather than silently collapsing accounting rows. Validate that identity against the actual GL report before production reconciliation.

Debits and credits are retained separately. Signed amount is debit minus credit. Original amount digits are checked before decimal conversion, so excess nonzero precision cannot be rounded away. Each debit, credit and signed movement must fit decimal(19,2); exact extra trailing zeros are accepted. The reader and offline validator share this rule. Source as-of time is not supplied; displayed timestamps are fetch times. The page displays up to 500 rows while totals include all rows in the completed snapshot.

## Completion and failure behavior

Requests page 1,000 rows at a time and validate offsets, counts, stable totalResults and hasMore. The REST 100,000-result boundary is treated as a failure; select a smaller period. Missing IDs, duplicate identities, bad precision, unexpected scope, failed pages or changing counts reject publication. Concurrent source edits that preserve row counts cannot be ruled out by this API; compare totals against an agreed report/time window.

429/5xx query responses retry twice with bounded delay; long Retry-After requests surface a retry-later message. Provider error bodies/tokens are not exposed. Failed reads retain prior complete snapshots and display the failure. Collection is synchronous and serialized per company within a single application instance. Durable SQL background jobs, distributed leases, scheduled synchronization and source-change reconciliation invalidation remain separate implementation work.

## Persistence

Company credentials, account lists and completed GL extracts use company-specific ASP.NET Core Data Protection. PostgreSQL stores the encrypted documents and commits a completed extract together with its state update. Replacing a connection archives its prior state. The explicit SQLite development mode uses `App_Data/NetSuite/<company-id>/`. Back up hosted evidence with the matching Data Protection certificate. Publishing normalized ledger rows remains outstanding. [Database configuration](DATABASE_CONFIGURATION.md).

## Acceptance

T02 now has a [candidate contract, offline comparison tool and live review checklist](delivery/T02_NETSUITE_VALIDATION.md). New extracts record candidate query/key versions and a digest; older evidence keeps unknown provenance. These additions do not approve the candidate line key or establish live GL agreement.

Fixture checks cover authentication signing, complete pagination, failed second page, currency/sign/precision checks, IDs and company isolation. Live operation is unverified until credentials, record permissions and account-specific schema are supplied. Compare one selected period's row identities, debits, credits and signed total against the NetSuite GL report. Repeat the import and validate equal identities/totals, then test revoked credentials and a failed page without losing the last successful snapshot.

## Oracle references

- [SuiteQL REST, pagination, limits and bound parameters](https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_157909186990.html)
- [Accounting joins and accounting books](https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_1548805090.html)
- [OAuth 2.0 M2M assertion](https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_162790605110.html)
- [Token authentication HMAC-SHA256](https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_157652573296.html)
