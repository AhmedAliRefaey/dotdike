# Odoo synthetic AP posting and GL test — 17 November 2026

At the user's explicit request, created and posted six synthetic vendor bills and four manual payment journal entries in **NApp**, company **1**, at `https://napp.odoo.com`. The user approved posting the bills and the manual payment entries after reviewing their totals and bank-account scope. All amounts are in **SAR**. The vendor and every reference are labeled `DIKE TEST` / `DIKE-AP-TEST-2026`.

The initial November samples for bank accounts **118** and **119** returned zero visible posted rows. After the authorized postings, all four October/November bank samples returned the expected synthetic entries.

## Posted bills

References below have prefix `DIKE-AP-TEST-2026-`. All six moves are posted.

| Reference suffix | Move ID | Invoice/accounting date | Quantity × unit price | Total SAR | Applied payment SAR | Remaining SAR | Verified payment state |
| --- | --- | --- | --- | ---: | ---: | ---: | --- |
| OCT-01 | 1 | 2026-10-06 | 3 × 125.50 supplies + 1 × 75.00 delivery | 451.50 | 451.50 | 0.00 | paid |
| OCT-02 | 3 | 2026-10-20 | 2 × 450.00 services | 900.00 | 450.00 | 450.00 | partial |
| OCT-03 | 5 | 2026-10-28 | 1 × 1,250.00 maintenance | 1,250.00 | 0.00 | 1,250.00 | not_paid |
| NOV-01 | 6 | 2026-11-03 | 4 × 225.00 goods | 900.00 | 900.00 | 0.00 | paid |
| NOV-02 | 8 | 2026-11-10 | 2 × 375.75 training | 751.50 | 400.00 | 351.50 | partial |
| NOV-03 | 10 | 2026-11-16 | 3 × 500.00 software | 1,500.00 | 0.00 | 1,500.00 | not_paid |
| **Total** | | | | **5,753.00** | **2,201.50** | **3,551.50** | |

These fixtures exercise multiple invoice lines, varying dates/quantities/prices, fractional SAR values, equal bill totals with distinct identities, and fully paid/partially paid/unpaid balances. Taxes were explicitly empty for this synthetic validation batch.

## Posted manual payment entries and GL verification

Payment references append `-PAY` to the corresponding bill reference. They are accounting journal entries, not `account.payment` records or external bank submissions. Each debits AP account **148 / 220000 Payables**, credits the chosen bank GL and is reconciled against the bill's payable line.

| Payment move ID | Date | Bank GL | Amount SAR | Returned GL item ID | Observed debit / credit / balance |
| ---: | --- | ---: | ---: | ---: | --- |
| 2 | 2026-10-08 | 118 — Main Bank Account | 451.50 | 5 | 0.00 / 451.50 / -451.50 |
| 4 | 2026-10-23 | 119 — Main SAR Operating Account | 450.00 | 9 | 0.00 / 450.00 / -450.00 |
| 7 | 2026-11-04 | 118 — Main Bank Account | 900.00 | 15 | 0.00 / 900.00 / -900.00 |
| 9 | 2026-11-12 | 119 — Main SAR Operating Account | 400.00 | 19 | 0.00 / 400.00 / -400.00 |

Bills use purchase journal **10 / Purchases** and expense account **164 / 400400 Raw materials** as the existing synthetic expense target. Payment entries use general journal **11 / Miscellaneous Operations** with explicit bank and payable lines. No real vendor, account or journal configuration was changed. The API user ID observed was **2**; its dedicated-service-user status and broader privilege scope have not been certified.

## Evidence and remaining limits

- [Posting manifest](../../artifacts/odoo-ap-testdata-4706ebedeeed4d95a9d4300f33bef31f.json): created/reused IDs, observed posted states, totals, residuals and payment states. The original run created the records listed above.
- [Live monthly sample results](../../artifacts/odoo-readonly-20261117/live-samples.json): four successful account/month combinations, one returned item each, `HasMore: false`, `SampleOnly: true`.
- [Executable check log](../../artifacts/odoo-readonly-20261117/checks.log): **221 passing checks**, including **63 Odoo fixtures** and the prior 158 regressions. Seven-project build: zero warnings/errors. CLI help, malformed arguments and shared-secret identifier also checked.
- [Read-only tool guide](../../tools/Dike.OdooValidation/README.md): interactive company and loaded-account selection, weekly/monthly/custom dates and bounded row counts.

The separate [seed script](../../tools/seed-odoo-ap-test-data.ps1) defaults to a read-only plan. `-Apply -IncludePayments` was used for the explicitly approved batch. It checks company/currency/lock dates, validates existing synthetic references before reuse, and writes a progress manifest. Repeat-run duplicate prevention is implemented but was not exercised by posting the batch twice. Interrupted or conflicting runs require inspection of their manifest; the script does not automatically delete or reverse posted entries.

This proves live read access and the expected retrieval of a deliberately created small fixture batch. It does **not** prove complete historical coverage, immutable source identity, an independent Odoo GL report match or finance acceptance. T42 remains in progress.
