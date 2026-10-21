# Proposal

## Why

The customer API is implemented, but the Customer destination is still a placeholder and cannot exercise Dev 5's workflows from the desktop client. Implementing the Sprint 2 customer experience from the team's Stitch reference will connect the API to customer search, profile maintenance, loyalty history, purchase history, and reversible activity management without hard deletion.

## What Changes

- Extend customer search responses with the order count, total spending, and most recent completed purchase needed by the Stitch customer list. Count all orders; total spending includes only `Completed` orders; most recent purchase is the latest completion time. Keep the server-defined newest-created ordering without presenting a one-option sort control.
- Add a paginated read-only customer purchased-device history containing product/variant and linked serial/IMEI details, alongside the existing paginated order and loyalty-point histories.
- Add a frontend customer repository and presentation models backed by the customer API.
- Replace the legacy XAML customer list with a C# Markup customer-management page matching the supplied Stitch design, including live debounced search, tier/activity filtering, list metrics, tier-matched badge/avatar colors, create, profile details, deactivation, and reactivation. The default list shows active customers; Admins can switch to inactive customers or all customers.
- Add C# Markup customer create/edit and detail dialogs. The detail experience includes tier-matched profile presentation, profile summary cards, order history with the primary product category, purchased devices/IMEIs, and persisted loyalty transaction history.
- Add headless frontend and backend tests for the API contracts, mappings, ViewModel behavior, validation/error handling, and customer flows; remove only the replaced customer XAML files.
- Keep customer removal soft and reversible: expose `PATCH /api/customers/{id}/deactivate` and `PATCH /api/customers/{id}/activate`, expose no customer `DELETE` route, and preserve all customer, order, and loyalty history.

## Capabilities

### New Capabilities
- `frontend-customer-management`: Customer administration workflows in the desktop client, consuming the customer API.

### Modified Capabilities
- `customer-management`: Extend paginated customer search with order aggregates, add a paginated purchased-device history query, and replace the delete-shaped deactivation route with explicit deactivate/reactivate operations.

## Impact

- Backend customer API: paginated customer aggregates, paginated purchased-device history, and explicit idempotent deactivate/reactivate operations. No customer hard-delete operation, loyalty accrual rule, foreign-key change, or database migration is introduced.
- Frontend: `frontend/TechStore.Client/Repositories/`, `Models/`, `ViewModels/Customers/`, `Views/Customers/`, and UI service registration.
- Tests: `tests/Backend.UnitTests/` and `tests/Frontend.UnitTests/`.
- Shared customer/order response contracts are extended with the primary product category used by customer order history. Existing design tokens, dialog infrastructure, and Uno C# Markup patterns are reused; no new package or database migration is proposed.
