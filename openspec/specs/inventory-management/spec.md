# inventory-management Specification

## Purpose

Enables inventory stock balance tracking, stock initialization, serial/IMEI hardware lifecycle management, and batch import with strict concurrency and audit ledgers.

## Requirements

### Requirement: Aggregated Stock Querying
The system SHALL provide paginated inventory stock balances via direct route `/api/inventory/stocks` with filtering by a keyword `search`, `categoryId`, Product ID, Variant ID and stock status (`lowStock`, `outOfStock`, `inStock`). The three statuses SHALL be disjoint and use the available quantity (`Quantity - ReservedQuantity`) against the record's `MinStockAlert`. Each item SHALL carry `categoryId` and `categoryName` of its product. Filters combine with AND.

#### Scenario: Query stock balances with pagination
- **WHEN** client sends a GET request to `/api/inventory/stocks` with valid pagination parameters
- **THEN** system returns HTTP 200 with `PagedResultDto<InventoryStockDto>` containing quantity, reserved quantity, product metadata, `categoryId` and `categoryName`

#### Scenario: Filter stocks by low stock alert
- **WHEN** client sends a GET request to `/api/inventory/stocks?lowStock=true`
- **THEN** system returns only stock balance records where `0 < Quantity - ReservedQuantity <= MinStockAlert`, so out-of-stock records are not included

#### Scenario: Filter stocks that are out of stock or in stock
- **WHEN** client sends `outOfStock=true`, or `inStock=true`
- **THEN** system returns only records whose available quantity is `<= 0`, or respectively `> MinStockAlert`

#### Scenario: Status filters do not overlap
- **WHEN** client queries the same data once with each of `inStock=true`, `lowStock=true` and `outOfStock=true`
- **THEN** every stock record appears in exactly one of the three results, and their total counts add up to the unfiltered total count

#### Scenario: Filter stocks by Product and Variant
- **WHEN** client sends a GET request to `/api/inventory/stocks` filtered by specific `productId` and optional `variantId`
- **THEN** system returns matching inventory stock balances for the specified product and variant

#### Scenario: Filter stocks by category
- **WHEN** client sends `categoryId={id}` for an existing category
- **THEN** system returns only records whose product is directly assigned to that category, not to its subcategories

#### Scenario: Keyword search treats wildcard characters literally
- **WHEN** client sends `search=50%` or `search=a_b`
- **THEN** system matches product name, product SKU, variant name or variant SKU containing that exact text case-insensitively, and `%` or `_` do not act as wildcards

### Requirement: Initial Stock Balance Initialization
The system SHALL allow staff with the Admin or WarehouseStaff role to initialize stock balances via direct route `/api/inventory/stocks/initialize` within an ACID transaction and record an audit movement ledger, validating product existence, variant ownership, and strictly positive opening quantities.
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
- **WHEN** client initializes stock for a product with `is_serial_tracked = true` without `unitCost`
- **THEN** system accepts it and creates no purchase order, because each device carries its own cost

#### Scenario: Stock initialization unauthorized access
- **WHEN** an unauthenticated request, or an authenticated user without the Admin or WarehouseStaff role (including Manager and SalesStaff), calls the initialization endpoint
- **THEN** system rejects the request with HTTP 401 Unauthorized or HTTP 403 Forbidden and writes no stock row or movement

### Requirement: Serial and IMEI Hardware Listing
The system SHALL provide paginated query capabilities for physical Serial/IMEI items via direct route `/api/inventory/serials` filtered by a keyword `search`, Product ID, Variant ID, and lifecycle Status.
#### Scenario: Query serial/IMEI items with filters
- **WHEN** client sends a GET request to `/api/inventory/serials` specifying product ID and status filter
- **THEN** system returns HTTP 200 with `PagedResultDto<SerialImeiDto>` matching the criteria

#### Scenario: Query serial/IMEI items across statuses
- **WHEN** client queries serials filtering by status `InStock`, `Reserved`, or `Sold`
- **THEN** system filters matching physical items with their warranty information and purchase order linkage

#### Scenario: Serial keyword search treats wildcard characters literally
- **WHEN** client sends `search` containing `%` or `_`
- **THEN** system matches serial number, IMEI, product name or variant name containing that exact text case-insensitively, without wildcard expansion

### Requirement: Batch Import Serial and IMEI Records
The system SHALL allow staff with the Admin or WarehouseStaff role to import batches of up to 500 Serial/IMEI records via direct route `/api/inventory/serials/batch-import`, validating product existence, variant ownership, duplicates, payload size limits, syncing stock balance quantities, and recording an audit movement ledger.
#### Scenario: Successful batch import of serials and IMEIs
- **WHEN** an authorized user posts a batch of unique serial numbers or IMEIs (up to 500 items) for an existing product to `/api/inventory/serials/batch-import`
- **THEN** system inserts serial records in `InStock` status, updates inventory stock balance (initializing to 0 if absent, then accumulating imported count), stages an `InventoryMovement` audit record with `QuantityAfter` strictly set to the newly accumulated stock quantity after addition, and returns HTTP 201 Created with Location header `/api/inventory/serials?productId={productId}`

#### Scenario: Batch import payload size exceeds limit
- **WHEN** client sends a batch import request where `Items.Count > 500`
- **THEN** system rejects the request and returns HTTP 400 Bad Request indicating batch import size cannot exceed 500 items per request

#### Scenario: Batch import validation failure for non-existent product
- **WHEN** client sends a batch import request with a `productId` that does not exist in the catalog
- **THEN** system rejects the request and returns HTTP 400 Bad Request indicating the product does not exist

#### Scenario: Batch import validation failure for mismatched variant
- **WHEN** client sends a batch import request with a `variantId` that does not belong to the specified `productId`
- **THEN** system rejects the request and returns HTTP 400 Bad Request indicating the variant does not belong to the product

#### Scenario: Batch import duplicate serial or IMEI detection
- **WHEN** a batch import request contains a serial number or IMEI that already exists in the system or duplicates within the batch payload
- **THEN** system aborts the transaction, raises a duplicate collision error, and returns HTTP 409 Conflict formatted as an RFC 7807 ProblemDetails identifying the duplicate identifiers

#### Scenario: Batch import format validation failure
- **WHEN** client sends serial/IMEI entries with empty identifiers or invalid 15-digit IMEI patterns
- **THEN** system rejects the request and returns HTTP 400 Bad Request with validation details

#### Scenario: Batch import requires a cost for every device
- **WHEN** an item has neither its own `unitCost` nor a request-level `defaultUnitCost`, or the effective cost is negative or above 1,000,000,000,000
- **THEN** system rejects the whole request with HTTP 400 Bad Request under `items[n].unitCost` and imports nothing

#### Scenario: Batch import stores each device's cost
- **WHEN** a batch sets `defaultUnitCost` 20,000,000 and one item sets `unitCost` 21,500,000
- **THEN** that item's `serial_imeis.unit_cost` is 21,500,000 and every other item's is 20,000,000

#### Scenario: Batch import unauthorized access
- **WHEN** an unauthenticated caller, or an authenticated user without the Admin or WarehouseStaff role (including Manager and SalesStaff), attempts batch import
- **THEN** system rejects the request with HTTP 401 Unauthorized or HTTP 403 Forbidden and imports nothing

### Requirement: Stock detail API
The inventory API SHALL expose `GET /api/inventory/stocks/{stockId}/detail` for authenticated staff and return `InventoryStockDetailDto`, including the product's `categoryId` and `categoryName`, or HTTP 404 when the balance is absent.

#### Scenario: Authorized detail request
- **WHEN** an authenticated staff user requests an existing stock ID
- **THEN** the API returns HTTP 200 with quantity metrics, `categoryId`, `categoryName` and recent movement DTOs

#### Scenario: Unknown detail request
- **WHEN** the requested stock ID is absent
- **THEN** the API returns HTTP 404 ProblemDetails

### Requirement: Transactional stock adjustment API
The inventory API SHALL expose `POST /api/inventory/stocks/{stockId}/adjust` for Admin and WarehouseStaff and return the refreshed detail after a successful adjustment.

#### Scenario: Valid adjustment request
- **WHEN** an authorized user posts a non-zero change with a reason
- **THEN** the API commits the adjustment and returns HTTP 200 with the updated detail

#### Scenario: Invalid or conflicting adjustment
- **WHEN** the request is invalid or violates the reserved-quantity invariant
- **THEN** the API returns HTTP 400 or 409 ProblemDetails without a partial write

### Requirement: Stock deletion API
The inventory API SHALL expose `DELETE /api/inventory/stocks/{stockId}` only to Admin and WarehouseStaff. It SHALL delete the balance and its in-stock Serial/IMEI records in one transaction, and SHALL refuse with HTTP 409 when the balance has reserved quantity, any movement history, quantity not backed by serials, or a serial that is not `InStock`.

#### Scenario: Authorized delete of an unused balance
- **WHEN** an Admin or WarehouseStaff user deletes a balance with no reservation, no movement and only `InStock` serials
- **THEN** the API returns HTTP 204 and the balance and those serials no longer exist

#### Scenario: Delete refused by business rule
- **WHEN** the balance has reserved quantity, movement history, unreconciled quantity or a serial that is reserved, sold or otherwise not `InStock`
- **THEN** the API returns HTTP 409 ProblemDetails with a Vietnamese reason and deletes nothing

#### Scenario: Unknown balance
- **WHEN** the stock ID does not exist
- **THEN** the API returns HTTP 404 ProblemDetails

#### Scenario: Delete denied by role
- **WHEN** an unauthenticated caller, or an authenticated Manager or SalesStaff user, calls the delete endpoint
- **THEN** the API returns HTTP 401 or HTTP 403 respectively and deletes nothing
