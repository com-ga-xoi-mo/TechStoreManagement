# Design

## Context

See `proposal.md` for the motivation. The Dashboard currently has a XAML page, fixture-only and unavailable repository registrations, and a ViewModel whose period control is enabled only in demo mode. `AdminDashboardDto` and `GET /api/analytics/dashboard` are already implemented; the API is Admin-protected, accepts `today`, `last7Days`, and `thisMonth`, and returns an as-of snapshot with UTC timestamps, UTC+07 dates, summary values, daily revenue, low-stock balances, and recent orders. `IApiClient` already owns bearer transport, cancellation, normalized errors and current-session 401 expiration. `PageViewModel` already tracks activation and latest-request generations. Existing `DashboardSnapshot`/rows use `Demo`-specific semantics, including `DemoDisplayId` and a serial label absent from the live DTO.

## Goals / Non-Goals

**Goals:**
- Keep a single presentation model for preview and live sources while retaining source metadata for truthful labels.
- Preserve latest-request and page-activation behavior while adding session-generation ownership to prevent cross-session commits.
- Use the existing endpoint and transport contract without duplicating auth, query, or analytics aggregation in the client.
- Keep the view declarative C# Markup and use the established chart geometry, controls, design resources and route lifecycle.
- Match the visual hierarchy and composition in `docs/sample_dashboard.png` while preserving live data truth and existing API contracts.

**Non-Goals:**
- Change Analytics API, RBAC, seed data, order lifecycle, Shell login/logout workflow, or shared DTO contracts.
- Add Dashboard drill-down, order actions, persistent period preferences, or new comparison analytics.
- Expand this change into all Sprint 3 integration/performance QA.
- Add customer phone numbers or IMEI/serial identifiers to the Analytics API/DTO. The current API does not return those fields; never create placeholder or inferred values to imitate the screenshot.

## Decisions

### Live repository selection and DTO mapping

Add an `AnalyticsApiRepository` that depends only on `IApiClient` and calls `api/analytics/dashboard?period=<key>`, mapping `Today` → `today`, `Last7Days` → `last7Days`, and `ThisMonth` → `thisMonth`. Deserialize the existing shared `AdminDashboardDto`; do not create a duplicate API DTO or call backend endpoints directly from the ViewModel. Register it as `IAnalyticsRepository` when preview is disabled; retain `FixtureAnalyticsRepository` only when preview is explicitly enabled. Remove `UnavailableAnalyticsRepository` from normal selection, or retain it only if another caller still needs an explicit no-source test double. No catch in DI/repository may turn an API exception into fixture or unavailable success.

Map `StartDate`/`EndDate` unchanged, validate `TimeZone` equals the current contract value `UTC+07:00`, and interpret `AsOf`/recent `CreatedAt` as UTC before constructing `DateTimeOffset`. Map summary `ActiveProductCount` to the product KPI and preserve nullable comparisons. Map low-stock `ProductName`, optional `VariantName`, `AvailableQuantity`, `Quantity`, `ReservedQuantity`, and `MinStockAlert`; use a readable no-variant label without fabricating stock. Map `OrderCode`, optional customer name (display “Khách lẻ” when null), product summary, total, parsed real status, and UTC creation time. Do not synthesize a serial field because the API does not return one. Malformed period/status/time-zone/required fields fail as invalid data; they must not render as empty or zero.

### Presentation state and refresh behavior

Keep the current default period `Last7Days`. Make period selection available whenever the source is live or preview and a request is not in flight; route every period change and Refresh through the same latest-request pipeline. Clear the previous snapshot while loading so a new period label cannot appear alongside old KPI data. A successful response with zero summary values is Content; empty recent/low-stock lists show local empty messages while the other panels remain visible. A transport, status, serialization, or mapping error becomes Error with retry for the currently selected period. Keep messages safe and distinguish 401/session-expired from 403/access-denied without exposing raw error bodies.

The snapshot `SourceKind` distinguishes `Live` from `Preview`; `DashboardReadResult.Unavailable` is no longer a production path. Preview retains its explicit banner/date. Live mode shows API period/as-of metadata and real order codes; the current XAML copy that calls live rows “minh họa” is removed. Low-stock presentation should surface available quantity and threshold and may include physical/reserved quantities from the DTO; it must not relabel available as physical balance.

### Authentication and session changes

Continue to rely on `BearerTokenHandler`: the request uses the token snapshot current when sent; a 401 expires only that matching active generation. A 403 keeps the session. Anonymous 401 is shown as a sign-in-required error and does not create or expire a session. Dashboard observes `ISessionStore.SessionChanged` for its activation lifetime (attach on activation/load and detach on deactivation/lease disposal). An `Expired` event cancels work, clears the snapshot, and sets an authentication-expired Error itself because the handler may cancel the request before its `ApiException` reaches the ViewModel. An `Anonymous` event from logout cancels work, clears the snapshot and shows sign-in-required Empty state. A new `Active` generation cancels/clears old data then requests the selected period for that generation. Session callbacks must not start work on a deactivated ViewModel.

Request success/error/finalization commits only if its latest-request generation, page activation and captured session generation are still current. The existing `RunLatestRequestAsync` guards page/latest generations; extend its integration or add a Dashboard-specific session-generation guard at commit time rather than reimplementing transport. Expected cancellation from period change, navigation, window close or logout returns to the current appropriate state and never displays an error. Logout leaves the Shell itself navigable and Dashboard in a cleared sign-in-required state; Shell navigation/auth flow is not changed here.

### C# Markup view

Replace `DashboardPage.xaml` and `.xaml.cs` with one `DashboardPage.cs` class inheriting `Page` and implementing `IRoutePage`, following the existing `SettingsPage`/C# Markup patterns. Build the four KPI cards, period selector, refresh action, error/empty states, revenue panel/chart table, low-stock list and recent-order list in code using fluent bindings and existing shared controls/resources. Keep view-only responsive arrangement and chart drawing in the page; reuse `RevenueChartGeometry` and ensure labels/table remain accessible without hover. Hook/unhook any view-level events at Loaded/Unloaded and do not move control references into the ViewModel. Remove XAML only after the C# page is registered/buildable. DI keeps the existing transient page and ViewModel scope through `ShellRoutePageFactory`/`PageActivationLease`.

### Visual reference fidelity

Treat `docs/sample_dashboard.png` as the target composition at its 1600×1200 screenshot size; match proportions and hierarchy rather than copying its sample values. Keep the existing Shell navigation/session behavior, and make only visual adjustments necessary for the reference frame: a white fixed-width left pane with brand block, section caption, highlighted Overview row, compact icon/menu rows, divider above Settings, and account footer. Keep the current initials avatar when no real avatar image exists; do not add a fake portrait.

In the Dashboard content, use a pale cool canvas, roughly 24 px outer padding, 16–20 px panel gaps, white surfaces, thin cool-gray borders and restrained rounded corners. Put title at the top-left and a three-option segmented period selector at the top-right; retain refresh as a compact adjacent action and keep live/preview metadata truthful without letting a technical status subtitle dominate the header.

Arrange four equal-height KPI cards in one row on wide windows. Each card has a compact label, prominent value and unit, a small icon block aligned top-right, a trend pill only when comparison data exists, and one short muted detail. Keep zero values visible and do not hardcode screenshot figures. Use Vietnamese labels and units (`₫`, `đơn`, `sản phẩm`, `mặt hàng`); avoid the English word “balance”. Preserve the active-product and low-stock definitions from the API contract.

Below KPIs, use a two-column row on wide layouts: revenue chart at about 70% width and low-stock panel at about 30%; stack them at narrow widths. The chart has a compact title and date/peak summary, horizontal grid lines and readable Y-axis labels, date/weekday X-axis labels, daily bars with a restrained blue gradient, a smooth/connected revenue line with circular markers, and a clear emphasis for the peak day. Reuse `RevenueChartGeometry` for coordinate and scale calculations where suitable. Keep an accessible daily-value table available without hover, preferably collapsed under an expander so it does not dominate the chart. Tooltips may supplement values but must not be the only way to read them.

Render low-stock rows with a small product-type icon, ellipsized product name, muted variant subtitle, right-aligned red available quantity, and subtle row separators. Show at most the API's five rows and retain a clear empty-list message. The emphasized number must mean available quantity; physical, reserved, and threshold values may appear in a quieter secondary line without mislabeling available stock.

Render recent orders in a full-width panel below the chart row. Put the title at left and an “Xem tất cả” action only if it navigates to the existing Orders destination with a meaningful behavior. Use a table-like layout with muted uppercase headers, five rows, subtle horizontal separators, blue order code, customer name, product summary, right-aligned amount, and a status badge whose tone reflects the real order status. The screenshot also contains customer phone and IMEI columns, but neither is present in `AdminDashboardDto`; omit those values in this change rather than fabricating them. If the owner requires them, create a separate API-contract change first.

The recent-orders table MUST consume the available width of its panel on wide layouts. The current visual defect is that the rows and headers stop around the middle of a wide panel, leaving a large unused area on the right. Do not solve this by widening the panel or adding fake columns. Ensure the table and each item row stretch to the panel's available content width, and calculate header/row columns from that same width. Distribute the usable width across all five real columns: keep order code and customer compact, give product summary the largest flexible share, and allocate enough width to amount and status to avoid clipping. Header and data-row column definitions MUST stay aligned. Avoid a horizontal `ScrollViewer` or fixed `MinWidth` forcing unconstrained measurement at wide sizes; use horizontal scrolling only below the responsive breakpoint, or reflow into compact rows there. At wide sizes, separators should span the full table width and the product column should use the space that is currently blank.

Match the reference's typography hierarchy (large bold page title and KPI values, medium panel titles, muted compact metadata) using existing typography resources. At wide widths use four KPI columns and the 70/30 analytics split; at medium widths use two KPI columns and a stacked analytics row; at narrow widths use one KPI column and a stacked layout. The orders table may scroll horizontally or switch to compact rows when necessary. Theme colors must remain resource-bound for Light/Dark/System; avoid fixed light-only token brushes in data visualization.

### Verification boundary

After all implementation is complete, run frontend headless tests/build, then validate the already implemented backend/seeder path against a disposable PostgreSQL test database. Verify seeded values via the real API using an Admin session, verify 401/403 semantics, and verify a completed order becomes visible after Dashboard refresh. Record live UI evidence per supported target separately; unavailable targets remain unverified. Owner visual acceptance is a separate task and must not be inferred from technical tests.

## Risks / Trade-offs

- [Shared DTO or enum/status drift] → Assert required DTO fields and reject unknown order status/time-zone values as invalid response; add repository mapping tests against representative JSON.
- [Session event races a late network response] → Capture session generation per request and guard every state commit, including error and finalization; test transports that ignore cancellation.
- [Default live Dashboard makes a request at anonymous development startup] → Keep Shell construction independent of API availability and contain the request as sign-in-required/network Error; explicit preview remains offline.
- [Visual reference requires fields missing from the API] → Match structure and typography with available contract data; do not fake phone/IMEI or sample KPI values. Track a separate contract change only if those fields are explicitly required.
- [C# Markup visual refinement changes layout or theme behavior] → Use theme resources, verify wide/medium/narrow layouts and target-specific visual behavior; do not treat unit/build success as owner acceptance.
- [Recent-orders table measures only to its content and leaves blank panel space] → Make the table and item rows stretch to the actual panel width, use matching shared column sizing for headers and rows, and verify visually at a wide desktop size that all five columns distribute across the full width; constrain horizontal scrolling to narrow layouts.
- [Seed/API verification depends on PostgreSQL and credentials] → Use only an isolated test database and Admin test identity; report missing environment or platform as unverified.

## Migration Plan

No database or API migration is required. Change only frontend DI/model/repository/ViewModel/view/tests and the two spec deltas. Keep the preview flag behavior backward-compatible. Rollback consists of reverting the Dashboard client changes and restoring the prior page/DI selection; API and persisted data remain untouched.
