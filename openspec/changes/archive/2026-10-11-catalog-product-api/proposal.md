# Proposal

## Why

Milestone 2 Sprint 1 requires the Catalog module (Dev 2) to expose product search, product detail, and product creation through the REST API so that the Uno client, the order flow, and the dashboard can work against real data. Today `backend/Catalog/` only contains entities and Fluent API configurations from Milestone 1: there is no service, no controller, no shared DTO, and no pagination contract. At the same time, the documentation assigns one shared `ICatalogService` / `ProductsController` to both product work (Dev 2) and category work (Dev 3), which forces two developers to edit the same files in the same sprint.

## What Changes

- **Documentation split of the Catalog module (done first, separate commit)**: replace the single `ICatalogService` / `CatalogService` with ownership-based services:
  - Dev 2: `Services/IProductService.cs`, `Services/ProductService.cs`, `ProductsController.cs` (`/api/products`).
  - Dev 3: `Services/ICategoryService.cs`, `Services/CategoryService.cs`, `CategoriesController.cs` (`/api/categories`).
  - Controllers stay at the module root (no `Controllers/` folder), matching `docs/ARCHITECTURE.md` section 2.2.
  - Files updated: `AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/MILESTONE_2_PLAN.md` (`CatalogServiceTests.cs` → `ProductServiceTests.cs`), `backend/Catalog/README.md`. No category code is written in this change.
- **UC-PROD-01 – Search & filter products** (`GET /api/products`): keyword search across product and variant name/SKU/barcode, filters by brand, category, active flag, display-price range and typed JSONB spec containment on effective specs, sorting, and pagination.
- **UC-PROD-02 – Product detail** (`GET /api/products/{id}`): full product data with variants and pre-merged effective specs.
- **UC-PROD-03 – Create product** (`POST /api/products`): create a product together with optional variants in one request, with field-level validation (required fields, non-negative prices, existing category, SKU uniqueness across products and variants, flat snake_case specs).
- **Shared pagination contract**: `PagedResultDto<T>` in `shared/DTOs/`, reusable by other modules; page/pageSize normalization is a backend helper in `backend/Catalog/Services/` (see design D8).
- **Shared contracts**: `ProductDto`, `ProductVariantDto`, `ProductSummaryDto`, `ProductSearchRequest`, `CreateProductRequest`, `CreateProductVariantRequest` in flat `shared/DTOs/` and `shared/Requests/`.
- **Error responses** use the built-in `[ApiController]` ProblemDetails helpers (400/404/409); no shared exception middleware is introduced.
- **Tests and manual verification**: unit tests for DB-independent logic in `tests/Backend.UnitTests/ProductServiceTests.cs`; sample requests for all three endpoints in `backend/TechStore.Api.http`.

Out of scope: product update/delete (UC-PROD-04/05), any category code, stock quantities in product DTOs, image upload, subcategory filtering, accent-insensitive Vietnamese search, shared exception middleware, authentication/RBAC, frontend, seed changes, integration-test project.

## Capabilities

### New Capabilities
- `product-catalog`: Product search/filter, product detail with variants and effective specs, product creation with validation, and the paginated result contract used by catalog listings.

### Modified Capabilities
*(None. The `database-schema` JSONB contract — flat snake_case specs and `product.specs || variant.specs` inheritance — is reused as-is, not changed.)*

## Impact

- **Backend (`backend/`)**:
  - New: `Catalog/Services/IProductService.cs`, `Catalog/Services/ProductService.cs` (plus internal pure-logic helpers in `Catalog/Services/`), `Catalog/ProductsController.cs`.
  - Modified: `Program.cs` (one `AddScoped<IProductService, ProductService>()` line), `TechStore.Api.http` (sample requests).
  - Untouched: entities, configurations, migrations, `Common/`, other modules.
- **Shared contracts (`shared/`)**: new DTO and request classes listed above; `PagedResultDto<T>` becomes available to all modules and the Uno client.
- **API surface**: new endpoints `GET /api/products`, `GET /api/products/{id}`, `POST /api/products`.
- **Tests**: new `tests/Backend.UnitTests/ProductServiceTests.cs`.
- **Documentation**: `AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/MILESTONE_2_PLAN.md`, `backend/Catalog/README.md` describe the Product/Category service split.
- **Team coordination**: Dev 3 (category) must implement `ICategoryService` / `CategoriesController` under the names recorded in the updated documentation.
