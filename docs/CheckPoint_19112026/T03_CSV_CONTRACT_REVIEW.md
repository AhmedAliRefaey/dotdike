# T03 — CSV identity, coverage and comparison-date review

Status: **In progress; review preparation complete, representative sample and finance decisions pending.** Updated 16 November 2026. No CSV file was found in the workspace during this continuation, and no sample path has been supplied. D04/D06 remain open. This pack does not define an approved bank export format or implement the T22 importer.

T03's exit condition is a reviewed sample and recorded D04/D06 choices with duplicate-looking-row and replacement examples. Requirements come from the [design draft](../../design_draft.md), sections 7.1–7.2, 8.3–8.4, 13.2 and 15.2. Delivery dependencies remain on the [task board](TASKS.md).

## Evidence to collect

| Item | Required evidence | Current state |
| --- | --- | --- |
| Bank and export | Bank/product, export name/version and how the file was generated; include any selected filters | Not supplied |
| Representative statement | Original file location, SHA-256, byte length, capture time and finance owner | Not supplied |
| Scope | Trusted company, bank connection, external account, SAR currency and selected mapping/version | Not selected |
| Period | Inclusive From/To, the source field used for filtering and whether the export contains every booked movement in that scope | Unknown |
| Replay evidence | A second unchanged export and a reordered/reformatted export of the same statement, where supported | Not supplied |
| Corrections/replacements | Same-scope corrected statement, narrowed export and legitimate equal-looking rows; provider explanation of their identities | Not supplied |
| Date evidence | Booking and value fields, offsets/timezone, date formats and examples around midnight and Friday/Saturday | Not supplied |
| Reviewer | Named finance/provider owner for D04 and finance owner for D06 | Unassigned |

Keep the original statement in approved protected storage. Record a reference and digest here; do not paste account data or transaction contents into the repository. A redacted sample must preserve relevant ID relationships, duplicate-looking rows, formatting and date/amount semantics. Keep its relationship to the protected original explicit.

## Field and file-format worksheet

Fill this from the representative export. The normalized concepts below are requirements to map, not invented CSV headers.

| Concept | Mapping or rule to record |
| --- | --- |
| File grammar | Encoding/BOM, delimiter, quote escaping, embedded delimiters/newlines, header location and required/optional columns |
| Layout/version | Preamble, footer/totals, blank-row rules, version detection and behavior for unknown or duplicate headers |
| Account | Exact external account field or authoritative statement metadata; preserve identifiers as strings, including leading zeros |
| Transaction identity | Stable provider transaction ID, or the approved statement/line identity under D04; document uniqueness scope and reuse behavior |
| Booking date/time | Exact source field and accepted format; whether it is a date, an offset timestamp or a local timestamp |
| Value date/time | Exact field, optionality, accepted format and whether it can change after booking |
| Amount/direction | Signed amount, direction plus magnitude, or separate debit/credit columns; exact decimal/group separators and treatment of blanks/parentheses |
| Cash convention | Map documented inflows to positive and outflows to negative exactly once; specify contradictory sign/direction rejection |
| Money limits | SAR decimal(19,2), maximum magnitude 99,999,999,999,999,999.99; reject overflow or excess nonzero fractional digits without rounding |
| Currency | Exact field or authoritative account-level evidence; reject unsupported or contradictory currencies |
| Status | Document booked/pending/reversed states, or evidence that the export contains booked movements only; do not invent a missing status |
| Reference/description | Preserve original text; document any matching normalization separately from identity |
| Diagnostics | Physical line/logical record numbering for quoted multiline records; row positions are diagnostic locations, not financial identities |
| Limits | Agreed maximum file bytes, records and field lengths; reject over-limit content rather than truncate |
| Completeness | Source declaration and evidence for full-period coverage, partial/delta coverage or unknown coverage |

Company and mapping selection must come from an authorized upload route. A CSV company identifier cannot confer access. Validate the entire file before any publication; failed rows must not leave a partial current snapshot. Upload/evidence enforcement belongs to T12/T22/T23.

## D04 — identity and completeness

**Decision state: Open. Owner: finance/provider role; named person unassigned.** Neither option is selected.

| Candidate | Evidence needed | Failure condition |
| --- | --- | --- |
| A — stable bank transaction IDs | IDs remain stable across replay, reordered exports, overlapping periods and corrections; scope includes company, connection and external account | Missing, reused or changing IDs without documented linkage |
| B — authoritative statement and stable line identity | Statement identity, line identity that survives formatting/order changes, statement revision lineage, and explicit overlap/replacement rules | Physical row numbers masquerade as stable IDs, or overlapping statements cannot link the same movement |

An amount/date/reference hash cannot distinguish two genuine equal-looking transactions. A raw file digest identifies file bytes; it does not establish canonical transaction identity. Neither substitutes for A or B. A mapping ID/version must not become the canonical source identity because remapping must not make the same movement new.

If the real export supports neither defensible option, record the unsupported capability and obtain a suitable export/provider contract. Do not silently drop duplicate-looking rows or claim replay/removal safety.

### Synthetic identity and replacement cases for review

The IDs, values and outcomes below illustrate required behavior. They are not bank samples, executed importer tests or finance approval. Scope is one synthetic company/connection/account, SAR, 1–30 November 2026 unless a case states otherwise.

| Case | Inputs | Expected result to confirm against the chosen contract |
| --- | --- | --- |
| C01 — genuine equal-looking rows | IDs A and B both have 5 November, +100.00 and reference R1 | Preserve both logical movements: count 2, total +200.00. A matcher may find ambiguity; ingestion must not collapse them. |
| C02 — identical-file replay | Upload the same complete file twice | Record operational replay evidence as needed; no extra logical movements or unchanged-review invalidation. File digest alone does not define future identity. |
| C03 — reordered export | The same A and B appear in reverse order, with different quoting or line endings | If identities and normalized financial facts are unchanged, retain the same logical movements and revisions despite a different file digest. |
| C04 — duplicate canonical ID | The same scoped ID A occurs twice in one input, with equal or conflicting facts | Reject the ambiguous input before publication. Do not keep only the first/last occurrence. |
| C05 — correction and return to prior facts | ID A changes +100.00 → +120.00 → +100.00 across accepted observations | One logical movement, three successive immutable revisions. Reusing the first content hash must not erase the intervening history. |
| C06 — equivalent full replacement | Accepted full scope contains A and B; a later proven complete equivalent scope contains only A | B may be marked removed while its identity/history survive. Publication and affected-review invalidation must be atomic. |
| C07 — partial/narrowed/failed refresh | A-only input is filtered, delta, truncated, failed, or covers 1–15 November | Absence does not prove B was removed. Do not claim full-period coverage or advance unqueried coverage. |
| C08 — complete empty statement | An accepted, authoritative equivalent full scope explicitly contains zero movements | It can establish an empty current scope; zero parsed rows without completeness evidence cannot. |
| C09 — changed ID/reversal | A becomes ID C, or a separate -100.00 reversal appears | Keep separate identity unless verified replacement linkage exists. A reversal is its own posting and does not delete A. |
| C10 — overlapping statements without bank IDs | Statement S1 covers 1–30 November and S2 covers 15–30 November with equal-looking rows | Option B needs proven cross-statement linkage or an approved restriction that prevents ambiguous overlap. Do not infer identity from dates, amounts or row positions. |
| C11 — remapping or foreign scope | Same external ID appears after a mapping change, or in a different account/company | Remapping preserves the canonical movement within its source scope. Different company/connection/account scopes remain distinct and authorized separately. |

Equivalent coverage must establish the same canonical company/connection/account, currency, period/filter semantics, booked-status population and compatible contract version. Changed filters, uncertain completeness or a changed contract require review; they are not authority to remove previously observed movements. Preserve the previous current state on failure.

## D06 — comparison date

**Decision state: Open. Owner: finance role; named person unassigned.** The operator's local timezone and the proposed UI display zone do not select a source accounting rule.

| Candidate | Evidence needed | Tradeoff / failure condition |
| --- | --- | --- |
| A — booking date | Finance accepts booking-date comparison; source timezone/date semantics are documented | Aligns booked activity but can differ from value-date statements |
| B — value date | Finance accepts value-date comparison; the field and its correction behavior are documented | Aligns that statement but fails when value date is absent/unstable without an explicit approved rule |

Record the chosen field, timestamp-to-date conversion, account/source timezone identifier and version, and missing/invalid-value behavior. Preserve both original source fields and supplied offsets. For date-only fields, retain the calendar date without an implicit UTC conversion. A naive timestamp requires documented source timezone semantics, including any daylight-saving ambiguity handling.

No fallback is approved. Specify whether missing data rejects the row/run or uses a named fallback, with examples and a versioned rule. Keep the statement coverage filter field separate from the matching comparison-date field; they can have different meanings.

### Synthetic date cases for review

| Case | Input | Outcome to record |
| --- | --- | --- |
| C12 — midnight boundary | Booking instant 2026-11-05T21:30:00Z; **hypothetical** source timezone Asia/Riyadh | Source-local date is 6 November; UTC date is 5 November. Finance must choose the actual source rule. This example does not assign the pilot timezone. |
| C13 — booking versus value | Booking date Friday 6 November; value date Sunday 8 November; book date Thursday 5 November | The retained sar-v1/sar-v2 calendar counts 0 business days to the booking date and 1 to the value date. Preserve both and record which comparison is intended. |
| C14 — ambiguous text date | Source contains 05/11/2026 | Accept only the documented exact format; do not guess from operating-system culture. |
| C15 — missing chosen field | Value-date policy selected but value date absent | Apply only the recorded fallback/rejection rule; never substitute booking date silently. |
| C16 — source-local timestamp | Timestamp has no UTC offset, or falls in an ambiguous/nonexistent local clock interval | Require source timezone and documented resolution/rejection semantics; do not use the server timezone. |
| C17 — corrected comparison date | Same stable transaction ID later has a changed material date | Preserve a new immutable revision and invalidate affected active decisions during publication. |
| C18 — period boundary | Requested inclusive date range meets a timestamp-based source filter | Document exact boundary conversion and ensure adjacent ranges neither omit nor double-count a boundary movement. |

The existing matcher counts Sunday–Thursday, earlier date exclusive and later date inclusive, and has no holiday calendar. T03 chooses the source comparison date; it does not change that algorithm or establish AC20 acceptance.

## Review and acceptance record

| Record | Value |
| --- | --- |
| Protected original sample reference / SHA-256 | Pending |
| Bank/export/format version / scope / selected period | Pending |
| Field/file-format worksheet and version | Pending |
| D04 choice / identity fields and scope / supporting provider evidence | Open / pending |
| Complete, delta and replacement declarations / equivalence rules | Pending |
| Real duplicate-looking/replay/overlap/replacement examples and results | Pending |
| D06 choice / timezone / fallback / filtering semantics | Open / pending |
| Real date-boundary examples and expected outcomes | Pending |
| Finance/provider owner / finance reviewer / review date | Unassigned / unassigned / pending |
| Unresolved or unsupported capabilities | No representative contract available for assessment |
| T03 exit | Incomplete — sample and D04/D06 decisions required |

When a sample is available: preserve/hash it, complete the format worksheet, gather repeat/replacement evidence, and record named D04/D06 decisions against actual examples. Version the approved contract before T10/T22/T23 use it. Later implementation must test AC12/13/17/18 and the relevant date boundaries; this review pack provides no acceptance pass for those cases.

## Preparation verification

- Checked the pack against the source backlog's T03 done criterion and draft sections cited above.
- Included genuine duplicate-looking rows, identical/reformatted replay, correction history, complete versus partial replacement, overlapping statements and source-date boundaries.
- No bank header, identity option, timezone, completeness declaration or owner approval is represented as selected.
- No CSV parser, production configuration, finance schema or source data was changed for this preparation.
