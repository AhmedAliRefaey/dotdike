# T02 — NetSuite query, identity and GL report validation

Status: **In progress; offline preparation complete, live/finance validation pending.** Updated 16 November 2026. The user confirmed that the reference report, selected scope/period and finance reviewer details are not currently available. AC08/09 and T02's done criterion remain open. See the [task board](TASKS.md).

## Prepared deliverables

The user-requested Odoo counterpart is tracked separately as [T42 — Odoo connection and GL validation](T42_ODOO_VALIDATION.md). It runs alongside this task and uses its own provider contract and acceptance cases.

1. [Candidate contract](T02_CANDIDATE_CONTRACT.json) records the exact existing scope/posted-GL SQL, parameter order, query/key versions and digest. [Contract constants](../../src/Dike.Infrastructure/NetSuite/NetSuiteQueryContract.cs) are shared with the reader. No query semantics or source key was changed or approved.
2. New [reader extracts](../../src/Dike.Infrastructure/NetSuite/NetSuiteReader.cs) retain candidate query/key versions and digest. Historical extracts deserialize with null provenance; no new history is invented.
3. [Offline comparison tool and examples](../../tools/Dike.NetSuiteValidation/README.md) compare all scoped identities, dates, references, debits, credits and totals; reject incomplete/invalid input; record input hashes and issue positions. Synthetic agreement always leaves acceptance pending.
4. [Comparison regression checks](../../tests/Dike.Tests/GlValidationChecks.cs) cover false-match and invalid-input cases. The original fixture reader and local identity flows remain separate evidence levels.

The candidate query version is `netsuite-posted-gl-candidate-v1`; the candidate identity version is `transaction-line-book-account-candidate-v1`. Query SHA-256 is `46C2F4A2B94DF3657E156682D0FE619C0E711F1789FF70D9B8C1777F689C6A9E`, calculated over UTF-8 scope SQL, one LF, and posted GL SQL. This identifies what needs review, not a verified NetSuite contract.

## Required inputs and owner record

| Input / responsibility | Current evidence | Needed to proceed |
| --- | --- | --- |
| Integration engineer | Unassigned | Named person with permission to inspect the selected account's Records Catalog and run read-only extracts. |
| Finance reviewer | Not provided | Named reviewer who owns the reference report and signs the comparison. |
| Company and NetSuite connection | Not selected for this gate | Trusted company/connection identifiers, NetSuite account/environment, integration role and permission evidence; credentials stay in the app/approved secret store. |
| Subsidiary / primary book / bank GL | Not provided | Actual IDs and account/subsidiary currency, bank type, active state and primary-book evidence. |
| Statement period | Not provided | Inclusive start/end, report date/filter semantics and extraction/report capture window. |
| Independent GL reference | Not available | Full detail with stable transaction/line/book/account/subsidiary IDs, dates/references, debits, credits, signed movement total, row count and original file digest. |
| Records Catalog / query contract | Candidate only | Actual fields, join cardinality, stable identity dimensions and supported REST parameter binding. |

No owner name, account identifier, reviewer signoff or live result is inferred from the synthetic examples.

## Live verification procedure

1. Record the selected scope, capture window, integration role and independently generated report filters. Confirm the report covers posted bank-GL movements in SAR for the primary book; distinguish movement totals from opening/closing account balances. Preserve the original reference report.
2. Verify the candidate SQL in the actual Records Catalog and authorized REST environment. Check `transaction`, `transactionline`, `transactionaccountingline`, account/subsidiary/currency/book fields, posting flags and joins. Confirm REST binding supports the query payload/parameter ordering used by the reader. Fixtures cannot establish any of these account-specific facts.
3. Prove the accounting-line key. The existing key uses transaction ID, transaction-line ID, book ID and GL account ID within company/connection/subsidiary scope. Check multiple accounting lines per transaction line, reversals, journals, bank transfers and duplicate-looking entries. If extra dimensions are necessary, record them and revise/version the contract before implementing T07/T20. Never use an amount/date hash or row ordinal as a substitute.
4. Validate account/scope eligibility using independent metadata/report evidence. The existing preflight checks bank/SAR/primary flags but does not prove every account/subsidiary/book association. Exercise inactive, non-bank, non-SAR, nonprimary and wrong-subsidiary cases. Distinguish deliberately rejected requests from accepted imports.
5. Collect every page for the selected period; record actual run ID, fetch time, query/key version and limits. The existing reader rejects the 100,000-row REST boundary; disjoint automatic partitions are T21. Document the remote API's concurrent-edit limitation even if page counts remain stable.
6. Convert the full independent report to the tool's normalized JSON with a reviewed, lossless mapping. Preserve the original and transformation version/digest. Missing line IDs, report-filter differences or unknown coverage block agreement; do not manufacture a complete declaration. Obtain the full extract through an authorized harness/retrieval path, never the 500-row preview.
7. Run the [comparison](../../tools/Dike.NetSuiteValidation/README.md). Review every issue and reconcile exact identities, dates, references, debits, credits and signed totals. A zero difference only demonstrates agreement between the supplied files.
8. Repeat extraction under an agreed stable window and compare identities/facts; distinguish a changed real posting from a replay defect. Retain run IDs and source evidence for both. SQL deduplication/publication is T10/T11/T20, not an outcome of this offline comparison.
9. Have the named finance reviewer record scope, report/extract hashes, query/key version, outcome, date and evidence location. Record unresolved differences or unsupported capabilities explicitly. Mark T02 done only when live query/key evidence and AC08/09 report/scope agreement are accepted.

## Review worksheet

| Item | Value / evidence |
| --- | --- |
| Company / connection / provider environment | Pending |
| Subsidiary / primary book / SAR bank GL | Pending |
| Period / report filters / comparison date | Pending |
| Records Catalog evidence and verified query/key version | Pending |
| Full original report artifact/digest | Pending |
| Extract run(s), capture times and query digest | Pending |
| Report row count / debit / credit / signed total | Pending |
| Extract row count / debit / credit / signed total | Pending |
| Identity/row differences and dispositions | Pending |
| Negative scope cases and replay evidence | Pending |
| Remote consistency/limit caveats | Pending account-specific evidence |
| Integration engineer / finance reviewer / review date | Unassigned / unassigned / pending |
| AC08 outcome / AC09 outcome / T02 exit | Awaiting live validation / awaiting live validation / incomplete |

## Local verification

- Full six-project solution builds with zero warnings/errors on .NET SDK 10.0.401. Restore required approved execution outside the sandbox because the existing NuGet configuration was not readable there.
- The runner passed 81 assertions during the money-validation review. The latest T06 continuation passes 126: 56 domain, 31 NetSuite connector and 39 offline-validation checks. NetSuite-specific coverage is unchanged. No live provider request or application finance migration was made; the separate SQL mechanism probe uses synthetic tables only. The earlier preparation checkpoint had 71 passing assertions.
- The [delivery review](DELIVERY_REVIEW.md) reproduced and fixed original-digit precision loss and decimal(19,2) range gaps in the existing reader, using a shared money validator. The solution rebuild passed with zero warnings/errors. The editor test tool does not discover this custom runner; checks were executed through its compiled DLL.
- All 10 [command-line smoke checks](../../tests/netsuite-validation-smoke.ps1) passed: candidate/pending-acceptance status, complete synthetic row/totals comparison, exact input hashes, equal-total row mismatch, precision-loss rejection, existing output/input/contract protection and unsupported-command exit status. No provider calls were made.
- The saved candidate contract matches a freshly generated contract and an independently recomputed SQL digest. Query/key versions and SQL remain unchanged and unverified against a live account.

These results validate preparation only. Finance SQL persistence, immutable raw-page evidence, protected downloads, provider verification and finance signoff retain their separate task gates.
