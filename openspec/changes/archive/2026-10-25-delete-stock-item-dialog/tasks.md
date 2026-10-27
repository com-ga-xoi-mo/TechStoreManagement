# Tasks

## 1. Inventory contract and action seam

- [x] 1.1 Extend the shared inventory stock presentation contract with a nullable authoritative related-Serial/IMEI count and add typed client result/error semantics for a supported stock delete operation; verify existing stock GET behavior and API-client tests remain green.
- [x] 1.2 Replace the inventory delete action's action-only payload with a typed selected-row snapshot carrying stock/product identity, product name, SKU, and count metadata; verify non-delete action hooks and row identity in `InventoryStockViewModel` tests.
- [x] 1.3 Add the safe unavailable/deferred path for environments without a supported backend delete endpoint; verify no guessed destructive URL or mutation request is emitted and the user-facing outcome is actionable.

## 2. Delete dialog state and behavior

- [x] 2.1 Implement `DeleteStockItemDialogViewModel` as a headless `ObservableObject` with immutable target fields, nullable Serial/IMEI count, `IsBusy`, `ErrorMessage`, cancel/close command, and single-submit `ConfirmDeleteAsync`; verify initialization, cancel, busy gating, cancellation, and context preservation with unit tests.
- [x] 2.2 Map normalized API outcomes (401/403/400/404/409, unavailable, timeout, network, and cancellation) to recoverable Vietnamese messages while keeping the dialog open for non-cancellation failures; verify each mapping and that no optimistic row removal occurs.
- [x] 2.3 Publish typed close/success outcomes for the coordinator, including a success message and exactly-once refresh signal; verify successful delete closes once and emits the toast/refresh contract without duplicate requests.

## 3. C# Markup dialog and toast presentation

- [x] 3.1 Implement `Features/Inventory/Dialogs/DeleteStockItemDialog.cs` with C# Markup only: 460px max width, 16px corners, warning icon, Vietnamese title/subtitle, product/SKU card, Serial/IMEI warning, two equal footer buttons, busy ProgressRing, close action, and accessible labels; verify no `.xaml` file is added and the desktop project builds.
- [x] 3.2 Add the green success toast overlay and dismiss/auto-dismiss state to the inventory workspace using existing theme resources and bindings; verify it appears only after a confirmed success and disappears safely on page unload.

## 4. Workspace integration

- [x] 4.1 Wire the inventory row delete button to open the custom dialog through `ContentDialogPresenter.TryShowAsync`, pass the current `XamlRoot`, preserve focus/lifecycle, and leave the table unchanged while the dialog is open; verify with coordinator tests for cancel, unavailable host, and close behavior.
- [x] 4.2 On successful supported deletion, invoke one inventory reload, remove the row only through authoritative refreshed API results, and surface the success toast; verify refresh-failure handling never resubmits deletion.
- [x] 4.3 Register any new inventory dialog/coordinator services and keep the feature within the existing “Danh mục và Kho” tab without adding a Shell destination; verify DI resolution and category/inventory workspace tests.

## 5. Verification and delivery checks

- [x] 5.1 Add/complete `TechStore.Client.UnitTests` coverage for target metadata, unavailable count/endpoint, cancel/close, busy gating, all normalized failures, cancellation, success refresh, and toast coordination; verify the full frontend unit-test project passes.
- [x] 5.2 Build `net10.0-desktop` and `net10.0-windows10.0.26100` with zero warnings/errors, run `git diff --check`, and verify no new XAML files exist.
- [x] 5.3 Record the live-backend prerequisite for the authorized delete-stock endpoint and authoritative Serial/IMEI count; verify the client remains safely deferred until that contract is deployed.

## Workflow follow-up

- Archive the change after implementation review and verification are complete.
- Sync the delta specs into main specs before archiving.
