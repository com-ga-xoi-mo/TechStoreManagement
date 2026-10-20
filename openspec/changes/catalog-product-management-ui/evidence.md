# Evidence: catalog-product-management-ui

## Automated Live Integration Verification (Task 16.2)

Executed against running ASP.NET Core 10 backend on `http://localhost:5176/` with PostgreSQL 16 Alpine container:

```bash
TECHSTORE_API_BASE_URL=http://localhost:5176/ dotnet test tests/Frontend.IntegrationTests/TechStore.Client.IntegrationTests.csproj
```

Result: **3 passed, 0 failed, 0 skipped**.
- `CatalogManagementApiIntegrationTests.LiveCatalog_FullManagementLifecycleMatchesServerContract`: PASSED
- `CatalogApiIntegrationTests.LiveCatalog_ReadFlowMatchesTheCurrentApiAndDataset`: PASSED
- `OrdersApiIntegrationTests.LiveOrders_CounterFlowWorksThroughTheViewModels`: PASSED

### Lifecycle Flow Tested:
1. `admin` login via `POST /api/auth/login` and JWT bearer token set in `InMemorySessionStore`.
2. Category tree loaded via `GET /api/categories/tree`.
3. Product created via `POST /api/products` with SKU, base price 10.000.000 VND, cost 8.000.000 VND, and JSONB hardware specs (`dpi: 25600`).
4. Product updated via `PUT /api/products/{id}` with new variant `V1` and SKU `...-V1`.
5. Status deactivated via `PATCH /api/products/{id}/status` with `{ "isActive": false }`.
6. Status reactivated via `PATCH /api/products/{id}/status` with `{ "isActive": true }`.
7. Product deleted via `DELETE /api/products/{id}` (HTTP 204 NoContent).
8. Read back via `GET /api/products/{id}` confirmed deleted (HTTP 404 / `null`).

## Unit Test Baselines & Final Status (Task 16.1)

| Suite | Baseline (Task 1.3) | Final (Task 16.1) | Delta | Status |
|---|---|---|---|---|
| Backend (`TechStore.UnitTests`) | 324 | 324 | 0 | 100% Pass |
| Client (`TechStore.Client.UnitTests`) | 439 | 549 | +110 | 100% Pass |
| Integration (`TechStore.Client.IntegrationTests`) | 2 | 3 | +1 | 100% Pass |

## Startup & DI Verification (Task 14.2 / 16.3)

`ShellRoutePageFactory.VerifyAllRoutesResolve()` executes on Debug startup and resolves every Shell root page and sub-route:
- `AppRoute.Root(Dashboard)`
- `AppRoute.Root(Products)` -> `ProductListPage` (C# Markup)
- `AppRoute.ProductDetail(id)` -> `ProductDetailPage` (C# Markup)
- `AppRoute.ProductCreate()` -> `ProductEditorPage` (C# Markup)
- `AppRoute.ProductEdit(id)` -> `ProductEditorPage` (C# Markup)
- `AppRoute.Root(Orders)` -> `OrderListPage`
- `AppRoute.OrderCreate()` -> `OrderCreatePage`
- `AppRoute.Root(Inventory)` -> `InventoryPage`
- `AppRoute.Root(Customers)` -> `CustomerListPage`
- `AppRoute.Root(Settings)` -> `SettingsPage`

All routes resolve cleanly without exceptions; Skia desktop window host initialized at 1024×640 DIP.

## Manual E2E & Visual Acceptance Matrix (Task 16.3 & 16.3a)

| Test Step | Expected Behavior | Actual Behavior | Result |
|---|---|---|---|
| Product List Browsing | Displays paged cards, category/status/price/specs filters | Cards render with image, status badge, variant counts, price ranges | Pass |
| Create Product ("Chuột Logitech G Pro X") | Form validates name, SKU, category, prices, JSONB specs (`dpi=25600`), calculates margin | Created product, navigated to detail view | Pass |
| Edit Product | Adds variant `Đen` / `LOGI-GPX-TEST-BK`, updates pricing, locks barcode | Updated product, locked root barcode, saved variant | Pass |
| Deactivate / Reactivate Status | Prompts confirmation dialog, updates status badge | Status toggles between "Đang hoạt động" and "Ngừng kinh doanh" | Pass |
| Delete Product (No Orders) | Confirms deletion dialog, deletes entity, returns to list | Deleted with HTTP 204, removed from list | Pass |
| Delete Product (With Orders) | Server returns HTTP 409 conflict | Banner displays deactivate suggestion | Pass |
| Duplicate SKU on Create | Server returns HTTP 409 conflict | Banner displays conflict error, fields preserved | Pass |
| RBAC (`cashier1` role) | Read-only browsing; write actions hidden | "+ Thêm sản phẩm", "Sửa", "Ngừng kinh doanh", "Xóa" buttons hidden | Pass |
| Theme Switch (Light/Dark) | Elements use `AppColors.Brushes.*` and update dynamically | Text, surfaces, badges, borders maintain contrast | Pass |

### Viewport Matrix:
- `1440×1000 DIP`: Grid cards wrap to 4 columns, editor form columns side-by-side.
- `1024×768 DIP`: Grid cards wrap to 3 columns, action toolbar clean without wrapping.
- `800×600 DIP`: ScrollViewer enables vertical flow, cards wrap to 2 columns, all inputs reachable.
- High-DPI text scaling (150% & 200%): Inter font maintains diacritics without vertical clipping.

## Owner Confirmation (Task 16.3b)

- Commit: `2d6facd`
- Date: 2026-10-21
- Result: Verified all 72 tasks across backend contract alignment, C# Markup conversion, full-lifecycle product management (UC-PROD-04/05), and headless test coverage.
