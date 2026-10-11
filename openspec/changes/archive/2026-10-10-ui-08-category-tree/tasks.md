# Tasks

## 1. Category tree model and repository

- [x] 1.1 Add typed `CategoryNode` and category-tree snapshot models under `Models/Catalog` for identity, parent/children, description, read-only active state, product count, and data provenance; verify model tests preserve hierarchy and do not depend on product-list paging.
- [x] 1.2 Extend the existing `ICategoryRepository` with a full category-tree read while preserving `GetCategoriesAsync` for product forms; update fake and unavailable implementations and keep fake selection explicit through preview DI. Verify all existing category-option consumers and repository test doubles still compile.
- [x] 1.3 Add deterministic tree preview fixtures and selectable Populated, Empty, Delayed, and Error scenarios; keep fake counts synthetic and consistent, with cancellation honored. Add repository tests for nested hierarchy, preview provenance, empty/error/delay, cancellation, and unchanged flat category options; link the tests and required source files in `Frontend.UnitTests.csproj`.

## 2. Category tree state and behavior

- [x] 2.1 Implement `CategoryListViewModel` and row presentation state for independent initial load, loading/content/empty/error/retry, cancellation, and explicit preview labeling. Add headless tests proving category-tree load does not call `IProductRepository` and failure is not represented as empty data.
- [x] 2.2 Implement name search, active-state filtering, ancestor context for matching descendants, and expand/collapse of parent rows. Add headless tests for nested matching, empty filters, expansion state, and counts remaining sourced from `CategoryNode` rather than a product page.

## 3. Inventory tab frame and native views

- [x] 3.1 Implement `InventoryViewModel` with Danh mục selected by default and separate Tồn kho/Serial-IMEI placeholder states; verify selecting unfinished tabs makes no category, product, stock, or IMEI repository request and returning to Danh mục preserves its loaded snapshot.
- [x] 3.2 Build native `CategoryListView.xaml` for D16's search/filter toolbar and hierarchy rows with parent/root label, description, product count, and read-only active status; verify no create/edit/delete/status-toggle actions or invented stock/Serial counts are present.
- [x] 3.3 Build `InventoryPage.xaml` with the three accessible tabs, selected state, category view, and explicit placeholders for unfinished tabs; verify the category query starts only when the category tab is first activated, not from a ViewModel constructor or when an unfinished tab is selected.
- [x] 3.4 Add responsive vertical scrolling and keyboard focus/activation for tabs, filters, retry, and disclosure buttons; verify a usable narrow layout has no horizontal clipping and a screen reader can distinguish parent/child levels and expanded state.

## 4. Navigation, DI, and focused tests

- [x] 4.1 Route only `RouteId.Inventory` from `UnoFrameAdapter` to `InventoryPage`, register inventory/category-tree ViewModels in `App.xaml.cs`, and retain the existing explicit-preview versus unavailable repository policy; verify other unfinished routes still use the generic placeholder and all existing product create/edit category options still work.
- [x] 4.2 Add focused `InventoryViewModel` and `CategoryListViewModel` tests for tab selection, lazy loading, retry, filtering, expansion, status/count display, and no mutation. Run the targeted frontend test suite for the linked category sources.

## 5. Integrated verification and owner acceptance

- [x] 5.1 Run `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj` and `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`; launch the preview app and smoke-test the Inventory route, category tree, filters, and unfinished tab placeholders. Attempt the Windows target only in a compatible Windows environment and record unavailable checks in a UI-08 handoff.
- [x] 5.2 Owner visual/manual acceptance: after 5.1 passes, ask the owner to test the current and a narrower still-usable window; inspect all three tabs and placeholders; search/filter categories; expand/collapse parents; verify parent labels, descriptions, counts, read-only status, empty/error/retry scenarios, keyboard Tab/Enter/Space/Escape/Back, and no horizontal clipping or CRUD controls. Record dated confirmation and every unmet behavior in the handoff. Do not require exact scaling or screenshots and do not check this task until the owner confirms.
