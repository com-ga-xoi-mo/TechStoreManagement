# Design

## Context

See proposal.md for motivation and specs for observable behavior. UI-08 already owns the persistent inventory workspace and category tree. `InventoryViewModel` currently switches between category, stock, and Serial/IMEI tabs, with the latter two unfinished; `ICategoryRepository` already supplies category filter options. The client has no stock model or inventory repository. The backend has an `InventoryStock` balance entity with `Quantity`, `ReservedQuantity`, and `MinStockAlert`, but no inventory list controller/service or response contract was found. Its balance uniqueness is per product and optional variant.

The implementation follows the repository's active client conventions where they differ from earlier architecture notes: Uno native XAML, CommunityToolkit.Mvvm, MVVM, and DI, with the Model, View, ViewModel, Repository, and Service areas at the same client level. This change adds no HTTP or backend behavior.

## Goals / Non-Goals

**Goals:**

- Deliver the D08 stock table experience within the existing inventory tab, with product/variant identity, category, selling price, physical/reserved/available quantities, status, filters, and paging.
- Keep preview data deterministic, explicitly labelled, and isolated behind an inventory repository so a future real contract can replace the source.
- Preserve the existing category tree behavior and leave Serial/IMEI visibly unfinished.
- Keep the UI read-only and usable by keyboard at normal and narrower usable desktop widths.

**Non-Goals:**

- Import/export, stock receipt, adjustment, detail dialogs, deletion, multi-warehouse selection, stock mutation, or a real API client.
- Treating preview quantities, status thresholds, or sample data as live inventory or backend authorization.

## Decisions

### Balance row and quantity semantics

Represent one row per product plus optional variant, matching the backend balance entity's grain. The Model carries physical quantity, reserved quantity, available quantity, alert threshold/status, product and optional variant identifiers/names/SKUs, category identity, and selling price. Available is `Quantity - ReservedQuantity`; preview fixtures must satisfy nonnegative quantity and `0 <= ReservedQuantity <= Quantity`. Do not expose cost price in the stock table. This avoids silently aggregating variant balances or implying a different inventory invariant.

**Alternatives considered:** one row per product would hide variant-level balances and require an undocumented aggregation rule; using only an available quantity would conflate reserved stock with physical stock.

The fake repository supplies a consistent low-stock status using its fixture threshold. The real status thresholds, returned price source, and exact response semantics remain a later integration contract because the backend currently has no inventory query contract.

### Repository boundary and preview source

Add `IInventoryRepository` beside its fake and unavailable implementations under the existing `Repositories/Inventory` area. It accepts a query containing search, category, stock status, and page; it returns the page rows, total row count, and data provenance. `StockListViewModel` calls only this interface. Reuse `ICategoryRepository.GetCategoriesAsync` for filter choices rather than creating another category source.

Register the fake repository only when the existing explicit preview mode is active; outside preview mode register an unavailable implementation that returns a clear not-configured error. The UI must label fake rows as preview data. No `HttpClient`, API interface, HTTP repository, endpoint guess, token work, or backend change is part of this design.

**Alternatives considered:** direct fixture access from the ViewModel would couple presentation to data; a fake HTTP endpoint would blur the missing real contract and preview behavior.

### Filtering, paging, and request freshness

Use eight rows per page, matching D08. Search checks product name and product or variant SKU; category and status criteria combine with the search. Any criteria change resets to page one. The repository applies filtering before paging and provides a total count for page navigation. Use cancellation plus a monotonically increasing request generation (or equivalent latest-query guard) so stale completions cannot replace newer results. A retry reuses the current query. Tab activation triggers the initial stock load lazily; returning to categories does not discard the category tree snapshot already managed by UI-08.

**Alternatives considered:** client-side filtering after paging would make page counts and filters misleading; allowing overlapping requests to apply in completion order can show results for stale criteria.

### View composition and scope trim from D08

Implement the stock content as a native XAML view hosted by the existing `InventoryPage`. Use a compact filter area, table/grid with distinct columns for physical, reserved, and available quantities, clear preview and query-state feedback, and paging controls. Do not add the D08 Import/Export header actions, warehouse selector, or row actions for details/adjustment because their contracts/dialogs are outside UI-09. Do not show hardcoded dashboard or tab counts from the design sample.

At narrower usable widths, let the content scroll or reflow while preserving access to filter controls and all three quantity values; keep keyboard navigation, visible focus, and accessible labels. Owner manual acceptance is a separate task and must remain unchecked until the owner confirms.

### Tests and source layout

Keep the ViewModel independent of Uno controls and HTTP. Add or update headless tests under `tests/Frontend.UnitTests`, including the required linked source entries in that test project. Cover combined filters, page reset/page boundaries, quantity invariants, loading/empty/error/retry, cancellation, and an earlier delayed request completing after a newer one. UI smoke/build checks cover the native view and workspace integration.

## Risks / Trade-offs

- [Preview data could be mistaken for live stock] → Label the source visibly, register it only in explicit preview mode, and use an unavailable repository outside that mode.
- [Available quantity or low-stock status could differ from the eventual API contract] → Keep semantics explicit in the Model and fixtures; record the contract dependency and avoid describing preview values as production behavior.
- [Variant rows may make the table wider] → Preserve the separate quantity columns and allow horizontal scrolling/reflow at narrow widths; verify with owner manual acceptance.
- [Category filter load can fail independently] → Reuse the category repository state/error handling and do not fabricate category choices; allow the stock list to show its own query state separately.

## Migration Plan

No backend or database migration is needed. Add the read-only preview flow behind the existing DI preview selection. When a real inventory response contract is available, introduce a separate integration change that maps that contract into the stock Model and selects the HTTP repository through DI; retain the fake source for preview and tests. Rollback is removing the stock tab content and DI registrations, restoring the existing unfinished stock placeholder.

## Open Questions

- The future inventory API contract must define exact low-stock thresholds, selling-price source, query/filter support, and paging/count semantics. These do not block the preview implementation because the fake source owns its sample values and the UI makes their provenance explicit.
