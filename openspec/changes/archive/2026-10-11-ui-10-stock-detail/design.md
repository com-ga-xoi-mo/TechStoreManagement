# Design

## Context

UI-09 provides a paged, preview-backed stock list through `IInventoryRepository.GetStockPageAsync` and `StockBalance`; its current contract deliberately has no row action. `InventoryPage` already has a Serial/IMEI tab, but its content is an explicit placeholder, and the app already uses a DI-resolved dialog service pattern with focus restoration. No inventory movement endpoint/DTO or UI-11 adjustment flow exists. D03 depicts a balance summary, five recent movements, last-updated text, a Serial/IMEI link, and an adjustment action; this change keeps the first four and excludes adjustment.

## Goals / Non-Goals

**Goals:**

- Add a read-only stock detail dialog opened from the selected UI-09 balance.
- Load a coherent balance plus repository-provided recent movements, with loading, populated, empty, error/retry, refresh, and preview states.
- Provide a route to the existing Serial/IMEI placeholder while preserving product/variant context.
- Define a frontend refresh notification seam for a future successful inventory mutation and request stock-list refresh without local balance arithmetic.
- Keep native Uno XAML, CommunityToolkit.Mvvm, constructor DI, and Models/Views/ViewModels/Repositories/Services at the same level.

**Non-Goals:**

- Adjustment forms/actions, stock mutations, imports/exports, warehouse allocation, or mutation success claims.
- Deriving movements from the current balance or fabricating preview movements as live history.
- HTTP repository, endpoint, JWT behavior, backend, shared DTO, or database changes.
- Implementing the UI-12 Serial/IMEI records/filter page.

## Decisions

1. **Extend the existing inventory repository boundary.** Add typed stock-detail and recent-movement result models and repository queries to `IInventoryRepository`, keeping its fake and unavailable implementations together in the existing inventory repository folder. This reuses UI-09's DI source selection and keeps the ViewModel independent of HTTP. A separate detail repository was considered but would split one inventory read boundary without adding an independent source or lifecycle.

2. **Treat balance and movements as one detail snapshot.** The detail query returns the selected product/optional variant balance, up to five newest movements, provenance, and last-refreshed metadata together. The UI renders the movement type, timestamp, signed quantity change, physical quantity after the event, and available note from returned records. It never reconstructs events or computes a displayed historical balance from the current quantity. A single snapshot was chosen over separate requests so one dialog refresh cannot mix mismatched balance and history responses.

3. **Use deterministic mock fixtures, clearly marked as preview.** Fake data may include representative movement types and a five-row history for the UI. Empty and error scenarios are selected through preview settings. The unavailable implementation reports unavailable real inventory. No mock source is an implicit fallback for an HTTP failure or evidence of persisted stock.

4. **Open the dialog through a service and retain the stock row as context.** Add a DI-registered `IStockDetailDialogService` following the existing product-detail dialog pattern. The stock-list ViewModel sends a stable product/variant key; the detail ViewModel loads by that key, exposes refresh/retry and cancellation, and does not reference XAML controls. The dialog service owns `ContentDialog`, modal coordination, Escape/close behavior, and restoring focus to the invoking row.

5. **Refresh through repository reload and a future change notification.** Opening/reopening and the explicit refresh action request a new detail snapshot. Define a small frontend inventory-change notification seam that a future UI-11 flow can publish after server success; while the dialog is open, matching product/variant changes trigger a reload and the stock-list ViewModel becomes stale and reloads when active. Until UI-11 exists, tests invoke the notification seam directly; this change does not simulate an adjustment or mutate fake balances locally. A notification bus was preferred over coupling the detail ViewModel to a future adjustment dialog.

6. **Route to the existing placeholder and defer actual serial filtering.** The Serial/IMEI action navigates to the inventory destination, selects the existing Serial/IMEI tab, and passes product/variant context to that destination. Since the tab is currently a placeholder, it must continue to say the feature is unfinished and show no fabricated serials; UI-12 is responsible for consuming the context to filter records. Adding a new serial screen here was rejected because it exceeds UI-10.

7. **Update UI-09's row contract narrowly.** Replace the prohibition on future-dialog actions with one read-only detail action for the corresponding balance. Adjustment, deletion, and other mutation actions remain absent. This is necessary because the existing stock-list spec otherwise forbids the UI-10 entry point.

## Risks / Trade-offs

- **Movement API semantics are not agreed** → Keep the model contract explicit and preview-only; record the required movement ordering, quantity semantics, identifiers, permission behavior, and pagination/retention as dependencies for a separate integration change.
- **UI-11 does not exist to publish mutations** → Add and test only the notification seam and reload response; document that real mutation-to-refresh wiring remains pending UI-11/API integration.
- **Serial/IMEI destination is a placeholder** → Route with context and retain the unavailable message; do not imply records were loaded or filters applied until UI-12.
- **D03 includes an adjustment action and sample counts that may not match the selected balance** → Omit adjustment controls and use one coherent preview fixture for summary and movement values.
- **Dialog layout can exceed a narrow desktop window** → Use a scrollable body and reflowed movement rows/actions; require the owner to manually accept the running UI at normal and narrower usable widths.
