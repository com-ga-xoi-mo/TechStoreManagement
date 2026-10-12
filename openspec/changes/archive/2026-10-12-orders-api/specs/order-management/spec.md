# Spec Delta

## Purpose

Lets store staff create retail orders against live catalog and inventory data, find and inspect orders, and move an order through its payment and fulfilment lifecycle while the sold devices and stock stay consistent, through the TechStore REST API with predictable validation and conflict responses.

## ADDED Requirements

### Requirement: Create a pending order
`POST /api/orders` SHALL create an order in status `Pending` with at least one line and return 201 with the full order and a `Location` header `/api/orders/{id}`. A `Pending` order is a draft: it SHALL NOT reserve devices or stock. The order's cashier SHALL be the acting user. `customerId` is optional (a walk-in sale has none); when provided it SHALL reference an existing, active customer. `paymentMethod` is optional and defaults to `Cash`; `notes` is optional and at most 500 characters. `posSessionId`, `paidAt` and `completedAt` SHALL be empty on creation.

#### Scenario: Walk-in order created
- **WHEN** a client posts a valid request without `customerId`
- **THEN** the response is 201 with `status` = `Pending`, `customerId` = null, `paymentMethod` = `Cash`, a generated `orderCode`, and the stored lines

#### Scenario: Unknown or inactive customer
- **WHEN** a client posts a `customerId` that does not exist or belongs to an inactive customer
- **THEN** the response is 400 with an error under `customerId` and nothing is stored

#### Scenario: Pending order holds no reservation
- **WHEN** a pending order containing an `InStock` device is created
- **THEN** that device is still `InStock` and the stock row's `reserved_quantity` is unchanged

### Requirement: Server-side pricing and snapshots
The server SHALL determine each line's `unitPrice` from the catalog: the variant's `price` when a variant is selected, otherwise the product's `basePrice`. The client SHALL NOT supply prices. Each line SHALL snapshot the product name, variant name (null without a variant) and SKU (the variant SKU when a variant is selected, otherwise the product SKU). A line's `totalPrice` SHALL equal `unitPrice × quantity − discountAmount`; the order's `subtotal` SHALL equal the sum of line totals; `discountAmount` and `taxAmount` on the order SHALL be 0 in this milestone; `totalAmount` SHALL equal `subtotal − discountAmount + taxAmount`. The optional line `discountAmount` defaults to 0, SHALL NOT be negative and SHALL NOT exceed `unitPrice × quantity`.

#### Scenario: Price taken from the variant
- **WHEN** a line selects a variant priced 29,990,000 with quantity 1
- **THEN** the stored line has `unitPrice` = 29,990,000 and `totalPrice` = 29,990,000, and the order `totalAmount` = 29,990,000

#### Scenario: Price taken from the product when no variants exist
- **WHEN** a line selects a product without variants whose `basePrice` is 350,000 with quantity 3 and `discountAmount` 50,000
- **THEN** the stored line has `unitPrice` = 350,000 and `totalPrice` = 1,000,000

#### Scenario: Client-supplied prices are ignored
- **WHEN** a request body contains a `unitPrice` or `totalAmount` property
- **THEN** the stored values are still calculated from the catalog

#### Scenario: Line discount larger than the line
- **WHEN** a line's `discountAmount` exceeds `unitPrice × quantity`
- **THEN** the response is 400 with an error under that line's `discountAmount`

### Requirement: Line validation
Every line SHALL reference an active product. When the product has variants (active or inactive), `variantId` is required, SHALL belong to that product and SHALL be active; when the product has no variants, `variantId` SHALL be omitted. For a serial-tracked product (`is_serial_tracked = true`) the line SHALL carry one `serialImeiId`, its `quantity` SHALL be 1 (or omitted), and the device SHALL exist, be `InStock`, belong to the same product and have the same variant as the line (a null variant only matches a null variant). For a non-tracked product `serialImeiId` SHALL be omitted, `quantity` defaults to 1 and SHALL be a positive integer not above available stock (`quantity − reserved_quantity` of its `inventory_stocks` row), and the stock row SHALL exist. The same device SHALL NOT appear twice in a request and two lines of one request SHALL NOT share the same non-tracked product and variant. An order SHALL have between 1 and 100 lines. All input errors SHALL be reported together in one 400 response keyed by camelCase field path such as `items[1].serialImeiId`.

#### Scenario: Device already sold
- **WHEN** a line references a device whose status is `Sold`
- **THEN** the response is 400 with an error under `items[i].serialImeiId` and nothing is stored

#### Scenario: Device belongs to another product
- **WHEN** a line's `serialImeiId` references a device of a different product or a different variant
- **THEN** the response is 400 with an error under `items[i].serialImeiId`

#### Scenario: Serial-tracked line without a device
- **WHEN** a line for a serial-tracked product has no `serialImeiId`
- **THEN** the response is 400 with an error under `items[i].serialImeiId`

#### Scenario: Variant required
- **WHEN** a line selects a product that has variants but omits `variantId`
- **THEN** the response is 400 with an error under `items[i].variantId`

#### Scenario: Insufficient stock for a non-tracked line
- **WHEN** a non-tracked line asks for more units than `quantity − reserved_quantity`
- **THEN** the response is 400 with an error under `items[i].quantity`

#### Scenario: Missing stock row
- **WHEN** a line references a product or variant that has no `inventory_stocks` row
- **THEN** the response is 400 with an error under that line indicating stock is not initialized

#### Scenario: Several errors reported together
- **WHEN** a request has an unknown `customerId`, an unknown `paymentMethod` and a line with a negative `discountAmount`
- **THEN** one 400 response lists the errors under `customerId`, `paymentMethod` and `items[0].discountAmount`

#### Scenario: Empty order
- **WHEN** `items` is missing or empty
- **THEN** the response is 400 with an error under `items`

### Requirement: Order code generation
Each order SHALL receive a unique code `ORD-{yyyyMM}-{NNNN}` using the UTC creation month and a 4-digit sequence that continues from the highest existing code of that month (first order of a month is `0001`). When two orders race for the same code the loser SHALL retry with a fresh code instead of failing.

#### Scenario: First order of a month
- **WHEN** no order code with prefix `ORD-202611-` exists and an order is created in November 2026 UTC
- **THEN** its code is `ORD-202611-0001`

#### Scenario: Sequence continues
- **WHEN** the highest code of the month is `ORD-202610-0007`
- **THEN** the next order of that month receives `ORD-202610-0008`

#### Scenario: Code collision retried
- **WHEN** saving fails because the generated code is already used by a concurrent order
- **THEN** the service retries with a new code and the request still succeeds

### Requirement: Enumerations are exchanged as strings
`status` and `paymentMethod` SHALL be sent and returned as their enum names (`Pending`, `Cash`, `VietQr`...), parsed case-insensitively; numeric values and unknown names SHALL be rejected with 400.

#### Scenario: Unknown payment method
- **WHEN** a client posts `paymentMethod` = `Bitcoin` or `1`
- **THEN** the response is 400 with an error under `paymentMethod`

### Requirement: Paginated order listing
`GET /api/orders` SHALL return a paginated result with `items` (order summaries), `page`, `pageSize`, `totalCount`, `totalPages`; `page` defaults to 1 (values below 1 become 1), `pageSize` defaults to 20 and is clamped to 1..100. Optional filters combine with AND: `status`, `customerId`, `from` (created at or after), `to` (created before) and `q` (case-insensitive substring of the order code, the customer's full name or phone, with wildcard characters escaped). `from` later than `to` SHALL be a 400 error under `from`. `sort` accepts `createdAt:asc|desc` and `totalAmount:asc|desc`, defaults to `createdAt:desc`, and an unknown value SHALL be a 400 error under `sort`; `Id` is the tie-breaker so pages are stable. A summary SHALL contain id, order code, status, payment method, customer id and name (null for walk-in), total amount, line count, created, paid and completed timestamps.

#### Scenario: Default listing
- **WHEN** a client calls `GET /api/orders`
- **THEN** the response is 200, newest orders first, at most 20 items

#### Scenario: Filter by status
- **WHEN** a client calls `GET /api/orders?status=completed`
- **THEN** only orders with status `Completed` are returned and `totalCount` counts only them

#### Scenario: Keyword matches customer phone
- **WHEN** a client calls `GET /api/orders?q=0901234567`
- **THEN** orders of the customer with that phone are returned

#### Scenario: Invalid filters
- **WHEN** a client calls `GET /api/orders?status=foo&sort=customer`
- **THEN** the response is 400 with errors under `status` and `sort`

#### Scenario: Page beyond the last page
- **WHEN** a client requests a page number greater than `totalPages`
- **THEN** the response is 200 with empty `items` and correct totals

### Requirement: Order detail
`GET /api/orders/{id}` SHALL return the order with its customer summary and every line ordered by product name, SKU then id. Each line SHALL include product / variant ids, snapshot names, SKU, `unitPrice`, `quantity`, `discountAmount`, `totalPrice` and, for serial-tracked lines, the device's `serialImeiId`, `imei` and `serialNumber`. Line cost snapshots (`unit_cost`) SHALL NOT be returned. An unknown id SHALL return 404.

#### Scenario: Detail with sold device
- **WHEN** a client fetches a completed order containing a phone line
- **THEN** the line shows the IMEI of the sold device

#### Scenario: Unknown order
- **WHEN** a client fetches an id that does not exist
- **THEN** the response is 404

### Requirement: Order status transitions
`PATCH /api/orders/{id}/status` with `{ "targetStatus": "..." }` SHALL apply only these transitions: `Pending → Processing`, `Pending → Completed`, `Processing → Completed`, `Pending → Cancelled`. Any other combination from the current status (including repeating the current status and every transition out of `Completed`, `Cancelled`, `PartiallyRefunded` or `Refunded`) SHALL return 409 and change nothing. A `targetStatus` that is not one of `Processing`, `Completed`, `Cancelled` (for example `Pending`, `Refunded` or an unknown name) SHALL return 400. An unknown order SHALL return 404. The endpoint SHALL lock the order row, so concurrent transitions of one order execute one after another. On success the response is 200 with the updated order detail.

#### Scenario: Pending to Processing
- **WHEN** a `Pending` order is moved to `Processing`
- **THEN** every serial-tracked line's device becomes `Reserved`, the stock rows' `reserved_quantity` increases by the line quantities, `paid_at` is set, `completed_at` stays empty, and no inventory movement is written

#### Scenario: Processing to Completed
- **WHEN** a `Processing` order is moved to `Completed`
- **THEN** devices become `Sold` with warranty start = completion time and warranty end = start + `warranty_months`, stock `quantity` and `reserved_quantity` decrease by the line quantities, one `Sale` movement per line is recorded with the line id, each line's `unit_cost` is snapshotted, and `completed_at` is set while `paid_at` is unchanged

#### Scenario: Cash shortcut
- **WHEN** a `Pending` order is moved straight to `Completed`
- **THEN** both steps above happen in one transaction and `paid_at` equals `completed_at`

#### Scenario: Cancel a pending order
- **WHEN** a `Pending` order is moved to `Cancelled`
- **THEN** only the order status changes; no device or stock is touched

#### Scenario: Paid order cannot be cancelled here
- **WHEN** a `Processing` order is moved to `Cancelled`
- **THEN** the response is 409 and the order, devices and stock are unchanged

#### Scenario: Repeated transition
- **WHEN** a `Completed` order is moved to `Completed` again
- **THEN** the response is 409 and no second sale movement, stock deduction or warranty reset happens

#### Scenario: Disallowed target status
- **WHEN** a client sends `targetStatus` = `Refunded`
- **THEN** the response is 400 with an error under `targetStatus`

### Requirement: Cost snapshot at fulfilment
When an order is completed, each line's `unit_cost` SHALL be set from the sold device's `unit_cost` for serial-tracked lines, falling back to the product's `cost_price` when the device has none, and from the product's `cost_price` for non-tracked lines. Completed lines SHALL keep that value even if the product's cost later changes.

#### Scenario: Device cost wins
- **WHEN** a line's device has `unit_cost` 21,500,000 and the product's `cost_price` is 22,000,000
- **THEN** the completed line's `unit_cost` is 21,500,000

#### Scenario: Fallback to product cost
- **WHEN** the device has no `unit_cost`
- **THEN** the completed line's `unit_cost` is the product's `cost_price`

### Requirement: Conflicts and atomicity on transition
A transition that moves devices or stock SHALL run in one transaction that locks the order, then the affected devices in ascending id order, then the affected stock rows in ascending product / variant order. If any device is no longer `InStock` (or `Reserved` for fulfilment), the product or variant no longer matches, a stock row is missing or lacks available quantity, the response SHALL be 409 and the order status, devices, stock, movements and warranty SHALL be exactly as before the call.

#### Scenario: Two orders share one device
- **WHEN** two `Pending` orders contain the same `InStock` device and the first is completed
- **THEN** completing the second returns 409, the second order stays `Pending`, and the device remains `Sold` for the first order only

#### Scenario: Partial failure rolls back everything
- **WHEN** an order has two lines and the second line's stock is insufficient when it is completed
- **THEN** the first line's device and stock changes are rolled back too

#### Scenario: Product deactivated before payment
- **WHEN** a product (or selected variant) of a `Pending` order has been deactivated and the order is moved to `Processing` or `Completed`
- **THEN** the response is 409, the order stays `Pending` and nothing is reserved

#### Scenario: Reservation protects a paid order
- **WHEN** an order is `Processing` and another `Pending` order containing the same device is moved to `Processing`
- **THEN** the second transition returns 409 because the device is `Reserved`

### Requirement: Acting user
Until authentication exists, the acting user SHALL be taken from the `sub` / name-identifier claim when present, otherwise the fixed default administrator id `11111111-1111-1111-1111-111111111111`. The acting user SHALL be stored as `cashier_user_id` on creation and as `performed_by_user_id` on movements written by a transition.

#### Scenario: Unauthenticated request
- **WHEN** a request has no claims
- **THEN** the order is created with `cashier_user_id` = `11111111-1111-1111-1111-111111111111`
