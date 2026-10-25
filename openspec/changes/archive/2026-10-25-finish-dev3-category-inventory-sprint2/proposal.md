# Proposal

## Why

Dev 3's Sprint 2 slice (Feature 3: Danh mục & Tồn kho/IMEI) is feature-complete on `feat/dev3-catalog-inventory-management` but cannot be merged: the committed test hosts reference an auth helper that does not exist, so `dotnet build TechStore.sln` and `dotnet test` fail for the whole team. Two pieces of the Sprint 2 instruction are also unfinished — the old `InventoryPage` XAML is still compiled and registered, and the category/inventory RBAC described in the specs does not match what the controllers enforce. Closing these gaps is what makes the slice mergeable and lets Milestone 2 DoD #4 (`dotnet test ... 100% PASS`) be evaluated.

## What Changes

- Repair the two backend test hosts (`Catalog/CategoryApiHost`, `Inventory/InventoryApiHost`) so they register JWT bearer authentication with the same configuration keys and validation parameters as Dev 1's `Program.cs` (`Jwt:SecretKey`, `Jwt:Issuer`, `Jwt:Audience`), instead of the non-existent `AddStoreAuthentication` / `Jwt:SigningKey`.
- Drop the two startup assertions that expect fail-fast rejection of invalid JWT configuration, because production does not implement that behaviour; keep the token-level cases (missing, malformed, expired, wrong signature/issuer/audience, role).
- Require an authenticated session on `GET /api/categories/tree` (all four roles), matching `InventoryController` and the `category-workspace` loading requirement.
- Align `category-catalog` and `inventory-management` role wording with the roles the controllers actually enforce (Admin for category writes; Admin + WarehouseStaff for stock initialization and serial import; Admin + Manager + WarehouseStaff for stock delete/adjust). No server behaviour changes.
- Delete the unreachable legacy placeholder `Views/Inventory/InventoryPage.xaml` + `.xaml.cs`, the empty `ViewModels/Inventory/InventoryViewModel.cs` stub, and their DI registrations. The Inventory shell route already opens `CategoryManagementPage`.
- Delete the DEBUG-only `Features/Categories/Preview/CategoryPreviewApiClient.cs`, which no DI registration or launch switch can reach.
- Add headless tests for `SerialImeiViewModel` (paging, status filter, error state), the only Dev 3 view model without coverage.
- Keep the whole solution green: `dotnet build TechStore.sln`, backend unit tests (with PostgreSQL-dependent tests still opt-in via `TECHSTORE_TEST_CONNECTION`), and the existing 630 frontend headless tests.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `category-catalog`: category tree retrieval now requires an authenticated session; write-role wording is corrected to the Admin-only policy that is enforced today.
- `inventory-management`: stock initialization and serial batch import role wording is corrected to Admin + WarehouseStaff, matching the controller and the existing controller tests.

## Impact

- Tests: `tests/Backend.UnitTests/Catalog/CategoryApiHost.cs`, `tests/Backend.UnitTests/Catalog/CategoryAuthorizationTests.cs`, `tests/Backend.UnitTests/Inventory/InventoryApiHost.cs`; new `tests/Frontend.UnitTests/Features/Inventory/SerialImeiViewModelTests.cs`.
- Backend: `backend/Catalog/CategoriesController.cs` (authorize tree endpoint) — no schema, DTO, or service changes.
- Frontend: delete `frontend/TechStore.Client/Views/Inventory/` (2 files), `ViewModels/Inventory/InventoryViewModel.cs`, `Features/Categories/Preview/CategoryPreviewApiClient.cs`; remove 3 DI lines in `UiServiceCollectionExtensions.cs`.
- Specs: `openspec/specs/category-catalog/spec.md`, `openspec/specs/inventory-management/spec.md`.
- No database migration, no API contract shape change, no change to the Category/Inventory UI screens.
