# inventory-purchase-receipts Specification

## Purpose

Lets warehouse staff record each delivery of goods as one purchase receipt that adds stock, devices, purchase cost and ledger history in a single all-or-nothing step, and lets staff review past receipts.

## Requirements

### Requirement: Purchase receipt request validation
`POST /api/inventory/purchase-receipts` SHALL take `clientRequestId`, an active `supplierId`, optional `supplierDocumentNumber` (≤ 100 characters) and `note` (≤ 1000), and 1–100 lines. Each line SHALL name an existing product and, if given, a variant of it, appear once per receipt, have `quantity` 1–100,000, a `unitCost` from 0 to 1,000,000,000,000 whose line total fits `numeric(18,2)`, and `warrantyMonths` 0–120 (default 12).

#### Scenario: Invalid header
- **WHEN** the request has an empty `clientRequestId`, no supplier, an unknown or inactive supplier, the "Số dư đầu kỳ" supplier, or text above its length limit
- **THEN** the API returns HTTP 400 ValidationProblem keyed by field and writes nothing

#### Scenario: Invalid line
- **WHEN** a line has an unknown product, a variant of another product, quantity 0 or negative, a missing or negative unit cost, or warranty months outside 0–120
- **THEN** the API returns HTTP 400 ValidationProblem keyed as `lines[n].<field>` and writes nothing

#### Scenario: Same product twice
- **WHEN** two lines name the same product and the same variant (or both no variant)
- **THEN** the API returns HTTP 400 under `lines[n]` and writes nothing

#### Scenario: Empty receipt
- **WHEN** `lines` is missing, empty or has more than 100 entries
- **THEN** the API returns HTTP 400 under `lines` and writes nothing

### Requirement: Variant required for products with variants
A line for a product that has variants SHALL name one of its active variants, because orders sell such products only by variant. A line for a product without variants SHALL name no variant.

#### Scenario: Variant missing
- **WHEN** a line names "iPhone 16", which has the variant "128GB Đen", without `variantId`
- **THEN** the API returns HTTP 400 under `lines[n].variantId` and writes nothing

#### Scenario: Inactive variant
- **WHEN** a line names a variant whose `is_active` is false
- **THEN** the API returns HTTP 400 under `lines[n].variantId` and writes nothing

### Requirement: Serial and IMEI lines on a receipt
When goods are received, a line for a serial-tracked product SHALL carry exactly `quantity` devices, each with a serial number of at most 100 characters after trimming, an IMEI of exactly 15 digits, or both. A line for a product that is not serial-tracked SHALL carry no devices. One receipt SHALL carry at most 500 devices. A serial number or IMEI repeated within the receipt or already present in the system SHALL be refused.

#### Scenario: Device count differs from quantity
- **WHEN** a serial-tracked line has quantity 5 and 4 devices
- **THEN** the API returns HTTP 400 under `lines[n].devices` and writes nothing

#### Scenario: Devices on goods without serial
- **WHEN** a line for a product with `is_serial_tracked = false` carries devices
- **THEN** the API returns HTTP 400 under `lines[n].devices` and writes nothing

#### Scenario: Bad IMEI format
- **WHEN** a device has an IMEI that is not 15 digits, a serial number longer than 100 characters, or neither serial number nor IMEI
- **THEN** the API returns HTTP 400 under `lines[n].devices[m]` and writes nothing

#### Scenario: Duplicate identifiers
- **WHEN** an IMEI or serial number appears twice in the receipt, or already belongs to any device in the system
- **THEN** the API returns HTTP 409 ProblemDetails listing the duplicate identifiers and writes nothing

### Requirement: Receipt is recorded in one transaction
A valid immediate receipt (default) SHALL, in one transaction, create a `Received` purchase order with a server-generated unique number `PN-yyyyMMdd-XXXXXXXX`, the caller as creator and receiver, the current UTC time as ordered and received time, and `total_cost` = Σ quantity × unit cost. It SHALL add one line per request line with ordered = received = quantity, and return HTTP 201 with the receipt detail and Location `/api/inventory/purchase-receipts/{id}`.

#### Scenario: Receive goods without serial
- **WHEN** a WarehouseStaff user receives 20 "Cáp USB-C 1m" at 45,000 from supplier "FPT Trading" with document number "HD-0912"
- **THEN** the API returns HTTP 201 with total 900,000, supplier "FPT Trading", document "HD-0912", the user as receiver and one line of 20 × 45,000

#### Scenario: Client totals are ignored
- **WHEN** the request also carries a total or line total that differs from quantity × unit cost
- **THEN** the stored and returned totals are the ones computed by the server

#### Scenario: Failure leaves nothing behind
- **WHEN** any line fails validation, a duplicate is found, or the database rejects a write partway through
- **THEN** no purchase order, line, stock change, movement or device from that request exists afterwards

### Requirement: Receipt updates stock, devices and ledger
For each physically received line the receipt SHALL add `quantity` to the product/variant stock balance, creating the balance with quantity 0, reserved 0 and low-stock alert 5 when absent. It SHALL write one `PurchaseReceipt` movement linked to the line with the new `quantity_after` and a note naming the receipt number, and create each device `InStock` with the line's unit cost and warranty months and a link to the receipt.

#### Scenario: Restock an existing balance
- **WHEN** a product has quantity 8 and reserved 2, and a receipt adds 20
- **THEN** quantity becomes 28, reserved stays 2, and the stock detail history shows a `PurchaseReceipt` movement of +20 with quantity after 28 and the receipt number in its note

#### Scenario: First stock for a product
- **WHEN** a receipt includes a product that has no stock balance
- **THEN** a balance is created with the received quantity and the product appears in the stock list

#### Scenario: Receive phones with IMEI
- **WHEN** a receipt line receives 5 "iPhone 16 128GB Đen" at 18,200,000 with 5 IMEIs and 12 warranty months
- **THEN** 5 devices exist with status `InStock`, `unit_cost` 18,200,000, `warranty_months` 12 and this receipt as purchase order, and the variant's stock quantity equals its `InStock` plus `Reserved` devices

### Requirement: Received cost feeds cost of goods sold
Only physically received quantities SHALL count as received purchase cost, so a later sale of goods without serial snapshots the average received unit cost weighted by quantity across all received lines of that product/variant, and a sold device snapshots its own unit cost.

#### Scenario: New lot at a different price
- **WHEN** a product has an opening balance of 10 at 100,000, a receipt adds 10 at 120,000, and one unit is then sold and completed
- **THEN** the order line's cost snapshot is 110,000

### Requirement: Repeated submission is recorded once
A receipt SHALL be identified by its `clientRequestId`. A request whose `clientRequestId` already belongs to a receipt SHALL return HTTP 200 with that receipt and change nothing, before any check against the current supplier, product or stock data, and including when two such requests run at the same time.

#### Scenario: Retry after a network error
- **WHEN** the client sends the same receipt twice with the same `clientRequestId`
- **THEN** the first call returns HTTP 201, the second returns HTTP 200 with the same receipt id, and stock, devices and movements increased only once

#### Scenario: Concurrent duplicates
- **WHEN** two requests with the same `clientRequestId` run in parallel transactions, for products that have no stock balance yet and with IMEI lines
- **THEN** exactly one receipt exists afterwards, one caller receives HTTP 201 and the other HTTP 200 with the same receipt id, and neither receives HTTP 409

#### Scenario: Retry after the supplier changed
- **WHEN** a receipt was recorded, its supplier was then deactivated, and the client resends the same `clientRequestId`
- **THEN** the API returns HTTP 200 with the recorded receipt instead of a validation error

### Requirement: Concurrent receipts stay consistent
Receipts SHALL lock the affected stock balances in ascending product, then variant order, so that receipts and sales on the same balance never lose an update or deadlock.

#### Scenario: Two receipts for the same product
- **WHEN** two receipts for the same product of 10 and 15 units commit in parallel on a balance of 5
- **THEN** the quantity is 30 and there are two `PurchaseReceipt` movements with quantity after 15 and 30 in commit order

#### Scenario: Two receipts with the same IMEI
- **WHEN** two different receipts carrying the same IMEI run in parallel
- **THEN** one returns HTTP 201, the other HTTP 409, and only one device with that IMEI exists

### Requirement: Purchase receipt listing
`GET /api/inventory/purchase-receipts` SHALL return a page of Ordered, PartiallyReceived, Received and Cancelled purchase orders, including opening-balance orders, newest receipt activity first (received time or creation time), filtered by `search` (receipt number, supplier document number or supplier name, case-insensitive, wildcards literal) and `supplierId`. Each item SHALL show id, number, status, supplier name, document number, received time, receiver name (empty when unknown), line count, total quantity and total cost.

#### Scenario: Find a receipt by document number
- **WHEN** a Manager requests `/api/inventory/purchase-receipts?search=HD-0912`
- **THEN** the API returns HTTP 200 with the receipt whose supplier document number is "HD-0912"

#### Scenario: Opening balances are listed
- **WHEN** stock was initialized for goods without serial
- **THEN** its "OB-" purchase order appears in the list with supplier "Số dư đầu kỳ"

### Requirement: Purchase receipt detail
`GET /api/inventory/purchase-receipts/{id}` SHALL return the receipt header with note and every line with product name, SKU, variant name, quantity, unit cost, line total and, for serial-tracked lines, each device's serial number, IMEI and current status. Unknown ids SHALL return HTTP 404.

#### Scenario: View a receipt
- **WHEN** an Admin opens the receipt of 5 iPhones of which one was sold since
- **THEN** the detail lists the 5 devices, one with status `Sold` and four `InStock`

#### Scenario: Unknown receipt
- **WHEN** the id does not exist
- **THEN** the API returns HTTP 404 ProblemDetails

### Requirement: Purchase receipt permissions
Creating a receipt SHALL be allowed to Admin and WarehouseStaff; listing and detail SHALL be allowed to Admin, Manager and WarehouseStaff. SalesStaff SHALL receive HTTP 403 and unauthenticated callers HTTP 401. Recorded receipts SHALL NOT be editable or deletable through the API.

#### Scenario: Sales cannot see purchase costs
- **WHEN** a SalesStaff user calls list, detail or create
- **THEN** the API returns HTTP 403 and writes nothing

#### Scenario: Manager cannot receive goods
- **WHEN** a Manager posts a receipt
- **THEN** the API returns HTTP 403 and writes nothing

### Requirement: Incremental receiving and cancellation

Admin and WarehouseStaff SHALL be able to set `receiveLater` on creation. An ordered receipt SHALL record quantities, unit cost and warranty, accept no device identifiers and leave physical stock and actual cost unchanged. Manager SHALL have read-only access. Legacy callers SHALL retain immediate receipt behavior.

#### Scenario: Two deliveries against one receipt
- **WHEN** a supplier delivers two of five units, then the other three
- **THEN** the receipt progresses from Ordered to PartiallyReceived to Received, with each batch recording its actor, timestamp, source document where supplied, stock movements and actual cost

#### Scenario: Conflicting or retried delivery
- **WHEN** two clients post deliveries to the same receipt
- **THEN** the API locks the receipt before checking each line's expected received quantity and remaining balance. It applies each client request ID once per receipt. A stale or excessive batch returns 409 and writes no lines

#### Scenario: Cancel the unreceived balance
- **WHEN** a writer cancels an Ordered or PartiallyReceived receipt
- **THEN** the receipt becomes Cancelled, the actor and cancellation time are saved, prior stock, devices and costs are preserved, and later new deliveries return 409. Repeating cancellation returns the closed receipt. A fully received receipt cannot be cancelled
