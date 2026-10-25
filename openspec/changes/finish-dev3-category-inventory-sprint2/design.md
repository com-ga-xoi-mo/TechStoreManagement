# Design

## Context

See proposal.md - Why. Current state that constrains the approach:

- Auth lives in `backend/Program.cs` (JWT bearer, configuration section `Jwt`, property `SecretKey`) plus `backend/Identity/Configurations/JwtOptions.cs`. No shared registration extension exists, and `Program.cs` has no startup validation of JWT settings.
- `tests/Backend.UnitTests/Catalog/CategoryApiHost.cs` and `tests/Backend.UnitTests/Inventory/InventoryApiHost.cs` call `AddStoreAuthentication(...)` and import `TechStore.Api.Identity.Authentication`; neither exists, and both write `Jwt:SigningKey`. Because the test project is one assembly, this blocks the entire backend suite and `dotnet build TechStore.sln`.
- `GET /api/categories/tree` has no `[Authorize]`; `InventoryController` is `[Authorize]` at class level.
- Enforced write roles today: category create/update/delete `Admin`; stock initialize and serial batch import `Admin,WarehouseStaff`; stock delete/adjust `Admin,Manager,WarehouseStaff`. `InventoryControllerTests` already asserts Manager and SalesStaff get 403 on initialize/import.
- `Views/Inventory/InventoryPage.xaml` + `.xaml.cs` and `ViewModels/Inventory/InventoryViewModel.cs` (empty `PageViewModel` stub) are still compiled and registered in DI, but `ShellRoutePageFactory` maps `ShellDestination.Inventory` to `CategoryManagementPage`.
- `Features/Categories/Preview/CategoryPreviewApiClient.cs` is `internal`, DEBUG-only, referenced only by itself; no DI registration or launch switch selects it.
- Frontend headless tests compile production view models by explicit glob; `SerialImeiViewModel` is included and compiles but has no test file.

## Goals / Non-Goals

**Goals:**
- One green `dotnet build TechStore.sln` and a runnable backend unit test suite on the merged tree.
- Auth contract of the two test hosts identical to Dev 1's production configuration, so a later auth refactor only changes one place on the test side.
- Category tree requires a session; specs match enforced roles.
- Dev 3's feature folders contain no unreachable code and no leftover XAML.

**Non-Goals:**
- No change to stock/category business rules, DTOs, transactions, or database schema.
- No restyle or rewrite of `CategoryManagementPage`, `InventoryStockPage`, `SerialImeiPage`, or their dialogs.
- No new integration-test project or live-PostgreSQL test wiring (`TECHSTORE_TEST_CONNECTION` stays opt-in).
- Not adding startup fail-fast validation of JWT configuration; that belongs to Feature 1 (Dev 1).

## Decisions

### D1. Test hosts mirror `Program.cs`, no new production abstraction
Register JWT bearer inside a test-only helper under `tests/Backend.UnitTests/` and call it from both hosts, binding `JwtOptions` (`SectionName = "Jwt"`, `SecretKey`, `Issuer`, `Audience`) and using the same `TokenValidationParameters` as `Program.cs` (validate issuer/audience/lifetime/signing key, `RequireHttpsMetadata = false`, `ClockSkew = TimeSpan.Zero`). Hosts keep their existing token builder, which signs with `UTF8(Base64(key))`, so `Jwt:SecretKey` is set to `Convert.ToBase64String(key)` — the same key material production would read.

- Why: the user's direction is to build on Dev 1's existing auth rather than invent a parallel one. Binding the real `JwtOptions` type keeps one section name and one key name.
- Alternative rejected: extract `AddStoreAuthentication` in `backend/Identity/`. It would touch `Program.cs` while Feature 1 is still landing, risks a merge conflict, and changes production startup semantics that no requirement asks for.
- One test-only helper rather than duplicating 12 lines twice: the helper is the single place to update if Dev 1 later extracts a shared extension (then it becomes a one-line call).

### D2. Delete the startup-invalid-configuration theory
`Startup_Should_Reject_Invalid_Jwt_Configuration` asserts an exception message ("Configure Jwt:Issuer...") for a validation that no production code performs. Remove those cases instead of implementing fail-fast auth validation.

- Why: a test must describe shipped behaviour. Adding fail-fast would change startup behaviour for every environment and belongs to Dev 1.
- Consequence: token-level cases (missing, malformed, expired, wrong signature, wrong issuer, wrong audience, wrong role) remain and are what actually protect the endpoints.

### D3. `[Authorize]` on the tree action, all four roles
Add `[Authorize]` to `CategoriesController.GetTree`. Any authenticated role keeps working, so `ProductListPage` and `InventoryStockViewModel` category option loading (both run as the signed-in user) is unaffected.

- Why: matches `InventoryController`, `category-workspace`'s authenticated-loading requirement, and avoids breaking non-Admin flows (Admin-only would 403 the product filter for Manager/SalesStaff/WarehouseStaff).
- Alternative rejected: place `[Authorize(Roles = Admin)]` on the whole controller — silently breaks catalog browsing for the other three roles.

### D4. Specs follow enforced roles; no role behaviour change
The two spec drifts are corrected in the delta rather than in code:
- `category-catalog`: tree returns all categories (hidden ones included, as the management workspace needs `Đã ẩn`) and requires a session; creation is Admin-only.
- `inventory-management`: initialization and batch import are Admin + WarehouseStaff.

- Why: controller attributes and `InventoryControllerTests`/`CategoryAuthorizationTests` already agree with each other; the specs are the outliers. Changing enforcement instead would be a product decision nobody asked for and would break the existing 403 assertions.
- Note: the category tree requirement now documents ACTUAL behaviour (hidden categories included). The previous text ("inactive excluded") was already stale; `CategoryRepository.GetActiveCategoriesAsync` returns every category and `CategoryServiceTests.GetTreeAsync_Should_Include_Inactive_Categories` pins it.

### D5. Delete unreachable code, keep the reachable equivalent
- Remove `Views/Inventory/InventoryPage.xaml`, `Views/Inventory/InventoryPage.xaml.cs`, `ViewModels/Inventory/InventoryViewModel.cs` and the two DI lines (`AddTransient<InventoryViewModel>`, `AddTransient<InventoryPage>`). `ShellRoutePageFactory` never resolves them, and the "Danh mục và Kho" destination is already `CategoryManagementPage`.
- Remove `Features/Categories/Preview/CategoryPreviewApiClient.cs`; it is unreachable (no registration, no `--category-preview` switch).

- Why: the Sprint 2 instruction explicitly requires deleting old XAML after conversion, and dead code keeps the XAML compiler and DI graph larger than the app needs.
- Risk control: grep for every symbol before deletion; if a later design-preview need appears, restoring from git is trivial.

### D6. Serial/IMEI view model tests describe server-side filtering
Add `tests/Frontend.UnitTests/Features/Inventory/SerialImeiViewModelTests.cs` with a hand-written fake `IInventoryApiClient` (same style as `InventoryStockViewModelTests.FakeApi`). Cover: page mapping and labels (`TotalLabel`, `FooterText`, `PageNumbers`), the request sent for search text (trimmed, empty → null) and status selection (`All` → `null`, others → matching `SerialImeiStatus`), pagination bounds (`GoToPage` clamps, previous/next `CanExecute`), 403 → permission message, 500/transport → retryable message with rows cleared, and a stale response not overwriting a newer load.

- Why: this is the only Dev 3 view model without headless coverage and it holds the enum-offset mapping `(int)filter - 1`, which a test pins to `SerialImeiStatus` order.

## Risks / Trade-offs

- [Dev 1 later extracts `AddStoreAuthentication`, host helper becomes duplicate] → the helper is one file; switching both hosts to the production extension is a two-line change.
- [Admin-only category writes are harsh for Manager] → documented in the delta as the enforced policy; relaxing it later is a spec + controller change with its own tests, not a silent edit.
- [Deleting the placeholder page removes a shell fallback if `CategoryManagementPage` fails to construct] → `ShellRoutePageFactory` already fails the route and the shell keeps the previous page (covered by `frontend-shell-navigation` "Unknown or unavailable destination"); nothing depended on the placeholder.
- [PostgreSQL-dependent tests still skip locally] → they remain opt-in via `TECHSTORE_TEST_CONNECTION`; the task list verifies the suite passes with and without it.
- [Whole-suite verification needs a database to be meaningful] → run `docker compose up -d` before the full run; a skip-only run must be reported as such, not as proof of green.

## Migration Plan

No database or contract migration. Merge order: land this on `feat/dev3-catalog-inventory-management`, verify `dotnet build TechStore.sln` + both test projects, then open the PR to `dev`. Rollback is a normal revert; deleting the placeholder files and one controller attribute has no data impact.

## Open Questions

- Should Manager be allowed to initialize stock and import serials? Deferred; the delta records today's Admin/WarehouseStaff policy, and changing it later is a separate spec change.
- Should startup reject an invalid JWT configuration? Deferred to Feature 1 (Dev 1), which owns auth registration.
