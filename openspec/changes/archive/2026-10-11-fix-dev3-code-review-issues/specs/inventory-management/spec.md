# Spec Delta

## MODIFIED Requirements

### Requirement: Aggregated Stock Querying
The system SHALL provide paginated inventory stock balances via direct route `/api/inventory/stocks` with filtering support by Product ID, Variant ID, and low stock alert thresholds.

#### Scenario: Query stock balances with pagination
- **WHEN** client sends a GET request to `/api/inventory/stocks` with valid pagination parameters
- **THEN** system returns HTTP 200 with `PagedResultDto<InventoryStockDto>` containing quantity, reserved quantity, and product metadata

#### Scenario: Filter stocks by low stock alert
- **WHEN** client sends a GET request to `/api/inventory/stocks?lowStock=true`
- **THEN** system returns only stock balance records where `Quantity <= MinStockAlert`

#### Scenario: Filter stocks by Product and Variant
- **WHEN** client sends a GET request to `/api/inventory/stocks` filtered by specific `productId` and optional `variantId`
- **THEN** system returns matching inventory stock balances for the specified product and variant

### Requirement: Initial Stock Balance Initialization
The system SHALL allow authorized staff (Admin, Manager, WarehouseStaff) to initialize stock balances via direct route `/api/inventory/stocks/initialize` within an ACID transaction and record an audit movement ledger, validating product existence, variant ownership, and strictly positive opening quantities.

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

#### Scenario: Stock initialization unauthorized access
- **WHEN** an unauthenticated request or a user lacking Admin, Manager, or WarehouseStaff roles calls the initialization endpoint
- **THEN** system rejects the request with HTTP 401 Unauthorized or HTTP 403 Forbidden

### Requirement: Serial and IMEI Hardware Listing
The system SHALL provide paginated query capabilities for physical Serial/IMEI items via direct route `/api/inventory/serials` filtered by Product ID, Variant ID, and lifecycle Status.

#### Scenario: Query serial/IMEI items with filters
- **WHEN** client sends a GET request to `/api/inventory/serials` specifying product ID and status filter
- **THEN** system returns HTTP 200 with `PagedResultDto<SerialImeiDto>` matching the criteria

#### Scenario: Query serial/IMEI items across statuses
- **WHEN** client queries serials filtering by status `InStock`, `Reserved`, or `Sold`
- **THEN** system filters matching physical items with their warranty information and purchase order linkage

### Requirement: Batch Import Serial and IMEI Records
The system SHALL allow authorized staff (Admin, Manager, WarehouseStaff) to import batches of up to 500 Serial/IMEI records via direct route `/api/inventory/serials/batch-import`, validating product existence, variant ownership, duplicates, payload size limits, syncing stock balance quantities, and recording an audit movement ledger.

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

#### Scenario: Batch import unauthorized access
- **WHEN** an unauthenticated caller or a user without Admin, Manager, or WarehouseStaff roles attempts batch import
- **THEN** system rejects the request with HTTP 401 Unauthorized or HTTP 403 Forbidden
