## ADDED Requirements

### Requirement: One settlement step for Cash and QR
Completing a sale SHALL go through one shared settlement step called by both cash checkout and VietQR confirmation inside the caller's transaction, which opens, saves and commits once. For a given order it SHALL, in this order: confirm the sale through `IInventoryFulfillmentService.ConfirmSaleAsync` (stock deduction, movements, per-line cost, warranty dates), consume the held voucher use through `IVoucherService.RedeemAsync` when the order has a voucher, stage the loyalty points through `IRefundLoyaltyService.StageSaleAsync` when the order has a customer, set `Completed` with `PaidAt` = `CompletedAt` = the payment time, and credit the shift: the order total to `cash_sales_total` for Cash or to `vietqr_sales_total` for VietQr. The step SHALL be idempotent per order and SHALL NOT be repeated in either caller.

#### Scenario: Cash sale uses the shared step
- **WHEN** a cash checkout completes
- **THEN** stock, cost, warranty, voucher use, points and the shift cash total are applied once by the shared step

#### Scenario: QR sale uses the same step
- **WHEN** a QR is confirmed
- **THEN** the same effects are applied once and the amount goes to `vietqr_sales_total`, not `cash_sales_total`

#### Scenario: Any step fails
- **WHEN** a step such as the voucher consumption or the stock confirmation is refused
- **THEN** nothing is saved: the order stays `Pending`, stock, voucher, points and shift totals are unchanged

#### Scenario: Second invocation
- **WHEN** the step runs again for an order already `Completed`
- **THEN** it changes nothing

### Requirement: Cash does not touch QR money
Cash sales SHALL NOT change `vietqr_sales_total` and QR sales SHALL NOT change `cash_sales_total` or the expected cash at close.

#### Scenario: Expected cash ignores QR
- **WHEN** a shift opened with 1,000,000 has cash sales 500,000, QR sales 2,000,000 and cash refunds 0
- **THEN** the expected closing cash is 1,500,000 and the QR total 2,000,000 is shown separately
