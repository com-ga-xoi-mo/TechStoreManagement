# Tasks

UI-03 ends at **UI/mock complete**. The real Analytics API and server-authoritative definitions are a separate integration dependency. Tick a task only after its entire stated verification has passed; missing evidence or an unavailable target remains `[ ]`. Reuse UI-00, UI-01 and UI-02 code; do not implement destination business pages, HTTP AnalyticsRepository, JWT or backend changes here.

## 1. Analytics model and deterministic mock

- [x] 1.1 Define `Models/Analytics` types for selected range, snapshot/as-of/provenance, four KPI values, comparison, dated revenue points, low-stock items and recent orders with `decimal` money; verify a headless model test can distinguish period totals from current inventory snapshot values.
- [x] 1.2 Add `IAnalyticsRepository` and `FakeAnalyticsRepository` together under `Repositories/Analytics` with cancellation-aware async loading and a single internally consistent synthetic dataset; verify executable tests derive whole-range revenue/order count independently of the five displayed recent orders and use no D01 figure as a hard-coded KPI.
- [x] 1.3 Implement provisional mock range boundaries in `Asia/Ho_Chi_Minh` for Hôm nay, inclusive last seven local days and Tháng này, plus immediately preceding equal-length comparison; verify boundary tests for first/last included instant, month boundary, zero prior denominator, chart bucket sum and newest-five ordering.
- [x] 1.4 Supply controlled populated, no-sales, delayed, error and one-point chart fixtures through DI/test seams, with a documented Debug-only preview setting to reach each state in the running app and no visible design-state switcher; verify executable tests reach each outcome/cancellation and a desktop preview run can select each fixture. Document fixture meanings and provisional metric definitions next to the repository.

## 2. Dashboard ViewModel state and commands

- [x] 2.1 Add `DashboardViewModel` with CommunityToolkit.Mvvm observable selected period, snapshot, loading/empty/error and retry state; inject only `IAnalyticsRepository` and UI-independent dependencies. Verify a headless test loads the default 7 ngày qua range and exposes all four cards, points and lists from the same snapshot.
- [x] 2.2 Make period commands cancel/obsolete prior requests and atomically publish only the latest selected result; verify headless tests for rapid Hôm nay → Tháng này switching, failure followed by retry, and no stale overwrite after leaving/re-entering the page.
- [x] 2.3 Expose formatted values, comparison-unavailable text and textual chart point summaries without putting money strings into repository models; verify tests for `0`, null prior comparison and Vietnamese currency/date presentation or focused converter behavior.

## 3. Native Dashboard page and chart

- [x] 3.1 Create `Views/Dashboard/DashboardPage.xaml(.cs)` using D01's four-card/chart/low-stock/recent-order composition and existing D31 resources, with a visible “Dữ liệu mẫu / Preview” label; verify Uno desktop runtime shows the sections and no copied D01 administrator identity or fake live-data claim.
- [x] 3.2 Bind Hôm nay, 7 ngày qua and Tháng này controls to VM commands and show the chosen range, loading, empty, error and retry feedback; verify in the running desktop app that each choice updates all period-dependent sections together while current inventory stays labelled as-of.
- [x] 3.3 Implement the revenue chart with native XAML shapes bound to VM points and a readable date/amount equivalent; verify desktop runtime for populated, one-point, all-zero and no-sales data, resize behavior and keyboard-readable values without relying on hover or color. Build Windows target on an available Windows host, otherwise record it as unverified.
- [x] 3.4 Make low-stock rows and up to five newest orders readable with quantity/unit, status and localized amount, and reflow/scroll at a narrower usable desktop width; verify desktop runtime has no inaccessible fields or horizontal clipping and matches the mock snapshot. Do not turn these rows into destination pages.
- [x] 3.5 Keep “Xem tất cả”, product and order affordances unavailable or route them to a clear “chưa triển khai” message until their own UI changes exist; verify activation never opens a fabricated detail screen and the Dashboard remains usable.

## 4. Shell integration and acceptance

- [x] 4.1 Register the mock analytics source and Dashboard VM in DI and map only `RouteId.Dashboard` to `DashboardPage` in `UnoFrameAdapter`; verify preview login opens Dashboard, Back/return keeps Shell selection correct and four other business routes still show their existing placeholders. Add a focused navigation test if the route mapping can be tested headlessly.
- [x] 4.2 Run `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`, the focused frontend tests, and an Uno desktop smoke run through explicit UI-02 preview for filters, data states, chart, Tab/Enter/Escape/focus and narrow-window use; record exact commands, results and unavailable Windows target checks. A build alone does not prove native interactions.
- [x] 4.3 Record a UI-03 handoff near the Dashboard view: D01 comparison, changed files, chosen chart control and target evidence, mock data definitions, test/runtime results, and the separate real API dependency (settlement/refunds, date/timezone, comparison, stock thresholds, paging and RBAC); verify it does not claim backend analytics or real permissions.
- [x] 4.4 **Owner manual acceptance, last gate:** after agent build/tests and runtime smoke test, ask the project owner to try Dashboard in their normal desktop window and a narrower usable window: Vietnamese labels, card/chart/list readability and scroll, three filters, loading/empty/error/retry, keyboard/focus and unavailable links. Record the owner's dated confirmation and each remaining failure in the handoff. Keep `[ ]` until the owner confirms the complete visual/interaction checklist; do not require a screenshot, exact scale or window dimensions.
