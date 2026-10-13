# Design

## Context

The Inventory Shell route currently resolves to the generic placeholder through `UnoFrameAdapter`. `ICategoryRepository` already serves flat `CategoryOption` values to product create/edit flows; its fake and unavailable implementations are registered explicitly by preview mode. The backend has a self-referencing Category entity with name, description, parent, and active fields, but repository inspection found no category query service/controller or response contract, and no defined product-count semantics for a tree response.

The D16 reference shows a category/inventory heading, three tabs, name search, active-state filter, parent/child rows, product counts, active status, row actions, and paging. This change implements the category tree and shared tab frame only; row mutation controls and stock/Serial tab contents are out of scope.

## Goals / Non-Goals

**Goals:**

- Attach an Inventory page to the existing route without replacing the Shell.
- Reuse and extend `ICategoryRepository`, keeping its existing flat options query intact.
- Load a complete fake category-tree snapshot through explicit preview DI and provide an explicit unavailable implementation outside preview mode.
- Show hierarchy, description, status, repository-provided counts, name search, active-state filter, and loading/empty/error/retry states.
- Keep category data separate from product-list paging and mark preview data clearly.

**Non-Goals:**

- Stock and Serial/IMEI screen content, API calls, JWT, category CRUD or active-state mutation.
- A backend endpoint, database migration, or a claim that preview counts or access are authoritative.
- Inventing a count contract or calculating category counts from the currently loaded product page.
- Reproducing D16's add/edit/delete actions. The visual pager is deferred until a hierarchy-safe category paging contract is defined; the preview shows the complete tree so parent/child relationships are not split across pages.

## Decisions

1. **Route only Inventory to the new workspace.** Update `UnoFrameAdapter` to resolve `RouteId.Inventory` to `InventoryPage`; preserve the generic placeholder behavior for other unfinished routes. This is a narrow route composition change and keeps the persistent Shell unchanged.

2. **Use a small tab frame with a category-only feature ViewModel.** `InventoryViewModel` owns selected tab and the two explicit placeholder states. `CategoryListViewModel` owns tree load/filter/expand state and receives `ICategoryRepository` through constructor injection. Category load is triggered when the category tab is first selected; stock and Serial/IMEI do not trigger data calls.

3. **Extend the existing category repository instead of adding a parallel repository.** Add a full-tree read method while retaining `GetCategoriesAsync` for product forms. Add a typed `CategoryNode` client Model. Fake and unavailable implementations implement both methods; the fake provides deterministic parent/child fixtures and selectable populated/empty/delayed/error scenarios, while the unavailable implementation fails explicitly. The fake is registered only in explicit preview mode.

4. **Treat counts as repository data and filters as presentation state.** The UI displays the count value attached to each category node and never sums or infers it from a paged product list. The D16 image mixes “sản phẩm” and “SKU”; until a backend contract defines units and whether parent counts include descendants, preview fixture counts are visibly synthetic and use a consistent product-count label. Real integration must settle count unit, descendant aggregation, and paging behavior.

5. **Keep tree structure intact while filtering.** Search is case-insensitive over category names and status filtering is local over the loaded tree. A matching descendant remains visible with its ancestor context; matching paths expand to the descendant. Expand/collapse state otherwise stays in the tree item presentation model. No paging is applied to the tree because splitting a hierarchy can orphan children; a later API-backed design can add paging only with a root-group or equivalent hierarchy-preserving contract.

6. **Use native XAML and standard controls.** Build the header, tab strip, search/filter bar, and tree rows with Uno/WinUI XAML controls and existing D31 theme resources. Use accessible buttons for tabs and disclosure actions, read-only status text/badges, and a vertically scrolling content area. No embedded HTML or image screenshot is used as UI.

## Risks / Trade-offs

- **[Category endpoint and product-count semantics are not defined] →** Keep this change preview-only; record the required query shape, category authorization, count units/aggregation, and hierarchy paging contract for a later integration change.
- **[Filtering may hide context if implemented as a flat row filter] →** Filter recursively and retain ancestors for matching descendants; test parent/child visibility and empty results.
- **[Adding a method to `ICategoryRepository` affects product form test doubles] →** Update every implementation/test double and preserve the existing flat options method; run frontend unit tests and desktop build.
- **[Uno control behavior can differ by target] →** Use standard controls and verify desktop runtime, keyboard behavior, and a narrower window during owner acceptance.

## Migration Plan

No data or backend migration is included. Register the new page and view models in the existing frontend composition root, extend current category repository implementations, route the Inventory destination to the page, and retain the current preview-only/unavailable selection policy.

## Open Questions

- Before real API integration, backend owners must define the category-tree response, product-count unit and aggregation, active-status visibility rules, paging behavior, and required permission. These do not block UI/mock implementation.
