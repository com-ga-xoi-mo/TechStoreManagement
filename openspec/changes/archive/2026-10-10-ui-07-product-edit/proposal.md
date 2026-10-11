# Proposal

## Why

UI-04 through UI-06 provide a preview product list, showroom, and create form, but staff cannot edit an existing product or its variants. UI-07 adds a preview-backed edit dialog while keeping draft state isolated until save succeeds and keeping server-owned validation and authorization outside the client.

## What Changes

- Add an accessible edit action to product cards that opens a native D24-style edit dialog for the selected product.
- Load product edit data through `IProductRepository`; edit product identity, image URL, specifications, category/brand, and variant SKU/barcode/prices/specifications in an isolated draft.
- Preserve each variant's unsaved values while switching D24 variant tabs; allow adding and removing variants within the draft, without adding product deletion from UI-16.
- Provide editable, explicitly non-authoritative SKU suggestions, validation, loading/saving, success, failure, and simulated conflict feedback through preview repositories selected by DI.
- Apply preview changes to the shared in-memory catalog only after successful save; cancel or close discards the draft. Refresh the current list criteria only after save succeeds.
- Keep serial-tracking mode read-only in this edit flow unless a future server contract explicitly permits changing it; show cost fields only when the preview session provides the relevant capability.
- Track image URL preview as a known dependency: UI-06 handoff reports that adding/using an image link currently does not work. This change does not implement file upload or claim that URL/CDN image handling is complete.

## Capabilities

### New Capabilities
- `client-product-edit`: Preview-backed product and variant editing with isolated drafts, validation, conflict feedback, and explicit boundaries.

### Modified Capabilities
- `client-product-list`: Product cards gain an accessible edit entry point and refresh after successful edits while retaining list criteria.

## Impact

- Client: catalog models, `IProductRepository` and its fake/unavailable implementations, `ProductListViewModel` and page, a new edit ViewModel, native dialog and dialog service, DI registrations, and focused frontend tests.
- Uses existing product list, category repository, session capability checks, dialog coordination, theme, and UI-06 create-form patterns where appropriate.
- No HTTP repository, ApiClient, endpoint, JWT handling, backend change, product deletion, inventory mutation, or transaction/Serial/IMEI workflow is included.
- Missing integration dependencies: product edit request/response and concurrency token/version contract; category/brand update rules; SKU/barcode uniqueness and conflict payloads; variant transaction constraints; cost-write permission contract; and a resolved image URL/upload strategy. Fake results remain preview-only.
