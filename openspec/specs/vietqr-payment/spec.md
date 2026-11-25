# vietqr-payment Specification

## Purpose
TBD - created by archiving change m4-vietqr-settlement. Update Purpose after archive.
## Requirements
### Requirement: Create a VietQR payment request
`POST /api/pos/vietqr` SHALL, for SalesStaff or Admin with an `Open` shift of their own, in one transaction create a `Pending` order with `paymentMethod` = `VietQr` bound to that shift and the acting cashier, reserve its devices and stock, hold one use of the voucher when the order uses one, and store one `vietqr_transactions` row `Pending` that expires 15 minutes after creation. The request carries `posSessionId`, `checkoutKey`, `customerId?`, `orderDiscountAmount?`, `voucherCode?`, `applyVipDiscount?`, `expectedTotal?`, `notes?` and `items` (as in cash checkout, with no cash fields). Prices, discount, tax and total SHALL come from the single server pricing service; client amounts SHALL be ignored. The response (201) SHALL contain `id`, `orderId`, `orderCode`, `status`, `amount`, `transactionCode` (the transfer memo), `bankBin`, `bankAccountNumber`, `bankAccountName`, `qrContent` (EMVCo VietQR string with CRC), `qrImagePng` (base64 PNG), `createdAt` and `expiresAt`. The receiving bank SHALL come from configuration `VietQr:BankBin`, `VietQr:AccountNumber` and `VietQr:AccountName`; when any is missing the endpoint SHALL return 503 and store nothing.

#### Scenario: QR created with holds
- **WHEN** a SalesStaff with an open shift posts a valid cart of one in-stock device priced 10,000,000
- **THEN** the response is 201 with `amount` = 10,000,000 and `status` = `Pending`; the device is `Reserved`, the order is `Pending` with method `VietQr` and the shift id, and `expiresAt` is 15 minutes after `createdAt`

#### Scenario: QR payload matches the order
- **WHEN** a QR is created for an order of 9,500,000 with transfer code `TS7K2M9X4Q1B`
- **THEN** `qrContent` encodes the configured bank and account, the amount 9500000 and the memo `TS7K2M9X4Q1B`, and ends with a valid CRC16 checksum

#### Scenario: Voucher use held
- **WHEN** the order uses a voucher with one use left
- **THEN** that use is held for the order and a second order using the same voucher is refused

#### Scenario: Retry with the same checkout key
- **WHEN** the same request is posted again with the same `checkoutKey`
- **THEN** the response is 200 with the stored QR and nothing is reserved or created twice

#### Scenario: Shift closed or not the caller's
- **WHEN** the named shift is closed, unknown or belongs to another user
- **THEN** the response is 409, 404 and 404 respectively and nothing is stored

#### Scenario: Unavailable device
- **WHEN** a line names a device that is no longer `InStock`
- **THEN** the response is 409 naming the line and no order, reservation, QR or voucher hold remains

#### Scenario: Bank not configured
- **WHEN** `VietQr:BankBin`, `AccountNumber` or `AccountName` is missing
- **THEN** the response is 503 and no order or hold is created

### Requirement: One pending QR per order and a frozen order
An order SHALL have at most one `Pending` QR (partial unique index). While its QR is `Pending` the order's lines, total and method SHALL NOT change through any API, and `PATCH /api/orders/{id}/status` SHALL NOT complete it without a confirmed QR for the full total.

#### Scenario: Second pending QR
- **WHEN** a second QR is requested for an order that already has a `Pending` one
- **THEN** the request is refused with 409 and the first QR is unchanged

### Requirement: Read QR status
`GET /api/pos/vietqr/{id}` SHALL return the same DTO with the current `status` (`Pending`, `Confirmed`, `Failed` or `Expired`) and the order status. SalesStaff SHALL read only their own QR (404 otherwise); Manager and Admin SHALL read any. A `Pending` QR past `expiresAt` SHALL be expired by the read before it is returned.

#### Scenario: Poll after payment
- **WHEN** the QR was confirmed
- **THEN** the read returns `status` = `Confirmed` and the order `Completed`

#### Scenario: Poll after the deadline
- **WHEN** a `Pending` QR is read after `expiresAt`
- **THEN** the read returns `Expired`, the order is `Cancelled` and the held devices and voucher use are released once

### Requirement: Cancel and expire release every hold once
Cancelling or expiring a `Pending` QR SHALL set the QR to `Failed` or `Expired`, the order to `Cancelled`, release the reserved devices and stock through the inventory service, and release the held voucher use through the voucher service, in one transaction keyed by the QR id. Repeating the call SHALL change nothing. A `BackgroundService` SHALL expire due `Pending` QRs every 30 seconds using the same logic; each QR SHALL be handled in its own transaction so one failure does not stop the rest. Cancel SHALL be refused after the deadline and expire before it.

#### Scenario: Cancel with a voucher
- **WHEN** a SalesStaff cancels their pending QR whose order holds a voucher use
- **THEN** the QR is `Failed`, the order `Cancelled`, the devices are `InStock` and the voucher use is available again, each exactly once

#### Scenario: Cancel twice
- **WHEN** the same cancel is sent twice
- **THEN** the second response is 200 with the same state and no second release occurs

#### Scenario: Worker expires an overdue QR
- **WHEN** a `Pending` QR passes `expiresAt` and the worker runs
- **THEN** it becomes `Expired` with its holds released once, without any request from the cashier

#### Scenario: Other cashier
- **WHEN** another SalesStaff cancels the QR
- **THEN** the response is 404 and nothing changes

### Requirement: Manual confirmation by Admin or Manager
`POST /api/pos/vietqr/{id}/confirm` SHALL let Admin and Manager confirm a `Pending`, unexpired QR with `referenceCode` (the bank's transaction reference, required) and `receivedAmount` (the amount seen on the bank statement, required). It SHALL record `provider` = `manual`, `providerTransactionId` = `referenceCode`, `confirmedByUserId` and `confirmedAt`, then complete the sale through the shared settlement. SalesStaff and WarehouseStaff SHALL receive 403. `receivedAmount` SHALL equal the QR amount exactly; any other value SHALL be refused with 400 and the QR SHALL stay `Pending`.

#### Scenario: Manager confirms
- **WHEN** a Manager confirms a pending QR of 10,000,000 with reference `FT26112500123` and received amount 10,000,000
- **THEN** the QR is `Confirmed` with the manager and time recorded, the order is `Completed`, stock and warranty are applied once and the shift `vietqr_sales_total` rises by 10,000,000

#### Scenario: Wrong amount
- **WHEN** `receivedAmount` is 9,000,000 for a 10,000,000 QR
- **THEN** the response is 400, the QR stays `Pending` and the order is unchanged

#### Scenario: Reference already used
- **WHEN** the same `referenceCode` was used for another QR
- **THEN** the response is 409 and nothing changes

#### Scenario: Sales cannot confirm
- **WHEN** a SalesStaff calls the endpoint
- **THEN** the response is 403

### Requirement: Sandbox webhook
`POST /api/payments/vietqr/webhook` SHALL be reachable without a user token but SHALL require headers `X-Timestamp` (Unix seconds, within 5 minutes of server time) and `X-Signature` (lowercase hex HMAC-SHA256 of `timestamp + "." + rawBody` with `VietQr:WebhookSecret`), compared in constant time. The JSON body carries `transactionId`, `amount`, `transferContent`, `accountNumber` and `transferredAt`. A missing or wrong signature, a stale timestamp or a missing secret SHALL be refused (401, 401, 401, 503) and SHALL store nothing. A valid call SHALL record the event, then match `transferContent` to a transfer code and confirm the QR with `provider` = `sandbox`, through the same service as manual confirmation. The endpoint and its responses SHALL state that it is a sandbox integration.

#### Scenario: Valid signed notification
- **WHEN** a correctly signed notification matches a pending QR in amount, account and code
- **THEN** the QR is confirmed with provider `sandbox`, the order completes once and the response is 200

#### Scenario: Replayed notification
- **WHEN** the same signed notification arrives again
- **THEN** the response is 200, nothing is applied a second time and the event is not duplicated

#### Scenario: Bad signature
- **WHEN** the signature does not match the body
- **THEN** the response is 401 and no event is stored

#### Scenario: Lost response
- **WHEN** the first notification committed but its response was lost and the sender retries
- **THEN** the retry returns 200 with the stored outcome and no second payment is recorded

### Requirement: Confirmation races with expiry and cancel
Confirmation, cancel and expiry of the same QR SHALL be serialized by locking, in this order: the shift, the order, the order's QR rows, then inventory and voucher rows. Exactly one outcome SHALL win; a confirmation that loses SHALL NOT complete the order.

#### Scenario: Confirm against expire
- **WHEN** a confirmation and an expiry of the same QR run at the same time
- **THEN** either the order is `Completed` and the QR `Confirmed`, or the QR is `Expired` and the order `Cancelled` with the money kept for reconciliation, never both

#### Scenario: Confirm against cancel
- **WHEN** the cashier cancels while a notification arrives
- **THEN** one wins; if the cancel wins the money is stored for reconciliation and stock is released once

### Requirement: List QR requests
`GET /api/pos/vietqr` SHALL return a page of QR requests newest first, filterable by `status` and `posSessionId`, with order code, amount, cashier, status, `expiresAt` and seconds left. SalesStaff SHALL see only their own; Admin and Manager SHALL see all. The POS page uses it to resume a pending QR and the confirm screen uses it to list pending ones.

#### Scenario: Resume after leaving
- **WHEN** a cashier with a pending QR lists `status=Pending` for their shift
- **THEN** exactly that QR is returned with the remaining seconds

#### Scenario: Sales sees own only
- **WHEN** two cashiers each have a pending QR and one lists them
- **THEN** only that cashier's QR is returned

