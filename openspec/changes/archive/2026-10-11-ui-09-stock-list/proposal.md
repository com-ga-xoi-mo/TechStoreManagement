# Proposal

## Why

UI-08 currently leaves the Tồn kho tab as a placeholder, so staff cannot browse stock balances. This change adds a preview-backed stock table that keeps physical, reserved, and available quantities distinct while leaving inventory mutations and real API behavior to their own changes.

## What Changes

- Implement the Tồn kho tab inside the existing Danh mục & Kho workspace without changing the Shell route or removing the category and Serial / IMEI tabs.
- Show product/variant identity, category, selling price, physical quantity, reserved quantity, available quantity, and a readable stock status in a paged table.
- Add combined search by product name/SKU, category filtering, and stock-status filtering; reset to the first page when criteria change and ignore obsolete query results.
- Add loading, populated, empty, error/retry, and clearly labelled preview states using a fake inventory repository selected through DI.
- Keep category options supplied by the existing category repository and preserve the category-tree state when switching tabs.
- Do not add table actions that open later detail/adjustment dialogs, delete controls, stock receipt, export, warehouse selection, or stock mutations in this change.
- Do not add an HTTP repository, API client, endpoint, JWT flow, backend code, or database change. Record the missing inventory list contract as a dependency for later integration.

## Capabilities

### New Capabilities
- `client-inventory-stock-list`: Preview-backed, read-only stock list with quantity distinctions, combined filters, paging, and query states.

### Modified Capabilities
- `client-category-tree`: Replace the unfinished Tồn kho placeholder with the stock list while retaining category behavior and the Serial / IMEI placeholder.

## Impact

- Frontend inventory Models, ViewModels, Views, Repositories, DI registration in `App.xaml.cs`, and headless frontend tests.
- Reuses `InventoryPage`, `InventoryViewModel`, and `ICategoryRepository.GetCategoriesAsync` for category filter options.
- The backend has an `InventoryStock` entity/configuration with `Quantity`, `ReservedQuantity`, and `MinStockAlert`, but no inventory controller/service or client response contract was found. Preview data must not be represented as live stock or server authorization.
- A later API integration must define balance row grain (product/variant), quantity and availability semantics, stock-status threshold behavior, price source, category and status filter support, paging/counts, permissions, and error/cancellation behavior. Multi-warehouse scope requires an additional contract and is excluded here.
