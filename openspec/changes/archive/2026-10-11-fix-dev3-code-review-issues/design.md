# Design

## Context

During Sprint 1 code review of branch `catalog-inventory-api` (`docs/DEV3_CODE_REVIEW_NOTES.md`), six deviations from repository guidelines and database constraints were flagged. Currently:
- `CategoriesController` and `InventoryController` declare redundant dual routes (`api/v1/...` and `api/...`) with hardcoded `/api/v1/...` Location headers, inconsistent with `ProductsController` and `REQUIREMENTS.md`.
- `InventoryController` defaults unauthenticated requests to `11111111-1111-1111-1111-111111111111`, but the initial seed data previously generated a random GUID for Admin, causing foreign key violations (`23503`) when logging movements in `inventory_movements`.
- `InventoryService` and `SerialImeiService` validate GUID presence but never check whether `ProductId` exists or whether `VariantId` actually belongs to the product, leaving database foreign key constraints (`23503`) to fail with HTTP 500 Internal Server Error.
- `SerialImeiService` does not bound batch import payload size, allowing potential resource exhaustion and large SQL parameter lists.
- `InventoryService.InitializeStockAsync` permits `quantity = 0`, generating `InventoryMovement` with `QuantityChange = 0` which trips PostgreSQL check constraint `ck_inventory_movements_qty_change_not_zero` (`23514`).

## Goals / Non-Goals

**Goals:**
- Unify all API endpoints under `/api/*` without `/v1` prefixes and update all Location headers, HTTP scratchpads, and shell test scripts.
- Ensure deterministic Admin user seeding with ID `11111111-1111-1111-1111-111111111111` to satisfy referential integrity.
- Enforce service-level domain validation returning HTTP 400 Bad Request with field-level errors for missing products, mismatched product-variant relationships, non-positive opening quantities (`quantity <= 0`), and batch imports exceeding 500 items.
- Maintain existing unit test coverage and add tests for all new validation paths.

**Non-Goals:**
- Full JWT token claim enforcement with `[Authorize]` attributes (scheduled for Sprint 2 Identity integration).
- Modifying database schema, migration scripts, or check constraints in PostgreSQL.
- Changing `Product` or `ProductVariant` entity models.

## Decisions

### Decision 1: Direct `/api/*` route conventions
- **Choice**: Remove `[Route("api/v1/categories")]` and `[Route("api/v1/inventory")]`, leaving only `[Route("api/categories")]` and `[Route("api/inventory")]`. Update all Location headers in `Created(...)` responses to omit `/v1`.
- **Rationale**: Eliminates route fragmentation across controllers (`ProductsController` already uses `/api/products`). Aligns with `docs/REQUIREMENTS.md`.
- **Alternatives considered**:
  - *Keep dual routes for backward compatibility*: Rejected because the API is currently internal in Sprint 1 development; dual routes encourage drift and complicate API documentation.

### Decision 2: Deterministic Seeder Admin User ID
- **Choice**: Explicitly assign `Id = Guid.Parse("11111111-1111-1111-1111-111111111111")` in `backend/Common/Data/DataSeeder.cs`.
- **Rationale**: Matches the audit fallback `GetUserId()` in `InventoryController`, preventing foreign key violation `23503` when inserting `InventoryMovement` records in local developer testing without an active JWT token.
- **Alternatives considered**:
  - *Remove fallback and require JWT immediately*: Premature; auth module is still being completed in parallel branch.
  - *Create a dummy user dynamically if missing*: Unnecessary overhead and complicates seed state idempotency.

### Decision 3: Cross-Module Read via `IProductRepository`
- **Choice**: Inject `IProductRepository` into `InventoryService` and `SerialImeiService`. Use `await _productRepository.GetWithVariantsAsync(productId, cancellationToken)` for read-only validation.
- **Rationale**: Follows the explicit architectural rule in `AGENTS.md`: *"Cross-module access: a service may call another module's repository interface for reads"*. `IProductRepository` is already registered as Scoped in DI container, and `GetWithVariantsAsync` uses `AsNoTracking()` with `.Include(p => p.Variants)`, providing all data needed to validate both product existence and variant association in a single query.
- **Alternatives considered**:
  - *Inject `AppDbContext` directly*: Strictly forbidden by `AGENTS.md` layering rules (`Controller -> Service -> Repository -> AppDbContext`).
  - *Inject `IProductService`*: Adds unnecessary DTO mapping overhead for internal domain validation.

### Decision 4: Single-Query Product & Variant Validation
- **Choice**:
  ```csharp
  var product = await productRepository.GetWithVariantsAsync(request.ProductId.Value, cancellationToken);
  if (product == null)
  {
      errors["productId"] = [$"Product with ID '{request.ProductId.Value}' does not exist."];
  }
  else if (request.VariantId.HasValue && request.VariantId.Value != Guid.Empty)
  {
      if (product.Variants == null || product.Variants.All(v => v.Id != request.VariantId.Value))
      {
          errors["variantId"] = [$"Variant with ID '{request.VariantId.Value}' does not belong to product '{request.ProductId.Value}'."];
      }
  }
  ```
- **Rationale**: Validates both product existence (Issue 3) and composite foreign key `(variant_id, product_id)` relationship (Issue 6) with a single repository call before starting a database transaction.
- **Alternatives considered**:
  - *Separate query for variant check*: Redundant database roundtrip since `GetWithVariantsAsync` already includes variants.

### Decision 5: Enforce Maximum Batch Size of 500
- **Choice**: In `SerialImeiService.BatchImportAsync`, validate `if (request.Items.Count > 500)` and add field error `errors["items"] = ["Batch import size cannot exceed 500 items per request."];`.
- **Rationale**: Limits memory consumption, prevents oversized SQL parameter expansion in `FindExistingIdentifiersAsync`, and ensures responsive API requests.
- **Alternatives considered**:
  - *Chunking internally within the service*: Adds complex partial transaction semantics; rejecting oversized requests forces client-side chunking with clear error boundaries.

### Decision 6: Enforce Strictly Positive Opening Balance
- **Choice**: Change `request.Quantity.Value < 0` to `request.Quantity.Value <= 0` in `InventoryService.InitializeStockAsync`, returning `"Opening quantity must be greater than zero."`.
- **Rationale**: Direct alignment with PostgreSQL check constraint `ck_inventory_movements_qty_change_not_zero (quantity_change <> 0)`. Stock balance initialization requires a non-zero opening ledger movement.
- **Alternatives considered**:
  - *Skip creating movement if quantity == 0*: Violates the inventory audit invariant that all stock balances must have a matching ledger origin.

## Risks / Trade-offs

- **[Risk] Breaking existing scripts and client calls targeting `/api/v1/...`** → **Mitigation**: Update `TechStore.Api.http` and `scripts/test_dev3_api.sh` in the same change; communicate route unification to client developers.
- **[Risk] Added latency in stock initialization and batch import from `GetWithVariantsAsync`** → **Mitigation**: The query is indexed by primary key `id` with eager join on `variants`, executing in <1ms. It executes before transaction start, reducing database lock hold time.
- **[Risk] Unit test breakage from changing service constructor signatures** → **Mitigation**: Update `InventoryServiceTests.cs` and `SerialImeiServiceTests.cs` to inject `FakeProductRepository` (already available in test project).

## Migration Plan

1. Update `backend/Common/Data/DataSeeder.cs` and verify seeder tests.
2. Update `CategoriesController` and `InventoryController` route attributes and Location headers.
3. Update `InventoryService` and `SerialImeiService` constructor signatures and validation logic.
4. Update `tests/Backend.UnitTests` test fixtures and add regression unit tests.
5. Update `backend/TechStore.Api.http` and `scripts/test_dev3_api.sh`.
6. Run `dotnet test` to verify all test suites pass.
