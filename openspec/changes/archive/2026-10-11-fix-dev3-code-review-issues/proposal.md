# Proposal

## Why

Sprint 1 code review for Feature 3 (`catalog-inventory-api`, recorded in `docs/DEV3_CODE_REVIEW_NOTES.md`) identified 6 discrepancies violating architectural standards and database integrity constraints: inconsistent `/v1` route prefixes, hardcoded fallback User ID causing foreign key violations (`23503`), missing service-level validation for product existence resulting in uncaught `500` errors, unbounded batch import sizes risking server exhaustion, permissive stock initialization allowing `quantity <= 0` and violating database check constraints (`23514`), and missing composite foreign key `(variant_id, product_id)` relationship validation. Resolving these issues immediately aligns the catalog and inventory modules with system architecture rules and prevents runtime failures.

## What Changes

- **BREAKING**: Remove redundant `/v1` route prefixes from `CategoriesController` and `InventoryController`. Controllers will only expose direct `/api/categories` and `/api/inventory` routes, and return corresponding Location headers. Update sample request files (`backend/TechStore.Api.http`) and test scripts (`scripts/test_dev3_api.sh`).
- **Data Seeder User ID Consistency**: Ensure `DataSeeder.cs` deterministically sets the initial `admin` user ID to `11111111-1111-1111-1111-111111111111`, guaranteeing foreign key referential integrity with the audit fallback in `InventoryController`.
- **Product Existence Validation**: Inject `IProductRepository` into `InventoryService` and `SerialImeiService`. Reject requests with non-existent `ProductId` with HTTP 400 Bad Request instead of bubbling up PostgreSQL foreign key violations as HTTP 500.
- **Batch Import Payload Size Limit**: Enforce a maximum batch size of 500 items in `SerialImeiService.BatchImportAsync`. Return HTTP 400 Bad Request when `request.Items.Count > 500`.
- **Opening Quantity Validation**: Update `InventoryService.InitializeStockAsync` to reject `quantity <= 0` with HTTP 400 Bad Request, preventing violation of the database check constraint `ck_inventory_movements_qty_change_not_zero` (`quantity_change <> 0`).
- **Composite Key `(variant_id, product_id)` Validation**: In both `InventoryService` and `SerialImeiService`, when `variantId` is provided, verify against `IProductRepository` that the specified variant belongs to the specified `productId`. Return HTTP 400 Bad Request if the variant does not belong to the product.

## Capabilities

### New Capabilities
*(None)*

### Modified Capabilities
- `category-catalog`: Update route specifications from `/api/v1/categories/*` to direct `/api/categories/*`.
- `inventory-management`: Update route specifications to `/api/inventory/*`, enforce strict positive opening stock quantity (`quantity > 0`), enforce product existence and variant belonging validations (HTTP 400), and enforce maximum batch import size of 500 items.
- `data-seeding`: Mandate fixed deterministic admin user ID (`11111111-1111-1111-1111-111111111111`) to preserve foreign key validity across unauthenticated audit entries.

## Impact

- **API Routes**: Endpoints under `CategoriesController` and `InventoryController` drop `/v1/`. Clients calling `/api/v1/categories/*` or `/api/v1/inventory/*` must migrate to `/api/categories/*` and `/api/inventory/*`.
- **Dependencies**: `InventoryService` and `SerialImeiService` take a dependency on `IProductRepository` (already registered in DI container).
- **Database / Seeder**: Seeded Admin account always receives `11111111-1111-1111-1111-111111111111`.
- **Test Suites**: Unit test fixtures for `InventoryServiceTests` and `SerialImeiServiceTests` will supply a `FakeProductRepository` to exercise product and variant validation scenarios.
