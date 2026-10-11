# Design

## Context

See [proposal.md](proposal.md) and both delta specs. Archived UI-00 supplies D31 theme tokens, UI-01 supplies `ShellPage`, `RouteId.Dashboard` and a `UnoFrameAdapter` that currently maps every business route to `PlaceholderPage`, and UI-02 supplies explicit preview login. Current backend source has order and stock entities but no implemented analytics endpoint or shared dashboard DTO; descriptions in `backend/Customers/README.md` and values in D01 are plans/examples, not live contracts. D01's `screen.png` and `code.html` show four cards, three period choices, a bar/line trend, low-stock panel and five-order table; its figures, customer identifiers and “Admin” footer are illustrative. Preserve the current Shell/footer rather than copying D01's old sidebar.

## Goals / Non-Goals

**Goals:** Make the Dashboard route reviewable with consistent mock analytics, understandable data provenance, deterministic time semantics and an accessible chart. Keep the VM testable without Uno controls.

**Non-Goals:** Live analytics, financial reconciliation against the database, HTTP/JWT, token persistence, backend aggregation, authorization policy, destination order/product/inventory pages, or copying D01's synthetic personal details into a production-looking data source.

## Decisions

### 1. Data boundary and route reuse

Use `Models/Analytics/DashboardSummary` plus typed revenue points, low-stock items and recent-order rows; `Repositories/Analytics/IAnalyticsRepository` and `FakeAnalyticsRepository` sit together. `DashboardViewModel` accepts only the interface, coordinates range selection and exposes primitive/typed state through CommunityToolkit.Mvvm. `App.xaml.cs` registers the fake for explicit preview and the VM through DI; no analytics HTTP implementation is added. `Views/Dashboard/DashboardPage.xaml` binds to that VM. Change only the Dashboard branch of `UnoFrameAdapter.NavigateToView`; all other business routes continue to use the existing placeholder. Preserve the current login gate and Shell footer. Alternative of putting metrics in `ShellViewModel` would couple navigation to analytics and duplicate route state.

### 2. Snapshot contract and provisional mock definitions

One repository call returns a snapshot for a selected range with `RangeStart`, `RangeEndExclusive`, `AsOf`, source/provenance, four KPI values, optional prior-period comparison, dated chart points, current low-stock items and at most five period-filtered recent orders. It also carries any display label needed to avoid mixing periods. Money remains `decimal`; formatting to Vietnamese đồng occurs only at presentation. The mock uses an internally consistent synthetic order/stock dataset rather than independent D01 numbers; KPI totals are calculated over its complete eligible dataset, never the five visible orders.

For deterministic UI tests, anchor the mock clock at one injectable instant in `Asia/Ho_Chi_Minh`. Hôm nay means local calendar day `[00:00, next 00:00)`, 7 ngày qua means today and six preceding local days, Tháng này means local first day through the next month boundary. The mock's comparable prior period is the immediately preceding interval of the same number of calendar days, labelled “kỳ trước”; if prior revenue is zero/missing, percentage is unavailable. Today groups chart points by hour; longer ranges by local day. Treat zero-sales buckets as zero, not missing data. A single selected-range result updates revenue, count, comparison, trend and recent orders atomically. Total products and low-stock values are **current as-of** snapshot metrics and keep that label when the sales period changes.

Provisional mock semantics: revenue is gross `TotalAmount` of completed/settled synthetic orders in the selected interval, with pending/cancelled/refunded orders excluded; order count uses the same eligibility. Product count means distinct active sellable products in the synthetic inventory. A low-stock item is a product/variant stock record where available quantity (`Quantity - ReservedQuantity`) is at or below its configured `MinStockAlert`; count means matching stock records, not units. The mock has no partial returns. These are **UI fixture definitions only**. A real-integration change must agree with the backend on settlement timestamp/status, refunds/net-vs-gross revenue, status mapping, active product/variant counting, multi-warehouse scope, threshold behavior, timezone, previous-period comparison, grouping, data freshness, pagination and RBAC before any figures are described as authoritative. Alternative of hard-coding D01's 482.650.000 ₫/128/1.420/7 would hide contradictions between cards and lists.

### 3. Native chart with cross-target baseline

Use WinUI/Uno XAML `Canvas`/`Rectangle`/`Path` or `Polyline` primitives inside a small Dashboard chart control bound to ViewModel-provided points. Limit code-behind to geometry/resize and focus/tooltip presentation; keep scaling and date/value semantics in testable UI-independent helpers where practical. For one point draw a bar/marker; for multiple points connect only actual adjacent buckets. Render axis/date labels and a textual point list or accessible names so numbers are available without hover or color. Do not add a chart package to this UI change. Uno's official [shapes support](https://platform.uno/docs/articles/features/shapes-and-brushes.html) and [Canvas implementation](https://platform.uno/docs/articles/implemented/microsoft-ui-xaml-controls-canvas.html) cover the Skia path; Windows native uses WinUI shapes. Validate desktop build/runtime here and Windows target on a Windows host if available. Alternative of HTML/WebView or a new chart library adds a second rendering stack and unverified package compatibility for one small chart.

### 4. Request lifecycle, data states and links

Use a monotonically increasing request version plus `CancellationTokenSource` when period changes or the page unloads. Only the latest completed request may publish its snapshot; loading and error states belong to the selected range. Retry reissues the current range through the same repository. The fake provides deterministic populated, empty, delayed, error and one-point scenarios via controlled fixtures/injection for executable tests. Make those scenarios reachable in runtime preview through a documented Debug-only setting such as `TECHSTORE_DASHBOARD_SCENARIO=empty|error|delayed|onepoint`, read only when explicit preview mode is active; do not expose design-only state-switch buttons in the product UI. Keep empty sales and current inventory distinguishable. Bind order/product/“Xem tất cả” affordances to an existing unavailable-destination message/disabled state until corresponding routes are implemented; never open a fabricated detail page. Alternative of retaining stale cards under a new selected label risks misleading figures.

## Risks / Trade-offs

- Mock metrics may be mistaken for business truth → visible “Dữ liệu mẫu / Preview” label, synthetic identifiers and a handoff note separating UI/mock completion from real analytics.
- Chart may render differently between Skia and WinUI → native primitives, readable text equivalent, desktop smoke test and Windows verification on an available Windows host; report unavailable platforms honestly.
- Wide D01 order table may clip on narrow windows → reflow rows or provide a clearly reachable scroll area; owner manual test at normal and narrower usable widths, without an exact scale target.
- Period changes may race or show inconsistent sections → single snapshot, cancellation and latest-request guard with focused headless tests.
- Real aggregation definitions are not agreed with backend → keep the above definitions explicitly provisional, record the missing contract and leave HTTP integration to another change.

## Migration Plan

1. Add typed analytics models, mock repository and focused tests for period boundaries, KPI/chart/list consistency and stale-request suppression.
2. Register DI and replace only the Dashboard placeholder route. Build the D01-inspired page with UI-00 controls, current Shell and native chart.
3. Run frontend desktop build, focused tests and app smoke tests in explicit preview for populated/empty/error/retry, filter switching, keyboard/focus and narrow layout. Check Windows only where a Windows host is available; document any unverified target.
4. Ask the owner to test the running page in their normal and narrower usable windows after agent checks; record a dated confirmation or failures. Mark UI/mock complete only after that gate. Document the real analytics contract dependency separately.

Rollback changes the Dashboard frame mapping back to `PlaceholderPage` and removes Dashboard-only DI; it does not alter UI-02 login or other routes. No database migration is part of UI-03.
