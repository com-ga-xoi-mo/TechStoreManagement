# Tasks

## 1. Repair the backend test hosts so the solution builds

- [x] 1.1 Add a test-only JWT helper in `tests/Backend.UnitTests/` that binds `JwtOptions` from the `Jwt` section and registers the same JwtBearer `TokenValidationParameters` as `backend/Program.cs` (`RequireHttpsMetadata = false`, `SaveToken = true`, validate signing key/issuer/audience/lifetime, `ClockSkew = TimeSpan.Zero`); verify it compiles with `dotnet build tests/Backend.UnitTests/TechStore.UnitTests.csproj`
- [x] 1.2 Switch `tests/Backend.UnitTests/Catalog/CategoryApiHost.cs` to the helper, remove `using TechStore.Api.Identity.Authentication` and `AddStoreAuthentication`, and set `Jwt:SecretKey` (instead of `Jwt:SigningKey`) to the same base64 key material the token builder signs with; verify `CategoryAuthorizationTests` runs and its token-kind cases pass
- [x] 1.3 Switch `tests/Backend.UnitTests/Inventory/InventoryApiHost.cs` the same way and verify `InventoryControllerTests` passes (401 without token, 403 for Manager/SalesStaff writes, 201 for Admin/WarehouseStaff)
- [x] 1.4 Delete the `Startup_Should_Reject_Invalid_Jwt_Configuration` theory and its `invalidSetting`/`invalidValue` plumbing from `CategoryApiHost` and `CategoryAuthorizationTests`, because production performs no startup JWT validation; verify the remaining auth theory covers missing/malformed/expired/wrong-signature/wrong-issuer/wrong-audience tokens
- [x] 1.5 Run `dotnet build TechStore.sln` and confirm 0 errors, then run `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj` and confirm every test passes or is reported as skipped for a missing `TECHSTORE_TEST_CONNECTION`

## 2. Require a session on the category tree

- [x] 2.1 Add `[Authorize]` to `CategoriesController.GetTree` (all authenticated roles; leave the Admin role attributes on create/update/delete untouched) and verify the endpoint still returns the full tree for an Admin token
- [x] 2.2 Extend `tests/Backend.UnitTests/Catalog/CategoryAuthorizationTests.cs` with a theory asserting `GET /api/categories/tree` returns 401 for missing/malformed/expired tokens and 200 for Admin, Manager, SalesStaff, and WarehouseStaff; verify the new theory passes
- [x] 2.3 Confirm the client still loads category options with the session token by checking `ProductListViewModel` and `InventoryStockViewModel` category loading paths send an authenticated request (Bearer handler), and note the result in the task PR description

## 3. Remove Dev 3's unreachable frontend code

- [x] 3.1 Delete `frontend/TechStore.Client/Views/Inventory/InventoryPage.xaml`, `InventoryPage.xaml.cs`, `frontend/TechStore.Client/ViewModels/Inventory/InventoryViewModel.cs`, and the `AddTransient<InventoryViewModel>` / `AddTransient<InventoryPage>` registrations in `UiServiceCollectionExtensions.cs`; verify `grep -rn "InventoryPage\|InventoryViewModel" frontend tests` returns no matches
- [x] 3.2 Delete `frontend/TechStore.Client/Features/Categories/Preview/CategoryPreviewApiClient.cs`; verify `grep -rn "CategoryPreviewApiClient\|category-preview" frontend tests` returns no matches
- [x] 3.3 Build the desktop target (`dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`) and confirm 0 errors, then run the app and open "Danh mục và Kho" to confirm the three tabs still load through the live API

## 4. Cover the Serial/IMEI view model

- [x] 4.1 Add `tests/Frontend.UnitTests/Features/Inventory/SerialImeiViewModelTests.cs` with a hand-written fake `IInventoryApiClient`, asserting page data, `TotalLabel`, `FooterText`, and `PageNumbers`; verify `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj --filter "FullyQualifiedName~SerialImeiViewModelTests"` passes
- [x] 4.2 Add cases for the filter contract: trimmed search text or `null` when blank, `All` → `null` status, each other option → matching `SerialImeiStatus`, page reset to 1 on filter change; verify they pass
- [x] 4.3 Add cases for pagination bounds (`GoToPage` clamps to 1..TotalPages, previous/next `CanExecute`) and for failures (403 → permission message, other errors → retryable message with cleared rows, stale response does not overwrite a newer load); verify they pass

## 5. Integration verification

- [x] 5.1 Start PostgreSQL (`docker compose up -d`), apply migrations, then run `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj` with `TECHSTORE_TEST_CONNECTION` set so `CategoryPostgresTests` execute instead of skipping, and confirm the run is green with zero skipped database tests
- [x] 5.2 Run `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` and confirm the whole suite (previous 630 tests plus the new Serial/IMEI cases) passes
- [x] 5.3 Run `openspec validate "finish-dev3-category-inventory-sprint2" --strict` and confirm the change validates with no errors
- [x] 5.4 Manually exercise the Dev 3 flow on the desktop client (create category → initialize stock → import 15-digit IMEIs → verify stock quantity and `InStock` rows), then switch theme Sáng/Tối and confirm the three tabs stay readable
