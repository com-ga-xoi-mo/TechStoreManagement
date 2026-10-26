# Evidence: catalog-inventory-alignment

## 0. Baseline (Task 0.1)

- **Branch:** `feat/dev3-catalog-inventory-management` (base commit `594320d`)
- **Build (`dotnet build TechStore.sln`):** 0 Warning(s), 0 Error(s)
- **Backend Unit Tests (`TB`):** Passed: 508, Skipped: 8, Failed: 0, Total: 516
- **Frontend Unit Tests (`TF`):** Passed: 882, Skipped: 0, Failed: 0, Total: 882

## 2. Category Projection SQL Verification (Task 2.5)

- **Command 1:** `TECHSTORE_TEST_CONNECTION="Host=localhost;Port=5432;Database=techstore_db;Username=techstore_user;Password=TechStorePassword123!" dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~CategoryPostgresTests"` -> Passed: 8/8.
- **Command 2:** `dotnet run --project backend/TechStore.Api.csproj -- --urls http://localhost:5179` + `GET /api/categories/tree` (with `admin` token) -> HTTP 200 OK:
  - `Accessories`: `productCount=9`, `skuCount=9`
  - `Laptops`: `productCount=2`, `skuCount=2`
  - `Monitors`: `productCount=3`, `skuCount=3`
  - `Smartphones`: `productCount=3`, `skuCount=3`
  - `Tablets`: `productCount=3`, `skuCount=3`

## 8. Runtime Theme Verification Screenshots (Task 8.7)

Verified live against `http://localhost:5179/` with `XDG_DATA_HOME=/tmp/opencode/xdg_data` on `net10.0-desktop`, toggling `ThemePreference.Light` and `ThemePreference.Dark` across all 3 workspace tabs and all 7 dialogs. Screenshots saved in `openspec/changes/catalog-inventory-alignment/evidence/`:

- `evidence/01-categories-light.png`: Categories tab (Light theme)
- `evidence/02-categories-dark.png`: Categories tab (Dark theme)
- `evidence/03-inventory-light.png`: Inventory Stock tab (Light theme)
- `evidence/04-inventory-dark.png`: Inventory Stock tab (Dark theme)
- `evidence/05-serials-light.png`: Serial / IMEI tab (Light theme)
- `evidence/06-serials-dark.png`: Serial / IMEI tab (Dark theme)
- `evidence/07-dialog-create-category-dark.png`: Create Category dialog (Dark theme)
- `evidence/08-dialog-edit-category-dark.png`: Edit Category dialog (Dark theme)
- `evidence/09-dialog-delete-category-dark.png`: Delete Category dialog (Dark theme)
- `evidence/10-dialog-import-stock-dark.png`: Import Stock dialog (Dark theme)
- `evidence/11-dialog-adjust-stock-dark.png`: Stock Adjustment dialog (Dark theme)
- `evidence/12-dialog-stock-detail-dark.png`: Stock Detail & History dialog (Dark theme)
- `evidence/13-dialog-delete-stock-dark.png`: Delete Stock Item dialog (Dark theme)

## 9. Folder Structure & Collision Check (Task 9.1)

Command: `git ls-files frontend/TechStore.Client/Features tests/Frontend.UnitTests/Features`

Target mapping verified against D8 with zero filename collisions:
- `Views/Inventory/`: `CategoryManagementPage.cs`, `CategoryTableRows.cs`, `WorkspaceParts.cs` (renamed from `CategoryWorkspaceStyles.cs`), `InventoryStockPage.cs`, `SerialImeiPage.cs`
- `Views/Inventory/Dialogs/`: `CreateCategoryDialog.cs`, `DeleteCategoryDialog.cs`, `EditCategoryDialog.cs`, `DeleteStockItemDialog.cs`, `ImportStockDialog.cs`, `StockAdjustmentDialog.cs`, `StockDetailHistoryDialog.cs`
- `ViewModels/Inventory/`: `CategoryManagementViewModel.cs`, `CreateCategoryDialogViewModel.cs`, `DeleteCategoryDialogViewModel.cs`, `EditCategoryDialogViewModel.cs`, `DeleteStockItemDialogViewModel.cs`, `ImportStockDialogViewModel.cs`, `InventoryStockViewModel.cs`, `SerialImeiViewModel.cs`, `StockAdjustmentDialogViewModel.cs`, `StockDetailHistoryDialogViewModel.cs`
- `Models/Inventory/`: `CategoryIconResolver.cs`, `CategoryRow.cs`, `PageButtonItem.cs`, `InventoryStockModels.cs`
- `Services/Inventory/`: `CategoryDeleteCoordinator.cs`, `ICategoryDialogRequests.cs`, `InventoryReportExporter.cs`
- `tests/Frontend.UnitTests/ViewModels/Inventory/`: `CategoryWorkspaceTests.cs`, `CreateCategoryDialogTests.cs`, `DeleteCategoryDialogTests.cs`, `EditCategoryDialogTests.cs`, `DeleteStockItemDialogViewModelTests.cs`, `ImportStockDialogViewModelTests.cs`, `InventoryReportBuilderTests.cs`, `InventoryStockViewModelTests.cs`, `SerialImeiViewModelTests.cs`, `StockAdjustmentDialogViewModelTests.cs`

## 10.1 Live API Verification on Port 5179 (Task 10.1)

Executed against `dotnet run --project backend/TechStore.Api.csproj -- --urls http://localhost:5179`:

- **(a) Hidden category rules on `POST` / `PUT /api/products`:**
  - `POST /api/products` with hidden `categoryId` (`e25927a0-eae0-6f64-2566-fe4bf4c69833`) -> `400 Bad Request` `{"categoryId": ["Category 'e25927a0-eae0-6f64-2566-fe4bf4c69833' does not exist or is inactive."]}`
  - `PUT /api/products/{id}` for product already in hidden category `e25927a0-eae0-6f64-2566-fe4bf4c69833`, keeping `categoryId` and updating `basePrice` -> `200 OK` (`basePrice: 10991000.0`, then restored)
  - `PUT /api/products/{id}` moving a product from `Tablets` into hidden category `e25927a0-eae0-6f64-2566-fe4bf4c69833` -> `400 Bad Request` `{"categoryId": ["Category 'e25927a0-eae0-6f64-2566-fe4bf4c69833' does not exist or is inactive."]}`
- **(b) Category ID filter on inventory stocks:**
  - `GET /api/inventory/stocks?categoryId=e25927a0-eae0-6f64-2566-fe4bf4c69833` -> `200 OK`, `totalCount=3`, all 3 returned items have `categoryId="e25927a0-eae0-6f64-2566-fe4bf4c69833"`.
- **(c) Escaped `%` in inventory stock search:**
  - `GET /api/inventory/stocks?search=%25` -> `200 OK`, `totalCount=0` (does not match all rows).
- **(d) Category tree product & SKU counts:**
  - `GET /api/categories/tree` -> `200 OK`:
    - `Accessories`: `productCount=9`, `skuCount=9`
    - `Laptops`: `productCount=2`, `skuCount=2`
    - `Monitors`: `productCount=3`, `skuCount=3`
    - `Smartphones`: `productCount=3`, `skuCount=3`
    - `Tablets`: `productCount=3`, `skuCount=3`
- **(e) Stock deletion RBAC (`DELETE /api/inventory/stocks/{id}`):**
  - No `Manager` user is seeded in `DemoSeedRepository` and there is no user creation API; skipped live Manager login per Task 10.1(e) instructions and verified via `InventoryControllerTests.DeleteStock_AuthorizeAttribute_ExcludesManager` (Task 3.6). Additionally verified non-privileged login (`cashier1`) receives `403 Forbidden` on `DELETE /api/inventory/stocks/8eb44299-0654-59aa-c31e-1f271245fff7`.
- **(f) Disjoint stock status filter counts:**
  - `GET /api/inventory/stocks?page=1&pageSize=1` -> `totalCount=15`
  - `GET /api/inventory/stocks?inStock=true&page=1&pageSize=1` -> `totalCount=8`
  - `GET /api/inventory/stocks?lowStock=true&page=1&pageSize=1` -> `totalCount=2`
  - `GET /api/inventory/stocks?outOfStock=true&page=1&pageSize=1` -> `totalCount=5`
  - Sum: `8 + 2 + 5 = 15 == totalCount`.

## 10.2 Live Desktop Client Verification (Task 10.2)

Executed with `XDG_DATA_HOME=/tmp/opencode/xdg_data TECHSTORE_API_BASE_URL=http://localhost:5179/ dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` while `Monitors` and `Tablets` were temporarily set to `isActive=false` (then restored):

1. **Create Product (`AppRoute.ProductCreate()`):**
   - `ProductEditorViewModel.Categories` -> `[Accessories, Laptops, Smartphones]` (neither hidden category `Monitors` nor `Tablets` is shown).
2. **Edit Product in Hidden Category (`AppRoute.ProductEdit(monitorProdId)`):**
   - `ProductEditorViewModel.Categories` -> `[Accessories, Laptops, Monitors (đang ẩn), Smartphones]` (includes the product's current hidden category with `(đang ẩn)`, and excludes the other hidden category `Tablets`).
   - `CategoryHint` -> `"Danh mục này đang bị ẩn. Sản phẩm vẫn lưu được, nhưng không thể chuyển sản phẩm khác vào danh mục này."`
   - Updated `BasePriceText` to `10992000` and executed `SaveCommand`: saved `200 OK`, navigated to `ProductDetailPage` with `Price=10992000.00`, `CategoryId=e25927a0-eae0-6f64-2566-fe4bf4c69833` (`Monitors`), then restored original price.
3. **Product List Filter (`ShellDestination.Products`):**
   - `ProductListViewModel.Categories` -> `[Accessories, Laptops, Monitors (đang ẩn), Smartphones, Tablets (đang ẩn)]` (hidden categories carry `(đang ẩn)` suffix).
4. **Inventory Stock Tab Category Filter (`ShellDestination.Inventory` -> `Tồn kho`):**
   - Selecting `SelectedCategoryId = e25927a0-eae0-6f64-2566-fe4bf4c69833` (`Monitors`) filtered `VisibleItems` to `3` items, all in category `Monitors`.

## 10.3 OpenSpec Validation (Task 10.3)

- `openspec validate catalog-inventory-alignment` -> `Change 'catalog-inventory-alignment' is valid`
- `openspec validate --specs` -> `Totals: 30 passed, 0 failed (30 items)`

## 12.8 Live Save Dialog & Report Export Verification (Task 12.8)

Executed against `dotnet run --project backend/TechStore.Api.csproj -- --urls http://localhost:5179` and `TechStore.Client` (`net10.0-desktop`) with `XDG_DATA_HOME=/tmp/opencode/xdg_12_8` and `org.freedesktop.portal.FileChooser` (version 3):

- **(a) Export all unfiltered stocks:**
  - Portal `SaveFile` opened with suggested name `bao-cao-ton-kho-20261026-....csv`; saved to `/tmp/opencode/home_12_8/export-all.csv`.
  - Toast: `ToastKind=Success`, `ToastTitle="Thành công"`, `Message="Đã xuất 15 dòng tồn kho vào /tmp/opencode/home_12_8/export-all.csv"`.
  - File verified: UTF-8 BOM (`EF BB BF`), 16 lines (`1` header `"Sản phẩm","SKU","Danh mục","Giá bán","Có sẵn","Đang giữ","Trạng thái"` + `15` data rows, Vietnamese diacritics intact).
- **(b) Filter by "Hết hàng" (`OutOfStock`) and export:**
  - Saved to `/tmp/opencode/home_12_8/export-out.csv`.
  - Toast: `ToastKind=Success`, `ToastTitle="Thành công"`, `Message="Đã xuất 5 dòng tồn kho vào /tmp/opencode/home_12_8/export-out.csv"`.
  - File verified: UTF-8 BOM (`EF BB BF`), 6 lines (`1` header + `5` out-of-stock rows).
- **(c) Cancel the save dialog (`Response = 1`):**
  - `IsSuccessToastVisible=False` (no toast shown, no new file created).
- **(d) Stop backend on 5179 and click "Xuất báo cáo":**
  - `HasError=False`, `ErrorMessage=null`, `VisibleItems.Count=5` (stock table remains visible), `IsSuccessToastVisible=True`, `ToastKind=Error`, `ToastTitle="Không thành công"`, `Message="Không thể xuất báo cáo tồn kho. Vui lòng thử lại."`.

## 13.4 Live Row Actions Menu & Scroll Performance Verification (Task 13.4)

Built Release (`dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop -c Release`) and executed against `http://localhost:5179`:

- **(a) Inventory stock row layout & tooltip:**
  - Product cell shows product name and SKU without a decorative icon box; category cell renders plain secondary text (`Monitors`, `Accessories`, `Tablets`); status badge retains its semantic color; action cell has one right-aligned `"•••"` button with `ToolTip` and `AutomationProperties.Name` = `"Thao tác cho ASUS ProArt PA278CV"`.
- **(b) Inventory row `"•••"` `MenuFlyout` items & dialog dispatch:**
  - Flyout opens below the button (`BottomEdgeAlignedRight`) with `"Xem chi tiết"`, `"Điều chỉnh tồn"`, and `"Xóa tồn"` (in `DangerText` red) in Inter font.
  - Invoking each item dispatches `ViewSerialsCommand` (`StockDetailHistoryDialog`), `AdjustStockCommand` (`StockAdjustmentDialog`), and `DeleteStockCommand` (`DeleteStockItemDialog`) for the exact row (`ASUS ProArt PA278CV`); cancelling delete leaves the table unchanged.
- **(c) Keyboard navigation:**
  - `Focus(FocusState.Keyboard)` focuses the `"•••"` button (`Focused=True`), opening and closing the `MenuFlyout` without mutating state.
- **(d) Release scroll benchmark:**
  - Continuous 16 ms `ScrollViewer.ChangeView` loop across 60 steps measured via `CompositionTarget.Rendering`: `61 frames in 1.01s` (**60.5 FPS**).
- **(e) Categories table `"•••"` `MenuFlyout`:**
  - Each category row has `"•••"` (`Tooltip="Thao tác cho Accessories"`) with `"Sửa danh mục"` and `"Xóa danh mục"` (`DangerText`), while expand chevrons, root icon boxes, `"Danh mục gốc"` badges, status switches, and row hover backgrounds remain intact.
- **(f) Theme switching (Light ↔ Dark) with `MenuFlyout` open:**
  - Verified in both Light and Dark themes after live theme toggle:
    - `evidence/14-inventory-menu-light.png`: Inventory tab `"•••"` menu open in Light theme
    - `evidence/15-inventory-menu-dark.png`: Inventory tab `"•••"` menu open in Dark theme
    - `evidence/16-categories-menu-dark.png`: Categories tab `"•••"` menu open in Dark theme

## 10.4 Final Summary & Hand-off Notes (Task 10.4, 12.9 & 13.6)

### Test Counts (Before vs. After)
| Suite | Baseline (Task 0.1) | Final (Task 13.6) |
|:--|:--|:--|
| Backend Unit Tests (`TechStore.UnitTests`) | 508 passed, 8 skipped (516 total; 516/516 with Postgres) | **515 passed, 9 skipped (524 total; 524/524 with Postgres)** |
| Frontend Unit Tests (`TechStore.Client.UnitTests`) | 882 passed, 0 skipped | **900 passed, 0 skipped** |

### Commit Log (`594320d..HEAD`)
- `56bcf2b` `docs(openspec): record catalog-inventory-alignment change artifacts`
- `a37ce32` `fix(catalog): keep hidden categories out of new products`
- `763119a` `refactor(catalog): project category counts and use TimeProvider`
- `761e0c2` `refactor(inventory): translate check violations, move serial locks, tighten stock rules`
- `aa2fbed` `feat(inventory): filter stocks by category id`
- `218d6ec` `refactor(client): route category workspace through IApiClient`
- `4ad7e16` `refactor(client): rename inventory api client to repository`
- `225238e` `refactor(inventory): page view model and bindings for category workspace`
- `53c79d2` `fix(inventory): follow app theme tokens in category and stock UI`
- `49df92e` `refactor(client): move category and inventory UI into team folders`
- `61aac9e` `docs(openspec): record integration verification evidence for catalog-inventory-alignment`
- `61f3fb0` `fix(inventory): filter stocks on the server and tidy category workspace`
- `2666e06` `feat(inventory): export filtered stock report through save dialog`
- `HEAD` `perf(inventory): keep stock and category rows light while scrolling`

### Note for Dev 6 (Dashboard vs. Inventory Low-Stock Definition)
- **Dashboard (`AnalyticsRepository.cs:40`)** still counts "sắp hết" using `Quantity <= MinStockAlert` (which includes out-of-stock rows and does not subtract `ReservedQuantity`), representing "items that need replenishment".
- **Inventory (`InventoryStockRepository` & `InventoryStockDto.IsLowStock`, per D11)** defines the 3 stock states as mutually exclusive based on `AvailableQuantity = Quantity - ReservedQuantity`:
  - `OutOfStock`: `AvailableQuantity <= 0`
  - `LowStock`: `0 < AvailableQuantity <= MinStockAlert`
  - `InStock`: `AvailableQuantity > MinStockAlert`
- Please coordinate with Dev 6 if Dashboard should keep its replenishment definition or align with D11.
