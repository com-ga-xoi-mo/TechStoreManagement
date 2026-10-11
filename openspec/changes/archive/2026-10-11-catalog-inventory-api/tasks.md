# Tasks

## 1. Shared Contracts (DTOs & Requests)

- [x] 1.1 Create `CategoryDto` and `CategoryTreeNodeDto` in `shared/DTOs/`, and `CreateCategoryRequest` in `shared/Requests/`; verify build succeeds with `dotnet build shared/TechStore.Shared.csproj`.
- [x] 1.2 Create `InventoryStockDto` and `SerialImeiDto` in `shared/DTOs/`, and `InventoryStockFilterRequest`, `InitializeStockRequest`, `SerialImeiFilterRequest`, `BatchImportSerialImeiRequest`, and `SerialImeiImportItemRequest` in `shared/Requests/`; verify build succeeds with `dotnet build shared/TechStore.Shared.csproj`.

## 2. Category Management (Catalog Module)

- [x] 2.1 Extend `ICategoryRepository` and `CategoryRepository` in `backend/Catalog/Repositories/` with `GetActiveCategoriesAsync`, `ExistsBySlugAsync`, and `Add(Category category)`; verify compilation succeeds with `dotnet build backend/TechStore.Api.csproj`.
- [x] 2.2 Implement `ICategoryService` and `CategoryService` in `backend/Catalog/Services/` with slug validation/generation, parent hierarchy validation, and in-memory tree building; write unit tests in `tests/Backend.UnitTests/CategoryServiceTests.cs` and verify tests pass with `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~CategoryServiceTests"`.
- [x] 2.3 Create `CategoriesController` at `backend/Catalog/CategoriesController.cs` exposing `GET /api/v1/categories/tree` and `POST /api/v1/categories` (with fallback routes `/api/categories/tree` and `/api/categories`), returning `CategoryTreeNodeDto` and HTTP 201 Created with Location header or RFC 7807 ProblemDetails on conflict/validation error; verify compilation succeeds.

## 3. Inventory Stock Management (Inventory Module)

- [x] 3.1 Implement `IInventoryStockRepository` and `InventoryStockRepository` in `backend/Inventory/Repositories/` with `SearchStocksAsync` (Product/Variant includes, low-stock filter, pagination), `GetByProductAndVariantAsync`, `GetForUpdateAsync` (`SELECT ... FOR UPDATE`), `Add`, and `AddMovement`; verify compilation succeeds with `dotnet build backend/TechStore.Api.csproj`.
- [x] 3.2 Implement `IInventoryService` and `InventoryService` in `backend/Inventory/Services/` with `GetStocksAsync` and `InitializeStockAsync` (orchestrating `IUnitOfWork` transaction, duplicate balance prevention, creating `InventoryStock` and `InventoryMovement` with `OpeningBalance`); write unit tests in `tests/Backend.UnitTests/InventoryServiceTests.cs` and verify tests pass with `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~InventoryServiceTests"`.

## 4. Serial/IMEI Lifecycle & Batch Import (Inventory Module)

- [x] 4.1 Implement `ISerialImeiRepository` and `SerialImeiRepository` in `backend/Inventory/Repositories/` with `SearchAsync` (filters for ProductId, VariantId, Status, pagination), `FindExistingIdentifiersAsync` (batch duplicate lookup), and `AddRange`; verify compilation succeeds with `dotnet build backend/TechStore.Api.csproj`.
- [x] 4.2 Implement `ISerialImeiService` and `SerialImeiService` in `backend/Inventory/Services/` with `GetSerialsAsync` and `BatchImportAsync` (validating IMEI format, checking payload duplicates, checking DB duplicates, locking stock row `FOR UPDATE`, enforcing strict 3-step sequence: 1) create InventoryStock with Quantity=0 if absent, 2) accumulate `stock.Quantity += importedCount`, 3) stage `InventoryMovement` with `QuantityAfter = stock.Quantity` strictly after accumulation, staging serial items, translating duplicate key exceptions to 409 Conflict); write unit tests in `tests/Backend.UnitTests/SerialImeiServiceTests.cs` and verify tests pass with `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~SerialImeiServiceTests"`.
- [x] 4.3 Create `InventoryController` at `backend/Inventory/InventoryController.cs` exposing `GET /api/v1/inventory/stocks`, `POST /api/v1/inventory/stocks/initialize`, `GET /api/v1/inventory/serials`, and `POST /api/v1/inventory/serials/batch-import` (with `/api/inventory/...` fallback routes), returning RFC 7807 ProblemDetails for 400, 404, 409; verify compilation succeeds.

## 5. Dependency Injection, HTTP Requests & Integration Verification

- [x] 5.1 Register all new repositories and services (`ICategoryService`, `IInventoryStockRepository`, `IInventoryService`, `ISerialImeiRepository`, `ISerialImeiService`) in `backend/Program.cs`; verify application builds cleanly with `dotnet build backend/TechStore.Api.csproj`.
- [x] 5.2 Add sample requests and validation scenarios for all 6 endpoints to `backend/TechStore.Api.http` (success scenarios, validation errors, low-stock filter, and 409 duplicate conflicts); verify requests are documented and formatted properly.
- [x] 5.3 Run all tests across the solution using `dotnet test TechStore.sln` and verify that all test suites pass with zero failures.
