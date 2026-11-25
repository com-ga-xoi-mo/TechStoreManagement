# pos-cash-checkout Specification

## Purpose

Bán hàng tiền mặt tại quầy trong một transaction: tạo đơn, thu tiền, tính tiền thừa, trừ kho, ghi giá vốn, kích hoạt bảo hành và cộng tiền vào ca; gửi lặp cùng khóa không ghi tiền hay trừ kho hai lần.
## Requirements
### Requirement: Atomic cash checkout
`POST /api/pos/checkout` SHALL, in one database transaction, create the order, verify and reserve the devices and stock through the inventory fulfilment service, confirm the sale (stock deduction, `Sale` movements, per-line cost snapshot, warranty dates), set `status` = `Completed`, `paymentMethod` = `Cash`, `paidAt` = `completedAt` = now, store `cashTendered` and `changeGiven`, credit the order total to the shift's `cashSalesTotal` and return 201 with the order. Request fields: `posSessionId`, `checkoutKey`, `customerId?`, `cashTendered`, `orderDiscountAmount?`, `expectedTotal?`, `notes?`, `items` (as in `CreateOrderRequest`). Prices, totals and cost SHALL be computed by the server; client prices SHALL be ignored. Only `SalesStaff` and `Admin` SHALL check out.

`POST /api/pos/quote` SHALL allow Admin and SalesStaff to price the same cart with the checkout pricing service without writing an order or reserving stock. A new checkout with an expected total different from the current price SHALL return 409 without recording a payment. Replay of a committed checkout key SHALL return the original order.

#### Scenario: Successful cash sale
- **WHEN** a SalesStaff with an open shift checks out one in-stock IMEI device priced 10,000,000 with `cashTendered` = 10,000,000
- **THEN** the response is 201, the order is `Completed` with `changeGiven` = 0, the device is `Sold` with warranty dates, a `Sale` movement exists, and the shift's `cashSalesTotal` rises by 10,000,000

#### Scenario: Change is calculated by the server
- **WHEN** the order total is 9,500,000 and `cashTendered` = 10,000,000
- **THEN** `changeGiven` = 500,000 and the shift credit is 9,500,000, not 10,000,000

#### Scenario: Insufficient cash
- **WHEN** `cashTendered` is lower than the server-calculated total
- **THEN** the response is 400 with an error under `cashTendered` and no order, reservation, movement or shift change exists

### Requirement: Shift ownership
A checkout SHALL name `posSessionId` of an `Open` shift owned by the acting user; the order SHALL store that shift. Any other shift, a closed shift or an unknown id SHALL be rejected.

#### Scenario: Closed shift
- **WHEN** the named shift is `Closed`
- **THEN** the response is 409 and nothing is stored

#### Scenario: Another user's shift
- **WHEN** SalesStaff B names the open shift of A
- **THEN** the response is 404 and nothing is stored

### Requirement: Idempotent checkout
`checkoutKey` (GUID) SHALL be required and unique per acting user (unique index on `cashier_user_id`, `checkout_key`). A request repeating a stored key SHALL return 200 with the existing order and SHALL NOT create an order, change stock or credit the shift again, including two simultaneous requests with the same key.

#### Scenario: Double click
- **WHEN** the same request is posted twice with the same `checkoutKey`
- **THEN** one order exists, stock decreases once, the shift is credited once, the second response is 200 with the same order id

#### Scenario: Retry after network failure
- **WHEN** the first response was lost and the client posts again with the same key
- **THEN** the stored order is returned and nothing is applied twice

### Requirement: All-or-nothing rollback
If any step fails (validation, availability, a device already sold or reserved, database error), the transaction SHALL roll back so no order, order line, reservation, movement, device status change, shift credit or key remains.

#### Scenario: One line unavailable
- **WHEN** one of three lines names a device that is no longer `InStock`
- **THEN** the response is 409 naming the line, and the other two lines' stock and devices are unchanged

#### Scenario: Concurrent sale of the last device
- **WHEN** two checkouts with different keys name the same last device at the same time
- **THEN** exactly one order is `Completed`, the other gets 409, and the device and stock change once

### Requirement: Request validation
The checkout SHALL reject an empty `items` list, more lines than `CreateOrderValidator.MaxLines`, quantity less than 1 or above available stock, the same IMEI twice in one request, a line discount above the line amount, an inactive customer or product, and a missing `checkoutKey`, with 400 or 409 as defined by order creation, and SHALL store nothing.

#### Scenario: Duplicate IMEI in the cart
- **WHEN** two lines carry the same `serialImeiId`
- **THEN** the response is 400 with an error under the second line's `serialImeiId`

#### Scenario: Empty cart
- **WHEN** `items` is empty
- **THEN** the response is 400 and nothing is stored

### Requirement: Paid orders are frozen
A `Completed` order created by checkout SHALL NOT be cancelled or have its lines, method or totals changed through `/api/orders`; `PATCH /api/orders/{id}/status` SHALL return 409 for any target. An order cannot be marked paid by QR without a confirmation.

#### Scenario: Cancel a paid order
- **WHEN** a client patches a `Completed` order to `Cancelled`
- **THEN** the response is 409 and the order is unchanged

### Requirement: Cash checkout consumes the voucher use
When a cash checkout prices an order with a voucher, the checkout SHALL hold one use of that voucher and consume it in the same transaction as the payment, through the shared settlement. If the voucher is no longer usable, has no use left or was taken by a concurrent checkout, the checkout SHALL return 409 and store nothing.

#### Scenario: Voucher use consumed
- **WHEN** a cash checkout uses a voucher with one use left
- **THEN** the sale completes and the voucher has no use left

#### Scenario: Last use taken by another sale
- **WHEN** two cash checkouts with the last use of a voucher run at the same time
- **THEN** exactly one completes and the other returns 409 with no stock, shift or points change

#### Scenario: Retry with the same key
- **WHEN** the completed checkout is posted again with the same `checkoutKey`
- **THEN** the stored order is returned and no second use is consumed

