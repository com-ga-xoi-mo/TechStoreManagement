# Spec Delta

## ADDED Requirements

### Requirement: Financial report dates settled refunds independently of delivery
The report SHALL record `refundPaidAmount` and `refundTaxAmount` from settled payouts on the UTC+07 date of `RefundedAt`. `refundedSalesExcludingTax` SHALL include only stored refunded sales excluding tax for orders delivered by the time of the refund, including deliveries from a prior period.

#### Scenario: Delivered order is partially and then fully refunded
- **WHEN** a delivered order is partially refunded and later fully refunded through two confirmed payouts
- **THEN** original sales stay on the delivery date. Each payout and sales adjustment is posted on its own refund date
- **AND** unpaid, bank-pending, failed, and replayed refund requests do not increase refunded amounts

#### Scenario: Processing order is fully refunded before delivery
- **WHEN** a `Processing` order is fully refunded before delivery
- **THEN** the stored payout and refunded tax appear on the payout date. The sales reduction and restocked cost are zero even after the order changes to `Refunded`

#### Scenario: Inspection splits an already paid return
- **WHEN** a paid return is split into multiple lines for inspection
- **THEN** the refunded sales, tax, and payout totals on the refund date stay the same

### Requirement: Financial report reverses cost only on sellable restock
`restockedCost` SHALL sum `QuantityChange × OrderItem.UnitCost` using the original cost for each positive `CustomerReturn` movement whose `order_return_id` links to a return line from a delivered order. Each movement SHALL count once on its movement date.

#### Scenario: One item is sellable and another is defective
- **WHEN** inventory staff restock part of the return on a date different from the refund and the remaining items are defective or under repair
- **THEN** only the items actually restocked reverse costs, on the movement date. The `IsRestocked` flag and standalone reservation, receipt, or inspection events do not reverse costs

#### Scenario: Original cost is missing or genuinely zero
- **WHEN** a qualifying movement has a missing or stored zero original cost
- **THEN** a missing cost makes `restockedCost` and dependent metrics `null` for the affected group. A known zero cost gives a zero cost reversal

### Requirement: Financial report displays replacement warranty cost separately
`warrantyCost` SHALL sum the replacement device costs recorded on `Replaced` cases with a warranty issue movement, on the date of issue. Each movement SHALL count once. Warranty costs SHALL be reported separately from sales, cash flow, net cost of goods sold, and gross profit.

#### Scenario: Replacement is replayed and repair is handed back
- **WHEN** a replacement is issued once despite a replayed request, or a repair case only returns the customer’s device
- **THEN** the replacement contributes one cost in the issue period. Receiving a device for repair or returning the original device incurs no replacement cost. Unknown replacement cost returns `null`

### Requirement: Financial report reconciles cash by shift and separates VietQR
The report SHALL separate cash receipts and payouts by actual shift from confirmed QR receipts and payouts. Cash receipts belong to the selling shift on the payment date. Cash refunds belong to the paying shift on the refund date. QR SHALL have no effect on the cash drawer. Eligible amounts SHALL come from stored payment records.

#### Scenario: Cash sale and refund use different shifts
- **WHEN** the selling shift receives cash and a different operator refunds it from their own shift
- **THEN** the receipt belongs to the selling shift and the payout to the paying shift. `summary` and `daily[].totals` record `cashReceivedAmount` and `cashRefundPaidAmount` by event date. `cashByShift` returns `posSessionId`, `cashReceivedAmount`, `cashRefundPaidAmount`, and `cashNetMovement` for the period. Net cash movement is receipts minus payouts when data is complete
- **AND** the same payout is counted once across shift records and refund lines

#### Scenario: Confirmed QR flows use bank timestamps
- **WHEN** a QR receipt is confirmed or the supporting records for a QR refund have been reconciled successfully
- **THEN** `vietQrReceivedAmount` and `vietQrRefundPaidAmount` in period and daily totals use the receipt confirmation date and actual payout date respectively. Pending, expired, and failed QR transactions and unreconciled late payments are excluded

### Requirement: Financial report reconciles net amounts across dates
The report SHALL calculate `netSalesExcludingTax = salesExcludingTax − refundedSalesExcludingTax`, `netCostOfGoodsSold = costOfGoodsSold − restockedCost`, and `netGrossProfit = netSalesExcludingTax − netCostOfGoodsSold`. Each event stream SHALL use the same report boundaries and snapshot.

#### Scenario: Only refund or restock occurs within the current period
- **WHEN** an order was delivered in a prior period and the current period contains only a refund or physical restock
- **THEN** the after-sales event counts on its event date. Net figures may be negative, and each additive metric reconciles from daily values to the period total when data is complete
- **AND** original sales, rankings, cashier performance, the Dashboard, date ranges, and authorization retain their existing contracts

## MODIFIED Requirements

### Requirement: Financial report distinguishes zero and unavailable amounts
Cost of goods sold SHALL sum `UnitCost × Quantity`. In each period, day, and cashier group, a single line with `UnitCost=null` SHALL set cost of goods sold and sales gross profit to `null`. When all costs are known, sales gross profit SHALL equal sales excluding tax minus cost of goods sold.

#### Scenario: Empty, zero and missing costs
- **WHEN** the range includes an empty day, a day with only known zero costs, and a day with missing costs
- **THEN** the empty day returns zero sales figures and receipts, the day with known zero cost retains zero, and the day with missing costs and the period return `null` for dependent metrics
- **AND** each additive metric reconciles from daily values to the period total under the same `null` propagation rules

#### Scenario: Change 2 amounts are present but unavailable
- **WHEN** an after-sales or cash flow source is unavailable, or a qualifying event lacks a required allocation, cost, or supporting record
- **THEN** the field appears as JSON `null` in the affected group and propagates to the period total and dependent metrics. Independent fields, such as an actual payout whose allocation is missing, retain their known values

#### Scenario: Available sources have no matching events
- **WHEN** data sources are complete for the group but the day or period contains no eligible events
- **THEN** the corresponding fields in `summary` and `daily[].totals` return zero. The API retains the seven published after-sales fields in `summary` and `daily[].totals`, adds `refundTaxAmount`, `cashReceivedAmount`, `cashRefundPaidAmount`, `vietQrReceivedAmount`, and `vietQrRefundPaidAmount`, and adds `cashByShift` at the response level
