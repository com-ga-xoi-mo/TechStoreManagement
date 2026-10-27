# Tasks

## 1. API boundary and registration

- [x] 1.1 Add the inventory stock API-client contract and implementation for `GET api/inventory/stocks`, forwarding the existing `InventoryStockFilterRequest`, bearer session, cancellation token, and `PagedResultDto<InventoryStockDto>` envelope; verify with a recording-transport unit test that route/query and response metadata are preserved.
- [x] 1.2 Register the inventory API client and feature services in the existing frontend DI composition without changing backend projects; verify `ShellRoutePageFactory.VerifyAllRoutesResolve` and the frontend service-registration tests still pass.
- [x] 1.3 Add API-client tests for empty pages, malformed/normalized HTTP errors, 401/403 session behavior, timeout/network failure, and caller cancellation; verify the complete API-client test class passes without a live API.

## 2. ViewModel and presentation rules

- [x] 2.1 Create `InventoryStockViewModel` with observable search/status/category/warehouse state, server page metadata, loading/empty/error state, cancellation-safe load commands, and deferred action hooks; verify headless tests cover initial load, refresh, stale-load suppression, and cancellation.
- [x] 2.2 Implement pure inventory presentation helpers for available-quantity status, row emphasis, Vietnamese quantity/currency formatting, unavailable markers, and device icon/tint inference; verify unit tests for `>5`, `1..5`, `0`, negative/invalid data, and unknown product families.
- [x] 2.3 Implement immediate local search and status filtering over the loaded page, reset visible pagination state on filter changes, and expose category/warehouse selectors as disabled/unavailable when the current DTO lacks those fields; verify tests prove no fabricated matches and correct filtered ranges.
- [x] 2.4 Add ViewModel tests for page-boundary commands, zero-row results, retry after error, and deferred stock/serial/adjust/delete hooks; verify the targeted frontend unit-test suite passes.

## 3. C# Markup inventory workspace

- [x] 3.1 Create the inventory stock page/tab entirely in C# Markup with the HTML-derived header, action buttons, underline tabs, filter toolbar, and loading/empty/error surfaces; verify no `.xaml` file is added and the page compiles for `net10.0-desktop`.
- [x] 3.2 Build the seven-column card table with product/SKU identity, inferred icon, category/unavailable marker, sale price/unavailable marker, available/reserved quantities, derived status badge, and deferred action buttons; verify visual review against `docs/designs/inventory-stock.html` and markup smoke tests for column order.
- [x] 3.3 Add deterministic pagination footer bindings and keyboard/focus/accessibility labels for filters, tabs, pagination, and actions; verify page navigation and disabled-boundary behavior through ViewModel/UI binding tests.

## 4. Category workspace tab integration

- [x] 4.1 Replace the category workspace’s visual-only Tồn kho tab with bound selected-tab state and host the inventory stock workspace while preserving the category tree and deferred Serial/IMEI tab; verify tab switching does not create a new Shell destination and category state remains intact.
- [x] 4.2 Preserve the existing category workspace design tokens, route ownership, and C# Markup-only constraint while sharing header/tab chrome; verify `ShellRoutePageFactory` route tests and category workspace tests continue to pass.
- [x] 4.3 Add an integration-level headless test that selects Tồn kho, exercises a stock refresh, returns to Danh mục, and confirms no duplicate loads or stale tab content; verify the targeted frontend test suite passes.

## 5. Verification and delivery

- [x] 5.1 Run `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj` and resolve all inventory/API/tab regressions; verify the complete frontend unit-test suite is green.
- [x] 5.2 Build `frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` with zero errors and zero warnings; verify no project or generated source introduces a new XAML file.
- [x] 5.3 Perform the documented visual and interaction review against `docs/designs/inventory-stock.html`, including loading, empty, error, unsupported-filter, status styling, pagination, keyboard focus, and tab-return behavior; record backend prerequisites separately from frontend verification.

## Workflow follow-up

- Review the completed implementation and test evidence before running `/opsx-archive`.
- Coordinate a future backend/shared-contract change for search text, category identity, warehouse identity, price, and server-side stock-status filtering before enabling those selectors.

