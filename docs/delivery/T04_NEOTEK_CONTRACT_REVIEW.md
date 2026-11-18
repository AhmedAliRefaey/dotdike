# T04 — Neotek product and integration contract review

Status: **In progress; documentation discovery complete, subscribed-product and live contract validation pending.** Updated 16 November 2026. The user supplied the [Neotek developer portal](https://neotek-developer.readme.io/docs). Its guides and relevant API reference schemas were inspected in the browser; no API request, subscription, consent or bank operation was performed.

The [design draft](../../design_draft.md), section 8.2, targets the customer's subscribed **Single API** capabilities. This is the working documentation target, not evidence that the customer has that subscription. D05 remains open. T24/T25 implementation and AC14/15/16 acceptance are not claimed.

## Sources and evidence level

All links below were inspected on 16 November 2026. The supplied ReadMe portal displays v1.0; several pages describe their last update as about a year ago. These are documentation observations, not a verified deployed API version.

| Source | What was inspected | Evidence limit |
| --- | --- | --- |
| [Supplied authentication guide](https://neotek-developer.readme.io/docs/authentication) | Environment/token flow and scope guidance | No token obtained |
| [Supplied Open Banking guide](https://neotek-developer.readme.io/docs/open-banking-pre-requistes) | Institution discovery, consent creation and link lifecycle | Some guide examples conflict with the detailed schema |
| [Supplied Single API guide](https://neotek-developer.readme.io/docs/single-api) | Profiles, profile-link association and read endpoints | Subscription and customer profile mode unknown |
| [Token reference](https://neotek-developer.readme.io/reference/generatetoken-5) | Form fields, token endpoint and error responses | No application credentials inspected |
| [Create Accounts Link reference](https://neotek-developer.readme.io/reference/createaccountslink) | Expanded request Data schema, permission groups and account types | Sandbox examples do not prove corporate-bank eligibility |
| [Profile-link reference](https://neotek-developer.readme.io/reference/addprofileaccountslink) | Required PSU/link information and client/self-managed distinction | Actual association/refresh mode unknown |
| [Transactions reference](https://neotek-developer.readme.io/reference/listprofiletransactions) | Filters, ordering, expanded 200 response and pagination metadata | No actual page, replay or correction captured |
| [Balances reference](https://neotek-developer.readme.io/reference/listprofilesbalances) | Filters, expanded 200 response, balance types and timestamps | No actual account balance captured |
| [Public authentication](https://neotek.sa/docs/getting-started/authentication), [Open Banking guide](https://neotek.sa/docs/getting-started/open-banking-prerequisites) and [Single API overview](https://neotek.sa/docs/single-api) | Compared with the supplied portal | Differences below require a contract/version resolution |
| [Public transaction example](https://neotek.sa/docs/single-api/list-profile-transactions) | Compared status representation and pagination example | Example is not a customer response |
| [Public product catalog](https://neotek.sa/docs/getting-started/explore-our-products) and [B2B Statements overview](https://neotek.sa/docs/statements) | Checked that B2B Statements is a separate product family | No evidence authorizes substituting it for the draft's Single API |

## Documented Single API flow

1. Establish the subscribed product and environment. Both documentation sets describe server-side client credentials and distinct sandbox/production hosts. Retain token expiry from the response; the exact authorized scope set needs the resolution below. [Authentication guide](https://neotek-developer.readme.io/docs/authentication).
2. Discover the permitted institution, create a link for a server-resolved PSU and complete bank consent through the returned redirect. The supplied guide distinguishes Active, Pending, Rejected, Revoked and Expired links. An application token is not the customer's bank consent. [Open Banking guide](https://neotek-developer.readme.io/docs/open-banking-pre-requistes).
3. Confirm profile association. The supplied guide includes linking an authorized bank link to a profile and SINGLE/PERIODIC fetching modes; the reference distinguishes an explicit client-managed profile from a self-managed default. A mode name does not establish a polling interval. [Single API guide](https://neotek-developer.readme.io/docs/single-api), [profile-link reference](https://neotek-developer.readme.io/reference/addprofileaccountslink).
4. Read the authorized accounts, balances and transactions under that confirmed association. Validate returned scope on every page and retain evidence before common publication. The latter controls are Dike requirements, not claims that the provider enforces Dike's company boundaries.

Documented sandbox host: `https://test.api.neotek.sa`. The authentication guide identifies production as `https://api.neotek.sa`. No host or endpoint has been added to runtime configuration.

| Purpose | Documented method/path | Source |
| --- | --- | --- |
| Token | POST `/oauth2security/oauth2/token` | [Token reference](https://neotek-developer.readme.io/reference/generatetoken-5) |
| Institutions | GET `/financial-institutions-information/v1/financial-institutions` | [Open Banking guide](https://neotek-developer.readme.io/docs/open-banking-pre-requistes) |
| Create/list bank links | POST/GET `/accounts-information/v1/accounts-links` | [Open Banking guide](https://neotek-developer.readme.io/docs/open-banking-pre-requistes) |
| Associate profile/link | POST `/single-api-aggregator/v1/profiles/profile-accounts-link` | [Profile-link reference](https://neotek-developer.readme.io/reference/addprofileaccountslink) |
| Accounts | GET `/single-api-aggregator/v1/profiles/accounts` | [Single API guide](https://neotek-developer.readme.io/docs/single-api) |
| Balances | GET `/single-api-aggregator/v1/profiles/balances` | [Balances reference](https://neotek-developer.readme.io/reference/listprofilesbalances) |
| Transactions | GET `/single-api-aggregator/v1/profiles/transactions` | [Transactions reference](https://neotek-developer.readme.io/reference/listprofiletransactions) |

These paths identify candidates for contract verification. No create/update endpoint was called.

## Differences that must be resolved

| ID | Observed difference | Required disposition before dependent implementation |
| --- | --- | --- |
| N01 — token scopes | The [supplied authentication guide](https://neotek-developer.readme.io/docs/authentication) describes one product per token; the [public guide](https://neotek.sa/docs/getting-started/authentication) permits multiple subscribed scopes. | Pin the applicable contract and use the actual app's permitted scope set. Do not combine examples from different versions. |
| N02 — consent scope | The [supplied Open Banking guide](https://neotek-developer.readme.io/docs/open-banking-pre-requistes) names `ob_connect`; the [public guide](https://neotek.sa/docs/getting-started/open-banking-prerequisites) describes product scopes and separate prerequisite subscriptions. | Confirm the scope/subscription needed for institution and link operations in the customer's environment. |
| N03 — consent payload | The supplied guide shows string entries for DataGroups, while its own expanded [Create Accounts Link schema](https://neotek-developer.readme.io/reference/createaccountslink) requires structured groups with an ID and permissions. | Obtain the versioned schema and validate the structured request in an authorized sandbox. The guide's minimal example cannot be treated as schema proof. |
| N04 — status values | The supplied transaction reference uses `KSAOB.Booked`/`KSAOB.Pending`; the public transaction example uses `Booked`. | Record the actual response/filter enums. Do not silently strip prefixes or merge incompatible contracts. [Reference](https://neotek-developer.readme.io/reference/listprofiletransactions), [public example](https://neotek.sa/docs/single-api/list-profile-transactions). |
| N05 — profile association | The supplied Single API guide describes explicit profile-link association; the public overview summarizes access through an active link and PSU. | Confirm whether association is explicit or supplied by this customer's profile mode. An abbreviated overview does not prove the association step is unnecessary. [Supplied guide](https://neotek-developer.readme.io/docs/single-api), [public overview](https://neotek.sa/docs/single-api). |

The supplied consent schema includes a corporate account-type option, but that enum does not establish support for either pilot bank/account. Likewise, mock-bank flows and public examples are not actual customer entitlements. [Consent schema](https://neotek-developer.readme.io/reference/createaccountslink).

## Data contract: known and still unknown

| Area | Documented evidence | Remaining customer/provider evidence |
| --- | --- | --- |
| Request scope | Transactions require PSUId and offer account/link/institution/profile filters | Trusted company-to-PSU binding, permitted corporate banks, selected accounts and actual subscription |
| Pagination | Transaction and balance references accept Page starting at 1 and return Meta page number/total pages | Page size/caps, empty-page convention, stable ordering/tie-breaks, concurrent-edit behavior and exact completion semantics |
| Transaction identity | The transaction schema describes an immutable institution-scoped transaction ID | Real uniqueness/replay evidence, re-consent behavior, correction linkage and any exceptional ID replacement |
| Status and time | Transaction schema distinguishes status/direction and booking/value timestamps | Resolve N04; confirm missing fields, source timezone, filter inclusivity and D06 comparison-date rule |
| Balance | Expanded schema requires type, direction, amount and DateTime | Actual bank types, freshness meaning and treatment of missing/invalid source timestamps |
| Refresh | Guide exposes SINGLE/PERIODIC modes | Allowed cadence, quotas, retry headers, data-lag limits, overlap/full-refresh rules and service guarantees |
| Corrections/removals | No sufficient correction/removal protocol was established from the reviewed pages | Stable-ID changes, pending-to-booked linkage, reversals, deletions, authoritative completeness and historical window limits |

Sources for the first four rows: [transactions reference](https://neotek-developer.readme.io/reference/listprofiletransactions); pagination/balance details: [balances reference](https://neotek-developer.readme.io/reference/listprofilesbalances); fetching modes: [Single API guide](https://neotek-developer.readme.io/docs/single-api). “Unknown” means not established by this review; it does not assert that the provider cannot support the capability.

Balance as-of and fetch time must remain distinct under Dike's design. If a response violates a required provider timestamp, reject or quarantine it according to the recorded contract; never replace it with the local fetch time. The current [core balance contract](../../src/Dike.Core/Models.cs) has no separate fetch time and cannot represent unknown as-of. T07/T24 must resolve that limitation before UI freshness claims.

No numeric polling schedule, history limit or page size is approved. In particular, the B2B Statements page-size and history rules cannot be imported into Single API: the public catalog describes a different subscription/scope, and its Statements overview describes a separate stored-history service. [Product catalog](https://neotek.sa/docs/getting-started/explore-our-products), [Statements overview](https://neotek.sa/docs/statements).

## Implementation preparation and acceptance cases

After resolving the version and customer contract, T24/T25 should exercise the following cases. These are planned Dike checks, not executed provider tests:

| Case | Required result |
| --- | --- |
| N06 — authorization | Correct app token with inactive/foreign bank link cannot authorize a read; server-owned company/PSU/account scope is rechecked |
| N07 — full paging | Every required page is captured; repeats, gaps, invalid counters, failed middle pages and configured caps prevent full publication |
| N08 — identity replay | Equal-looking rows with distinct verified IDs survive; unchanged replay preserves logical rows; changed IDs are not guessed to be equivalent |
| N09 — correction/status | Pending rows cannot enter approval; booked corrections and reversals preserve history according to the confirmed linkage |
| N10 — money/date | Original precision and supported currency/direction are validated; both source dates/offsets survive; D06 controls comparison |
| N11 — balance semantics | Distinct balance types and source timestamps remain distinct; movements are not labeled available balance |
| N12 — failure/revocation | Credential/consent loss pauses reads; bounded retry uses one total budget; failed collection retains the previous accepted state |
| N13 — coverage/removal | Partial/delta/narrowed/uncertain data never proves disappearance; only agreed authoritative equivalent coverage may remove a current member |

These extend AC12–18 and the durable-processing checks after their dependencies exist. The current [INeotekClient declaration](../../src/Dike.Core/Models.cs) is still only an interface; this review adds no functioning Neotek adapter.

## D05 decision and exit record

| Record | Current value |
| --- | --- |
| Documentation supplied by user | ReadMe portal above; inspected |
| Working product from draft | Single API; actual subscription unconfirmed |
| Named integration owner / provider contact | Unassigned / not supplied |
| App environment / subscribed products / corporate-bank access | Not supplied |
| Accepted schema version/digest and N01–N05 resolutions | Pending |
| Authorized account/link/profile and consent evidence | Pending |
| Pagination/history/ID/correction evidence | Documentation partially established; real evidence pending |
| Cadence/quota/retention choices | Pending |
| AC14/15/16 / T04 exit | Not accepted / incomplete |

Next: obtain the customer's versioned API schema and subscription details, resolve the five differences with that contract, then collect an authorized representative response set and owner decision. Keep credentials in the application/approved secret store. T05/T06 preparation can proceed independently while these inputs wait.
