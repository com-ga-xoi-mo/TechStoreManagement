# Spec Delta

## ADDED Requirements

### Requirement: Financial report uses a validated Vietnam date range
The API SHALL provide `GET /api/analytics/financial-report` and require exactly one `period=custom`, `from`, and `to`, with dates in `yyyy-MM-dd` format. The inclusive range SHALL span 1–31 days and end on or before the UTC+07 date at `asOf`. The response SHALL return `period`, `startDate`, `endDate`, `timeZone`, and UTC `asOf`. All figures SHALL come from one snapshot.

#### Scenario: UTC+07 boundaries and current-day cutoff
- **WHEN** the caller requests a valid range
- **THEN** the inclusive start is midnight UTC+07 on `from`. The exclusive end is the earlier of midnight UTC+07 on the day after `to` and `asOf`
- **AND** the daily series contains exactly one entry per day in ascending order

#### Scenario: Invalid report period
- **WHEN** an authorized caller omits, repeats, or sends an empty required parameter, uses a period other than `custom`, sends a malformed date or one that cannot be converted to a UTC boundary, reverses the range, exceeds 31 days, or ends the range in the future
- **THEN** the API returns HTTP 400 `ValidationProblemDetails` for the affected field and skips report aggregation

### Requirement: Financial report recognizes original delivered sales
The report SHALL include `Completed`, `PartiallyRefunded`, and `Refunded` orders whose `CompletedAt` falls in the range. Line sales excluding tax SHALL be `TotalPrice − OrderDiscountAmount`, and line tax SHALL be the stored `TaxAmount`. Period, daily, product, and cashier aggregates SHALL use the same set of sales lines.

#### Scenario: Original sale survives a refund
- **WHEN** an order is delivered in the range and later partially or fully refunded, or a paid order is still `Processing`
- **THEN** a delivered order contributes its original sales on the delivery date. A `Processing` order contributes no sales even when it has a `PaidAt` timestamp

#### Scenario: Legacy line allocations remain as stored
- **WHEN** a stored line allocation is zero
- **THEN** the report uses zero. If line tax totals differ from `Order.TaxAmount` or line sales excluding tax differ from `Order.TotalAmount − Order.TaxAmount`, the report counts that order once in `allocationMismatchOrderCount` for its day and period
- **AND** the report uses the stored allocations without redistributing differences or looking up current prices, taxes, or promotions

#### Scenario: The Dashboard keeps its own contract
- **WHEN** the caller uses `GET /api/analytics/dashboard`
- **THEN** the Dashboard endpoint, periods, and DTO stay the same. Dashboard revenue follows the existing KPI requirement in this spec and sums original `TotalAmount` for delivered orders in `Completed`, `PartiallyRefunded`, or `Refunded` status. The financial report does not affect Dashboard figures

### Requirement: Financial report separates receipts from sales
`paymentReceivedAmount` SHALL sum `Order.TotalAmount` for orders whose `PaidAt` falls in the range, counting each order once and grouping by the UTC+07 date of `PaidAt`. The response SHALL report original receipts separately from sales excluding tax, tax, and refunds.

#### Scenario: Payment and delivery fall on different days
- **WHEN** an order is paid before its delivery date, including when the order is still `Processing`
- **THEN** the report records original receipts on the payment date. Only a qualifying delivery contributes sales on the delivery date

### Requirement: Financial report distinguishes zero and unavailable amounts
Cost of goods sold SHALL sum `UnitCost × Quantity`. In each period, day, and cashier group, a single line with `UnitCost=null` SHALL set cost of goods sold and sales gross profit to `null`. When all costs are known, sales gross profit SHALL equal sales excluding tax minus cost of goods sold.

#### Scenario: Empty, zero and missing costs
- **WHEN** the range includes an empty day, a day with only known zero costs, and a day with missing costs
- **THEN** the empty day returns zero sales figures and receipts, the day with known zero cost retains zero, and the day with missing costs and the period return `null` for dependent metrics
- **AND** each additive metric reconciles from daily values to the period total under the same `null` propagation rules

#### Scenario: Change 2 amounts are present but unavailable
- **WHEN** the caller reads the Sprint 1 report
- **THEN** period totals and every daily total include `refundPaidAmount`, `refundedSalesExcludingTax`, `restockedCost`, `warrantyCost`, `netSalesExcludingTax`, `netCostOfGoodsSold`, and `netGrossProfit` as JSON `null`, including on empty days

### Requirement: Financial report ranks products and groups cashier performance
The report SHALL return at most 10 products per ranking and group all variants by `ProductId`. The quantity ranking SHALL sort by quantity descending, then sales excluding tax descending. The sales ranking SHALL reverse the priority of the two descending metrics. Both SHALL break ties by `ProductId` ascending. Cashier performance SHALL group qualifying delivered orders by `CashierUserId`.

#### Scenario: Ties and historical cashier attribution
- **WHEN** two products tie on both metrics or multiple cashiers share a display name
- **THEN** both rankings use stable ID order, and each cashier ID has a separate row with a distinct count of delivered order IDs, quantity, sales excluding tax, cost of goods sold, and sales gross profit

#### Scenario: No delivered sales
- **WHEN** the range contains no qualifying delivered orders
- **THEN** both rankings and the cashier performance list are empty arrays
