# Proposal

## Why

Milestone 2 Sprint 2 assigns Dev 2 the remaining product use cases: UC-PROD-04 (update a product and its hardware specs) and UC-PROD-05 (retire or delete a product). Today `/api/products` can only search, read and create, so an Admin cannot fix a wrong price, edit specs, add a new variant or stop selling a product. The product write endpoints are also not protected by any role check, while the requirements demand server-side RBAC.

The rules for these operations already exist as notes in `docs/database/schema.dbml` ("use `is_active` to retire records; hard deletion is only for unused records", "products with variants have no barcode", "once variants exist, stock and order lines must select a variant"), and other modules already depend on them: `OrderService` rejects inactive products and variants, the order-create screen only offers active products, and the analytics branch counts active products. This change turns those notes into enforced API behavior.

## What Changes

- **UC-PROD-04 – Update product** (`PUT /api/products/{id}`): replace the product's editable fields (`categoryId`, `name`, `brand`, `barcode`, `basePrice`, `costPrice`, `description`, `imageUrl`, `specs`, `isSerialTracked`) and upsert variants:
  - a variant item with `id` updates that variant's `variantName`, `barcode`, `price`, `specs` and optionally `isActive`;
  - a variant item without `id` adds a new variant;
  - existing variants that are not listed stay unchanged (variants are never deleted by this endpoint).
  - The product SKU and existing variant SKUs cannot be changed.
  - Business guards: the target category must exist and be active; `isSerialTracked` cannot change, and the first variant cannot be added, once the product is in use (has inventory stock, serial/IMEI, purchase order or order lines).
- **UC-PROD-05 – Retire / delete product** (decision "A"):
  - `PATCH /api/products/{id}/status` with `{ "isActive": bool }` stops or resumes selling a product (soft delete). It is idempotent.
  - `DELETE /api/products/{id}` permanently removes a product and its variants only when the product has never been used; a product in use returns 409 telling the caller to deactivate it instead.
- **Barcode rule**: a product that has variants must not have a product-level barcode (applies to both create and update). This is a small new validation on `POST /api/products`.
- **Specification filter suggestions** (added after review): `GET /api/products/spec-keys?categoryId=` lists the spec keys and values that really exist, each with a ready-to-send filter value; the spec filter accepts a double-quoted value as a JSON string so string values such as `"5.2"` can be matched (design D12).
- **RBAC on product writes**: `POST`, `PUT`, `PATCH /status` and `DELETE` on `/api/products` require an authenticated user with role `Admin` or `Manager` (401 without a token, 403 for other roles). Read endpoints (`GET /api/products`, `GET /api/products/{id}`) are unchanged in this change.
- **Internal refactor**: the duplicated `Product → ProductDto` mapping in `ProductService` moves to one mapper used by detail, create and update.
- **Tests and manual checks**: unit tests for the new validator, service rules and controller authorization attributes; sample requests in `backend/TechStore.Api.http`.

Out of scope: frontend (Catalog pages, dialog, client repositories), category CRUD (Dev 3), requiring login on `GET` endpoints (would break the anonymous live integration tests of Catalog and Orders; tracked as a team follow-up), changing SKU letter case normalization, barcode uniqueness, image upload, optimistic concurrency tokens, database migrations.

## Capabilities

### New Capabilities
*(None.)*

### Modified Capabilities
- `product-catalog`: adds product update with variant upsert, activation status changes, conditional hard delete, the barcode-with-variants rule, and role requirements on product write endpoints.

## Impact

- **Backend (`backend/Catalog/`)**:
  - Modified: `ProductsController.cs` (new actions + `[Authorize]`), `Services/IProductService.cs` (new methods and result records), `Services/ProductService.cs`, `Services/CreateProductValidator.cs` (barcode rule), `Repositories/IProductRepository.cs`, `Repositories/ProductRepository.cs` (locked load, usage check, remove).
  - New: `Services/UpdateProductValidator.cs`, `Services/ProductDtoMapper.cs`.
  - Untouched: entities, EF configurations, migrations (no schema change), `Common/`, other modules.
- **Shared contracts (`shared/Requests/`)**: new `UpdateProductRequest`, `UpdateProductVariantRequest`, `UpdateProductStatusRequest`. Existing DTOs are unchanged.
- **API surface**: new `PUT /api/products/{id}`, `PATCH /api/products/{id}/status`, `DELETE /api/products/{id}`; `POST /api/products` now requires Admin/Manager and rejects a barcode on a product with variants.
- **Other modules**: `OrderService` keeps calling `IProductRepository.GetWithVariantsAsync` and `SearchAsync`; their behavior does not change. `FakeProductRepository` in backend tests gains the new methods.
- **Team coordination**: the demo seeder on the Dev 6 branch matches products by SKU and refuses a changed category or serial-tracking flag; SKU stays immutable here, and Dev 6 should be told that category edits are now possible.
