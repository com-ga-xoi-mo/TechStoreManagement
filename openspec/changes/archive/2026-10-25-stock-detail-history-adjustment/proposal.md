# Proposal

## Why

The inventory workspace can currently list stock balances and expose action hooks, but users cannot inspect movement history or safely correct a stock quantity from the UI. This change completes that workflow with authoritative stock detail data, audited adjustments, and role-aware dialogs aligned with the approved HTML designs.

## What Changes

- Add an authenticated stock-detail endpoint returning product metadata, quantity metrics, serial count, and recent movement-ledger entries.
- Add an Admin/WarehouseStaff-only stock-adjustment endpoint using a row lock, quantity invariants, an ACID transaction, and an `Adjustment` movement record.
- Add shared contracts for adjustment requests, movement DTOs, and stock-detail DTOs.
- Add `StockDetailHistoryDialog` and `StockAdjustmentDialog` as 100% C# Markup views, with loading/error states, live quantity projection, validation, and cancellation-safe commands.
- Wire the inventory table's visibility and edit actions to the dialogs; refresh the authoritative list and show a success toast after adjustment.
- Preserve the existing safe-delete flow and seven-column inventory workspace contract.
- Add backend and frontend headless tests for mapping, validation, concurrency invariants, HTTP failures, and successful refresh behavior.

## Capabilities

### New Capabilities

- `inventory-stock-detail`: View authoritative stock metrics and recent movement history, and perform audited, role-controlled stock adjustments.

### Modified Capabilities

- `inventory-management`: Extend inventory APIs and service behavior with stock detail retrieval and transactional quantity adjustments.
- `inventory-stock-workspace`: Replace visibility/edit deferred hooks with the detail and adjustment dialogs while retaining refresh, error, and pagination behavior.

## Impact

- Shared contracts under `shared/DTOs` and `shared/Requests`.
- Inventory controller, service, repository, and unit-of-work transaction paths under `backend/Inventory`.
- Inventory API client, ViewModels, C# Markup dialogs, and workspace integration under `frontend/TechStore.Client`.
- Backend and frontend unit-test suites; no XAML files or direct client-to-database access.
- Existing `inventory_movements.reason` remains the persistence seam for adjustment notes unless a later schema migration introduces a dedicated note column.
