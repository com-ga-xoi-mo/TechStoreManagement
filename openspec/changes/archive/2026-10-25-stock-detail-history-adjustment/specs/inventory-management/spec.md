# Spec Delta

## ADDED Requirements

### Requirement: Stock detail API
The inventory API SHALL expose `GET /api/inventory/stocks/{stockId}/detail` for authenticated staff and return `InventoryStockDetailDto` or HTTP 404 when the balance is absent.

#### Scenario: Authorized detail request
- **WHEN** an authenticated staff user requests an existing stock ID
- **THEN** the API returns HTTP 200 with quantity metrics and recent movement DTOs

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
