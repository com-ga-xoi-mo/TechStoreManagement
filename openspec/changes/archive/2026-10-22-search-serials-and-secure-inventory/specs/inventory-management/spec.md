# Spec Delta

## MODIFIED Requirements

### Requirement: Serial and IMEI Hardware Listing
The system SHALL provide paginated query capabilities for physical Serial/IMEI items via `/api/inventory/serials`, filtered by Product ID, Variant ID, lifecycle Status, and an optional keyword `Q` that matches SerialNumber or Imei case-insensitively.

#### Scenario: Query serial/IMEI items with filters
- **WHEN** an authorized reader sends a GET request to `/api/inventory/serials` specifying product ID and status filter
- **THEN** the system returns HTTP 200 with `PagedResultDto<SerialImeiDto>` matching the criteria

#### Scenario: Query serial/IMEI items across statuses
- **WHEN** an authorized reader queries serials filtering by status `InStock`, `Reserved`, or `Sold`
- **THEN** the system filters matching physical items with their warranty information and purchase order linkage

#### Scenario: Search by IMEI keyword
- **WHEN** an authorized reader sends `/api/inventory/serials?q=123456789012345`
- **THEN** the system returns serial records whose IMEI contains the keyword without case sensitivity

#### Scenario: Search by serial keyword
- **WHEN** an authorized reader sends `/api/inventory/serials?q=sn-abc`
- **THEN** the system returns serial records whose SerialNumber contains the keyword without case sensitivity

#### Scenario: Combine keyword, filters, and pagination
- **WHEN** an authorized reader supplies `Q` together with Product ID, Variant ID, Status, Page, and PageSize
- **THEN** the system applies all predicates before calculating the total count and page slice

### Requirement: Initial Stock Balance Initialization
The system SHALL allow only Admin or WarehouseStaff users to initialize stock balances via `/api/inventory/stocks/initialize` within an ACID transaction and record an audit movement ledger, validating product existence, variant ownership, and strictly positive opening quantities.

#### Scenario: Successful stock initialization
- **WHEN** an authorized Admin or WarehouseStaff posts valid initial stock quantities (`quantity > 0`) for an existing product and matching variant
- **THEN** the system creates the stock record, records an `OpeningBalance` movement with the authenticated user ID, and returns HTTP 201 Created

#### Scenario: Initialize existing stock balance conflict
- **WHEN** an Admin or WarehouseStaff attempts to initialize an already existing product and variant balance
- **THEN** the system returns HTTP 409 Conflict indicating stock is already initialized

#### Scenario: Stock initialization validation failure
- **WHEN** an Admin or WarehouseStaff posts a non-positive quantity, negative minimum alert, missing product, or mismatched variant
- **THEN** the system returns HTTP 400 Bad Request with validation details

#### Scenario: Stock initialization validation failure for non-positive quantity
- **WHEN** an Admin or WarehouseStaff posts `quantity <= 0`
- **THEN** the system returns HTTP 400 Bad Request indicating opening quantity must be greater than zero

#### Scenario: Stock initialization validation failure for non-existent product
- **WHEN** an Admin or WarehouseStaff posts a `productId` that does not exist in the database
- **THEN** the system returns HTTP 400 Bad Request indicating the product does not exist

#### Scenario: Stock initialization validation failure for mismatched variant
- **WHEN** an Admin or WarehouseStaff posts a `variantId` that does not belong to the specified `productId`
- **THEN** the system returns HTTP 400 Bad Request indicating the variant does not belong to the product

#### Scenario: Stock initialization unauthorized access
- **WHEN** an unauthenticated caller or a Manager or SalesStaff attempts stock initialization
- **THEN** the system returns HTTP 401 Unauthorized or HTTP 403 Forbidden before inventory mutation

### Requirement: Batch Import Serial and IMEI Records
The system SHALL allow only Admin or WarehouseStaff users to import batches of up to 500 Serial/IMEI records via `/api/inventory/serials/batch-import`, validating product ownership, duplicates, payload limits, stock synchronization, and audit movements.

#### Scenario: Successful batch import of serials and IMEIs
- **WHEN** an authorized Admin or WarehouseStaff posts a batch of unique serial numbers or IMEIs for an existing product
- **THEN** the system inserts records in `InStock`, updates stock, records an audit movement with the resulting quantity, and returns HTTP 201 Created

#### Scenario: Batch import payload size exceeds limit
- **WHEN** an Admin or WarehouseStaff sends more than 500 items
- **THEN** the system returns HTTP 400 Bad Request indicating the batch size limit

#### Scenario: Batch import validation failure for non-existent product
- **WHEN** an Admin or WarehouseStaff sends a product ID that does not exist
- **THEN** the system returns HTTP 400 Bad Request indicating the product does not exist

#### Scenario: Batch import validation failure for mismatched variant
- **WHEN** an Admin or WarehouseStaff sends a variant that does not belong to the product
- **THEN** the system returns HTTP 400 Bad Request indicating the variant mismatch

#### Scenario: Batch import duplicate serial or IMEI detection
- **WHEN** an Admin or WarehouseStaff sends an identifier already present in the system or duplicated within the payload
- **THEN** the system aborts the transaction and returns HTTP 409 Conflict identifying the duplicate identifiers

#### Scenario: Batch import format validation failure
- **WHEN** an Admin or WarehouseStaff sends empty identifiers or an invalid IMEI format
- **THEN** the system returns HTTP 400 Bad Request with validation details

#### Scenario: Batch import unauthorized access
- **WHEN** an unauthenticated caller or a Manager or SalesStaff attempts batch import
- **THEN** the system returns HTTP 401 Unauthorized or HTTP 403 Forbidden before inventory mutation

## ADDED Requirements

### Requirement: Inventory Read Authorization
The system SHALL require a valid JWT for `/api/inventory/stocks` and `/api/inventory/serials`, allowing Admin, Manager, SalesStaff, and WarehouseStaff roles.

#### Scenario: Authorized inventory read
- **WHEN** a caller with any allowed inventory role requests stocks or serials
- **THEN** the system permits the read operation

#### Scenario: Unauthenticated inventory read
- **WHEN** a caller without a valid JWT requests stocks or serials
- **THEN** the system returns HTTP 401 Unauthorized before invoking the inventory service

#### Scenario: Inventory read with an unsupported role
- **WHEN** an authenticated caller with a role outside the four inventory roles requests stocks or serials
- **THEN** the system returns HTTP 403 Forbidden before invoking the inventory service

### Requirement: Authenticated Inventory Actor Identity
The system MUST obtain the actor ID for inventory writes from a parseable `NameIdentifier` or `sub` JWT claim and MUST reject missing or invalid claims with HTTP 401; no fixed fallback identifier is permitted.

#### Scenario: Valid actor claim is forwarded
- **WHEN** an Admin or WarehouseStaff performs stock initialization or batch import with a valid GUID claim
- **THEN** the exact claimed GUID is passed to the inventory service for audit attribution

#### Scenario: Missing actor claim
- **WHEN** an otherwise authenticated write request has no `NameIdentifier` or `sub` claim
- **THEN** the API returns HTTP 401 and does not invoke the inventory write service

#### Scenario: Malformed actor claim
- **WHEN** an otherwise authenticated write request has a non-GUID `NameIdentifier` and `sub` claim
- **THEN** the API returns HTTP 401 and does not invoke the inventory write service
