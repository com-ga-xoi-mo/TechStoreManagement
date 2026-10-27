# Design

## Context

The inventory workspace is already a C# Markup page hosted inside `CategoryManagementPage`. `InventoryStockViewModel` loads `InventoryStockDto` rows and currently emits a coarse delete action hook without the selected row. `IInventoryApiClient` only supports stock listing, while `ContentDialogPresenter.TryShowAsync` already provides the Shell-owned custom ContentDialog lifecycle, theme, serialization, cancellation, and focus recovery. See `proposal.md` and the two delta specs for the behavior contract.

## Goals / Non-Goals

**Goals:**

- Keep the dialog and ViewModel feature-first, headless-testable, and free of direct WinUI references in state/commands.
- Preserve an immutable stock-row snapshot and nullable authoritative Serial/IMEI count; never fabricate a count.
- Route delete requests through the existing bearer-aware API transport, gate duplicate submits, map normalized failures, and refresh the inventory list once after success.
- Render the 460px/16px C# Markup modal and a themed success toast without adding XAML or a new Shell destination.

**Non-Goals:**

- Implementing Serial/IMEI detail editing, stock adjustment, or import workflows.
- Deleting products from the catalog or bypassing the inventory service/repository boundary.
- Inventing a backend delete route or silently treating an unsupported operation as successful.

## Decisions

1. **Use a dedicated inventory dialog ViewModel and ContentDialog.** The ViewModel owns target fields, nullable serial count, busy/error state, commands, and close/success events. The view only builds C# Markup and bindings. This matches existing category dialogs and keeps tests independent of Uno controls. A generic text `IDialogService` confirmation was rejected because it cannot render the stock card, warning copy, progress state, or typed failure retention.

2. **Pass the selected row through a typed action request.** Extend the inventory action seam so delete carries the `InventoryStockRow`/DTO identity instead of only `InventoryStockAction.Delete`. The page/coordinator creates a snapshot before showing the modal; the table is not optimistically edited. This avoids reaching into ItemsControl containers and preserves identity if the list refreshes.

3. **Make the API delete capability explicit and safe.** Add a typed client operation/result for the server-authorized delete endpoint and an optional nullable `TotalSerialsCount` contract field or trusted count lookup. When the operation is not configured/supported, the ViewModel returns an unavailable outcome and does not send a guessed route. A fire-and-forget HTTP call or client-side row removal was rejected because either could destroy data or desynchronize the authoritative list.

4. **Coordinate success in the inventory workspace owner.** On the ViewModel success event, the page/coordinator invokes exactly one `LoadCommand`, then exposes a success-toast state (`SuccessToastMessage`/visibility with dismiss support). The toast is a C# Markup overlay using the existing success theme resources; it is not a separate window or modal. Refresh-before-toast ordering ensures the notification represents a completed authoritative operation.

5. **Use existing dialog presenter lifecycle.** Construct the custom dialog through `ContentDialogPresenter.TryShowAsync`, attach the current `XamlRoot`, and let the presenter restore focus. The dialog uses explicit close/cancel commands and no direct `Click`/`PropertyChanged` event synchronization in the view, following the C# Markup rules.

6. **Map failures without losing context.** API status/error kinds map to Vietnamese recoverable messages for unauthorized, forbidden, validation/conflict, not-found, unavailable, timeout, network, and cancellation outcomes. Non-cancellation failures keep the modal open; cancellation remains cancellation and never triggers refresh or success.

## Risks / Trade-offs

- **[Backend endpoint unavailable]** → The production delete path cannot be accepted as live until the backend publishes an authorized endpoint and serial-count contract; keep the typed seam deferred and show an actionable unavailable state.
- **[Serial/IMEI count becomes stale between list load and confirmation]** → Treat the count as display context only and let the backend enforce deletion constraints; do not use it as authorization or a client-side delete predicate.
- **[Refresh failure after a successful delete]** → Do not resubmit deletion; expose a recoverable refresh error and retain the success/operation outcome separately so the next retry is a GET only.
- **[ContentDialog host unavailable or another modal is open]** → Return a controlled unavailable result from the presenter/coordinator and leave the inventory list unchanged.
- **[Toast lifetime outlives the page]** → Bind it to the page ViewModel and cancel its auto-dismiss token during page unload; never retain a view reference from the ViewModel.

## Migration Plan

1. Add the nullable count/delete contract and typed client seam without changing existing GET behavior.
2. Add the dialog ViewModel/view, row action payload, coordinator wiring, and toast state behind the existing inventory tab.
3. Run headless dialog/workspace tests, then build both desktop targets.
4. Enable the destructive operation only after the backend endpoint is deployed and authenticated integration tests verify 204/success and failure mappings. Before that deployment, the UI remains safely deferred.

