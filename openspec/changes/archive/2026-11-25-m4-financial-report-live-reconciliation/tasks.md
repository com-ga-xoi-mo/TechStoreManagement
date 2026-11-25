# Tasks

## 1. Establish the integrated test baseline

- [x] 1.1 Record the integrated API SHA, worktree state, PostgreSQL version, and available owner workflows. Create `FinancialReportLiveReconciliationPostgresHttpTests` with `PostgresFact` and `IdentityPostgresApi`. Run it with `TECHSTORE_TEST_CONNECTION` and verify it uses a migrated, generated Npgsql database and real JWT clients. Record the command in `docs/M4_FINANCIAL_REPORT.md`.
- [x] 1.2 Take Manager's pre-activity snapshot for a valid UTC+07 range and capture the read-back inputs. Run `DashboardLiveFlowPostgresHttpTests` unchanged and verify the October 2026 fixture remains intact. Verify Admin/Manager 200, SalesStaff/WarehouseStaff 403, missing or invalid JWT 401 and invalid period 400. Record the baseline and access evidence.

## 2. Reconcile the Cash workflow

- [x] 2.1 Receive the two test items through the Warehouse API and sell them through Cash checkout. Verify that persisted price, order discount and VAT allocations, original costs, receipt, stock and shift match the D2 Expected row. Verify the 18-point accrual from the customer and loyalty records. Add the committed IDs and Expected/Actual to the evidence document.
- [x] 2.2 Settle the first VND 990,000 refund, restock the item after the UTC+07 day boundary, then settle the final VND 990,000 refund and inspect the other item as defective. Verify both tax allocations, both nine-point reversals, the payout and restock dates, sellable quantities, and the absence of a cost reversal for the defective item. Record Expected/Actual for the daily rows, cumulative checkpoints and full period after Manager's final read. Verify the known daily sums and a zero-activity date.
- [x] 2.3 Compare `cashByShift` with both cash ledgers, expected and actual closing counts, and the persisted refund shift IDs. In separate tracked-device and voucher cases, read back the defective IMEI, voucher consumption or restoration, and balances under the owners' existing rules. Verify a defective IMEI cannot be sold and record any mismatch or unavailable workflow with the owner and rerun condition.

## 3. Reconcile after-sales, QR and failure scenarios

- [x] 3.1 Exercise QR sale confirmation and refund through the integrated workflow that is available. Verify bank and payout dates and amounts, unchanged drawer cash, and pending, expired and replayed events. Identify sandbox/provider, manual confirmation or reconciliation, or webhook in the evidence. Record a missing receipt path or callback as **Not verified** with Đạt and the condition to rerun. Label any stored confirmation case as Analytics fixture coverage.
- [x] 3.2 Fully refund a paid undelivered order and verify receipts, payout and tax with zero sales or cost reversal. Run a separate warranty replacement and repair handback, verifying IMEI and stock history, linked movement and issue-day cost, and warranty expense outside gross profit. Record API and read-back results with owners for any unavailable case.
- [x] 3.3 Replay the supported operation identifiers and compare the committed IDs, owner balances, and report data. Exercise rejected requests and a real transaction rollback. Verify rejected and rolled-back operations produce no extra side effects and record the response and observed failure boundary in the evidence document.
- [x] 3.4 Run the return/warranty dispute for one IMEI, checkout on the final sellable unit and the final voucher slot when available. Record distinct PostgreSQL backend PIDs, target locks and overlapping transaction intervals before checking outcomes and report deltas. Require observed overlap for **Pass**. Document missing overlap or dependencies with owners and rerun conditions.

## 4. Verify desktop and AI against the API

- [ ] 4.1 Run the report on Skia and Windows native where available. Compare the same range, `asOf`, daily and period values, and VND format with the captured API response. Check role visibility, logout or account replacement during load and retry after a connection failure. Record both target results, build SHAs and any runtime blockers in the UI evidence section.
- [ ] 4.2 Run the assistant against the live report with all four roles. Verify allowed tool results against the captured API values and verify restricted roles receive no financial figures. Record the prompt, tool trace, expected and actual result, status and SHA in the AI evidence section. Record missing integration with T. Vinh and the setup needed to rerun.
- [ ] 4.3 Run the new class and the existing Dashboard test with the PostgreSQL connection configured. Verify that `docs/M4_FINANCIAL_REPORT.md` contains English evidence for API/PostgreSQL, UI and AI, with commands, inputs, build identifiers, statuses, owners and rerun conditions. Review the diff for scope, unchanged owner tests and seed, and absence of temporary files. Check any commit message for an AI `Co-Authored-By` trailer and omit it.
