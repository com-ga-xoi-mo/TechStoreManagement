# Design

## Context

- The [Sprint 2 plan](../../../docs/MILESTONE_4_PLAN.md#sprint-2-tích-hợp-và-kiểm-tra-toàn-luồng) assigns Bình report totals, access, and shift reconciliation. The [sales API](../archive/2026-11-25-m4-financial-report-sales-api/design.md) dates original sales by delivery and receipts by payment. The [after-sales API](../archive/2026-11-25-m4-financial-report-refunds-restocks-warranty/design.md) dates refunds by payout, restocks by linked sellable inventory movements, and warranty replacement costs by issue movement. The [desktop change](../m4-financial-report-desktop/design.md) uses those values in Overview. Its Skia live theme check and Windows native runtime check still need evidence on the inspected worktree.
- [DashboardLiveFlowPostgresHttpTests](../../../tests/Backend.UnitTests/DashboardLiveFlowPostgresHttpTests.cs) uses `PostgresFact`, `IdentityPostgresApi`, a real ASP.NET TestServer and JWT clients, fixed event times, and independent `AsNoTracking` reads after commit. The existing change 2 tests cover Cash and manual QR refund reconciliation with a stored confirmed QR receipt fixture. The inspected Sales code exposes hold cancellation and expiration. A live QR receipt confirmation path and an AI report tool were not observed on this worktree. Recheck both against the SHA selected for the run.

## Goals / Non-Goals

**Goals** — Establish independent expected values, identify the persisted event behind each report amount, and make the evidence reproducible on an integrated build.

**Non-Goals** — Changes to production contracts, workflows, database schema, pricing or loyalty rules, report consumers, owner tests, or the October 2026 M3 fixture. Integrated workflows supply the committed data. A mismatch is a finding for its owner.

## Decisions

### D1. Real HTTP flows on an isolated PostgreSQL database

- Add `FinancialReportLiveReconciliationPostgresHttpTests` under `tests/Backend.UnitTests`. Reuse the shared `PostgresFact`, `TECHSTORE_TEST_CONNECTION`, and `IdentityPostgresApi.CreateAsync()` setup. Assert the generated database and Npgsql are in use. Use independent clients with role-specific JWTs, real DI, migrations, and committed data. The helper creates and drops its own database. Keep the M3 seed intact and run `DashboardLiveFlowPostgresHttpTests` as regression coverage.
- Use scenario-specific product, customer, voucher and device identifiers. Provision test inputs with existing APIs or established fixture setup before the baseline read. Drive purchase, payment, refund, inspections, stock changes, warranty, points and voucher outcomes through the owners' existing workflows. Read committed state through GET routes and fresh `AsNoTracking` contexts. Fixture setup must not insert the outcome rows under test or edit their timestamps to force a result.
- Use October 28–31, 2025 for the controlled financial case, as in the prior PostgreSQL run, to avoid overlapping the shared October 2026 seed. Use the existing `TimeProvider` seam for timestamped writes. Where needed, follow the Dashboard host's test clock pattern for the Analytics `asOf` query and delegate the data queries to the real repository. Keep the clock stable during each race and use a monotonic timer for overlap evidence. UI and AI can use a separate live run with equivalent inputs and actual dates if they cannot connect to the test host. Record that run's range and baseline.
- Have Manager capture a valid pre-activity report for D1 before receiving or selling. Take matching-window before/after reads as dates become valid in UTC+07. Set `asOf` after committed activity for each comparison. The API rejects future end dates, so query the full D1–D4 range after D4. Compare both one-day requests and the full response's daily rows. Compare data fields after replay while allowing `asOf` to advance.

### D2. Hand-calculated Cash expectations and physical restock timing

Receive two items at VND 600,000 each through the purchase receipt API. The cashier sells both for VND 1,000,000 each with a VND 200,000 order discount and 10% VAT. Expect VND 1,800,000 sales excluding tax, VND 180,000 VAT, VND 1,980,000 collected and 18 loyalty points for the test customer. Tender VND 2,000,000 and expect VND 20,000 change. Read the persisted order discount allocations, tax and cost before comparison. Manager settles each partial quantity through the real refund endpoint. Each payout is VND 990,000, made up of VND 900,000 net and VND 90,000 VAT, with VND 100,000 of original discount allocation and nine points reversed once.

Refund the first item at `2025-10-29T16:55:00Z`. Warehouse inspects and returns it to sellable stock at `2025-10-29T17:05:00Z`, on the next Vietnam calendar day. Refund the second item on October 31 and inspect it as defective. Read the actual good-stock movement and verify the saleable balance only increased by one. Use a separate tracked device to check the defective IMEI cannot be sold. Add a day with zero activity as a separate report check. All values below are VND and exclude the separate QR, warranty and pre-delivery scenarios.

| Date in UTC+07 | Sales excl. tax | Sales VAT | Receipts | Refund paid | Refund VAT | Sales reversed | Original cost | Restocked cost | Net sales | Net cost | Daily gross profit | Cumulative gross profit |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| D1 · Oct 28 | 1,800,000 | 180,000 | 1,980,000 | 0 | 0 | 0 | 1,200,000 | 0 | 1,800,000 | 1,200,000 | 600,000 | 600,000 |
| D2 · Oct 29 | 0 | 0 | 0 | 990,000 | 90,000 | 900,000 | 0 | 0 | -900,000 | 0 | -900,000 | -300,000 |
| D3 · Oct 30 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 600,000 | 0 | -600,000 | 600,000 | 300,000 |
| D4 · Oct 31 | 0 | 0 | 0 | 990,000 | 90,000 | 900,000 | 0 | 0 | -900,000 | 0 | -900,000 | -600,000 |
| Full period | 1,800,000 | 180,000 | 1,980,000 | 1,980,000 | 180,000 | 1,800,000 | 1,200,000 | 600,000 | 0 | 600,000 | -600,000 | -600,000 |

- Check the D1–D2 and D1–D3 subtotals independently to reproduce the plan's gross profit of -300,000 before restock and 300,000 after restock. Check that each fully known period metric equals its daily sum. A missing source or cost retains `null` for affected amounts, independently of a date with a known zero. Keep the D1 delivered order, quantity, original gross profit and original ranking and cashier attribution after both refunds.
- Use separate selling and paying shifts. With starting cash of 2,000,000 for the seller and 3,000,000 for Manager, expected closing counts are 3,980,000 and 1,020,000 following both refunds. Check shift IDs, payouts, actual versus expected close, and report `cashByShift` (net 1,980,000 and -1,980,000). Read voucher use and release separately in a case that actually uses a voucher, applying the integrated owner's contract for consumption or restoration. The plan's manual discount example has no voucher redemption.

### D3. Isolated variants, replay and rollback

| Scenario | Expected observation and owner evidence |
| --- | --- |
| QR sale and refund | Drive receipt confirmation and refund through the available production routes in sandbox or approved test mode. Record sandbox/provider, manual receipt or payout reconciliation, or webhook separately, with verified times and references. Match receipts to `ConfirmedAt`, payouts to `RefundedAt`, and keep both outside drawer cash. Check pending or expired attempts and replay of the active confirmation path. A stored confirmation can provide a labeled Analytics fixture result. Đạt supplies receipt confirmation and replay or late-payment support and An supplies payout reconciliation. |
| Full refund before delivery | Use an order the live workflow leaves in `Processing`, then settle it without delivery. Verify recorded receipts and actual refunds, including their tax and channel, release of any reservation where applicable, zero sales reversal and zero sellable restock cost, and the existing points and voucher outcome. An and Đạt own the lifecycle, with Vy covering pricing, loyalty and vouchers. |
| Warranty replacement | Use a separate previously sold IMEI and a known-cost spare. Through Warranty and Inventory APIs, verify the old and new IMEI lifecycle, one linked `WarrantyReplacement` issue movement and its cost by day, and a separate warranty expense with gross profit stable. Verify a repair handback yields no replacement cost. Q. Vinh owns the warranty case and An owns inventory evidence. |
| Replay and failure | Replay the same receipt, checkout, refund, inspection and replacement operation identifiers where supported. Read the same period and committed IDs, balances and status after each replay. Test a changed key on sold stock, excessive refund or wrong IMEI, and a transactional failure with rollback evidence. Assert the documented response and absence of extra order, payout, movement, warranty, points or voucher effects. If a request fails before transaction entry, record rejection evidence only. Use an established test-only fault seam or real rollback case for the rollback assertion. |

The read-back record ties API IDs to stored order header and line allocations, `OrderReturn` net/tax/discount and payout time, ledger payouts and QR transactions, stock and reservations, IMEI statuses and movements, warranty cost, and loyalty/voucher history. Read each through its owner's APIs or persisted tables. Record the first divergence as pricing or loyalty (Vy), cash or QR receipt and shift (Đạt), returns or movements (An), warranty (Q. Vinh), or report grouping or presentation (Bình), with a concrete rerun condition.

### D4. Concurrent transactions require PostgreSQL overlap evidence

- Use two independent HTTP clients and request scopes on the same disposable database. Cover a same-IMEI return versus warranty, competing checkout for the last available item, and competing use of a final voucher slot when that workflow is integrated. Distinguish these from sequential retries. The existing [warranty/return test](../../../tests/Backend.UnitTests/PosCheckoutPostgresHttpTests.WarrantyReturns.cs) demonstrates holding a target row while contenders arrive. Use the real warranty route in the new acceptance class when available.
- Hold the relevant test row via a separate transaction and confirm both contender sessions are in transaction simultaneously before release. Capture distinct PostgreSQL backend PIDs, target lock or wait state, and overlapping transaction interval with `pg_stat_activity` or `pg_locks`. Bound waits and release the blocker in cleanup. An HTTP launch gate or `Task.WhenAll` alone does not establish overlap. For each observed race, record the allowed winner and losing result, then reread stock, IMEI, ledgers and report deltas to verify capacity and atomicity. Record a missing workflow or absent overlap as **Not verified**, with owner and rerun condition. An observed invariant violation is **Fail**.

### D5. Evidence for the API, desktop UI and AI

- **API/PostgreSQL** — Record the integrated SHA, exact test command, database version, scenario IDs, input and event times, Expected/Actual by day and period, captured records and HTTP results. Verify Admin/Manager 200, SalesStaff/WarehouseStaff 403, missing or invalid JWT 401, and invalid range 400 with a valid token. Rerun the M3 Dashboard fixture and keep the existing assertions.
- **Desktop UI** — Use the integrated API and the same data range with an authorized account. Compare period and day, `asOf`, VND, sale/refund/tax/cash and cost figures against the captured API response. Verify role visibility, logout or account replacement during load, stale response suppression and retry after a connection failure. Record Skia and Windows native outcomes with client and API SHAs. Inherit the desktop change's platform rerun conditions when the environment still blocks a check.
- **AI** — Ask the integrated assistant for the same report period and verify the report-backed tool call and values against the API with Admin/Manager. Verify SalesStaff/WarehouseStaff cannot obtain financial values through the assistant or direct API. Record the prompts, tool/endpoint result, role, and status without logging secrets. T. Vinh owns the tool and four-role evidence. Mark unavailable routes, tools, or test configuration **Not verified**, with the build or setup needed to rerun.
- Append the three evidence sections to `docs/M4_FINANCIAL_REPORT.md`. Record SHA or dirty worktree state, exact command or reproduction steps, environment, case input, Expected/Actual and **Pass**, **Fail**, **Skipped** or **Not verified**. Differentiate source availability (`null`) from whether the test ran. Use inline extracts in that file and normal test output for diagnostics. Any commit omits AI `Co-Authored-By` trailers.

## Risks / Trade-offs

- A fixed request clock and the database `asOf` clock can diverge. Pin both through the existing test seams and verify the returned metadata and UTC+07 days explicitly.
- Demo data can change store-wide totals. Keep the shared seed and use a separate date window or a measured before/after delta. Additional scenarios use independent ranges or disposable databases so their events cannot alter the cash example.
- Seeded confirmations, unobserved race timing and inaccessible UI or AI can overstate coverage. Record the observed mode or blocker for each result and include the owner's rerun condition.

## Migration Plan

Run the test suite after the reporting and owner workflows are integrated at a recorded SHA. The test infrastructure applies existing migrations and disposes the generated database. A rollback can remove the new test class and its evidence entry.
