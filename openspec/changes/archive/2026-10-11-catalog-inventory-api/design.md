# Design

## Context

See `proposal.md` for motivation and `specs/category-catalog/spec.md`, `specs/inventory-management/spec.md` for the functional requirements.

Current architectural state and constraints:
- **Milestone 1 Entities & Schema**:
  - `Category` (`backend/Catalog/Entities/Category.cs`) supports recursive hierarchy (`ParentId`, `SubCategories`) and unique slug.
  - `InventoryStock` (`backend/Inventory/Entities/InventoryStock.cs`) enforces check constraints: `quantity >= 0`, `reserved_quantity >= 0`, `reserved_quantity <= quantity`, and unique composite nulls-not-distinct on `(product_id, variant_id)`.
  - `SerialImei` (`backend/Inventory/Entities/SerialImei.cs`) has unique B-tree indexes on `serial_number` and `imei`, check constraints on non-empty identifier and 15-digit IMEI pattern (`^[0-9]{15}$`), and status enum `SerialImeiStatus`.
  - `InventoryMovement` (`backend/Inventory/Entities/InventoryMovement.cs`) provides an append-only audit ledger with `MovementType = OpeningBalance`, `quantity_change <> 0`, and `quantity_after >= 0`.
- **4-Tier Layering & Unit of Work**:
  - Defined in `AGENTS.md` and implemented in Milestone 2: `Controller -> Service -> Repository -> AppDbContext`.
  - Services orchestrate business logic, handle DTO mapping, and own transaction boundaries via `IUnitOfWork` (`BeginTransactionAsync`, `SaveChangesAsync`, `CommitAsync`, `RollbackAsync`).
  - Repositories are the only classes interacting with `AppDbContext`. They stage changes and do not commit or save.
  - `UnitOfWork` catches PostgreSQL unique violation `23505` and throws `DuplicateKeyException` with constraint details.
- **Shared Contracts**:
  - Contracts live in flat folders `shared/DTOs/` and `shared/Requests/`.
  - `PagedResultDto<T>` is available in `shared/DTOs/` for reusable pagination.

## Goals / Non-Goals

**Goals:**
- Deliver all 6 use cases across Category (UC-CAT-01, UC-CAT-02) and Inventory (UC-INV-01, UC-INV-02, UC-IMEI-01, UC-IMEI-02) for Dev 3 (Bình 218).
- Strictly adhere to the 4-tier layer boundary with zero direct `AppDbContext` access in services or controllers.
- Ensure pessimistic row locking (`SELECT ... FOR UPDATE`) during inventory stock initialization and batch serial imports to guarantee anti-overselling and balance invariants.
- Maintain append-only audit trail in `inventory_movements` for every balance-altering operation.
- Provide robust duplicate detection for Serial/IMEI with clear RFC 7807 ProblemDetails error responses on HTTP 409 Conflict.
- Support both versioned `/api/v1/...` and canonical `/api/...` route conventions for maximum compatibility.

**Non-Goals:**
- POS counter cashier checkout and dynamic VietQR payment generation (Dev 4 - Sales module).
- Customer order fulfillment, returns, and warranty claims (Orders module).
- Purchase order creation and supplier invoice management (Purchase Orders workflow).
- Any modifications to PostgreSQL migrations or table schema configurations.

## Decisions

### D1. Repository Extension & Layering Partition
- **Category Repository (`backend/Catalog/Repositories/`)**:
  - Extend the existing `ICategoryRepository` / `CategoryRepository` stub with:
    - `GetByIdAsync(Guid id, CancellationToken ct)` (already present)
    - `GetActiveCategoriesAsync(CancellationToken ct)`: queries all categories with `IsActive == true` using `AsNoTracking()`.
    - `ExistsBySlugAsync(string slug, CancellationToken ct)`: checks if a slug already exists in `categories`.
    - `Add(Category category)`: stages a new category in the change tracker.
- **Inventory Stock Repository (`backend/Inventory/Repositories/`)**:
  - Implement `IInventoryStockRepository` / `InventoryStockRepository`:
    - `SearchStocksAsync(InventoryStockSearchCriteria criteria, CancellationToken ct)`: queries stocks with optional filters (`ProductId`, `VariantId`, `LowStock`), includes `Product` and `Variant`, and returns `(IReadOnlyList<InventoryStock> Items, int TotalCount)`.
    - `GetByProductAndVariantAsync(Guid productId, Guid? variantId, CancellationToken ct)`: retrieves tracked stock entity for balance check.
    - `GetForUpdateAsync(Guid productId, Guid? variantId, CancellationToken ct)`: executes `SELECT ... FOR UPDATE` via `FromSql` within an active transaction to acquire a pessimistic row lock.
    - `Add(InventoryStock stock)`: stages new balance row.
    - `AddMovement(InventoryMovement movement)`: stages audit movement entry.
- **Serial/IMEI Repository (`backend/Inventory/Repositories/`)**:
  - Implement `ISerialImeiRepository` / `SerialImeiRepository`:
    - `SearchAsync(SerialImeiSearchCriteria criteria, CancellationToken ct)`: paginated queries filtered by `ProductId`, `VariantId`, and `Status`.
    - `FindExistingIdentifiersAsync(IEnumerable<string> serialNumbers, IEnumerable<string> imeis, CancellationToken ct)`: executes batch check against `serial_imeis` returning any conflicting serial numbers or IMEIs.
    - `AddRange(IEnumerable<SerialImei> items)`: stages new physical items.

*Rationale*: Follows `AGENTS.md` strictly: repositories express domain intent, return entities, and stage changes without saving.

### D2. Category Tree Construction In-Memory
`CategoryService.GetTreeAsync` fetches all active categories in a single flat query via `ICategoryRepository.GetActiveCategoriesAsync` and reconstructs the recursive tree structure in-memory:
1. Projects all flat categories into `CategoryTreeNodeDto` dictionary keyed by `Id`.
2. Roots are nodes where `ParentId == null` or whose parent is not present in the active set.
3. Non-root nodes are attached to their corresponding parent's `SubCategories` collection.
4. Top-level categories and subcategories are sorted by name.

*Alternative*: Recursive CTE in PostgreSQL. Rejected because typical catalog trees contain tens to hundreds of categories; an in-memory assembly on a single flat select is faster, avoids complex raw SQL, and is easily unit-tested.

### D3. Category Slug Validation and Uniqueness Handling
`CategoryService.CreateAsync` validates:
- `Name`: required, max 200 chars.
- `Slug`: if omitted, auto-generated from `Name` via URL-friendly slugifier; if provided, validated against regex `^[a-z0-9]+(?:-[a-z0-9]+)*$`.
- Slug uniqueness check via `ICategoryRepository.ExistsBySlugAsync`. If already present, returns conflict result with HTTP 409.
- Parent category check via `ICategoryRepository.GetByIdAsync`. If `ParentId` is provided but non-existent, returns validation failure with HTTP 400.

### D4. Transactional Stock Initialization & Movement Ledger
`InventoryService.InitializeStockAsync`:
1. Validates inputs: `ProductId` non-empty, `Quantity >= 0`, `MinStockAlert >= 0`.
2. Opens transaction: `await _unitOfWork.BeginTransactionAsync(ct)`.
3. Checks if `InventoryStock` already exists for `(ProductId, VariantId)`:
   - If found: returns conflict (HTTP 409) since initial stock can only be set once per item.
4. Stages `InventoryStock`:
   ```csharp
   var stock = new InventoryStock
   {
       ProductId = request.ProductId!.Value,
       VariantId = request.VariantId,
       Quantity = request.Quantity!.Value,
       ReservedQuantity = 0,
       MinStockAlert = request.MinStockAlert ?? 5
   };
   _stockRepository.Add(stock);
   ```
5. Stages `InventoryMovement`:
   ```csharp
   var movement = new InventoryMovement
   {
       InventoryStock = stock,
       MovementType = InventoryMovementType.OpeningBalance,
       QuantityChange = request.Quantity!.Value,
       QuantityAfter = request.Quantity!.Value,
       PerformedByUserId = performedByUserId,
       Reason = request.Reason ?? "Initial balance initialization"
   };
   _stockRepository.AddMovement(movement);
   ```
6. Calls `await _unitOfWork.SaveChangesAsync(ct)`.
7. Calls `await _unitOfWork.CommitAsync(ct)`.
8. Catches `DuplicateKeyException` on constraint `idx_inventory_stocks_product_variant` and translates to 409 Conflict.

### D5. Batch Serial/IMEI Import, Duplicate Detection & Stock Sync
`SerialImeiService.BatchImportAsync`:
1. Validates input batch:
   - Non-empty batch items list.
   - For each item: at least one of `SerialNumber` or `Imei` must be non-empty (btrim check).
   - If `Imei` is provided, validate 15 digits (`^[0-9]{15}$`).
   - In-memory duplicate check: ensure no duplicate serials or IMEIs exist within the submitted payload itself. If duplicates exist within payload, return 400 Bad Request or 409 Conflict with duplicate identifiers.
2. Opens transaction: `await _unitOfWork.BeginTransactionAsync(ct)`.
3. Pre-checks existing identifiers in DB:
   - Queries `_serialImeiRepository.FindExistingIdentifiersAsync(serials, imeis)`.
   - If any existing serials or IMEIs are found, aborts transaction, rolls back, and returns Conflict Result (mapped to HTTP 409 ProblemDetails) detailing exactly which serial numbers/IMEIs already exist.
4. Synchronizes stock row balance and movement audit ledger in strict sequential order:
   - **Bước 1 (Kiểm tra tồn kho)**: Acquires row lock: `var stock = await _stockRepository.GetForUpdateAsync(productId, variantId, ct)`. If no stock row exists yet (`stock == null`), create a new `InventoryStock` with `Quantity = 0, ReservedQuantity = 0, MinStockAlert = 5` and stage it via `_stockRepository.Add(stock)`.
   - **Bước 2 (Cập nhật số lượng)**: Accumulate valid imported IMEIs into stock (`stock.Quantity += importedCount`).
   - **Bước 3 (Tạo lịch sử movement)**: ONLY stage the `InventoryMovement` record AFTER `stock.Quantity` has been accumulated in Bước 2. The property `QuantityAfter` MUST strictly receive the updated value from Bước 2 (`QuantityAfter = stock.Quantity`):
     ```csharp
     var movement = new InventoryMovement
     {
         InventoryStock = stock,
         MovementType = InventoryMovementType.OpeningBalance,
         QuantityChange = importedCount,
         QuantityAfter = stock.Quantity, // Strictly assigned AFTER stock.Quantity += importedCount
         PerformedByUserId = performedByUserId,
         Reason = request.Reason ?? "Initial batch serial/IMEI import"
     };
     _stockRepository.AddMovement(movement);
     ```
5. Stages new `SerialImei` entities with `Status = SerialImeiStatus.InStock` via `_serialImeiRepository.AddRange(serialEntities)`.
6. Calls `await _unitOfWork.SaveChangesAsync(ct)` and `await _unitOfWork.CommitAsync(ct)`.
7. Exception handling: catches `DuplicateKeyException` (e.g. from `idx_serial_imeis_serial` or `idx_serial_imeis_imei`) and returns HTTP 409 ProblemDetails.

### D6. Service Result Pattern & Controller ProblemDetails
Following the pattern established in `IProductService`:
- Services return strongly typed result records:
  - `CategoryTreeResult(bool IsSuccess, IReadOnlyList<CategoryTreeNodeDto> Data)`
  - `CreateCategoryResult(bool IsSuccess, CategoryDto? Data, Dictionary<string, string[]>? ValidationErrors, bool IsConflict, string? ConflictReason)`
  - `InventoryStockSearchResult(bool IsSuccess, PagedResultDto<InventoryStockDto>? Data, Dictionary<string, string[]>? ValidationErrors)`
  - `InitializeStockResult(bool IsSuccess, InventoryStockDto? Data, Dictionary<string, string[]>? ValidationErrors, bool IsConflict, string? ConflictReason)`
  - `SerialImeiSearchResult(bool IsSuccess, PagedResultDto<SerialImeiDto>? Data, Dictionary<string, string[]>? ValidationErrors)`
  - `BatchImportSerialImeiResult(bool IsSuccess, int ImportedCount, IReadOnlyList<string>? ConflictingSerials, IReadOnlyList<string>? ConflictingImeis, Dictionary<string, string[]>? ValidationErrors, bool IsConflict, string? ConflictReason)`
- Controllers inspect result status and call:
  - `Ok(result.Data)`
  - `CreatedAtAction(...)`
  - `ValidationProblem(modelState)` for 400
  - `Problem(statusCode: 409, title: "Conflict", detail: ...)` for 409
  - `NotFound()` for 404

### D7. User ID Resolution in Controllers
For write operations requiring `performedByUserId` (`InventoryMovement` audit log):
- Controller inspects `HttpContext.User`:
  - `User.FindFirst(ClaimTypes.NameIdentifier)?.Value` or `User.FindFirst("sub")?.Value`.
  - If a valid Guid is present, use it.
  - If unauthenticated (e.g., during development or integration tests where JWT is disabled/optional), fallback to `DataSeeder.AdminUserId` (`Guid.Parse("11111111-1111-1111-1111-111111111111")`).

### D8. Route Conventions
Controllers will support both `/api/v1/...` and `/api/...` attributes:
- `CategoriesController`:
  - `[Route("api/v1/categories")]`
  - `[Route("api/categories")]`
- `InventoryController`:
  - `[Route("api/v1/inventory")]`
  - `[Route("api/inventory")]`
Endpoints match the user request exactly:
- `GET /api/v1/categories/tree`
- `POST /api/v1/categories`
- `GET /api/v1/inventory/stocks`
- `POST /api/v1/inventory/stocks/initialize`
- `GET /api/v1/inventory/serials`
- `POST /api/v1/inventory/serials/batch-import`

## Risks / Trade-offs

- **[Risk] High concurrency duplicate serial import race condition** → *Mitigation*: Two-tier protection: 1) Pre-query check `FindExistingIdentifiersAsync`, and 2) PostgreSQL database unique indexes (`idx_serial_imeis_serial`, `idx_serial_imeis_imei`) caught by `UnitOfWork` and mapped to `DuplicateKeyException` returning 409 Conflict.
- **[Risk] Overselling or quantity desynchronization** → *Mitigation*: Service acquires explicit row-level lock (`SELECT ... FOR UPDATE`) on the `inventory_stocks` balance row before calculating quantity increments and writing `inventory_movements`.
- **[Risk] Cyclic category parent references** → *Mitigation*: Category creation prevents `ParentId` pointing to itself, and validates parent existence in database before persisting.
- **[Risk] Large serial batch import memory pressure** → *Mitigation*: Cap batch import payload size (e.g. max 500 items per request) in the request validator.
