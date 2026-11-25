# Tasks

## 1. Confirm the sources and implementation baseline

- [x] 1.1 Check the latest `origin/dev` and confirm the implementation branch contains both the sales change and the migrations for after-sales sources. Update the handoff status from D1 and record the source and build SHAs in the new section of `docs/M4_FINANCIAL_REPORT.md`.

## 2. Refunds, physical restocks, and warranty in Analytics

- [x] 2.1 Aggregate paid refund lines by `RefundedAt`. Report merchandise, tax, and payout amounts separately and distinguish refunds before delivery. Verify over HTTP on PostgreSQL for partial and subsequent full refunds, a full refund of a `Processing` order, pending and failed cases, missing legacy allocations, and a paid line split after inspection. Record expected and actual results in the verification document.
- [x] 2.2 Calculate `restockedCost` from positive `CustomerReturn` movements linked to a return line. Verify a restock on a different Vietnam date from the refund across the 17:00Z boundary, partial restocking, defective or repair-pending items, a flag without a movement, known zero or missing costs, and replayed inspections. Record the results in the verification document.
- [x] 2.3 Calculate `warrantyCost` separately from the replacement device cost snapshot and the issue movement timestamp. Read the report after using the real warranty flow. Verify that a replay produces one replacement charge, receiving a device for repair or returning the original device contributes zero replacement costs, missing cost returns `null`, and gross profit is independent. Record the results or the rerun conditions with Q. Vinh.

## 3. Cash flow by channel and shift, and the API contract

- [x] 3.1 Group cash receipts by selling shift and cash refunds by payout and paying shift. Report confirmed QR transactions separately using their supporting records. Test different shifts, complete and partial shift coverage, opening cash plus receipts minus payouts against expected closing cash, replayed payouts, missing records, manually reconciled QR refunds, and pending QR transactions. Verify QR receipts with a confirmed fixture. If the end-to-end receipt or reconciliation flow is unavailable, record **Not tested**, name Đạt as owner, and list rerun criteria from D1.
- [x] 3.2 Combine separately aggregated datasets in one snapshot and map old and new fields according to D5. Test legacy and extended JSON, zero versus `null`, daily reconciliation with period totals when data is complete, negative net figures, the exclusive `asOf` cutoff, and days with only after-sales activity. Record the HTTP and PostgreSQL reconciliation results in the verification document.

## 4. Complete verification within Bình’s scope

- [x] 4.1 Run integration tests with available sources and the hand-calculated D6 fixture. Read the report again after replays and reconcile every figure, including period totals. Rerun regressions for Admin/Manager access, 401 and 403, invalid ranges returning 400, the current Dashboard, sales rankings, and cashier performance. Record build SHA, expected and actual results, status, and limitations in `docs/M4_FINANCIAL_REPORT.md` as the sole verification document. For each case that could not run through the real source, record **Not tested**, the owner, and the rerun conditions.
- [x] 4.2 Review the diff to verify implementation covers only Analytics, Bình’s report tests, and `docs/M4_FINANCIAL_REPORT.md`. Check for temporary scripts, logs, and Markdown files. If committing, verify the message contains no AI `Co-Authored-By` trailer.
