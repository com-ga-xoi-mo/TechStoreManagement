# Tasks

## 1. Adjustment model and repository contract

- [x] 1.1 Add explicit serial-tracking metadata to the stock balance model and preview fixtures; verify serial and regular rows are distinguishable and existing stock/detail tests still pass.
- [x] 1.2 Add typed adjustment direction, request, and result/failure models alongside inventory Models; verify requests require exact product/optional-variant identity, positive integer quantity, and a non-empty reason.
- [x] 1.3 Extend `IInventoryRepository` with an adjustment operation and implement explicit preview success, 409 conflict, generic error, delayed, and unavailable behavior; verify only fake success changes its preview fixture and serial-tracked requests cannot mutate it.
- [x] 1.4 Add focused fake/unavailable repository tests for exact key matching, success result, conflict without mutation, failure without mutation, serial blocking, and cancellation; run the frontend unit-test project.

## 2. Adjustment ViewModel and recovery behavior

- [x] 2.1 Add a headless `StockAdjustmentViewModel` for selected-balance state, type/direction, amount, reason, projected physical quantity, serial block, validation, and preview provenance; verify invalid drafts cannot submit and projections never mutate the source balance.
- [x] 2.2 Implement asynchronous submit state and duplicate-submit prevention; verify the ViewModel publishes `IInventoryChangeNotifier` only after a successful repository result and leaves state unchanged on cancel.
- [x] 2.3 Implement 409 and general failure recovery; verify 409 retains direction/amount/reason, explicit reload refreshes only the balance while retaining the draft, and non-conflict failures preserve input without success notification.
- [x] 2.4 Add headless tests for validation, projected values, regular/serial branches, duplicate submit, cancellation, success notification, conflict draft retention/reload, and general errors; run the focused frontend unit-test project.

## 3. Native D02/D05 adjustment interaction

- [x] 3.1 Create `Views/Inventory/StockAdjustmentDialog.xaml` and minimal code-behind with native Uno controls for product/variant identity, current physical/reserved quantities, increase/decrease choice, amount, required reason, and an explicitly estimated resulting physical count; verify bindings target ViewModel state/commands and no row balance changes before success.
- [x] 3.2 Add the D05 serial-tracked blocked state with the current balance summary, explanation, no quantity editor or submit action, and a Serial/IMEI navigation command carrying product/variant context; verify it shows no fabricated serial records or claim that UI-12 filtering is active.
- [x] 3.3 Bind validation, submitting/disabled controls, preview label, success, conflict with “Tải lại số tồn”, general error, cancel/close, and accessible focus states; verify the body remains scrollable and `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore` succeeds.

## 4. Dialog service, stock-row action, and DI

- [x] 4.1 Add `IStockAdjustmentDialogService` and its implementation using the shared modal gate, transient ViewModel, cancellation on close, and focus restoration; verify Escape/cancel closes without notification and focus returns to the invoking row.
- [x] 4.2 Add the dedicated adjustment action to each stock row and route the exact product/variant and tracking flag to the dialog; verify row-to-dialog identity and that the row itself never changes inventory.
- [x] 4.3 Register dialog, ViewModel, notifier, and preview scenario through the existing DI root; verify explicit Preview selects the fake adjustment behavior and non-preview uses the unavailable inventory repository with no fallback.
- [x] 4.4 Verify a successful adjustment invalidates/reloads the stock list through the UI-10 notifier while a conflict does not; add focused integration tests for row identity, Serial/IMEI context handoff, notifier timing, and run the frontend unit-test project.

## 5. Build, runtime handoff, and owner acceptance

- [x] 5.1 Run the complete frontend unit-test project and build `net10.0-desktop`; attempt Windows build only on a supported host and record exact commands/results, preview scenarios, and API/backend dependencies in `Views/Inventory/UI_11_HANDOFF.md`.
- [x] 5.2 Launch the Preview app and smoke-test regular increase/decrease, validation, success/reload, 409 draft retention/reload, generic error, serial-tracked blocking/navigation, and absence of direct/optimistic stock mutation; record runtime issues and confirm no real API/backend/database behavior was added.
- [x] 5.3 After agent checks pass, ask the owner to manually test at the normal window size and a narrower still-usable width, including keyboard/focus, validation, success, conflict draft retention/reload, Serial/IMEI block/navigation, and cancel; record dated confirmation and unmet behavior in the handoff and leave this task unchecked until the owner confirms.
- [x] 5.4 Document the separate real-integration dependencies: adjustment endpoint and request/response/error contract, authoritative balance/movement result, `IsSerialTracked` mapping, permission/RBAC, concurrency/transaction/ledger semantics, and safe retry/idempotency behavior; verify preview labels do not imply persisted inventory or server authorization.
