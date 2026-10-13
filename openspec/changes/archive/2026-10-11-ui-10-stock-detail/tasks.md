# Tasks

## 1. Detail models and repository contract

- [x] 1.1 Add typed stock-detail identity, balance snapshot, movement row, and refresh metadata Models; verify IDs are required, quantities remain valid, and the model does not construct movement history from a balance.
- [x] 1.2 Extend `IInventoryRepository` with a detail query keyed by product plus optional variant and implement deterministic preview and unavailable behavior beside the existing inventory repositories; verify preview detail is coherent, labeled, and contains no more than five newest-first movement fixtures.
- [x] 1.3 Add preview empty-history, detail-error, and retryable fixture scenarios; verify empty history stays empty, errors are distinguishable from not-found/empty, and the unavailable source never returns sample data.
- [x] 1.4 Add focused repository/model tests for exact product/variant matching, returned movement ordering and values, refresh snapshots, and cancellation; run the frontend unit-test project.

## 2. Detail ViewModel and refresh coordination

- [x] 2.1 Add `StockDetailViewModel` that loads the selected balance and movements as one snapshot and exposes loading, populated, empty-history, error, retry, preview, last-refreshed, and cancellation state; verify behavior with headless tests and no references to XAML controls or HTTP types.
- [x] 2.2 Add explicit refresh and retry behavior that re-queries the same product/variant; verify a failed refresh preserves no stale-success claim and a successful refresh replaces the full snapshot only after completion.
- [x] 2.3 Add a frontend inventory-change notification seam for future UI-11 success events; verify a matching product/variant event reloads detail and marks the stock list stale, while unrelated keys do not trigger a detail reload and no local quantity mutation occurs.
- [x] 2.4 Add headless tests for close/cancellation, overlapping requests, refresh success/failure, preview provenance, and change notifications; run the frontend unit-test project.

## 3. Native stock detail dialog

- [x] 3.1 Create `Views/Inventory/StockDetailDialog.xaml` and its minimal code-behind using native Uno controls for the D03 summary, three distinct quantities, five recent movement fields, last-updated state, and scrollable/reflowing content; verify no adjustment form/action, fabricated history, or serial table is included.
- [x] 3.2 Bind loading, empty history, error/retry, preview, refresh, close, and Serial/IMEI navigation states to the ViewModel; verify XAML bindings point only to ViewModel state/commands and use accessible labels/focus styling.
- [x] 3.3 Add `IStockDetailDialogService` and its dialog implementation through DI, following existing dialog coordination; verify the selected product/variant key reaches the ViewModel, close cancels pending work, Escape closes, and focus returns to the invoking stock row.
- [x] 3.4 Verify the dialog supports keyboard operation and a narrower usable desktop width through an application smoke run; keep the body scrollable and all quantity/movement fields and actions reachable.

## 4. Inventory navigation and stock-list integration

- [x] 4.1 Add a read-only detail command/action to each stock row and open the dialog for that row's exact product/optional-variant identity; verify it never invokes an adjustment or other mutation command.
- [x] 4.2 Route the Serial/IMEI action to the existing inventory destination with product/variant context and retain an explicit unfinished placeholder until UI-12; verify no sample serial records or claim of an applied filter is shown.
- [x] 4.3 Register repository, dialog service, ViewModel, and inventory-change notification dependencies in the existing DI composition root; verify Preview selects fake data and non-preview selects the unavailable source.
- [x] 4.4 Add/adjust integration tests for row-to-detail identity, dialog routing, Serial/IMEI context handoff, and list invalidation; run the focused frontend unit-test project.

## 5. Build, handoff, and owner acceptance

- [x] 5.1 Run frontend unit tests and build `net10.0-desktop`; attempt the Windows target only when the host supports it, and record exact commands/results in `Views/Inventory/UI_10_HANDOFF.md`.
- [x] 5.2 Launch the desktop app and smoke-test opening details from product and variant rows, movement feedback, retry/refresh, Serial/IMEI placeholder routing, and absence of adjustment controls; record runtime issues and confirm no HTTP endpoint, backend, or database behavior was added.
- [x] 5.3 After build and launch checks pass, ask the owner to manually test the dialog at their normal window size and a narrower still-usable width, including keyboard open/close, refresh/retry, movement readability, and Serial/IMEI navigation; record the dated confirmation and unmet behavior in the handoff and leave this task unchecked until the owner confirms.
- [x] 5.4 Document the missing movement-history/API semantics, UI-11 mutation notification producer, and UI-12 serial filter consumer as separate dependencies; verify the handoff labels all mock detail data as preview and does not claim real inventory behavior.
