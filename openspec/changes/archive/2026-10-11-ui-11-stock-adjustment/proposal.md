# Proposal

## Why

The UI-09 stock table and UI-10 detail view are read-only, so staff cannot record a regular product's stock correction. UI-11 adds a carefully bounded adjustment flow while ensuring serial-tracked products follow their serial workflow and preview responses never imply a real server-side change.

## What Changes

- Add an adjustment action for a stock row that opens a native D02-style dialog for the exact product/variant balance.
- For non-serial-tracked products, let staff choose increase or decrease, enter a positive whole-number quantity and a required reason, and see an explicitly estimated resulting physical balance.
- Simulate success, loading, general failure, and HTTP-409-style conflict through the existing inventory repository's preview implementation selected by DI. On conflict, preserve the draft and offer a balance reload without submitting again automatically.
- Publish the existing inventory-change notification only after the repository reports preview success, then reload the stock list from the repository; do not optimistically edit quantities or create movement history in the ViewModel.
- For serial-tracked products, show the D05 blocking explanation and a route to the existing Serial/IMEI placeholder with product/variant context. Do not expose quantity inputs or an adjustment command.
- Keep real HTTP, JWT, shared request/response DTOs, backend transaction/ledger changes, database changes, and UI-12 serial records out of this change. Record their missing contracts as integration dependencies.

## Capabilities

### New Capabilities

- `client-inventory-stock-adjustment`: Type-aware, preview-backed stock adjustment interaction, including validation, conflict recovery, and success feedback.

### Modified Capabilities

- `client-inventory-stock-list`: Add a row action that opens the adjustment experience for the exact selected balance while keeping the row itself read-only.

## Impact

- Frontend inventory Models, `IInventoryRepository` and its fake/unavailable implementations, `StockListViewModel`/`InventoryViewModel`, a native adjustment dialog and dialog service, DI registration, and focused frontend unit tests.
- Reuses the UI-10 `IInventoryChangeNotifier` seam to refresh the list after a repository-confirmed preview success. Uses the existing `IsSerialTracked` catalog concept; the current stock-row model does not yet carry it, so preview fixtures need an explicit flag.
- The repository contains inventory entities and schema constraints, but no implemented inventory controller/service or adjustment API contract. `shared/Requests/README.md` mentions a future `StockAdjustmentRequest`; no request class or endpoint exists. Real concurrency, authorization, transaction, and append-only ledger behavior remain backend responsibilities for a separate integration change.
