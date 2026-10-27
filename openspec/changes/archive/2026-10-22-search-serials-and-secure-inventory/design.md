# Design

## Context

Serial lookup currently builds `SerialImeiSearchCriteria` from Product ID, Variant ID, Status, and pagination, while `SerialImeiRepository` owns all EF Core queries. `InventoryController` delegates to inventory services but currently has no authorization attributes and substitutes a fixed GUID when write requests do not contain a usable user claim. Existing JWT middleware and the category API test host provide the project conventions for role authorization tests.

## Goals / Non-Goals

**Goals:**

- Carry an optional trimmed keyword from the query request through the service and repository criteria.
- Apply a case-insensitive contains predicate to both SerialNumber and Imei before count and pagination, while preserving all existing filters.
- Enforce the requested role matrix declaratively on each inventory route.
- Make missing or malformed actor claims produce an explicit `401` before write services run.
- Add deterministic unit and API authorization tests with fake repositories/services and caller-generated GUID claims.

**Non-Goals:**

- No schema, migration, or new identifier index change.
- No changes to batch-import validation, stock transaction logic, or serial lifecycle transitions beyond authorization and actor extraction.
- No fallback identity, database lookup, or automatic user creation for malformed claims.

## Decisions

### Keyword travels through the existing criteria contract

Add `string? Keyword` to `SerialImeiFilterRequest` and `SerialImeiSearchCriteria`, normalize it once in the service, and let the repository combine it with the existing ProductId, VariantId, and Status predicates. A repository-only query parameter was rejected because it would bypass the service contract and make fake tests diverge from production behavior.

### Database-side case-insensitive matching

Use the provider-supported case-insensitive contains predicate for both nullable identifier columns, with a blank keyword treated as no filter. Apply the predicate before `CountAsync`, `Skip`, and `Take` so `TotalCount` and page metadata describe the filtered result. Loading all serials into memory was rejected because it breaks pagination and scales with inventory size.

### Declarative route authorization

Annotate reads with the four-role policy (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`) and writes with the two-role policy (`Admin`, `WarehouseStaff`). Authentication and role failures occur in middleware before service invocation, matching existing controller conventions. Duplicating role checks inside services was rejected because services do not own HTTP authorization concerns.

### Explicit claim failure instead of fallback identity

Replace `GetUserId` fallback behavior with a helper that checks `ClaimTypes.NameIdentifier` first and `sub` second, parses a GUID, and reports failure. Each write action returns HTTP 401 when parsing fails and only then calls the service with the claimed GUID. A fixed baseline ID was rejected because it destroys audit attribution and masks authentication errors.

### Tests use generated identities and observable service calls

Extend `FakeSerialImeiRepository` to filter keyword matches with case-insensitive `Contains`. Add service tests for IMEI, serial, and status-combined searches. Add an inventory API test host/fake service that records the supplied actor ID and verifies `401`, `403`, and allowed-role behavior. Tests generate GUIDs at runtime and never introduce production constants.

## Risks / Trade-offs

- [Risk] A contains search can scan identifier columns for broad keywords. → Keep existing Product/Variant/Status predicates composable, retain pagination, and leave index/schema tuning for a separate performance change.
- [Risk] Database collation/provider behavior can differ from in-memory tests. → Use the database case-insensitive predicate in production and add PostgreSQL-backed search coverage alongside fake-repository unit tests.
- [Risk] A valid JWT without a usable subject may reach the action. → Treat claim parsing as a required precondition and return 401 without invoking the write service.
- [Risk] Tightening Manager access for writes is a compatibility change. → Document the role matrix in the delta spec and cover both allowed WarehouseStaff/Admin and rejected Manager/SalesStaff paths.

## Migration Plan

1. Deploy the API and shared request/criteria changes; no database migration is required.
2. Verify read access with all four roles and write access with Admin and WarehouseStaff before enabling client barcode search.
3. Roll back by deploying the previous API version; existing serial data and stock records remain unchanged.
