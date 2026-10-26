## MODIFIED Requirements

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

### Requirement: Stock detail API
The inventory API SHALL expose `GET /api/inventory/stocks/{stockId}/detail` for authenticated staff and return `InventoryStockDetailDto`, including the product's `categoryId` and `categoryName`, or HTTP 404 when the balance is absent.

#### Scenario: Authorized detail request
- **WHEN** an authenticated staff user requests an existing stock ID
- **THEN** the API returns HTTP 200 with quantity metrics, `categoryId`, `categoryName` and recent movement DTOs

#### Scenario: Unknown detail request
- **WHEN** the requested stock ID is absent
- **THEN** the API returns HTTP 404 ProblemDetails

## ADDED Requirements

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
