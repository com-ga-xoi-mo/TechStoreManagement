# Evidence: orders-frontend

## 8.3a Live logic walkthrough (automated, real API and PostgreSQL)

Run on a freshly seeded database (`docker compose up -d`, `dotnet run --project backend/TechStore.Api.csproj --launch-profile http`):

```bash
TECHSTORE_API_BASE_URL=http://localhost:5176/ dotnet test tests/Frontend.IntegrationTests/TechStore.Client.IntegrationTests.csproj --filter "FullyQualifiedName~OrdersApi"
```

Result: 1 passed. The test drives the real `OrderCreateViewModel`, `OrderListViewModel`, `OrderRepository`, `OrderDeviceRepository` and `ProductRepository` through `ApiClient`; only navigation, dialogs and the detail presenter are recording doubles.

```text
draft ORD-202610-0003: Pending, server total 29.990.000 == estimate
sale ORD-202610-0004: Completed via VietQr, device 358912345678902 left InStock
race: ORD-202610-0005 Completed; ORD-202610-0006 refused -> "Line 1 (IP15PM-256-NT): Device is Sold; expected InStock."
ORD-202610-0007: 2 x 7.990.000 - 100.000 = 15.880.000 (server == estimate)
stock error on line 0: "Insufficient available stock (7 available)."
checks: estimate==server total (serial and no-IMEI), VietQr, device leaves InStock, 409 race, 400 on line, detail IMEI, filters, 404
```

Database after the run (`psql` inside `techstore_postgres`):

| Check | Result |
|---|---|
| Orders | 0003 Pending (draft), 0004 Completed, 0005 Completed (race winner), 0006 Cancelled (race loser, cancelled from the list ViewModel), 0007 Completed (15.880.000) |
| `inventory_stocks.reserved_quantity` | 0 on every row |
| `inventory_movements` of type `Sale` | -1 (quantity_after 3), -1 (2), -2 (7) |
| `serial_imeis` | 3 Sold, 2 InStock |

The test needs at least 3 `InStock` iPhone devices, so it fails fast with a "reset the database" message on a second run against the same data.

## Startup check

The Debug desktop client starts without an unhandled exception. `ShellRoutePageFactory.VerifyAllRoutesResolve` (run at Debug startup) constructs every Shell page, including the new `OrderListPage`, and now also the `OrderCreate` route, so the dependency registrations and the C# Markup trees build. The only error line in the client log is the existing Uno dev-server endpoint warning.

## Seen in the running client (one pass, list page only)

With a dev session and the seeded API, the Debug client opened Đơn hàng and showed the 7 real orders: code and local time, customer and "payment · lines", right-aligned VND totals, coloured status badges, row buttons (Xem, plus Xác nhận TT / Hoàn tất / Hủy on the Pending order), the status/sort/page-size ComboBoxes at their defaults, and "7 đơn · trang 1/1" with Prev/Next disabled. That pass also led to a layout fix (columns reduced to code, customer, total, status, actions so nothing wraps).

## Not seen on screen (task 8.3b)

The create page, the detail dialog, pressing the row buttons, filtering from the UI, and Sáng/Tối switching were not looked at. Their logic is covered by the unit tests and the live test above; binding paths in C# Markup are not compile-checked, so each still needs one look. The page was only checked at the default 1280x800 window; the list needs about 1000 px of width, below that the toolbar or the action column clips.

## Unit tests

- Client: 439 passed (`dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`).
- Backend: 263 passed (`dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj`), unchanged by this change.
