# Proposal

## Why

Pagination on Category, Inventory, and Serial/IMEI currently uses icon-only arrows and numbered buttons, unlike the labeled controls on Customers. Matching the existing customer pager will make page navigation consistent and easier to understand.

## What Changes

- Replace the inventory module's numbered/icon pagination controls with “Trang trước”, “Trang x / y”, and “Trang sau”.
- Preserve each ViewModel's existing page commands and disabled-state behavior.
- Apply the same presentation to Category, Inventory, and Serial/IMEI tabs.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `frontend-design-system`: Require the inventory module's pagination to follow the shared customer-list presentation.

## Impact

- Affects the Category, Inventory, and Serial/IMEI page footers and their page-label presentation properties.
- No changes to data requests, page size, table contents, or backend behavior.
