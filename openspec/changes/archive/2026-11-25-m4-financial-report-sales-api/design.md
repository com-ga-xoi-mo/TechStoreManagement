# Design

## Context

`AnalyticsController` already restricts access to Admin and Manager. The M3 Dashboard reads within a `RepeatableRead` transaction, uses `statement_timestamp()`, and groups by UTC+07 date in PostgreSQL. After An’s PR #31, Dashboard revenue sums original `Order.TotalAmount` values for delivered orders in `Completed`, `PartiallyRefunded`, or `Refunded` status. The financial report leaves that rule and the Dashboard code intact.

`OrderService` persists pricing on orders and their lines. `PaidAt` can precede `CompletedAt`. Cash checkout at the POS records both timestamps together, snapshots the cost of each line after stock is issued, and records the order total against the shift. The `AddOrderPricingAllocation` migration adds tax and discount allocations with a default of zero and no backfill. `OrderReturn` already has `RefundAmount`, `RefundedAt`, and `IsRestocked`. These fields do not fully capture the refunded sales excluding tax, restock date, or warranty costs required for M4.

## Goals / Non-Goals

- **Goals** — Provide a read API with a stable contract for T. Vinh. Reconcile values by day and period and against persisted sales lines. Distinguish zero, missing costs, and data sources that are not yet integrated.
- **Non-Goals** — UI, the Gemini tool, legacy data repair, pricing changes, refund, restock or warranty source integration, and shift balance reconciliation. The M3 Dashboard retains its current contract and KPIs.

## Decisions

### D1. Required custom range and access control

Example request `GET /api/analytics/financial-report?period=custom&from=2024-10-12&to=2024-10-14`.

- Sprint 1 accepts exactly one `period=custom`, one `from`, and one `to`. Reuse invariant `yyyy-MM-dd` parsing, `AnalyticsLimits.MaxCustomRangeDays` (31 days inclusive), and the Dashboard’s UTC+07 offset. The Dashboard retains its own presets and defaults.
- Return HTTP 400 `ValidationProblemDetails` with errors under `period`, `from`, or `to` for empty, missing or repeated values, unsupported periods, invalid dates or dates without a representable UTC boundary, reversed ranges, excessive length, or an end date in the future in UTC+07. Validate syntax before reading the database. Use the database snapshot clock to validate future dates before running aggregate queries.
- Use `[fromUtc, toExclusiveUtc)`. Start at midnight UTC+07 on the first day and end at midnight after the last day, capped at `asOf`. Fill missing dates on the Vietnam calendar, including the current day if it falls in the range.
- Use the controller’s existing `[Authorize(Roles = "Admin,Manager")]` and JWT pipeline. Admin and Manager have the same store-wide read scope. SalesStaff and WarehouseStaff receive 403. Missing or invalid JWTs receive 401, even for invalid ranges. Return aggregate data on successful requests only.

### D2. Separate sales and receipt dates

| Metric | Source and calculation |
| --- | --- |
| Delivered orders and quantity | Orders whose `CompletedAt` falls in the range and whose status is `Completed`, `PartiallyRefunded`, or `Refunded`. Count distinct order IDs and sum line `Quantity`. |
| Sales excluding tax and sales tax | Sum `OrderItem.TotalPrice − OrderItem.OrderDiscountAmount` and `OrderItem.TaxAmount` separately by the date of `CompletedAt`. `TotalPrice` already includes the line discount. |
| Cost of goods sold and sales gross profit | Line cost equals `UnitCost × Quantity`. A missing line cost gives the affected group `null` for both metrics. When costs are complete, sales gross profit equals sales excluding tax minus cost of goods sold and may be negative. |
| Original receipts | Sum `Order.TotalAmount` once per order whose `PaidAt` falls in the range, independently of delivery status and line items. Receipts include tax as recorded in the order total. The cash POS records `CashTendered − ChangeGiven` as the order total. |

A `Processing` order can have receipts before it contributes sales. Partial and full refunds preserve original sales on the delivery date and original receipts on the payment date. Receipts use the order payment timestamp. Bank and shift ledger reconciliation are outside this measure. Query sales and receipts independently to prevent duplicate receipts for orders with multiple lines or refunds. Use decimal VND amounts from persisted snapshots. Bình’s report uses those snapshots without calling `OrderPricingHelper` or applying current prices, tax rates, or vouchers.

### D3. Zero allocations and missing data

Use stored line values, including when all allocations are zero. Valid zero allocations still reconcile with the order header. Count each delivered order once in `allocationMismatchOrderCount` if its line tax differs from `Order.TaxAmount` or its line sales excluding tax differ from `Order.TotalAmount − Order.TaxAmount`. Check each order before aggregating by day and period so discrepancies cannot cancel one another. Line amounts can differ from legacy order headers. Substituting header amounts or reallocating differences would remove the basis for product-level reconciliation.

When PostgreSQL aggregates costs, count lines with `UnitCost IS NULL` separately because `SUM` skips null values. Days with no sales return zero for sales metrics, and days with no payments return zero receipts. Reconcile each additive period field with its daily values. If any day returns `null` for cost of goods sold or sales gross profit, the corresponding period total is also `null`. Apply the same missing-cost rule per cashier.

### D4. Products and cashiers

- Group all variants by `ProductId`. Use the snapshot name of the line with the latest `CompletedAt` and break ties by `OrderItem.Id` descending. Both lists use the same eligible lines as the period totals. Return at most 10 rows per list, ordered by `quantity DESC, salesExcludingTax DESC, ProductId ASC` or `salesExcludingTax DESC, quantity DESC, ProductId ASC`. Use PostgreSQL UUID ordering. Rank by original sales, including lines from refunded orders.
- Group all qualifying delivered orders by `CashierUserId`, including inactive accounts and distinct accounts with the same name. Use the snapshot’s `User.FullName` and fall back to the ID when the name is blank. Return delivered order count, quantity, sales excluding tax, cost of goods sold, and sales gross profit. Sort by sales descending, delivered order count descending, then ID ascending. Return an empty array when there are no sales. Delivery performance excludes figures based on `PaidAt`.

### D5. DTO and JSON for T. Vinh

Define the DTO in the new `shared/DTOs/FinancialReportDto.cs` file following the shared convention used by `AdminDashboardDto`. This gives the API, the `m4-financial-report-desktop` change, and the tests the same contract. JSON property names are camelCase. GUIDs are strings, `DateOnly` uses `yyyy-MM-dd`, `asOf` is UTC with a `Z` suffix, and VND amounts are JSON numbers. Nullable fields must appear even when their value is `null`.

| DTO | Shape |
| --- | --- |
| `FinancialReportDto` | `period: string`, `startDate/endDate: DateOnly`, `timeZone: string`, `asOf: DateTime`, `summary: FinancialReportTotalsDto`, `daily: FinancialReportDayDto[]`, `topProductsByQuantity/topProductsByRevenue: FinancialReportProductDto[]`, `cashierPerformance: FinancialReportCashierDto[]` |
| `FinancialReportTotalsDto` | `completedOrderCount/quantity/allocationMismatchOrderCount: long`, `salesExcludingTax/salesTax/paymentReceivedAmount: decimal`, `costOfGoodsSold/salesGrossProfit: decimal?`, and the seven `decimal?` fields for change 2 listed below |
| `FinancialReportDayDto` | `date: DateOnly`, `totals: FinancialReportTotalsDto` |
| `FinancialReportProductDto` | `productId: Guid`, `productName: string`, `quantity: long`, `salesExcludingTax: decimal` |
| `FinancialReportCashierDto` | `cashierUserId: Guid`, `cashierName: string`, `completedOrderCount/quantity: long`, `salesExcludingTax: decimal`, `costOfGoodsSold/salesGrossProfit: decimal?` |

Both `summary` and `daily[].totals` already include seven fields whose values are always `null` in this change.

| Change 2 field | Intended meaning |
| --- | --- |
| `refundPaidAmount` | Amount actually paid back to the customer by the date of `RefundedAt` |
| `refundedSalesExcludingTax` | Refunded sales excluding tax by the date of `RefundedAt` |
| `restockedCost` | Original cost reversed on the date the item is physically restocked |
| `warrantyCost` | Warranty costs in the range, reported separately |
| `netSalesExcludingTax` | Sales excluding tax minus refunded sales excluding tax |
| `netCostOfGoodsSold` | Cost of goods sold minus the cost of items actually restocked |
| `netGrossProfit` | Net sales excluding tax minus net cost of goods sold |

The HTTP 200 example below uses the D7 fixture. `salesGrossProfit` represents sales before the change 2 adjustments.

```json
{
  "period": "custom",
  "startDate": "2024-10-12",
  "endDate": "2024-10-14",
  "timeZone": "UTC+07:00",
  "asOf": "2024-10-15T02:00:00Z",
  "summary": {
    "completedOrderCount": 3, "quantity": 5, "allocationMismatchOrderCount": 1,
    "salesExcludingTax": 480, "salesTax": 36, "paymentReceivedAmount": 370,
    "costOfGoodsSold": null, "salesGrossProfit": null,
    "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
    "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
  },
  "daily": [
    {
      "date": "2024-10-12",
      "totals": {
        "completedOrderCount": 1, "quantity": 2, "allocationMismatchOrderCount": 0,
        "salesExcludingTax": 180, "salesTax": 18, "paymentReceivedAmount": 248,
        "costOfGoodsSold": 120, "salesGrossProfit": 60,
        "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
        "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
      }
    },
    {
      "date": "2024-10-13",
      "totals": {
        "completedOrderCount": 0, "quantity": 0, "allocationMismatchOrderCount": 0,
        "salesExcludingTax": 0, "salesTax": 0, "paymentReceivedAmount": 0,
        "costOfGoodsSold": 0, "salesGrossProfit": 0,
        "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
        "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
      }
    },
    {
      "date": "2024-10-14",
      "totals": {
        "completedOrderCount": 2, "quantity": 3, "allocationMismatchOrderCount": 1,
        "salesExcludingTax": 300, "salesTax": 18, "paymentReceivedAmount": 122,
        "costOfGoodsSold": null, "salesGrossProfit": null,
        "refundPaidAmount": null, "refundedSalesExcludingTax": null, "restockedCost": null,
        "warrantyCost": null, "netSalesExcludingTax": null, "netCostOfGoodsSold": null, "netGrossProfit": null
      }
    }
  ],
  "topProductsByQuantity": [
    { "productId": "00000000-0000-0000-0000-000000000001", "productName": "Sample A", "quantity": 2, "salesExcludingTax": 180 },
    { "productId": "00000000-0000-0000-0000-000000000002", "productName": "Sample B", "quantity": 2, "salesExcludingTax": 180 },
    { "productId": "00000000-0000-0000-0000-000000000003", "productName": "Sample C", "quantity": 1, "salesExcludingTax": 120 }
  ],
  "topProductsByRevenue": [
    { "productId": "00000000-0000-0000-0000-000000000001", "productName": "Sample A", "quantity": 2, "salesExcludingTax": 180 },
    { "productId": "00000000-0000-0000-0000-000000000002", "productName": "Sample B", "quantity": 2, "salesExcludingTax": 180 },
    { "productId": "00000000-0000-0000-0000-000000000003", "productName": "Sample C", "quantity": 1, "salesExcludingTax": 120 }
  ],
  "cashierPerformance": [
    { "cashierUserId": "10000000-0000-0000-0000-000000000001", "cashierName": "Cashier 1", "completedOrderCount": 2, "quantity": 3, "salesExcludingTax": 300, "costOfGoodsSold": 120, "salesGrossProfit": 180 },
    { "cashierUserId": "10000000-0000-0000-0000-000000000002", "cashierName": "Cashier 2", "completedOrderCount": 1, "quantity": 2, "salesExcludingTax": 180, "costOfGoodsSold": null, "salesGrossProfit": null }
  ]
}
```

### D6. Analytics integration

Add a dedicated action, query, result, and read models in Analytics. Reuse existing DI registration. Declare the report method in `IAnalyticsService` and `IAnalyticsRepository` without a default implementation and implement it in the existing production classes. The other implementations are Bình’s test doubles in `AnalyticsControllerTests`, `AnalyticsServiceTests`, `DataSeederPostgresIntegrationTests`, and `DashboardLiveFlowPostgresHttpTests`. Update those classes directly. Wrappers only need to forward the call. Verify HTTP 200 with the real DI setup so the route exercises the production implementation.

The service manages a `RepeatableRead` transaction through `IUnitOfWork`, reads the database clock before any aggregate query, builds validated UTC criteria, fills missing dates, and maps the DTO. The repository uses no-tracking queries and groups data in PostgreSQL. Filter on original timestamp columns and convert time zones only in grouping expressions. When LINQ cannot translate grouping by date, use parameterized SQL following the Dashboard pattern. Aggregate sales and receipts separately in one snapshot, pass through the cancellation token, and roll back on failure or cancellation. Group in the database and limit each ranking separately to keep result sets small. Avoid loading entire entities into memory.

### D7. PostgreSQL fixture and hand-calculated expectations

The October 12–14, 2024 range in UTC+07 corresponds to `[2024-10-11T17:00:00Z, 2024-10-14T17:00:00Z)`. All timestamps below are UTC. Orders A, B, and C have product IDs ordered A < B < C. Insert additional orders just before the start and exactly at the end, with both payment and delivery outside the range, to verify exclusion.

| Order | Status | CompletedAt | PaidAt | Cashier | Qty | TotalPrice | Order discount allocation / line tax | UnitCost | Order TotalAmount / TaxAmount |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| A | Refunded | 11/10 17:00 | 11/10 16:00 | 1 | 2 | 200 | 20 / 18 | 60 | 198 / 18 |
| B | PartiallyRefunded | 13/10 18:00 | 12/10 01:00 | 2 | 2 | 200 | 20 / 18 | null | 198 / 18 |
| C | Completed | 14/10 16:59:59 | 14/10 16:00 | 1 | 1 | 120 | 0 / 0 | 0 | 122 / 12 |
| D | Processing | null | 12/10 02:00 | 2 | 1 | 50 | 0 / 0 | 0 | 50 / 0 |

| Expected by Vietnam date | Delivered orders | Qty | Sales excluding tax | Tax | Receipts | Cost of goods sold | Sales gross profit | Orders with allocation mismatches |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 12/10 | 1 | 2 | 180 | 18 | 248 | 120 | 60 | 0 |
| 13/10 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| 14/10 | 2 | 3 | 300 | 18 | 122 | null | null | 1 |
| Full period | 3 | 5 | 480 | 36 | 370 | null | null | 1 |

Add a separate fixture with only order C to verify known zero cost. Add a case with multiple lines and variants to verify order counts, product grouping, receipts counted once, the two ranking orders, and the top-10 limit. Check 1-day and exactly 31-day ranges, invalid ranges, and the current-day `asOf` cutoff. Compare live HTTP results with the expected PostgreSQL values, rerun the existing Dashboard tests, and record the results in the sole verification document.

## Risks / Trade-offs

- Legacy order headers can disagree with line tax or sales amounts. Report the mismatch count so readers can see the limits of line-level data.
- Missing costs prevent profit calculation for a group. Return `null` for the affected group while preserving independent sales figures and groups with complete data.
- Sales and receipts can change concurrently. Read both in one snapshot. The 31-day maximum and top-10 caps limit the range and the size of the two rankings.

## Migration Plan

Start implementation by fetching and merging `origin/dev` into `feat/m4-financial-reports`. Record the source SHA used to build and test, plus the worktree state when building before a commit. Hand off the endpoint and permissions with the verified build. This change requires no migration or backfill. Roll back by reverting the API, authorization, and accompanying documentation changes.
