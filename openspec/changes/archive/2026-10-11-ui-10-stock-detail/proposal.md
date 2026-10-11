# Proposal

## Why

The stock list now exposes read-only balances, but staff need a focused view of the selected product or variant and its recent inventory movements. A detail view must show movement history supplied by its repository and preserve the distinction between preview data and real inventory.

## What Changes

- Add a read-only stock detail dialog for a selected product/variant, with physical, reserved, and available quantities plus the five most recent repository-provided movements.
- Support loading, empty, error/retry, preview provenance, refresh, and accessible close behavior.
- Add a read-only detail action to stock-list rows and pass product/variant context when navigating to the existing Serial/IMEI destination; that destination remains an explicit placeholder until UI-12.
- Do not add stock adjustment UI, infer movement history from current balances, or add HTTP/API/backend/database behavior.
- Record the future inventory contract and UI-11/UI-12 dependencies; use preview and unavailable repositories through DI until real contracts and destinations exist.

## Capabilities

### New Capabilities

- `client-inventory-stock-detail`: Read-only stock balance details and recent repository-provided movement history.

### Modified Capabilities

- `client-inventory-stock-list`: Allow a read-only action to open the selected balance detail while keeping stock mutation and adjustment actions out of UI-09.

## Impact

- Frontend inventory Models, repository interface and preview/unavailable implementations, ViewModels, native XAML views, dialog/navigation services, and DI registration.
- Frontend unit tests for detail models, repository scenarios, ViewModel state/refresh/navigation, and dialog integration where testable.
- No API, HTTP client, JWT, backend, shared DTO, or database changes. Real movement-history semantics, permissions, and refresh invalidation remain dependencies for a separate integration change and the future UI-11 adjustment flow.
