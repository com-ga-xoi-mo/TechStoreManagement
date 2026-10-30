# Proposal

## Why

Recent visual adjustments left headings, search controls, action buttons, inventory status labels, and settings rows inconsistent across the admin pages. This change records the intended shared presentation while keeping the Orders list scope limited to its title and filter toolbar.

## What Changes

- Align title and supporting text treatment on Products, Customers, Dashboard, Category and Inventory, and Settings pages.
- Match the product add/search actions and category actions to the shared primary button appearance; keep their plus icons appropriately sized.
- Use compact semantic status badges for inventory and Serial/IMEI rows.
- Keep the Settings page title, description, and card content aligned with consistent horizontal padding.
- On Orders, adjust only the page heading and filter toolbar shown in the reference; preserve the existing table and order detail modal.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `frontend-design-system`: Specify consistent presentation for admin page headings, primary actions, inventory status badges, and Settings rows.

## Impact

- Affects presentation code in the Catalog, Customers, Dashboard, Inventory, Orders, and Settings views, plus the shared search control and inventory status presentation helper.
- No API, ViewModel, database, order-table, or order-modal behavior changes.
