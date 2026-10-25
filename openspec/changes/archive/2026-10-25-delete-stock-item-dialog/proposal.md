# Proposal

## Why

The inventory table currently exposes a delete icon only as a deferred action hook, so users have no safe confirmation flow or clear warning about the stock row and related Serial/IMEI records. This change adds a focused C# Markup confirmation dialog while preserving the repository's rule that the client must not fabricate or issue destructive database operations when the backend contract is unavailable.

## What Changes

- Add a feature-first `DeleteStockItemDialog` and `DeleteStockItemDialogViewModel` implemented entirely in C# Markup/MVVM.
- Render the 460px rounded confirmation modal from `docs/designs/delete-stock-item.html`: danger icon, product/SKU context card, related Serial/IMEI warning, cancel/confirm actions, busy state, and accessible close behavior.
- Carry a stable stock-row snapshot into the dialog, including stock ID, product ID, product name, SKU, and authoritative related-serial count when supplied by the API contract.
- Replace the inventory row delete hook with a dialog request carrying the selected row; on a successful delete result, refresh inventory exactly once and expose a green success-toast event/message.
- Extend the inventory API seam with a typed delete operation only when a supported backend endpoint is available; otherwise the confirm action remains safe/deferred, reports unavailability, and sends no destructive request.
- Preserve normalized bearer-session, cancellation, busy-gating, and recoverable error behavior; failed deletes keep the dialog open and preserve its context.
- Add headless unit tests for initialization, cancel/close, one-request gating, unavailable endpoint behavior, success refresh/toast coordination, cancellation, and error retention.
- Do not add XAML or implement Serial/IMEI detail editing in this change.

## Capabilities

### New Capabilities

- `inventory-stock-delete-dialog`: Safe confirmation, error handling, refresh coordination, and success notification for removing an inventory stock item.

### Modified Capabilities

- `inventory-stock-workspace`: The stock-row delete action opens the confirmation dialog and refreshes only after a successful supported delete operation; unsupported destructive operations remain visibly deferred.

## Impact

- **Frontend:** new dialog/view-model files under `Features/Inventory`, inventory page action routing, toast/refresh coordination, and frontend unit tests.
- **Client API contract:** add a typed delete seam and optional related-serial-count field/lookup without bypassing `IApiClient` or the Bearer session provider.
- **Backend dependency:** production success requires a server-authorized delete-stock endpoint that defines whether an empty stock row and related Serial/IMEI records may be removed, plus an authoritative serial count. This proposal does not invent or call an unimplemented destructive route.
- **Data safety:** no direct database access, optimistic row removal, or destructive fallback; errors (400/401/403/404/409/transport) retain the dialog and user context.
- **Build/test:** both desktop targets and `TechStore.Client.UnitTests` must remain warning-free and pass without a live database for headless coverage.
