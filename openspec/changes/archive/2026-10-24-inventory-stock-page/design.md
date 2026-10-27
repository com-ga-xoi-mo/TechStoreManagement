# Design

## Context

See `proposal.md` for motivation and user-visible scope. The repository currently has an empty `InventoryViewModel`, a legacy placeholder `InventoryPage.xaml`, and routes `ShellDestination.Inventory` to `CategoryManagementPage`. The backend route `/api/inventory/stocks` already returns `PagedResultDto<InventoryStockDto>` through the shared HTTP transport, but `InventoryStockFilterRequest` only supports product/variant IDs, `lowStock`, page, and page size. `InventoryStockDto` has no category or warehouse identity. The implementation must therefore remain frontend-only and must not imply server filtering that the current contract cannot perform.

## Goals / Non-Goals

**Goals:**

- Add a testable inventory-stock presentation model and C# Markup page/tab that can be hosted by the existing “Danh mục và Kho” workspace.
- Reuse `IApiClient`, bearer-session handling, normalized API errors, cancellation, `PagedResultDto`, and existing design-token/presentation conventions.
- Keep quantity semantics explicit: display `Quantity`, `ReservedQuantity`, and `AvailableQuantity`; derive status and row emphasis from available quantity.
- Preserve the existing category route and make tab selection deterministic without adding another Shell menu item.
- Make API and UI limitations visible so later backend contract work can extend the feature without changing the presentation model shape.

**Non-Goals:**

- No backend, shared DTO, database, RBAC, or migration changes.
- No stock initialization, adjustment, serial/IMEI detail, deletion, export, or warehouse-management form implementation.
- No fabricated category, warehouse, price, product image, or counts absent from the API response.
- No new XAML files and no direct database access from the client.

## Decisions

### 1. Feature-first client boundary

Create `IInventoryApiClient` and an implementation under the frontend API/repository layer. It accepts the existing `InventoryStockFilterRequest`, calls the relative `api/inventory/stocks` route through `IApiClient`, and returns the paged DTO envelope. The ViewModel depends on this interface plus the existing session/activation lifecycle, never on `HttpClient`, controls, or database types.

**Alternative considered:** Reusing the empty `InventoryViewModel` and calling `IApiClient` from the page. Rejected because it violates the repository/API-client boundary and makes headless tests and token/error behavior inconsistent.

### 2. Local filtering over the loaded page

The ViewModel keeps the server page as the authoritative source, then derives a visible collection from search text and available-quantity status. Search matches product/variant name and SKU case-insensitively after trimming. “Còn hàng”, “Sắp hết hàng”, and “Hết hàng” are derived from `AvailableQuantity`; changing either input resets the displayed page index to one without issuing a write or pretending the server filtered fields it does not understand.

Category and warehouse selectors are rendered as disabled/unavailable until a later API contract supplies category/warehouse keys. The category selector may show the design labels for discoverability, but selecting an unsupported value must not alter rows or fabricate a result. If a future compatible API adds these fields, the request/model can be extended without changing the seven-column contract.

**Alternative considered:** Sending unsupported query names (`search`, `categoryId`, `warehouseId`, or `status`) to the current endpoint. Rejected because the backend ignores them, producing misleading results and breaking acceptance semantics.

### 3. C# Markup tab host

Extend the existing category workspace composition with a selected-tab state and bind the underline tabs to commands/state. The category content remains the current tree page; the inventory tab hosts the new stock page/control; Serial/IMEI remains a deferred hook. Shared chrome (header, tab bar, canvas) stays in the workspace so switching tabs does not recreate the Shell route or add a seventh destination.

All controls use fluent C# Markup bindings. View code owns layout and token references only. Data synchronization uses bindings and commands, not `PropertyChanged`/`TextChanged`/`Checked` handlers in the view.

### 4. Presentation and unavailable data

Use existing app color/typography/metric tokens and the inventory HTML as the visual source. Product icon tint uses the existing category/icon resolver; unavailable category, price, and warehouse values render an em dash. Status badges are pure presentation helpers so their rules can be unit-tested without WinUI. Deferred row actions expose commands/hooks and do not mutate stock.

### 5. Paging and state transitions

The API response determines total count, page size, and valid page numbers. Previous/next commands are disabled at boundaries. A new server page replaces the prior page atomically; loading, empty, error, and cancellation are separate observable states. A cancelled or stale activation cannot overwrite a later successful load. Local filtering only changes the derived visible rows and never rewrites server totals.

### 6. Verification strategy

Add headless tests for request URI/query construction, bearer propagation through the existing fake transport, paged JSON deserialization, normalized HTTP errors, cancellation, status derivation, search/status filtering, unavailable selectors, page-boundary commands, and deferred hooks. Build and visual review remain separate acceptance steps; live category/warehouse filtering is explicitly blocked until the backend contract is extended.

## Risks / Trade-offs

- **[Current API cannot filter search/category/warehouse server-side]** → Limit immediate filtering to fields and rows returned by the current page; show unsupported selectors as unavailable and document the backend prerequisite.
- **[Local filtering can make a server page appear shorter than its total]** → Keep the server total separate from the visible filtered count and label the limitation in the ViewModel/UI state until a compatible server query exists.
- **[Existing route currently resolves to category page]** → Integrate the tab host in that route and add a route-resolution test; do not duplicate Shell destinations.
- **[Legacy XAML InventoryPage may be registered]** → Leave it untouched for compatibility, but do not route the new workspace through it; add DI registration for the C# Markup feature and verify no new `.xaml` files are introduced.
- **[Backend authorization may differ from the proposal's expectation]** → Preserve 401/403 from the shared transport and surface a permission state; do not add client-side role assumptions or fixture data.

## Migration Plan

1. Add the API client, ViewModel, presentation helpers, C# Markup page/tab, DI registration, and category-workspace tab composition.
2. Add headless tests and run the frontend unit-test project plus the desktop build.
3. Roll back by removing the new tab registration/composition and API client; existing category route remains usable.

## Open Questions

- A future backend change must define whether category and warehouse are product-level or stock-row-level fields and whether prices belong to product or variant. This does not block the frontend-only implementation because unsupported fields remain unavailable.

