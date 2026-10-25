# Proposal

## Why

The current “Danh mục và Kho” workspace renders the category tree but leaves the **Tồn kho** tab as a visual placeholder. Warehouse staff therefore cannot inspect stock balances, quantities, or low-stock state from the desktop client even though the backend already exposes the paged `/api/inventory/stocks` route. This change adds a real C# Markup inventory-stock workspace while keeping backend contract expansion (search/category/warehouse filters and richer stock metadata) explicitly outside this frontend-only change.

## What Changes

- Add a feature-first `InventoryStockViewModel` and `InventoryStockPage`/tab implemented entirely with Uno C# Markup; no XAML files.
- Add an `IInventoryApiClient`/client implementation that calls the existing authenticated `GET /api/inventory/stocks` endpoint and preserves `PagedResultDto<InventoryStockDto>` metadata, cancellation, and normalized transport errors.
- Render the inventory dashboard from `docs/designs/inventory-stock.html`: header actions, underline tabs, search/filter toolbar, seven-column stock table, status badges, device icon inference, and pagination footer.
- Bind page state for search text, category/status/warehouse selections, pagination, loading, empty, and error states; apply immediately observable filtering for fields available in the current DTO/API.
- Keep “Nhập kho mới” and row adjustment/serial/delete controls as command/event hooks only; do not implement stock-adjustment or serial detail forms in this change.
- Integrate the Tồn kho tab with the existing “Danh mục và Kho” navigation without changing the existing category tree behavior.
- Add headless frontend unit tests for ViewModel filtering/pagination/status presentation and API-client request/response/error behavior.
- Document backend prerequisites: the current contract has no search text, category, warehouse, or explicit stock-status fields, and `InventoryStockDto` has no warehouse/category metadata. Until a later backend change adds these fields, unsupported filters remain disabled or clearly marked unavailable rather than fabricating results.

## Capabilities

### New Capabilities

- `inventory-stock-workspace`: Authenticated, paginated desktop inventory-stock browsing with seven-column presentation, client state management, status presentation, and action hooks.

### Modified Capabilities

- `category-workspace`: Replace the current inventory-tab placeholder with a functional tab host that can activate the inventory-stock workspace while preserving category-tab layout, route ownership, and no-XAML constraints.

## Impact

- **Frontend client:** new Inventory feature page, ViewModel, API client/repository registration, C# Markup presentation helpers, and tab coordination in the category workspace.
- **Shared contracts:** consume existing `InventoryStockDto`, `InventoryStockFilterRequest`, and `PagedResultDto`; no backend DTO or request changes are authorized by this proposal.
- **Backend/API dependency:** relies on `/api/inventory/stocks` and the existing bearer-token transport. Search/category/warehouse filtering, complete category metadata, warehouse identity, and server-side status filtering require a future compatible backend contract; this change must not claim those live capabilities until available.
- **Tests:** `tests/Frontend.UnitTests` gains InventoryStockViewModel and InventoryApiClient coverage, including empty/error/cancellation and unsupported-filter behavior.
- **Navigation and registration:** update DI and existing Shell/category workspace composition only as needed to expose the new tab; do not add a seventh shell destination or introduce XAML.
