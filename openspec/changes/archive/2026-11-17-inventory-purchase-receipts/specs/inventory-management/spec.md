# Spec Delta

## MODIFIED Requirements

### Requirement: Initial Stock Balance Initialization
The system SHALL allow staff with the Admin or WarehouseStaff role to initialize stock balances via direct route `/api/inventory/stocks/initialize` within an ACID transaction and record an audit movement ledger, validating product existence, variant ownership, and strictly positive opening quantities. It SHALL refuse products with `is_serial_tracked = true`, whose stock enters only together with its devices through a purchase receipt or a Serial/IMEI import.
#### Scenario: Successful stock initialization
- **WHEN** an authorized user posts valid initial stock quantities (`quantity > 0`) for an existing product and matching variant to `/api/inventory/stocks/initialize`
- **THEN** system creates the inventory stock record, creates an `InventoryMovement` record of type `OpeningBalance` with the user ID, and returns HTTP 201 Created with Location header `/api/inventory/stocks?productId={productId}`

#### Scenario: Initialize existing stock balance conflict
- **WHEN** client attempts to initialize stock for a product and variant pair that already has an inventory stock balance row
- **THEN** system rejects the request and returns HTTP 409 Conflict ProblemDetails indicating stock is already initialized

#### Scenario: Stock initialization validation failure
- **WHEN** client posts invalid quantities (non-positive quantity `quantity <= 0` or negative min stock alert), a non-existent product ID, or a mismatched variant ID
- **THEN** system rejects the request and returns HTTP 400 Bad Request with validation details

#### Scenario: Stock initialization validation failure for non-positive quantity
- **WHEN** client posts `quantity <= 0` in an initialization request
- **THEN** system rejects the request and returns HTTP 400 Bad Request indicating opening quantity must be greater than zero

#### Scenario: Stock initialization validation failure for non-existent product
- **WHEN** client posts a `productId` that does not exist in the catalog
- **THEN** system rejects the request and returns HTTP 400 Bad Request indicating the product does not exist

#### Scenario: Stock initialization validation failure for mismatched variant
- **WHEN** client posts a `variantId` that does not belong to the specified `productId`
- **THEN** system rejects the request and returns HTTP 400 Bad Request indicating the variant does not belong to the product

#### Scenario: Opening stock of goods without serial/IMEI records its cost
- **WHEN** an authorized user initializes stock for a product with `is_serial_tracked = false` and sends `quantity` 5 and `unitCost` 200,000
- **THEN** system also creates one received purchase line (ordered = received = 5, `unit_cost` 200,000) on a system purchase order for the inactive supplier "Số dư đầu kỳ", and links the `OpeningBalance` movement to that line through `purchase_order_item_id`

#### Scenario: Opening stock of goods without serial/IMEI requires a cost
- **WHEN** client initializes stock for a product with `is_serial_tracked = false` and omits `unitCost`, sends a negative `unitCost`, one above 1,000,000,000,000, or a `quantity` multiplied by `unitCost` too large for `numeric(18,2)`
- **THEN** system rejects the request with HTTP 400 Bad Request under `unitCost` and writes no stock, movement or purchase order

#### Scenario: Opening stock of serial-tracked goods needs no cost
- **WHEN** client initializes stock with `quantity` 5 for a product with `is_serial_tracked = true`, with or without `unitCost` (the cost is never the question: such goods cannot be initialized at all)
- **THEN** system rejects the request with HTTP 400 Bad Request under `productId` telling the user to receive the devices through a purchase receipt, and writes no stock, movement or purchase order

#### Scenario: Stock initialization unauthorized access
- **WHEN** an unauthenticated request, or an authenticated user without the Admin or WarehouseStaff role (including Manager and SalesStaff), calls the initialization endpoint
- **THEN** system rejects the request with HTTP 401 Unauthorized or HTTP 403 Forbidden and writes no stock row or movement

### Requirement: Serial and IMEI Hardware Listing
The system SHALL provide paginated query capabilities for physical Serial/IMEI items via direct route `/api/inventory/serials` filtered by a keyword `search`, Product ID, Variant ID, and lifecycle Status. Each device's `unitCost` SHALL be returned to Admin, Manager and WarehouseStaff and SHALL be `null` for SalesStaff.
#### Scenario: Query serial/IMEI items with filters
- **WHEN** client sends a GET request to `/api/inventory/serials` specifying product ID and status filter
- **THEN** system returns HTTP 200 with `PagedResultDto<SerialImeiDto>` matching the criteria

#### Scenario: Query serial/IMEI items across statuses
- **WHEN** client queries serials filtering by status `InStock`, `Reserved`, or `Sold`
- **THEN** system filters matching physical items with their warranty information and purchase order linkage

#### Scenario: Serial keyword search treats wildcard characters literally
- **WHEN** client sends `search` containing `%` or `_`
- **THEN** system matches serial number, IMEI, product name or variant name containing that exact text case-insensitively, without wildcard expansion

#### Scenario: Device cost hidden from sales staff
- **WHEN** a SalesStaff user lists serials of devices whose `unit_cost` is 18,200,000
- **THEN** every returned item has `unitCost = null`, while the same request by a WarehouseStaff or Manager user returns 18,200,000

### Requirement: Transactional stock adjustment API
The inventory API SHALL expose `POST /api/inventory/stocks/{stockId}/adjust` for Admin and WarehouseStaff and return the refreshed detail after a successful adjustment. Any change on the balance of a serial-tracked product SHALL be refused, because devices enter stock only through a purchase receipt or a Serial/IMEI import and leave it only through a sale or a per-device defect report.

#### Scenario: Valid adjustment request
- **WHEN** an authorized user posts a non-zero change with a reason
- **THEN** the API commits the adjustment and returns HTTP 200 with the updated detail

#### Scenario: Invalid or conflicting adjustment
- **WHEN** the request is invalid or violates the reserved-quantity invariant
- **THEN** the API returns HTTP 400 or 409 ProblemDetails without a partial write

#### Scenario: Adjustment refused for serial-tracked goods
- **WHEN** an authorized user posts `quantityChange` +5 or -1 for the balance of a product with `is_serial_tracked = true`
- **THEN** the API returns HTTP 409 ProblemDetails telling the user to receive devices through a purchase receipt and to remove a device by reporting it defective, and quantity and movements stay unchanged

#### Scenario: Goods without serial still adjust
- **WHEN** an authorized user posts `quantityChange` -2 with a reason for the balance of a product with `is_serial_tracked = false` and enough unreserved quantity
- **THEN** the API commits the adjustment as before and returns HTTP 200

## ADDED Requirements

### Requirement: Report a device defective
`POST /api/inventory/serials/{id}/defective` SHALL let Admin and WarehouseStaff move one `InStock` device to `Defective` with a required reason (1–200 characters) and optional note (≤ 1000). In one transaction it SHALL lower the device's stock quantity by 1 and write a `Defective` movement of -1 naming the device and reason, then return the updated device. Devices SHALL never be deleted by this operation; a lost device is reported the same way with its reason.

#### Scenario: Report a broken phone
- **WHEN** a WarehouseStaff user reports the `InStock` device with IMEI 356789012345678 defective with reason "Hư màn hình", and its balance has quantity 5
- **THEN** the API returns HTTP 200 with status `Defective`, the balance quantity is 4, the 4 other devices stay `InStock`, and the stock history shows a `Defective` movement of -1 with quantity after 4 whose note contains the IMEI and "Hư màn hình"

#### Scenario: Report a lost phone
- **WHEN** the user reports an `InStock` device defective with reason "Mất"
- **THEN** the device becomes `Defective`, stays in the Serial/IMEI list with that status, and the movement note contains "Mất"

#### Scenario: Device not in stock
- **WHEN** the device is `Reserved`, `Sold`, `Returned`, `UnderRepair` or already `Defective`
- **THEN** the API returns HTTP 409 ProblemDetails and changes nothing

#### Scenario: Invalid report
- **WHEN** the reason is blank or above 200 characters, or the note above 1000
- **THEN** the API returns HTTP 400 ValidationProblem and changes nothing

#### Scenario: Unknown device
- **WHEN** the id does not exist
- **THEN** the API returns HTTP 404 ProblemDetails

#### Scenario: Defective device cannot be sold
- **WHEN** an order tries to reserve a device that was reported defective
- **THEN** the reservation fails with the existing device conflict and the order is not paid

### Requirement: Return a repaired device to stock
`POST /api/inventory/serials/{id}/restore` SHALL let Admin and WarehouseStaff move one `Defective` device back to `InStock` with an optional note (≤ 1000). In one transaction it SHALL raise the device's stock quantity by 1 and write an `Adjustment` movement of +1 naming the device, then return the updated device. The device SHALL keep its unit cost, warranty months and purchase receipt link.

#### Scenario: Phone repaired
- **WHEN** a WarehouseStaff user restores a `Defective` device whose balance has quantity 4, with note "Đã thay màn hình"
- **THEN** the API returns HTTP 200 with status `InStock`, the balance quantity is 5, and the history shows an `Adjustment` movement of +1 with quantity after 5 whose note contains the IMEI and "Đã thay màn hình"

#### Scenario: Restore a device that is not defective
- **WHEN** the device is not `Defective`
- **THEN** the API returns HTTP 409 ProblemDetails and changes nothing

### Requirement: Device status changes keep stock consistent
Reporting and restoring SHALL lock the device row before its stock balance, the same order used by sales, so that a concurrent sale and status change on one device cannot both succeed. After either operation, a serial-tracked balance's quantity SHALL equal its `InStock` plus `Reserved` devices. Only Admin and WarehouseStaff SHALL call these routes.

#### Scenario: Report while a sale reserves the same device
- **WHEN** a defect report and an order payment that reserves the same `InStock` device run in parallel transactions
- **THEN** exactly one succeeds, the other receives HTTP 409, and quantity, reserved quantity and device status match the successful one

#### Scenario: Status change denied by role
- **WHEN** an unauthenticated caller, a Manager or a SalesStaff user calls report or restore
- **THEN** the API returns HTTP 401 or HTTP 403 and changes nothing

### Requirement: Serial-tracked balances stay reconciled
Every inventory write (purchase receipt, Serial/IMEI import, reserve, sale, defect report, restore, stock deletion) SHALL leave each serial-tracked balance with `quantity` equal to its `InStock` plus `Reserved` devices and `reserved_quantity` equal to its `Reserved` devices. A documented reconciliation query SHALL list every balance that breaks this rule, and it SHALL return no rows after any sequence of these operations, including concurrent ones.

#### Scenario: Reconciliation after mixed operations
- **WHEN** devices of one product are received, reserved, sold, reported defective and restored, some in parallel transactions
- **THEN** the reconciliation query returns no rows

#### Scenario: Reconciliation reports a broken balance
- **WHEN** a serial-tracked balance is changed outside the API so that its quantity differs from its counted devices
- **THEN** the reconciliation query returns that balance with its stored quantity, stored reserved quantity and the counted values
