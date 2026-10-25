# Spec Delta

## Purpose

Provide an authenticated desktop workspace for browsing paged inventory balances, understanding available and reserved quantities, and exposing safe hooks for future stock operations without fabricating unsupported backend data.

## ADDED Requirements

### Requirement: Authenticated paged stock loading
The workspace SHALL load inventory balances from `GET /api/inventory/stocks` with the current Bearer session and preserve the server page metadata and `InventoryStockDto` quantities.

#### Scenario: Load a page successfully
- **WHEN** an authenticated user opens the Tồn kho tab and the API returns a valid `PagedResultDto<InventoryStockDto>`
- **THEN** the workspace renders the returned rows, total count, current page, page size, and total pages without replacing values with fixtures

#### Scenario: Missing or denied session
- **WHEN** no access token is available or the API returns HTTP 401/403
- **THEN** the workspace shows a recoverable authentication/permission state and does not present stock rows as successfully loaded

### Requirement: Immediate available-field filtering
The workspace SHALL apply search, low-stock status, and supported product/variant filters immediately to the loaded result without a submit action, while clearly marking filters that the current API contract cannot evaluate.

#### Scenario: Search loaded product metadata
- **WHEN** the user types a product name, product SKU, variant name, or variant SKU fragment
- **THEN** matching loaded rows are shown case-insensitively and the visible range is recalculated from the filtered result

#### Scenario: Unsupported category or warehouse filter
- **WHEN** the user opens the category or warehouse selector but the current API response has no category/warehouse field
- **THEN** the selector remains disabled or displays an unavailable state, and the client does not fabricate category or warehouse matches

### Requirement: Stock status presentation
The workspace SHALL derive a display status from `AvailableQuantity`: `> 5` is “Còn hàng”, `1..5` is “Sắp hết hàng”, and `0` is “Hết hàng”; an out-of-stock row SHALL receive subdued styling.

#### Scenario: Render each stock state
- **WHEN** a row has available quantity above five, between one and five, or equal to zero
- **THEN** it displays the corresponding Vietnamese status badge and the zero-availability row uses the reduced-emphasis treatment

### Requirement: Seven-column inventory table
The table SHALL present exactly seven columns: product/SKU, category, sale price, available quantity, reserved quantity, status, and actions, with numeric values aligned as specified by the design.

#### Scenario: Render stock row content
- **WHEN** a stock DTO is displayed
- **THEN** product identity includes inferred device icon, name, and SKU; quantities show `Quantity`, `ReservedQuantity`, and `AvailableQuantity`; and unavailable fields display an em dash rather than fabricated values

### Requirement: Inventory workspace visual contract
The workspace SHALL be implemented without XAML and SHALL match the structure and tokens in `docs/designs/inventory-stock.html`, including the header actions, underline tabs, filter toolbar, seven-column card, and pagination footer.

#### Scenario: Render the Tồn kho tab
- **WHEN** the workspace is shown in the supported desktop theme
- **THEN** it displays “Danh mục và Kho”, stock total text, “Xuất báo cáo”, “Nhập kho mới”, an active Tồn kho tab, the three filter controls, and the designed typography, spacing, borders, colors, and focus states

### Requirement: Pagination and result states
The workspace SHALL expose deterministic previous, numbered-page, and next controls based on server metadata and SHALL distinguish loading, empty, error, and successful states.

#### Scenario: Navigate pages
- **WHEN** the user selects a valid page or previous/next control
- **THEN** one authenticated page request is made with the selected page and page size, and invalid controls are disabled at the boundaries

#### Scenario: Empty or failed page
- **WHEN** the API returns zero rows or a normalized HTTP/transport error
- **THEN** the workspace shows an empty-result or recoverable error state with retry, without retaining stale rows as current data

### Requirement: Deferred stock action hooks
The workspace SHALL expose actions for “Nhập kho mới”, viewing Serial/IMEI, adjusting stock, and deleting a stock row as hooks that do not mutate inventory until their later workflows are implemented.

#### Scenario: Activate a deferred action
- **WHEN** the user activates an inventory action whose detail workflow is not implemented
- **THEN** the client invokes the registered hook or reports the action as unavailable and issues no stock mutation request

### Requirement: API error and cancellation behavior
The inventory client SHALL use the shared API transport rules for bearer tokens, cancellation, normalized errors, and paged JSON responses.

#### Scenario: Cancel or fail a request
- **WHEN** a load is cancelled, times out, loses network connectivity, or receives a non-success response
- **THEN** cancellation remains cancellation, transport errors retain their type, HTTP status is preserved, and the ViewModel exposes a recoverable error without retrying writes

### Requirement: Inventory workspace verification
The frontend unit-test suite SHALL cover API request construction, paged deserialization, status derivation, immediate filtering, unsupported-filter handling, pagination boundaries, loading/empty/error states, cancellation, and deferred action hooks.

#### Scenario: Run inventory workspace tests
- **WHEN** `tests/Frontend.UnitTests` is executed
- **THEN** all inventory ViewModel and API-client tests pass without requiring a live backend or database

