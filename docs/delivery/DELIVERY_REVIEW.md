# Delivery file review — 16 November 2026

Scope: all seven files present in [delivery](.) at the start of this review, checked against the relevant source code, draft requirements and local verification. This is a direct consistency/code review, not an independent EGM review, live-provider validation or finance approval.

## File-by-file result

| File | Review result |
| --- | --- |
| [Resume checkpoint](CHECKPOINT.md) | Status distinctions were accurate. Updated current counts to 81 executable checks, recorded the money fix and T04 research, and replaced stale continuation steps. |
| [T01 baseline](T01_BASELINE.md) | Its five-project/31-check inventory is explicitly historical and remains valid to that scope. Preserved unchanged; the reader money gaps identified there are addressed by this review's bounded fix below. |
| [T01 manifest](T01_SOURCE_MANIFEST.json) | Valid JSON with 59 unique file paths and valid SHA-256 strings. All listed workspace files exist. At review entry, 52 matched and seven differed as expected after T02/documentation work. Preserved unchanged. |
| [T02 candidate contract](T02_CANDIDATE_CONTRACT.json) | Matches a fresh output of the compiled generator. Independently recomputed SQL digest matches the recorded value. Candidate/unverified status retained; query and key versions were not changed. |
| [T02 validation pack](T02_NETSUITE_VALIDATION.md) | Correctly separates input agreement from live approval. Updated verification for the reader fix; actual report/scope/Records Catalog/reviewer remain absent. |
| [T03 CSV pack](T03_CSV_CONTRACT_REVIEW.md) | All 18 example IDs are unique. Identity/replacement/date rules match the draft; cases remain labeled synthetic and unexecuted. D04/D06 and the representative sample remain pending. No arbitrary external headers or date policy selected. |
| [Task board](TASKS.md) | T01–T41 appear once with their dependencies and external gates. Updated T04 to in progress with linked documentation evidence. T20's dependency gate stays pending despite independent monetary hardening of the existing reader. |

The seven baseline differences at entry were the solution, root README, NetSuite connector guide, project status, NetSuite reader, connector checks and test runner. They correspond to the recorded T02/continuation work, not missing baseline files. Later files and fixes are not retroactively added to T01's historical manifest.

## Findings and actions

### R01 — high: source precision could be lost before validation — fixed

The existing reader converted source text to a .NET decimal before checking two-decimal precision. The value `125.50000000000000000000000000000001` could therefore become `125.50` and be accepted. The offline validator already checked the original digits; the reader did not share that protection.

Added a fixture regression first. It failed against the original implementation with `Expected rejection: GL reader rejects precision hidden by decimal rounding`. The fix shares [exact money parsing](../../src/Dike.Infrastructure/NetSuite/NetSuiteMoney.cs) between the [reader](../../src/Dike.Infrastructure/NetSuite/NetSuiteReader.cs) and [offline JSON parser](../../src/Dike.Infrastructure/NetSuite/GlValidationJson.cs), rejecting nonzero excess precision before conversion. Tiny nonzero amounts cannot silently become zero. Exact trailing zeros remain accepted.

### R02 — high: accepted source money could exceed the finance range — fixed

The reader accepted values outside decimal(19,2), and individually valid debit/credit values could produce an out-of-range signed movement. It now validates each original amount and the debit-minus-credit result before returning an extract. Positive and negative limits are covered. The offline comparison shares the same bound through its existing public constant.

Ten new [connector checks](../../tests/Dike.Tests/NetSuiteChecks.cs) cover rounding, tiny values, raw numeric JSON, both overflow directions, signed overflow, trailing zeros and exact boundary values. These fixes depend on the existing monetary requirement, not an unapproved source identity or finance schema. They do not complete T20 or prove SQL publication.

### R03 — unresolved provider contract differences — recorded for T04

The user supplied Neotek's developer portal during this review. Relevant guides and expanded transaction/balance/consent schemas are readable. They provide useful endpoint and field evidence but differ internally and from the public documentation on scopes, consent payloads, status enums and profile association. The [T04 contract review](T04_NEOTEK_CONTRACT_REVIEW.md) records the sources, five differences, known capabilities and evidence still required. No provider version or live capability is silently selected.

## Verification

- Full six-project solution build: zero warnings/errors.
- Custom executable runner: **81 PASS checks** — 11 domain, 31 connector, 39 offline validation. The new rounding regression failed before the fix and passes after it.
- NetSuite validation CLI: **10 PASS checks**, including precision rejection and overwrite protection.
- Saved candidate contract: generated content and independently calculated SQL digest agree.
- Delivery Markdown links resolve; source manifest JSON/paths/hashes and all 41 task IDs were checked.
- Editor diagnostics report no errors in the changed implementation.

No live NetSuite/Neotek request, customer consent, SQL migration, production data change or finance acceptance occurred. The 19 historical web smoke checks were not repeated: this change is tested at the connector/CLI boundary and does not alter web routes or Identity behavior.

## Continuation

T04 now has documented candidate endpoints, consent/profile flow, source comparisons and a planned validation matrix. D05 still requires the actual subscribed version, corporate-bank access, pagination/correction evidence and integration-owner decision. T02/T03 continue to wait for their actual finance/CSV inputs. T05/T06 are the next independent decision-preparation tasks; the [checkpoint](CHECKPOINT.md) gives the resume order.
