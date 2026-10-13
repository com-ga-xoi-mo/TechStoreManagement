# Proposal

## Why

The Shell currently routes Sản phẩm to a placeholder, so staff cannot inspect or find catalog items in the desktop client. UI-04 establishes the first usable product-browsing screen against a deterministic mock repository while the product API contract is still undefined.

## What Changes

- Add a responsive product-card grid with search, category/brand/price/stock filters, sorting, paging, and loading, empty, error, and retry states.
- Keep search and filter requests cancellation-aware so stale results cannot replace the latest selection; reset paging when criteria change.
- Route the existing Sản phẩm destination to this screen while preserving the shared Shell and its other destinations.
- Use an explicitly selected mock repository through DI; identify mock data as preview data and do not add HTTP calls, JWT handling, or backend changes.
- Leave list/table switching, product creation, detail, edit, and card action workflows for their own changes.

## Capabilities

### New Capabilities
- `client-product-list`: Browse a paged, searchable, filterable product catalog in a responsive grid, with explicit preview data and result states.

### Modified Capabilities
- None. The existing Shell already specifies that selected destinations show their matching page or an explicit placeholder; this change supplies the Catalog page without changing Shell behavior.

## Impact

- Frontend: `Models/Catalog`, `Repositories/Catalog`, `ViewModels/Catalog`, `Views/Catalog`, application DI registration, and the existing Shell route adapter.
- Tests: headless model/ViewModel tests for combined criteria, paging, cancellation/stale requests, and result states; Uno desktop build and runtime smoke verification.
- API/backend: no changes. No product list endpoint, request/response DTO, pagination/filter contract, category/brand source, effective stock threshold, or image policy is currently present in the client API layer; these remain dependencies for a separate real-integration change.
