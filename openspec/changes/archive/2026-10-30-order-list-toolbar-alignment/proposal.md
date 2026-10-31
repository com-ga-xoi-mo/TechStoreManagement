# Proposal

## Why

The order list heading, primary action, and filter row have inconsistent sizing and alignment. This change matches that toolbar to the supplied reference so the search field and filters read as one aligned section.

## What Changes

- Align the order list title and description with the reference layout.
- Match the “Lập đơn mới” action color, shape, and plus sign to the shared primary button.
- Align filter labels and controls; set the order search field and clear button to 40 DIP on this page.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `frontend-design-system`: Specify the expected alignment and sizing of the order list toolbar.

## Impact

- Changes are limited to the Orders list toolbar and a per-instance height option on the shared search control.
- No API, data, ViewModel, or order behavior changes.
