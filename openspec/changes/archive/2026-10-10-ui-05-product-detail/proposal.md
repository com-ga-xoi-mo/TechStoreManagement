# Proposal

## Why

UI-04 provides a product grid, but staff cannot inspect the selected product's specifications, valid variants, price, stock, or permission-gated cost information. UI-05 adds the D27 showroom dialog while the authoritative product-detail API contract remains undefined.

## What Changes

- Open a native Uno product showroom dialog from an existing product-list item, with loading, populated, empty/unavailable, error, and retry behavior backed by an explicit preview repository.
- Show product identity, image, warranty/model metadata, technical specifications, and only valid product-variant combinations.
- Update the selected variant's identifiers, selling price, availability, and variant-level specifications together; show cost and estimated margin only when the current session has the cost-read capability and the preview detail supplies those values.
- Keep preview provenance visible and treat all mock data and capability fixtures as demonstrations, not authoritative catalog values or server authorization.
- Keep the dialog dismissible and usable with keyboard focus and narrower desktop windows.
- Do not add edit, discontinue, Serial/IMEI, inventory, or other business actions; do not call an HTTP endpoint or modify backend behavior.

## Capabilities

### New Capabilities
- `client-product-detail`: Inspect a selected catalog product in a showroom dialog, select valid variants, and see only information available to the current session.

### Modified Capabilities
- `client-product-list`: Add an in-scope way to open the showroom for a selected product while preserving existing list behavior.

## Impact

- Frontend: `Models/Catalog`, `Repositories/Catalog`, `ViewModels/Catalog`, `Views/Catalog`, existing Catalog page/card activation, DI registration, and the dialog presentation boundary.
- Tests: headless detail ViewModel/repository tests for valid variant selection, cost-capability visibility, loading/error/retry, and data preservation; Uno desktop build/runtime smoke checks.
- API/backend: no changes. No product-detail endpoint, response contract, cost authorization contract, warehouse scope, stock semantics, or dynamic-spec shape is established for the client. A separate integration change must confirm these and enforce cost authorization on the server.

## Dependencies and Boundaries

- UI-00 theme, UI-01 Shell/dialog infrastructure, and UI-04 Catalog grid are implemented and archived; reuse their resources, DI conventions, repository, and navigation/dialog behavior.
- UI-07 product editing and UI-12 Serial/IMEI are not implemented. Do not create links that route to placeholders or include their screens in this change. A later change may connect those actions once their destinations exist.
