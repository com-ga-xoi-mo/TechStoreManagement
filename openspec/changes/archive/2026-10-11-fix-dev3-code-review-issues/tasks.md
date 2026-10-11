# Tasks

## 1. Route Standardization & Documentation

- [x] 1.1 Remove `[Route("api/v1/categories")]` from `backend/Catalog/CategoriesController.cs` and update Location header in `Create` action to `/api/categories/{result.Data!.Id}`. Verify with `dotnet build backend/TechStore.Api.csproj`.
- [x] 1.2 Remove `[Route("api/v1/inventory")]` from `backend/Inventory/InventoryController.cs` and update Location headers in `InitializeStock` (`/api/inventory/stocks?productId=...`) and `BatchImportSerials` (`/api/inventory/serials?productId=...`). Verify with `dotnet build backend/TechStore.Api.csproj`.
- [x] 1.3 Update sample request URIs in `backend/TechStore.Api.http` and `scripts/test_dev3_api.sh` from `/api/v1/...` to `/api/...`. Verify by checking diff with `git diff backend/TechStore.Api.http scripts/test_dev3_api.sh`.

## 2. Seed Data Referential Integrity

- [x] 2.1 Ensure `adminUser.Id` is set to fixed GUID `11111111-1111-1111-1111-111111111111` in `backend/Common/Data/DataSeeder.cs`. Verify by running `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~DataSeederTests"`.

## 3. Inventory Stock Service Validation

- [x] 3.1 Inject `IProductRepository` into `InventoryService.cs`. Update `InitializeStockAsync` validation to reject `quantity <= 0` with message "Opening quantity must be greater than zero.", verify product existence with `_productRepository.GetWithVariantsAsync`, and verify variant belongs to product when `variantId` is provided. Verify with `dotnet build backend/TechStore.Api.csproj`.
- [x] 3.2 Update `tests/Backend.UnitTests/InventoryServiceTests.cs` to pass `FakeProductRepository` to `InventoryService`, add test cases for `quantity <= 0`, non-existent `productId`, and mismatched `variantId`, and verify with `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~InventoryServiceTests"`.

## 4. Serial/IMEI Service Validation

- [x] 4.1 Inject `IProductRepository` into `SerialImeiService.cs`. Update `BatchImportAsync` to reject `request.Items.Count > 500` with message "Batch import size cannot exceed 500 items per request.", verify product existence with `_productRepository.GetWithVariantsAsync`, and verify variant belongs to product when `variantId` is provided. Verify with `dotnet build backend/TechStore.Api.csproj`.
- [x] 4.2 Update `tests/Backend.UnitTests/SerialImeiServiceTests.cs` to pass `FakeProductRepository` to `SerialImeiService`, add test cases for `Items.Count > 500`, non-existent `productId`, and mismatched `variantId`, and verify with `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~SerialImeiServiceTests"`.

## 5. End-to-End Verification & Code Review Notes Reconciliation

- [x] 5.1 Run solution-wide test suite and verify 0 failures with `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj`.
- [x] 5.2 Verify and reconcile all 6 issues in `docs/DEV3_CODE_REVIEW_NOTES.md` documenting their completed status.
