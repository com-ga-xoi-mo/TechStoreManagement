# UI-09 Stock List Handoff

## Delivered

- The existing Danh mục & Kho workspace now hosts a read-only Tồn kho tab with product/variant, category, selling price, SKU, physical quantity, reserved quantity, available quantity, and stock status.
- Available quantity is `physical - reserved`. Preview balances satisfy `physical >= 0` and `0 <= reserved <= physical`.
- Search by product name/product SKU/variant SKU, category and status filters combine before paging, and each page contains eight rows.
- Loading, populated, empty, error/retry, and preview provenance are represented in the ViewModel and native XAML view.
- The fake inventory repository is registered only for explicit preview mode (`TECHSTORE_AUTH_MODE=preview` in Debug). Other modes use an unavailable repository and show a message that the API is not configured and no real stock data is available.
- The existing category repository supplies filter choices. The category-tree tab remains the default and keeps its loaded snapshot when switching tabs. Serial / IMEI remains unfinished.
- No inventory HTTP client, endpoint, backend behavior, database change, stock mutation, import/export, row action, or warehouse selector was added.

## Verification

- `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj --no-restore` — passed, 177 tests.
- `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore` — passed, 0 warnings and 0 errors.
- `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-windows10.0.26100 --no-restore` — unavailable in this Linux environment; Uno reports `UNOB0014` because Windows target builds are unsupported on non-Windows hosts.
- Preview desktop app launched and remained running. Uno emitted RemoteControl/HotDesign connection and X11 EWMH warnings already seen in this environment. The shell session has no window/keyboard automation tool, so filters, keyboard navigation, and narrow-window behavior have not been verified here.
- To launch the preview on Linux/macOS desktop: `TECHSTORE_AUTH_MODE=preview dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`. Optional repository scenarios: set `TECHSTORE_STOCK_LIST_PREVIEW=Empty` or `Error` before launch to inspect empty/error feedback.
- Owner manual acceptance: **confirmed 2026-10-11**. The owner reported all checks passed; no unmet behavior was reported. No screenshot or exact-size comparison was required.

## Dependency for real inventory integration

The current frontend has no inventory list response contract or inventory query endpoint. A separate integration change must agree on:

- Balance row grain (product and optional variant) and stable identifiers.
- The definitions and invariants for physical, reserved, and available quantities.
- Low-stock and out-of-stock thresholds, including whether the server returns a status or the client derives it.
- Which selling-price field the list returns; cost price remains outside this UI.
- Search/category/status filtering, pagination and total-count semantics, sorting if later required, and cancellation/error behavior.
- Authentication, permissions, and which roles may view the stock list.
- Whether balances are per warehouse; multi-warehouse selection remains out of UI-09 and requires a contract.

Until that contract exists, all rows described in this handoff are synthetic preview data and do not represent persisted stock, server authorization, or successful inventory operations.
