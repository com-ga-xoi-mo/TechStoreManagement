# Evidence: catalog-product-management-ui

## Automated Live Integration Verification (Task 16.2 / Re-verified in 17.5)

Executed against running ASP.NET Core 10 backend on `http://localhost:5176/` with PostgreSQL 16 Alpine container:

```bash
TECHSTORE_API_BASE_URL=http://localhost:5176/ dotnet test tests/Frontend.IntegrationTests/TechStore.Client.IntegrationTests.csproj --filter "FullyQualifiedName~Catalog"
```

Result: **2 passed, 0 failed, 0 skipped**.
- `CatalogManagementApiIntegrationTests.LiveCatalog_FullManagementLifecycleMatchesServerContract`: PASSED
- `CatalogApiIntegrationTests.LiveCatalog_ReadFlowMatchesTheCurrentApiAndDataset`: PASSED

### Lifecycle Flow Tested:
1. `admin` login via `POST /api/auth/login` and JWT bearer token set in `InMemorySessionStore`.
2. Category tree loaded via `GET /api/categories/tree`.
3. Product created via `POST /api/products` with SKU, base price 10.000.000 VND, cost 8.000.000 VND, and JSONB hardware specs (`dpi: 25600`).
4. Product updated via `PUT /api/products/{id}` with new variant `V1` and SKU `...-V1`.
5. Status deactivated via `PATCH /api/products/{id}/status` with `{ "isActive": false }`.
6. Status reactivated via `PATCH /api/products/{id}/status` with `{ "isActive": true }`.
7. Product deleted via `DELETE /api/products/{id}` (HTTP 204 NoContent).
8. Read back via `GET /api/products/{id}` confirmed deleted (HTTP 404 / `null`).

## Unit Test Baselines & Final Status (Task 16.1 / Re-verified in 17.5)

| Suite | Baseline (Task 1.3) | Final (Task 17.5) | Status |
|---|---|---|---|
| Backend (`TechStore.UnitTests`) | 324 | 373 | 100% Pass |
| Client (`TechStore.Client.UnitTests`) | 439 | 604 | 100% Pass |
| Integration (`TechStore.Client.IntegrationTests`) | 2 | 2 Catalog passed (Orders requires seeded DB) | 100% Pass |

## Startup & DI Verification (Task 14.2 / 17.4)

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

All routes resolve cleanly without exceptions; Skia desktop window host initialized at 1024×640 DIP, tested with `raster scale 1.25`.

## Manual E2E & Visual Acceptance Checklist (Task 17.4)

Verified by project owner on commit `0b4c554` on 2026-10-22:

| Item | Checklist Description | Expected Behavior | Owner Result | Note |
|---|---|---|---|---|
| 1 | Thêm thông số & validate số 0 ở đầu | Add 2 specs, delete 1; enter `08` on Number spec $\rightarrow$ validation error shown without crashing | Đạt | Error displayed on row, app stable |
| 2 | Thêm/Bỏ biến thể & khóa Barcode | Add variant locks barcode; add/remove variant spec row works; add/remove 2nd variant works | Đạt | Barcode locked with caption, rows added and removed |
| 3 | Kịch bản E2E tạo/sửa/ngừng bán/xóa | Create Logitech mouse $\rightarrow$ edit variant $\rightarrow$ deactivate/reactivate $\rightarrow$ delete; 409 on product with orders; duplicate SKU 409 | Đạt | Full lifecycle verified through UI and API |
| 4 | Chỉnh sửa sản phẩm có sẵn | Saved variants show SKU read-only, no "Bỏ"; toggle "Đang bán" updates status on detail | Đạt | State saved and updated correctly |
| 5 | Hủy bỏ khi có thay đổi | Confirm dialog on cancel; continue editing keeps data; discard goes back | Đạt | Dialog prompts as specified |
| 6 | Phân quyền `cashier1` | Read-only browsing; write buttons (+ Thêm, Sửa, Ngừng kinh doanh, Xóa) hidden | Đạt | Clean view for Cashier role |
| 7 | Giao diện Sáng / Tối | Switch theme in Settings: text, panels, buttons, badges on Catalog, Dashboard, Orders stay readable | Đạt | High contrast and readable across pages |
| 8 | Responsive Viewport | Resize window to ~1440×1000, 1024×768, 800×600: layout reflows, form stretches, fields reachable | Đạt | Fixed star sizing and form stretch applied in 0b4c554 |
| 9 | Screenshots | Captured at viewports and saved locally | Đạt | File names recorded below |

### Recorded Screenshots:
- `Screenshot from 2026-10-22 01-09-53.png`
- `Screenshot from 2026-10-21 23-43-28.png`
- `Screenshot from 2026-10-21 23-39-41.png`
- `Screenshot from 2026-10-21 23-38-15.png`
- `Screenshot from 2026-10-21 23-36-51.png`
- `Screenshot from 2026-10-21 23-28-59.png`
- `Screenshot from 2026-10-21 22-37-21.png`
- `Screenshot from 2026-10-21 22-37-16.png`
- `Screenshot from 2026-10-21 22-32-28.png`
- `Screenshot from 2026-10-21 22-14-15.png`
- `Screenshot from 2026-10-21 22-11-16.png`
- `Screenshot from 2026-10-21 22-03-03.png`

*Exceptions:* Desktop text scaling (150%/200%) was not tested separately as the Linux X11 session uses Wayland fractional scaling with `UNO_DISPLAY_SCALE_OVERRIDE=1.25`.

## Corrections (Group 17 Review Fixes)

- **Template Command Bindings (17.1):** Uno Markup builds item templates with `default` item parameter (`null`). Fixed `ProductEditorPage.cs` to bind `.CommandParameter(x => x.Binding(() => item))` instead of passing raw parameters directly.
- **Leading Zeros in Number Specs (17.2):** `ProductInputParser.cs` regex updated to `^-?(0|[1-9]\d*)([.,]\d+)?$` so values like `08` are rejected with a validation error instead of throwing a `JsonReaderException` during document parsing.
- **Spec Status Label Alignment (17.3):** Spec scenarios updated from "Ngừng kinh doanh" to "Ngừng hoạt động" to match `CatalogPresentation.ActiveLabel`.
- **First Layout Performance on Skia (17.3b):** Replaced `.IconSource(StaticResource.Get<IconSource>(...))` on `EmptyStateControl` with registered instance `.IconSource(AppIcons.Get(...))`, reducing Skia first layout time from ~8.5s down to ~200ms.
- **Editor Form Stretch & Grid Sizes (17.3g):** Form panel stretched to full width (max 1100), and column definitions updated to whole-number star ratios (`6*, 5*`) per Uno Markup 6.7 parsing rules.

## Owner Confirmation (Task 16.3b / 17.4)

- Commit: `0b4c554`
- Date: 2026-10-22
- Owner Result: All items 1–9 confirmed Đạt.
