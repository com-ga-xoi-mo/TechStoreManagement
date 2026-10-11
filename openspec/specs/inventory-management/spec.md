# inventory-management Specification

## Purpose

Enables inventory stock balance tracking, stock initialization, serial/IMEI hardware lifecycle management, and batch import with strict concurrency and audit ledgers.

## Requirements

### Requirement: Aggregated Stock Querying
The system SHALL provide paginated inventory stock balances with filtering support by Product ID, Variant ID, and low stock alert thresholds.

#### Scenario: Query stock balances with pagination
- **WHEN** client sends a GET request to `/api/v1/inventory/stocks` with valid pagination parameters
- **THEN** system returns HTTP 200 with `PagedResultDto<InventoryStockDto>` containing quantity, reserved quantity, and product metadata

#### Scenario: Filter stocks by low stock alert
- **WHEN** client sends a GET request to `/api/v1/inventory/stocks?lowStock=true`
- **THEN** system returns only stock balance records where `Quantity <= MinStockAlert`

#### Scenario: Filter stocks by Product and Variant
- **WHEN** client sends a GET request to `/api/v1/inventory/stocks` filtered by specific `productId` and optional `variantId`
- **THEN** system returns matching inventory stock balances for the specified product and variant

### Requirement: Initial Stock Balance Initialization
The system SHALL allow authorized staff (Admin, Manager, WarehouseStaff) to initialize stock balances within an ACID transaction and record an audit movement ledger.

#### Scenario: Successful stock initialization
- **WHEN** an authorized user posts valid initial stock quantities to `/api/v1/inventory/stocks/initialize`
- **THEN** system creates the inventory stock record, creates an `InventoryMovement` record of type `OpeningBalance` with the user ID, and returns HTTP 201 Created

#### Scenario: Initialize existing stock balance conflict
- **WHEN** client attempts to initialize stock for a product and variant pair that already has an inventory stock balance row
- **THEN** system rejects the request and returns HTTP 409 Conflict ProblemDetails indicating stock is already initialized

#### Scenario: Stock initialization validation failure
- **WHEN** client posts invalid quantities (negative quantity or min stock alert) or non-existent product ID
- **THEN** system rejects the request and returns HTTP 400 Bad Request with validation details

#### Scenario: Stock initialization unauthorized access
- **WHEN** an unauthenticated request or a user lacking Admin, Manager, or WarehouseStaff roles calls the initialization endpoint
- **THEN** system rejects the request with HTTP 401 Unauthorized or HTTP 403 Forbidden

### Requirement: Serial and IMEI Hardware Listing
The system SHALL provide paginated query capabilities for physical Serial/IMEI items filtered by Product ID, Variant ID, and lifecycle Status.

#### Scenario: Query serial/IMEI items with filters
- **WHEN** client sends a GET request to `/api/v1/inventory/serials` specifying product ID and status filter
- **THEN** system returns HTTP 200 with `PagedResultDto<SerialImeiDto>` matching the criteria

#### Scenario: Query serial/IMEI items across statuses
- **WHEN** client queries serials filtering by status `InStock`, `Reserved`, or `Sold`
- **THEN** system filters matching physical items with their warranty information and purchase order linkage

### Requirement: Batch Import Serial and IMEI Records
The system SHALL allow authorized staff (Admin, Manager, WarehouseStaff) to import batches of Serial/IMEI records, validate duplicates, sync stock balance quantities, and record an audit movement ledger.

#### Scenario: Successful batch import of serials and IMEIs
- **WHEN** an authorized user posts a batch of unique serial numbers or IMEIs to `/api/v1/inventory/serials/batch-import`
- **THEN** system inserts serial records in `InStock` status, updates inventory stock balance (initializing to 0 if absent, then accumulating imported count), stages an `InventoryMovement` audit record with `QuantityAfter` strictly set to the newly accumulated stock quantity after addition, and returns HTTP 201 Created

#### Scenario: Batch import duplicate serial or IMEI detection
- **WHEN** a batch import request contains a serial number or IMEI that already exists in the system or duplicates within the batch payload
- **THEN** system aborts the transaction, raises a duplicate collision error, and returns HTTP 409 Conflict formatted as an RFC 7807 ProblemDetails identifying the duplicate identifiers

#### Scenario: Batch import format validation failure
- **WHEN** client sends serial/IMEI entries with empty identifiers or invalid 15-digit IMEI patterns
- **THEN** system rejects the request and returns HTTP 400 Bad Request with validation details

#### Scenario: Batch import unauthorized access
- **WHEN** an unauthenticated caller or a user without Admin, Manager, or WarehouseStaff roles attempts batch import
- **THEN** system rejects the request with HTTP 401 Unauthorized or HTTP 403 Forbidden
