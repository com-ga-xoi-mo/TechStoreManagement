# Tasks

## 1. Stock model and repository contract

- [x] 1.1 Add stock balance Model and query/page result types for one product plus optional variant per row; validate `Quantity >= 0`, `0 <= ReservedQuantity <= Quantity`, and `Available = Quantity - ReservedQuantity` with focused unit tests.
- [x] 1.2 Add `IInventoryRepository` and deterministic fake/unavailable implementations beside each other; verify the fake returns coherent preview fixtures with at least two pages, category IDs from the existing category options, and status consistent with fixture alert thresholds.
- [x] 1.3 Add repository tests for name/SKU search, category and status filters combining, filtering before paging, total-count/page boundaries, and empty results; run the focused frontend unit-test filter.

## 2. ViewModel query behavior

- [x] 2.1 Add `StockListViewModel` with loading, populated, empty, error/retry, preview provenance, filter, and paging state; verify its source links only to the repository interface and category options reuse `ICategoryRepository`.
- [x] 2.2 Implement criteria changes resetting to page one and loading the current query; verify combined criteria and page reset through headless ViewModel tests.
- [x] 2.3 Prevent stale results with cancellation and a latest-request guard; verify a delayed earlier request cannot overwrite a newer result and cancellation leaves the latest state coherent.
- [x] 2.4 Add headless tests for loading, empty, error, retry, and preview identification, linking any new plain C# source files into `tests/Frontend.UnitTests` as needed; run the frontend unit-test project.

## 3. Stock tab interface

- [x] 3.1 Create native XAML stock-list view with product/variant, category, selling price, physical, reserved, available, and status columns; verify all three quantity values remain separate and no import/export, warehouse, or row-action controls are present.
- [x] 3.2 Add search, category/status filters, paging, loading/empty/error/retry and preview label bindings; verify keyboard Tab order, Enter/Space activation, visible focus, and accessible labels in the running desktop UI.
- [x] 3.3 Ensure narrow usable desktop widths retain access to filters, paging, and all quantity distinctions through reflow or scrolling; verify by running the app at normal and narrowed usable widths.

## 4. Inventory workspace and DI integration

- [x] 4.1 Host the stock view in the existing Tồn kho tab and load it lazily on first activation; verify Danh mục remains the default and the Serial/IMEI placeholder remains unchanged.
- [x] 4.2 Preserve the category tree snapshot when switching between Danh mục and Tồn kho; verify tab switching does not trigger an unnecessary category reload.
- [x] 4.3 Register fake inventory repository only for explicit preview mode and unavailable repository otherwise; verify preview shows labelled sample rows and non-preview reports an unavailable state without live-data claims.

## 5. Integration verification and owner acceptance

- [x] 5.1 Run frontend unit tests and build the desktop target; build the Windows target when the environment supports it, recording commands and results in the handoff.
- [x] 5.2 Launch the desktop app and smoke-test inventory navigation, filter/paging interaction, three quantity columns, and query feedback; record any runtime issues and confirm no backend endpoint or database behavior was added.
- [x] 5.3 After build and launch are verified, ask the owner to manually test the stock tab at the normal window size and a narrower still-usable width, including keyboard filters, paging, and retry; record the confirmation date and any unmet behavior here, and leave this task unchecked until the owner confirms. Owner confirmed all checks passed on 2026-10-11; no unmet behavior reported.
- [x] 5.4 Document the missing real inventory contract (balance grain, physical/reserved/available semantics, status threshold, selling-price source, filter/paging/count support, permissions, and multi-warehouse behavior) as a dependency for a separate integration change; verify the handoff explicitly labels all current inventory rows as preview data.
