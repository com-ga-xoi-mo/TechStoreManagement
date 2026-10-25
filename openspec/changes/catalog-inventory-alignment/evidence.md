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
