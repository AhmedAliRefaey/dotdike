# NetSuite GL validation preparation

This local .NET command compares a complete extract with a separately prepared reference report. It makes no network or database calls and grants no finance approval. See [T02's live evidence checklist](../../docs/delivery/T02_NETSUITE_VALIDATION.md).

Run from the solution root:

```powershell
dotnet build Dike.slnx -p:UseAppHost=false
New-Item -ItemType Directory -Path artifacts/gl-validation -Force
dotnet tools/Dike.NetSuiteValidation/bin/Debug/net10.0/Dike.NetSuiteValidation.dll contract artifacts/gl-validation/query-contract.json
dotnet tools/Dike.NetSuiteValidation/bin/Debug/net10.0/Dike.NetSuiteValidation.dll compare tools/Dike.NetSuiteValidation/examples/extract.synthetic.json tools/Dike.NetSuiteValidation/examples/reference.synthetic.json artifacts/gl-validation/synthetic-comparison.json
```

Output paths must be new. Existing reports, inputs and contracts are never overwritten. On repeat runs choose a new report filename. Exit codes: `0` = contract written or input comparison matched; `1` = a comparison issue was found; `2` = invalid JSON/usage, inaccessible/oversize input or an output already exists. A successful comparison still records `AwaitingLiveValidationAndFinanceReview`.

The [extract](examples/extract.synthetic.json) and [reference](examples/reference.synthetic.json) examples contain synthetic data: two rows, SAR 125.50 debit, SAR 25.50 credit, SAR 100.00 signed movement. They are not financial statements or provider evidence.

## Input contract

Both files use the same explicit JSON envelope:

- `CompanyId`, `ConnectionId`: required opaque strings declaring the same trusted company/connection. These declarations do not authorize access or prove source authenticity.
- `Complete`: required boolean. Both must be true; the operator must independently verify full report/extract coverage.
- `Data`: the [GlExtract](../../src/Dike.Infrastructure/NetSuite/NetSuiteReader.cs) shape. For the reference, `RunId` identifies the independent report and `FetchedAt` is its capture time. Never replace an unknown provider as-of time with this capture time.
- `AccountId`, `SubsidiaryId`, `AccountingBookId`, `Currency`, inclusive `From`/`To`, and `Lines` must all be present. Dates are ISO `yyyy-MM-dd`; capture time is UTC. IDs remain strings. Only SAR is supported.
- Every line requires transaction/line/book/account/subsidiary IDs, date, reference (may be empty but not null), debit and credit. All rows must fit the declared scope/period. No grouping, rounding, deduplication or sign conversion is performed.
- Debit/credit are JSON numbers using decimal notation, with no exponent, quoted value or thousands separator. Excess nonzero fractional precision and decimal(19,2) overflow fail before parsing can round. Trailing zeroes are accepted. Signed values are calculated as debit minus credit; totals may exceed a single-row money bound.
- The extract must carry the recorded `QueryVersion`, `IdentityVersion` and `QuerySha256` from collection. Old extracts remain readable in the application, but cannot pass this gate without their actual historical provenance. Do not fill in missing provenance from today's contract.

UTF-8 with/without a BOM is accepted. Inputs are limited to 64 MiB each. Missing constructor fields, unknown fields, duplicate JSON properties, null money, duplicate identities and partial coverage fail. Candidate identity comparison uses structured components, including subsidiary, within equal company/connection scope; this does not establish a finance-approved canonical key.

Input order may differ. The tool compares every keyed row's date, reference, debit and credit, then compares counts and debit/credit/signed totals. Same net amount with different debit/credit facts fails. Complete empty periods may match. Same-looking transactions with distinct keys remain separate.

## Output and evidence limits

The JSON report includes hashes of the exact input bytes, query/key versions, query digest, totals and issue codes with 1-based input row positions. It does not print raw rows, tokens or secret values. Preserve input files and the original report in approved private evidence storage; reports contain financial totals and hashes alone do not make a file authentic or protected.

This tool does not parse arbitrary bank CSVs or NetSuite report layouts. Map the actual independent report into this format only after finance verifies its columns, IDs and filters; retain the original and conversion method. If line IDs cannot be obtained, report the evidence gap instead of generating them from row order/amount/date.

The current web UI shows only 500 preview rows and has no protected full-extract download route yet. Never use that preview as a complete input. T12/T16 provide controlled retrieval; an authorized integration harness can also wrap a full returned GlExtract. This tool does not decrypt the application's credential/state files or fetch provider data.
