# Design

## Context

See `proposal.md` (Why) and `specs/product-catalog/spec.md` (required behavior). Current state that shapes the approach:

- `ProductService` (primary constructor: `IProductRepository`, `ICategoryRepository`, `IUnitOfWork`) implements search, detail and create. The `Product → ProductDto` mapping is written twice (in `GetByIdAsync` and `CreateAsync`).
- `IProductRepository.GetWithVariantsAsync` is `AsNoTracking` and is also used by `OrderService` (Orders), `InventoryService` and `SerialImeiService` (Inventory). Changing it would change their behavior, so writes need a separate, tracked load.
- `ProductVariant` → `Product` is `OnDelete(Cascade)`. `inventory_stocks`, `serial_imeis`, `purchase_order_items` and `order_items` reference `products` with `RESTRICT`. `inventory_movements` has no `product_id` (it points to stock rows), so it is covered by the stock check.
- `UnitOfWork.SaveChangesAsync` maps only `23505` to `DuplicateKeyException`; a foreign-key violation (`23503`) would surface as HTTP 500.
- JWT tokens carry `ClaimTypes.Role` with the `RoleType` name (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`), so `[Authorize(Roles = "Admin,Manager")]` works as in `StoreController`.
- No database migration is needed: every column already exists.
- Backend tests use hand-written fakes (`FakeProductRepository`, `FakeCategoryRepository`, `FakeUnitOfWork`), no mocking library. `FakeProductRepository` is also used by the Orders tests.

## Goals / Non-Goals

**Goals:**
- Every write is atomic and race-safe against concurrent stock/order inserts for the same product.
- `OrderService` and its tests keep compiling and behaving the same.
- The work is split into small steps a junior developer can follow one by one (see `tasks.md`).

**Non-Goals:**
- No change to `UnitOfWork` or other `Common/` files (the Dev 6 branch edits them; avoid merge conflicts).
- No optimistic concurrency token (`xmin`/`updatedAt` check). Last write wins.
- No change to read endpoints, `ProductDto`, `ProductSummaryDto` or `ProductVariantDto`.

## Decisions

### D1. Request contracts in `shared/Requests/`
- `UpdateProductRequest`: `CategoryId?`, `Name?`, `Brand?`, `Barcode?`, `BasePrice?`, `CostPrice?`, `Description?`, `ImageUrl?`, `Specs?` (`Dictionary<string, JsonElement>?`), `IsSerialTracked?`, `Variants?` (`List<UpdateProductVariantRequest>?`). **No `Sku` property** (SKU is immutable).
- `UpdateProductVariantRequest`: `Id?` (`Guid?`), `VariantName?`, `Sku?`, `Barcode?`, `Price?`, `Specs?`, `IsActive?` (`bool?`).
- `UpdateProductStatusRequest`: `IsActive?` (`bool?`).
- All properties are nullable, same as `CreateProductRequest`, so a missing value becomes a field error instead of a silent default. `IsSerialTracked` is required on update (a full replace must not silently flip it to a default).

*Alternative:* reuse `CreateProductRequest` for PUT. Rejected: it carries `Sku` and has no variant `Id`/`IsActive`.

### D2. Two validation layers
1. **`UpdateProductValidator.Validate(UpdateProductRequest?)`** — pure static class, no database, same style and messages as `CreateProductValidator`: required fields, lengths, non-negative prices, specs format, and per-variant item rules:
   - item without `Id`: `variantName`, `sku`, `price` required (new variant);
   - item with `Id`: `variantName` and `price` required, `sku` optional;
   - `Id` equal to `Guid.Empty` → error on `variants[i].id`;
   - the same `Id` twice → error on the later `variants[i].id`;
   - new-item SKUs duplicated within the request (case-insensitive) → error on the later `variants[i].sku`.
2. **Service checks that need stored data** (`ProductService.UpdateAsync`), run after the product row is locked:
   - category exists **and** `IsActive` → else 400 `categoryId`;
   - each `Id` belongs to this product → else 400 `variants[i].id`;
   - an existing item's `sku`, if given, equals the stored SKU ignoring case → else 400 `variants[i].sku`;
   - a new item's SKU equal to the stored product SKU (case-insensitive) → 400 `variants[i].sku`;
   - barcode rule: `finalVariantCount = existing variants + new items`; if `> 0` and `barcode` is not blank → 400 `barcode`;
   - all of these are collected into one error dictionary before returning.
- `CreateProductValidator` gets one extra rule: `Variants` has at least one item and `Barcode` is not blank → error on `barcode`.

*Alternative:* one shared validator base class for create and update. Rejected for now: it would refactor tested create code; the duplication is small and kept side by side.

### D3. Locked, tracked load for writes
New repository method `GetForUpdateAsync(Guid id, CancellationToken)`:
```csharp
var product = await dbContext.Products
    .FromSqlInterpolated($"SELECT * FROM products WHERE id = {id} FOR UPDATE")
    .FirstOrDefaultAsync(cancellationToken);
if (product != null)
{
    await dbContext.Entry(product).Collection(p => p.Variants).LoadAsync(cancellationToken);
}
return product;
```
- Tracked (no `AsNoTracking`), so the service edits the entity and `SaveChangesAsync` writes the diff.
- Must run inside `IUnitOfWork.BeginTransactionAsync`, like `InventoryStockRepository.GetForUpdateAsync`.
- Variants are loaded in a second statement instead of `.Include(...)` so EF does not wrap the `FOR UPDATE` query in a subquery.
- `FOR UPDATE` on the product row also blocks concurrent inserts of child rows that reference it (their FK check takes `FOR KEY SHARE` on the parent row). This makes "check usage, then delete/flip flag" race-safe without touching `UnitOfWork` (see D5).

### D4. One "in use" query
New repository method `IsInUseAsync(Guid productId, CancellationToken)` returns `true` when any of these exists for the product id:
`InventoryStocks`, `SerialImeis`, `PurchaseOrderItems`, `OrderItems` (`AnyAsync(x => x.ProductId == productId)` on each, stop at the first `true`).
- Used by: change of `IsSerialTracked`, adding the first variant, and delete.
- It reads other modules' tables through the shared `AppDbContext`. Accepted, because the precedent already exists in the other direction (`OrderService` uses Catalog's repository), and the alternative (injecting `IInventoryStockRepository`, `ISerialImeiRepository`, `IOrderRepository` into `ProductService`) would couple Catalog to three other teams' interfaces for a single yes/no question.
- When the product has no variants, every referencing row is product-level, so "in use" is exactly the condition from the schema note "reconcile existing product-level stock before this transition".

### D5. Transaction shape for every write
`UpdateAsync`, `SetStatusAsync` and `DeleteAsync` follow the same skeleton (based on AGENTS.md section 4):
```csharp
// pure validation first: no transaction, no repository call
await unitOfWork.BeginTransactionAsync(cancellationToken);
try
{
    var product = await productRepository.GetForUpdateAsync(id, cancellationToken);
    if (product == null) { await unitOfWork.RollbackAsync(cancellationToken); return NotFound; }
    // ...business checks; each failing check: RollbackAsync + return
    // ...mutate entity / Add / Remove
    await unitOfWork.SaveChangesAsync(cancellationToken);
    await unitOfWork.CommitAsync(cancellationToken);
    return Success;
}
catch (DuplicateKeyException)
{
    await unitOfWork.RollbackAsync(cancellationToken);
    return Conflict("A SKU in this request is already in use.");
}
catch
{
    await unitOfWork.RollbackAsync(cancellationToken);
    throw;
}
```
- `RollbackAsync` clears the change tracker, so a failed check leaves no staged edits.
- `SetStatusAsync` with an unchanged value: rollback (nothing staged) and return success without saving, so `updatedAt` stays the same.
- New variant SKUs are checked with the existing `FindExistingSkusAsync` before saving (409 with the conflicting SKUs); the `DuplicateKeyException` catch covers the race.

### D6. Result records
Add one shared record in `IProductService.cs` for the three new operations:
```csharp
public enum ProductCommandStatus { Success, NotFound, ValidationFailed, Conflict }

public record ProductCommandResult(
    ProductCommandStatus Status,
    ProductDto? Data,
    Dictionary<string, string[]>? ValidationErrors,
    string? ConflictMessage)
{
    public static ProductCommandResult Success(ProductDto? data) => new(ProductCommandStatus.Success, data, null, null);
    public static ProductCommandResult NotFound() => new(ProductCommandStatus.NotFound, null, null, null);
    public static ProductCommandResult ValidationFailed(Dictionary<string, string[]> errors) => new(ProductCommandStatus.ValidationFailed, null, errors, null);
    public static ProductCommandResult Conflict(string message) => new(ProductCommandStatus.Conflict, null, null, message);
}
```
New interface methods:
```csharp
Task<ProductCommandResult> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
Task<ProductCommandResult> SetStatusAsync(Guid id, UpdateProductStatusRequest request, CancellationToken cancellationToken = default);
Task<ProductCommandResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
```
- `DeleteAsync` returns `Success(null)`.
- Existing `CreateProductResult`, `ProductDetailResult`, `ProductSearchResult` are left as they are (no churn in tested code).

### D7. Controller
- `ProductsController` gets three actions: `[HttpPut("{id:guid}")] Update`, `[HttpPatch("{id:guid}/status")] SetStatus`, `[HttpDelete("{id:guid}")] Delete`.
- `[Authorize(Roles = "Admin,Manager")]` is put on **each write action** (`Create`, `Update`, `SetStatus`, `Delete`), not on the class, so `Search` and `GetById` stay anonymous.
- One private helper `ToActionResult(ProductCommandResult result, Func<ProductDto?, IActionResult> onSuccess)` maps: `NotFound` → `NotFound()`, `ValidationFailed` → `ValidationProblem(modelState)`, `Conflict` → `Problem(statusCode: 409, title: "Conflict", detail: message)`. A second private helper `ToModelState(Dictionary<string, string[]>)` replaces the copy-pasted loops (also used by `Search` and `Create`).
- Success: `Update`/`SetStatus` → `Ok(dto)`, `Delete` → `NoContent()`.
- Add `[ProducesResponseType]` attributes like `CategoriesController` does.

### D8. Shared mapper
New `internal static class ProductDtoMapper` in `Catalog/Services/ProductDtoMapper.cs`:
`public static ProductDto ToDto(Product product, string categoryName)`; variants ordered by `Price` then `Sku`, specs parsed with `ProductSpecHelper.ParseJsonSpecs`, effective specs with `MergeEffectiveSpecs`. `GetByIdAsync` and `CreateAsync` switch to it (behavior unchanged; existing tests prove it), and `UpdateAsync`/`SetStatusAsync` use it. Category name comes from `product.Category?.Name` for reads and from the category fetched through `ICategoryRepository` for writes (the locked load does not include `Category`).

`InternalsVisibleTo` is not needed: tests go through `ProductService`.

### D9. Field handling on update
- Strings are trimmed; blank optional strings become `null`; `Specs == null` becomes `"{}"`.
- `UpdatedAt = DateTime.UtcNow` on the product, and on every variant that is updated or added (same clock source as the existing `CreateAsync`; no `TimeProvider` to avoid changing the constructor used by 13 existing tests).
- New variants: `Id = Guid.NewGuid()`, `ProductId = product.Id`, `IsActive = item.IsActive ?? true`, `CreatedAt = now`, SKU trimmed with the casing sent (same as create).
- **New variants must be staged explicitly** with a new repository method `AddVariant(ProductVariant variant)` (`dbContext.ProductVariants.Add(variant)`), in addition to `product.Variants.Add(variant)` (call the collection add first, then `AddVariant`). Reason: `ProductVariant.Id` is configured as generated on add (`gen_random_uuid()` default). When EF discovers a child with an already-set key only through the navigation of a tracked parent, it treats it as an existing row and issues `UPDATE`, which fails with `DbUpdateConcurrencyException` (0 rows affected). `CreateAsync` does not have this problem because the whole new graph is added through `Products.Add`.
- Existing variants: `IsActive` changes only when `item.IsActive` has a value.
- Product `IsActive` is **not** touched by PUT (only by PATCH status).

### D10. Delete
After lock and `IsInUseAsync == false`, call new repository method `Remove(Product product)` (`dbContext.Products.Remove(product)`). Variants are tracked (loaded in D3), so EF deletes them before the product; the database cascade is a second safety net. In-use → `Conflict("Product is in use by stock, purchase or order records. Deactivate it instead.")`.

### D11. Why reads stay anonymous
`tests/Frontend.IntegrationTests/CatalogApiIntegrationTests.cs` and the Orders live tests call `GET /api/products` without logging in, and `OrdersController` itself has no `[Authorize]` yet. Locking reads now would break other developers' suites. Requiring login on all reads should be one team-wide change (all controllers + integration tests log in first).

### D12. Specification filter suggestions (added after review)
The list page's free-text spec filter gave no way to know which keys exist, and filtering is an exact match, so only stored values can ever match. `GET /api/products/spec-keys?categoryId=` lists them.
- Repository: `GetSpecValueCountsAsync(Guid? categoryId)` runs one read-only SQL through `Database.SqlQuery<SpecValueCount>` (record `Key`, `ValueJson`, `ProductCount`): effective specs built exactly like the search filter (product specs for products without variants, `p.specs || v.specs` per variant), `jsonb_each`, `jsonb_typeof IN ('number','string','boolean')`, `GROUP BY key, value::text`, `COUNT(DISTINCT product_id)`. No tracking, no write.
- Service: `GetSpecKeysAsync` groups rows by key, sorts (D12 order in the spec), caps 100 values per key and builds `filterValue`: numbers as their raw JSON text, booleans `true`/`false`, strings as-is unless the filter would read them as a number or boolean or they start and end with `"`, in which case they are quoted.
- Filter parser (`ProductSpecFilterHelper`): a value of at least two characters that starts and ends with `"` becomes the string between the quotes (empty → 400). Before this, a stored string such as `"5.2"` could not be filtered.
- Contract: `shared/DTOs/ProductSpecKeyDto.cs` (`Key`, `ProductCount`, `Values`) and `ProductSpecValueDto` (`Value` as `JsonElement`, `Label`, `FilterValue`, `ProductCount`). Route `GET api/products/spec-keys` is a literal segment, so it does not clash with `{id:guid}`. Anonymous like the other reads (D11).

### D13. Specification definitions table (added after review)
Spec keys stay snake_case data inside `specs` (schema contract); their human names live in one shared dictionary instead of being repeated in every product.
- Table `spec_definitions` (Catalog, no foreign key): `key varchar(64)` primary key with check `^[a-z][a-z0-9_]*$`, `label varchar(100)` not null with unique index `idx_spec_definitions_label_lower` on `lower(label)` (raw SQL in the migration; EF cannot express it), `unit varchar(20)` null, `value_kind varchar(10)` not null with check `IN ('number','string','boolean')`, `created_at timestamptz default now()`. Seeded through `HasData` with the 19 keys of the seed data and the E2E script. Migration `AddSpecDefinitions`.
- Entity `Catalog/Entities/SpecDefinition` (not `BaseEntity`: the key is the identity). Repository `ISpecDefinitionRepository` / `SpecDefinitionRepository` (own aggregate): `GetAllAsync`, `GetByKeysAsync`, `FindKeysByLabelsAsync` (lower-case match, `AsNoTracking`), `Add`. `ProductService` gets it as a fourth constructor dependency.
- Writes ride on the product write: `CreateProductRequest`/`UpdateProductRequest` gain `SpecLabels` (`Dictionary<string, SpecLabelRequest>`). `SpecLabelValidator` (pure, used by both validators) checks shape and that each key is used by the request and its `valueKind` matches the values. The service then loads definitions for all keys in the request: values of defined keys must match their kind (400 `specs` / `variants[i].specs`); for undefined keys named in `specLabels` it checks the label is free (400 `specLabels.<key>`) and stages `Add`. Create saves once; update inside its existing transaction. Insert-only: a definition that exists is never overwritten (renaming is out of scope).
- A unique violation on `spec_definitions` (another user created the same key or label meanwhile) is mapped by constraint name to 409 "A spec name was created by someone else; reload and save again." instead of the SKU message (`CreateProductResult` gains an optional `ConflictMessage`).
- Reads: `GET /api/products/spec-definitions` (anonymous, D11) and `label`/`unit`/`valueKind` on `spec-keys`.

### D14. One spelling per brand (added after review)
Brand stays a free-text column (no table: a brand has nothing but its name). Two repository reads, both `AsNoTracking`: `GetBrandCountsAsync()` (`GROUP BY brand` with counts) and `GetBrandSpellingsAsync(brand, excludeProductId)` (`ILIKE` on the escaped brand, so an exact match ignoring case; the product under update is excluded). The service picks the spelling used by most products (ties: ordinal order) for create/update and merges spellings for `GET /api/products/brands` (`BrandDto { Name, ProductCount }`, anonymous). No migration; existing mixed spellings are merged in the list and fixed the next time each product is saved.

## Risks / Trade-offs

- [`FOR UPDATE` raw SQL cannot run on the EF InMemory provider] → unit tests use `FakeProductRepository`; the real lock is checked by the manual `.http` run against PostgreSQL (task group 9).
- [Concurrent insert of stock for a product being deleted waits, then fails with an FK error in the Inventory request] → acceptable: that request targets a product that no longer exists; Catalog itself never returns 500.
- [Last write wins on concurrent edits] → documented non-goal; can add an `updatedAt` precondition later without changing these endpoints' shape.
- [`IsInUseAsync` reads other modules' tables] → it is read-only, one method, and named after the business question; if Inventory/Orders later expose a usage API, only this method changes.
- [Adding methods to `IProductRepository` breaks every implementation] → there is exactly one production implementation and one fake (`tests/Backend.UnitTests/FakeProductRepository.cs`, also used by `OrderServiceTests` and `SerialImeiServiceTests`); both are updated in the same task group, before the service code, and only new members are added (existing members keep their behavior).
- [New variant saved as `UPDATE` instead of `INSERT`] → `AddVariant` (D9); the live run in task group 12 adds a variant to a stored product to prove it.
- [Demo seeder (Dev 6 branch) throws on startup if a demo product's category changed] → it only logs the error and skips seeding; tell Dev 6 that category edits are now allowed so the seeder can relax that check.
- [`POST /api/products` now needs a token] → no client creates products yet; the `.http` samples are updated to log in first.

## Coordination notes

These notes replace direct messages to other developers. They are copied into the pull request description (task 13.4) so every reviewer sees them before the code reaches `dev`.

- **Dev 4 (Orders) and Dev 3 (Inventory):** `IProductRepository` only gains new members (`GetForUpdateAsync`, `IsInUseAsync`, `AddVariant`, `Remove`). `GetWithVariantsAsync`, `SearchAsync` and `FindExistingSkusAsync` are unchanged, so `OrderService`, `InventoryService` and `SerialImeiService` behave as before. The shared `FakeProductRepository` keeps all existing members.
- **Dev 6 (Seeder / Analytics):** a product's category can now be changed through `PUT /api/products/{id}`. The demo seeder on `feat/dev6-sprint2-analytics-dashboard` throws (and skips seeding) when a demo product's category differs from the fixture, so it should accept an edited category. SKU is immutable, and `isSerialTracked` cannot change once a product has stock, serials or orders, so the seeder's SKU key and serial-tracking check stay valid.
- **Dev 3 (Categories):** `POST /api/categories` still has no `[Authorize]`, although `category-catalog` requires Admin or Manager. Product writes in this change use `[Authorize(Roles = "Admin,Manager")]` per action, which can be reused.
- **Everyone:** `POST /api/products` now returns 401 without a token. The samples in `backend/TechStore.Api.http` log in first (task 12.0).

## Migration Plan

No schema migration. Deploy is a normal backend restart. Rollback is reverting the commit; no data format changes.
