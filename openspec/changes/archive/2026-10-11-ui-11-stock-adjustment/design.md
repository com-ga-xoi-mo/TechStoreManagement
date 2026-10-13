# Design

## Context

UI-09 renders read-only stock rows, UI-10 adds detail and an inventory-change notification seam, and the app serializes `ContentDialog` presentation with a shared modal gate. `StockBalance` currently has no serial-tracking flag, while the catalog model already represents `IsSerialTracked`. The client repository has list/detail reads but no adjustment operation. Backend inventory entities and schema describe stock and an append-only movement ledger, but this checkout has no implemented inventory controller/service or `StockAdjustmentRequest` source file.

See `proposal.md` for motivation and the delta specs for user-visible behavior.

## Goals / Non-Goals

**Goals:**

- Add a typed adjustment workflow through the existing inventory repository boundary and preview-mode DI selection.
- Carry exact product/variant identity and serial-tracking metadata into the dialog.
- Show a projection before submit, preserve drafts on failures, and publish refresh notifications only after a successful repository result.
- Make the real API dependency explicit so the mock cannot be mistaken for persisted inventory or backend authorization.

**Non-Goals:**

- Implement HTTP transport, endpoint/DTO contracts, backend transaction/ledger code, RBAC, database changes, purchase receipt, multi-warehouse movement, or serial import.
- Adjust serial-tracked quantities directly or fabricate serial records.
- Apply local quantity arithmetic to the stock table as if it were an authoritative server result.

## Decisions

1. **Open adjustment from the stock row, not from inside the stock-detail dialog.** Add a dedicated row action beside the existing detail action. D02 depicts adjustment as a modal opened from a stock-table action, and `DialogModalCoordinator` permits one `ContentDialog` at a time; a row entry avoids nested modal ownership and deadlock. The stock row itself remains read-only until a successful repository response causes a normal reload.

2. **Extend `IInventoryRepository` instead of adding another inventory service.** Add a typed adjustment request/result operation alongside current stock list/detail queries. This keeps inventory reads and the related write seam together and follows the project's interface-beside-implementation convention. The ViewModel calls only this interface and remains independent of XAML and HTTP.

3. **Carry an explicit serial-tracking flag on the stock balance model.** The current row has no such field; use the existing catalog concept in the Model and deterministic fake fixtures. The real integration must map this value from the eventual server/shared response. Missing tracking metadata must not silently enable direct adjustment; the UI should show an unavailable/blocked state until classification is known.

4. **Treat increase/decrease quantity as a positive integer and reason as required.** Direction is a separate enum; the amount is never signed by the user. The ViewModel computes only an informational projected physical count from the loaded balance and draft. It must not mutate `StockBalance`, movement rows, or the stock list before repository success. Backend constraints such as reserved quantity, nonnegative balance, and concurrency remain authoritative.

5. **Use a deterministic fake mutation result and publish through the UI-10 notifier.** Preview mode gets explicit success, conflict/409, generic failure, and delayed scenarios. Only a fake success may replace its in-memory preview fixture and notify subscribers; the ViewModel does not apply a delta itself. The list then reloads through `GetStockPageAsync`. Non-preview DI uses the unavailable repository and never falls back to fake data.

6. **Recover from conflict explicitly while retaining the draft.** A 409-shaped result shows a conflict message and enables “Tải lại số tồn”. Reload the selected balance but preserve direction, amount, and reason; do not automatically resubmit, since a real retry contract may require idempotency. Other failures also preserve input and cannot show success.

7. **Use the D05 blocked state for serial-tracked rows.** Render the current and reserved quantities, explanatory copy that quantity is derived from serial/IMEI records, and a button using the existing inventory navigation service/context. No quantity editor, projected count, or submit command is present for this state; until UI-12 consumes context, the destination continues to identify itself as unfinished.

8. **Keep the interaction as one native modal with accessible states.** `StockAdjustmentDialogService` resolves a transient `StockAdjustmentViewModel`, follows the shared modal gate and focus-restoration pattern, and cancels pending work on close. XAML binds to ViewModel state/commands and uses the existing theme styles. The body scrolls at narrow widths; feedback distinguishes validation, submitting, success, conflict, and generic failure.

9. **Select preview scenarios only through explicit DI configuration.** In `TECHSTORE_AUTH_MODE=preview`, configure `FakeInventoryRepository` with the adjustment scenario from a dedicated preview setting. In normal mode, keep `UnavailableInventoryRepository`. Preview labels remain visible for both success and conflict simulations.

## Risks / Trade-offs

- **No real adjustment API contract exists** → Keep the operation as a fake repository contract and document endpoint, payload, authoritative balance response, error envelope, permissions, and movement response as integration dependencies.
- **A concurrent update can invalidate the projection** → Label the value as estimated; let the server/repository return conflict, retain the draft, and require explicit reload before the user decides to submit again.
- **Real retry may duplicate a committed request if its response is lost** → Do not add automatic retry. The API integration must decide idempotency/request identity and safe retry behavior before adding HTTP submission.
- **Serial-tracking metadata is missing from `StockBalance`** → Add it to preview data now, fail closed when unknown, and require a real inventory/catalog response contract to carry it before API integration.
- **A fake success can look persistent** → Keep a visible Preview marker, scope any fake balance update to the fake repository instance, and never describe it as a server-written movement or authorization result.
- **UI-12 remains a placeholder** → Pass product/variant context through the existing navigation seam but clearly retain the unfinished state; filtering and serial records belong to UI-12.
