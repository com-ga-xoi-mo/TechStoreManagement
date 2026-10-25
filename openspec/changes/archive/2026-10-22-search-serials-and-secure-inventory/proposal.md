# Proposal

## Why

Inventory serial lookup currently supports only structured filters and cannot quickly locate a device from a scanned barcode or partial serial/IMEI keyword. The Inventory controller also assigns a fixed fallback user ID when authentication claims are absent, which can attribute stock operations to the wrong actor instead of rejecting an unauthenticated request.

## What Changes

- Add optional `Q` keyword input to `GET /api/inventory/serials` and search case-insensitively across `SerialNumber` or `Imei`.
- Preserve composition with Product ID, Variant ID, Status, and existing pagination semantics.
- Protect inventory endpoints with role-based JWT authorization: all four roles for reads, and Admin or WarehouseStaff for stock initialization and serial batch import.
- Remove the hardcoded baseline user ID fallback; require a parseable `NameIdentifier` or `sub` claim for write operations and return `401 Unauthorized` when it is absent or invalid.
- Add TDD coverage for IMEI/serial keyword search, status composition, pagination, role rejection, and claim-derived user attribution.

## Capabilities

### New Capabilities

None. The behavior belongs to the existing inventory-management capability.

### Modified Capabilities

- `inventory-management`: extend serial/IMEI listing with keyword search and tighten read/write authorization and authenticated-user attribution for inventory endpoints.

## Impact

- `shared/Requests/SerialImeiFilterRequest.cs`, `backend/Inventory/Repositories/SerialImeiSearchCriteria.cs`, and `SerialImeiRepository`: carry and apply the case-insensitive keyword predicate while retaining existing filters and pagination.
- `backend/Inventory/Services/SerialImeiService.cs`: map `Q` into repository criteria without bypassing service orchestration.
- `backend/Inventory/InventoryController.cs`: add role policies to all four endpoints, remove the fixed GUID fallback, and return `401` for missing or malformed user claims before write services run.
- `tests/Backend.UnitTests/FakeSerialImeiRepository.cs`, `SerialImeiServiceTests`, and inventory API authorization tests: cover search and security behavior without hardcoded production identifiers.
- No database schema or migration change is expected; existing serial/IMEI columns and indexes remain the persistence contract.
