# Tasks

> Checklist rule: mark a task complete only after its stated implementation and verification both pass. If the evidence is missing or uncertain, leave it unchecked.

## 1. Catalog models and preview repository

- [x] 1.1 Complete the typed catalog query/result models for category, brand, price band, all four stock states, sort, paging, product image, and variant availability; add executable xUnit coverage for VND boundaries (including exactly 10M and 25M), nullable image and page metadata in `tests/Frontend.UnitTests`.
- [x] 1.2 Complete `FakeProductRepository` filtering and sorting for every D23 choice: case-insensitive name/SKU, category, brand, price based on the documented product starting price, stock state, newest and price/stock ordering; use deterministic tie-breaking and verify combined-filter totals and eight-item paging with executable repository tests.
- [x] 1.3 Add explicit Debug preview scenarios for populated, empty, delayed, error and missing-image results, selectable through configuration without an in-app scenario panel; update the Catalog README and verify each scenario can be selected in the running app. Owner confirmed all preview scenarios passed manual acceptance on 2026-10-10.

## 2. Product list ViewModel behavior

- [x] 2.1 Complete `ProductListViewModel` state and criteria for search, category, brand, price, stock, sort and paging; add executable headless tests for loading, populated, empty, error, retry and count/page presentation.
- [x] 2.2 Connect text search to a 300 ms debounce and connect every filter/sort control to a combined query that resets to page one; verify with ViewModel tests that each changed criterion is sent and the total count corresponds to the full filtered set.
- [x] 2.3 Make query execution latest-wins, including cancellation, stale success/error suppression and route exit/re-entry; verify using a controlled repository that deliberately ignores cancellation and completes requests out of order.
- [x] 2.4 Complete retry and per-card variant selection so the selected variant updates only its card's price, SKU and available quantity/status; verify each behavior with executable tests.

## 3. Native Uno product grid and Shell integration

- [x] 3.1 Complete the D23 header and responsive native grid layout using D31 resources; verify in the running `net10.0-desktop` app that cards reflow and vertical scrolling works in normal and narrower usable windows without horizontal clipping. Owner confirmed normal and narrower window behavior passed manual acceptance on 2026-10-10.
- [x] 3.2 Add and bind visible category, brand, price and stock filters, search, all D23 sort choices including stock descending, result count and paging controls; verify each can be operated at runtime and by keyboard, and page commands disable at the correct boundaries. Owner confirmed runtime and keyboard operation passed manual acceptance on 2026-10-10.
- [x] 3.3 Bind card images to their image source and show the fallback only for missing/failed images; display name, SKU, brand, selected variant, price, available quantity and localized textual stock status. Verify populated, failed-image and variant fixtures in the running app; controls must remain keyboard reachable. Owner confirmed image fallback, card information, variant selection and keyboard reachability passed manual acceptance on 2026-10-10.
- [x] 3.4 Make loading, empty, error and retry states mutually clear and reachable through the configured fixtures; verify runtime retry preserves the current criteria and the selected mock source. Owner confirmed loading, empty, error and retry behavior passed manual acceptance on 2026-10-10.
- [x] 3.5 Map `RouteId.Catalog` to `ProductListPage` in `UnoFrameAdapter`; verify preview login -> Sản phẩm opens the actual list, Back/return preserves Shell selection, and Dashboard plus the other three unfinished business routes keep their existing behavior. Owner confirmed route navigation and placeholders passed manual acceptance on 2026-10-10.

## 4. Executable verification, handoff and acceptance

- [x] 4.1 Link the Catalog model/repository/ViewModel sources and real Catalog xUnit tests into `tests/Frontend.UnitTests/Frontend.UnitTests.csproj`; verify `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj` discovers and passes the Catalog tests rather than relying on static `VerifyAsync` helpers compiled with the app.
- [x] 4.2 Run the frontend test command and `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`; run the explicit-preview desktop app through route entry, filters, sorting, paging, loading/empty/error/retry and record exact commands and results. Build the Windows target only on an available Windows host; otherwise record it as unverified. Re-run on 2026-10-10: 59 tests passed; desktop build succeeded with 0 warnings/errors. Owner confirmed preview runtime scenarios and interactions passed; Windows target remains unverified on this Linux host.
- [x] 4.3 Correct the Catalog handoff to distinguish implemented behavior from pending work, list actual test/build/runtime evidence, remove the earlier guessed endpoint claim and nonexistent task 5.x references, and document the real API contract dependencies without inventing an endpoint.
- [x] 4.4 **Owner manual acceptance gate:** only after tasks 4.1–4.3 pass, ask the project owner to test the running list in their normal desktop window and a narrower usable window. Check card readability/scroll, Vietnamese labels, search/filter combinations, sort/page controls, variant selection, image fallback, loading/empty/error/retry and keyboard/focus. Record the dated confirmation and unmet behavior in the handoff. Do not require exact dimensions/scale or a screenshot; leave this task unchecked until the owner confirms. Owner confirmed all requested checks passed on 2026-10-10; no unmet behavior reported.
