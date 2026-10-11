# Proposal

## Why

UI-04 provides a preview catalog and UI-05 provides a product showroom, but staff cannot yet open a create-product form. D28 includes initial-stock and draft actions whose server contracts are not implemented, so the client needs a clearly preview-only create flow that does not pretend to receive inventory or save drafts.

## What Changes

- Add a native product-create dialog for basic identity, category, price, image URL preview, dynamic specifications, variants, and field validation.
- Use injected fake product and category repositories for the preview flow; retain entered values on simulated failure and refresh the product list after simulated success.
- Add an accessible “Thêm sản phẩm” entry point to the existing product list.
- Keep initial inventory receipt, per-device Serial/IMEI entry, draft persistence, and real API integration outside this change until their server contracts and dependent UI exist.

## Capabilities

### New Capabilities
- `client-product-create`: Preview-backed product creation form, validation, and its explicit non-inventory boundary.

### Modified Capabilities
- `client-product-list`: Add a creation entry point and refresh the list after a successful preview create operation.

## Impact

- Client areas: `Views/Catalog`, `ViewModels/Catalog`, `Models/Catalog`, `Repositories/Catalog`, `Services/Dialog`, and DI registration in `App.xaml.cs`.
- Related existing client behavior: product list refresh after successful creation.
- API/backend: no endpoint, HTTP repository, ApiClient, JWT, server authorization, inventory mutation, or backend edit is included. Current backend catalog and inventory files contain entities/schema guidance but no product or receipt controllers/services; the shared request folder is documentation only.
- Dependencies not ready: real category/product API contracts, inventory receipt workflow and Serial/IMEI intake (UI-09/UI-13), and any draft persistence contract. These require separate changes.
