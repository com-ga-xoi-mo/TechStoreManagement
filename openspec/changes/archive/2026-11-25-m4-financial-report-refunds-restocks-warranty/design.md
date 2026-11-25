# Design

## Context

- The comparison baseline is `origin/dev` at SHA `f5756b07afa366bab6690287fdc8fd28d9078aa4` (PR #31). On November 25, 2026, the GitHub SHA matched the local ref. Data sources were checked with `git show origin/dev:<path>`. The sales API was checked against the current branch and [m4-financial-report-sales-api](../2026-11-25-m4-financial-report-sales-api/design.md), since that change lives on the reporting branch.
- The sections for An, Q. Vinh, Bình, Đạt, and Vy in the [M4 plan](../../../../docs/MILESTONE_4_PLAN.md) identify who owns each write path. `OrderReturn.cs`, `InventoryMovement.cs`, `docs/database/schema.dbml`, and the [refund handoff](../../../../docs/M4_REFUND_HANDOFF.md) describe the finalized data. Check the writers to distinguish existing columns from working write paths.

## Goals / Non-Goals

**Goals** — Add after-sales figures that can be reconciled by source, date, and period. Preserve JSON compatibility when a metric has incomplete data.

**Non-Goals** — Refund and money-transfer logic, stock reservation, receiving and issue, shift recording, pricing, points and vouchers, migrations and backfills, UI, and Gemini. Warranty reporting covers the replacement device costs available on the baseline. Repair labor and parts would need separate sources from Q. Vinh if the scope expands.

## Decisions

### D1. Data sources and owner handoffs

| Owner / workflow | Available on the baseline | Pending or limited | Handoff owner and rerun conditions |
| --- | --- | --- | --- |
| An — Refunds | `OrderReturn` provides `Id`, `OrderId`, `OrderItemId`, `CaseLineId`, `NetAmount`, `TaxAmount`, `DiscountAmount`, `RefundAmount`, `RefundMethod`, `RefundedAt`, and `RefundPosSessionId`. `ReturnCaseLine.CaseId` identifies the refund. `ReturnCase.WasUndelivered` and `Order.CompletedAt` distinguish pre-delivery from post-delivery refunds. Cash settlement is recorded in the same transaction. QR refunds are reconciled manually and store `BankReference` and a verified payout timestamp. | M3 lines may lack allocations or a refund ID. Pending and failed refunds remain at the case level with no paid lines. Missing supporting records leave the data unavailable. | An verifies historical supporting records and allocations when available. Bình reruns legacy cases when verified data exists and manual QR refunds when a reconciled case is available. |
| An — Inspection and restocking | `CustomerReturn` entries in `inventory_movements` provide `Id`, `OrderReturnId`, `QuantityChange`, and `CreatedAt`. `IsRestocked`, `Disposition`, `InspectionId`, and `InspectedAt` reflect inspection state. `OrderItem.UnitCost` holds the original stored cost. | A legacy status flag without a movement is insufficient evidence of a restock. Original cost is nullable. | An provides the movement, return line link, and valid cost snapshot. Bình rechecks missing data when the original link or supporting record becomes available. |
| Đạt — Cash drawer and shifts | The cash POS records `Order.PaidAt`, `PaymentMethod`, `PosSessionId`, `TotalAmount`, `CashTendered`, and `ChangeGiven`. The payout service stores `CashRefundPayout.RefundId`, unique by `ReturnCase.Id`, plus `ShiftId` and `Amount`. `ReturnCase.RefundedAt` is the actual payout timestamp. `PosSession` stores opening cash, total cash sales and refunds, expected closing cash, and the actual closing count. | Legacy payouts may lack a shift record or link. Cumulative shift totals reconcile the full shift and cannot break down a shift by report period. | Đạt confirms that receipts and payouts are recorded once and attributed to the selling and paying shifts. Bình verifies through the shift API, real transactions, and a report using a dedicated fixture. |
| Đạt — QR receipts | `VietQrTransaction` provides `Status`, `Amount`, `ConfirmedAt`, and either `ConfirmedByUserId` or `Provider/ProviderTransactionId`. It links to the order and shift and permits one confirmation per order. The baseline seeds `Confirmed` transactions, reads stored confirmations, and supports cancellation and expiration. | The checked SHA had no observed production flow creating receipt confirmations, recording QR sales against a shift, or handling callbacks and late payments. Seed data and manual bank reconciliation of refunds do not verify end-to-end QR receipts. | Đạt hands off receipt confirmation and callback or reconciliation records, identifying real versus sandbox data. Bình reruns real QR receipt and callback replay tests once that flow is in the integrated build. |
| Q. Vinh — Warranty | `WarrantyCase` provides `Id`, `Status`, `OrderItemId`, `ReplacementDeviceId`, `ResolutionOperationId`, `MovementId`, `ReplacementUnitCost`, and `ClosedAt`. The baseline API writes the `Replaced` case, the `WarrantyReplacement` movement, and replacement cost in one transaction. | `ReplacementUnitCost` may be missing. No separate source for labor or parts was found. | Q. Vinh provides a fixture through the API that issues a replacement, with the movement and original cost. Recheck missing costs when supporting records become available and document that repair costs are outside the current scope. |
| Vy — Sales snapshots | Price, discount, and tax are stored on `OrderItem`. An stores refund amounts based on original allocations. | Historical allocations cannot be inferred from current settings, prices, points, vouchers, or recalculation. | Vy explains the source data as needed for reconciliation. Bình aggregates finalized data only. |

### D2. Refunds by actual payout time

Group `order_returns` by `RefundedAt` and count each paid line ID once. To read `WasUndelivered` or reconcile payment channels from newer cases, join through `CaseLineId → ReturnCaseLine.CaseId`, with many lines per case. `Pending`, `AwaitingBank`, and `Failed` cases do not contribute. Legacy lines without a case still use their stored `RefundAmount` and `RefundedAt`. Exclude refund quotes and approved payouts that have yet to be confirmed.

- `refundPaidAmount` sums `RefundAmount` for every paid refund. `refundTaxAmount` sums `TaxAmount` from the same set, including tax refunded before delivery. Stored records provide nonnegative amounts for both measures.
- `refundedSalesExcludingTax` sums `NetAmount` when `CompletedAt <= RefundedAt` and the order has neither pre-delivery refund marker (`WasUndelivered` or `Disposition=Undelivered`). Include orders delivered in earlier periods. A full refund before delivery contributes the payout and any known refunded tax, with zero sales adjustment.
- Inspection may split a paid line into restocked, defective, and pending portions. Sum the recorded portions once per ID. Overall refund payouts exclude quoted `ReturnCaseLine` amounts and shift payouts counted elsewhere. Use the stored `DiscountAmount`, `NetAmount`, and `TaxAmount` allocations. The actual payout remains usable if a component is missing.

### D3. Restocked costs and replacement device costs

- Read positive `CustomerReturn` movements with an `OrderReturnId`. Join movement → return → original sales line on both the order and line IDs. Verify the link to a delivered order and exclude refunds before delivery. Group movements by `CreatedAt` and sum `QuantityChange × OrderItem.UnitCost` using the stored original cost. The movement determines the actual restock date and quantity independently of the refund date, inspection date, or `IsRestocked`.
- Reverse costs only for quantities returned to sellable stock through a movement. Defective, under-repair, or pending items and reservation releases following pre-delivery refunds do not reverse costs. `IsRestocked=true` without a movement creates no cost entry. Record historical limits in the verification document. A qualifying movement with missing cost retains its quantity for reconciliation and makes the group’s cost reversal `null`.
- Use `WarrantyCase.Status=Replaced` and link `MovementId` to the negative `WarrantyReplacement` issue movement for the same sales line. Use `Movement.CreatedAt` for the issue date and calculate cost from stored `ReplacementUnitCost × abs(QuantityChange)`. The baseline records one device per case with a unique `MovementId`. Missing replacement cost makes the group’s `warrantyCost` `null`. Receiving a device for repair or returning the original device generates no replacement cost. Warranty movements are tracked separately from net cost of goods sold, gross profit, sales, and refunds.

### D4. Cash flow by channel and shift

- Group receipts by `PaidAt` for cash orders with a `PosSessionId`. Use `TotalAmount` once per order. At the POS this matches cash tendered minus change. Cash orders outside a shift can still contribute to the original `paymentReceivedAmount`. They have no assigned cash drawer.
- Read cash payouts from `cash_refund_payouts` by unique `RefundId` and use `ReturnCase.RefundedAt` for settled cash cases. Group by the paying operator’s `ShiftId`. Reconcile each payout with the cash refund lines for the same case and shift. When a required shift record is missing or the payout differs in amount or shift, the affected daily and period payout figures are `null`. If the shift is known, its payout and net movement are also `null`. An unknown shift remains unassigned. Refund lines do not substitute for a missing or mismatched payout.
- Use `ConfirmedAt` and `Amount` for documented `Confirmed` QR receipts that match a VietQR order, at most one per order. `PaidAt` is not the bank timestamp. Use `RefundedAt` for `RefundMethod=VietQr` refund lines when the case has been reconciled with a reference code. A receipt or payout marked as completed without the required supporting record leaves the corresponding QR metric unknown. Reserved, pending, failed, or expired transactions and unreconciled late payments are excluded. QR and Card do not affect the cash drawer.
- `paymentReceivedAmount` remains the original receipt total by `PaidAt` from change 1. Confirmed QR amounts and shift cash amounts are separate measures and may cover different transactions or dates. The original receipt total does not include those measures a second time.
- `cashByShift` appears at the response level and includes shifts with cash events in the period, sorted by `posSessionId` ascending. Each row includes `posSessionId`, `cashReceivedAmount`, `cashRefundPaidAmount`, and `cashNetMovement` (receipts minus payouts), based on in-period events. For a complete shift, reconcile opening cash plus cash receipts minus cash payouts with the stored expected closing cash. Keep the actual count separate. When a report covers only part of a shift, show movement within the window. Opening and closing balances cannot be inferred from it.

### D5. JSON compatibility, aggregation, and unknown values

Populate the seven published nullable fields in `summary` and `daily[].totals`. Add nullable decimal fields `refundTaxAmount`, `cashReceivedAmount`, `cashRefundPaidAmount`, `vietQrReceivedAmount`, and `vietQrRefundPaidAmount`, plus the `cashByShift` array from D4. Use a typed response projection in Analytics. Preserve camelCase names and field placement so existing clients can still read the response with the shared DTO. The endpoint, Admin/Manager authorization, custom range, top rankings, sales performance, and Dashboard retain their existing contracts.

- Use stored decimal VND amounts and a single `RepeatableRead` snapshot. Filter each source by its original event timestamp in the change 1 `[fromUtc, toExclusiveUtc)` range. Convert to UTC+07 only for grouping. Aggregate each source separately by day or shift before joining results. Joining receipts directly to payouts, multiple movements, or multiple allocation lines can duplicate amounts.
- Net cost of goods sold equals cost of goods sold minus restocked cost. Net sales excluding tax equals sales excluding tax minus refunded sales on delivered orders. Gross profit equals net sales minus net cost. A restock-only day can have negative net cost, and a refund-only day can have negative net sales. Empty days with complete sources return zero. Preserve precision and negative values.
- Track missing data separately from known totals. Return `null` for the affected group when a metric’s source is unavailable or a qualifying event lacks an input. A missing day makes that metric’s period total `null`, and dependent calculations propagate `null`. Other groups and independent metrics remain available. API data availability and test execution status are separate. A `Confirmed` fixture can verify Analytics before end-to-end QR receipts are available.

### D6. Hand-calculated expectations and verification

The fixture extends R1, C1, and C4 in An’s handoff. Order A was delivered and paid in cash on October 28. It has two items costing VND 600,000 each, sales excluding tax of VND 1,800,000, and VND 180,000 in tax. Refund one item in cash from the Manager’s shift at `2026-10-29T16:55:00Z`. Restock one item at `2026-10-29T17:05:00Z`. Refund the remaining defective item on October 31 through reconciled QR, retaining its cost. Add a VND 500,000 replacement device issued on October 30 for a different, older order and a case returning a device after repair. Order B was paid via QR for VND 550,000 (VND 500,000 sales and VND 50,000 tax), remained undelivered, and was fully refunded via QR the following day. Replay the refund, inspection, and replacement issue with the same identifiers. Compare results in the same data window after each replay. The `asOf` metadata can differ.

| UTC+07 date | Sales excluding tax | Cost of goods sold | Refund payout | Refunded tax | Refunded sales excluding tax | Restocked cost | Net sales | Net cost of goods sold | Gross profit | Warranty cost |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 28/10 | 1,800 | 1,200 | 0 | 0 | 0 | 0 | 1,800 | 1,200 | 600 | 0 |
| 29/10 | 0 | 0 | 990 | 90 | 900 | 0 | -900 | 0 | -900 | 0 |
| 30/10 | 0 | 0 | 0 | 0 | 0 | 600 | 0 | -600 | 600 | 500 |
| 31/10 | 0 | 0 | 1,540 | 140 | 900 | 0 | -900 | 0 | -900 | 0 |
| Full period | 1,800 | 1,200 | 2,530 | 230 | 1,800 | 600 | 0 | 600 | -600 | 500 |

The table is in thousands of VND. The API returns VND. The selling shift receives VND 1,980,000 in cash and the Manager’s shift pays out VND 990,000. With VND 2,000,000 in opening cash, the Manager’s expected closing cash is VND 1,010,000. The QR fixture records a VND 550,000 receipt on October 30 and VND 1,540,000 in payouts on October 31. Neither affects the two cash shifts. Verify that a range limited to October 29 or 30 still reports the refund or restock for the order delivered on October 28. Also test a batch split after inspection, missing legacy allocations, missing and zero restock or replacement costs, the UTC+07 date boundary, and the `asOf` cutoff. Known daily values sum to the period totals.

Append expected and actual results, case IDs, data scope, build SHA, and test outcomes to `docs/M4_FINANCIAL_REPORT.md`. For sources not exercised through a real workflow, record **Not tested**, the owner, and the rerun criteria in D1. Use a dedicated report fixture with independently calculated results and retain each owner’s separate end-to-end verification.

## Risks / Trade-offs

- A paid line may split after inspection, and one case may have multiple lines but one shift payout. Aggregate each source by its event key and reconcile values before and after splits and replays.
- Historical costs or payment channel records may be missing. Preserve independent figures, return `null` for the unavailable metric, and document the limitation in the verification evidence.
- Another PR changed Dashboard KPIs. Use the Dashboard on the current baseline for regression testing. The financial report change adds after-sales data and the new cash flow metrics.

## Migration Plan

At implementation time, check the latest `origin/dev` and keep both the business workflow baseline and the sales change on the implementation branch. Apply existing migrations to the PostgreSQL test database. The Analytics change reads data and adds compatible JSON fields. Roll back by reverting the Analytics change.
