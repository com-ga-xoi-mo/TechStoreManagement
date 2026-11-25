# Design

## Context

See [proposal.md](proposal.md). Currently `AnalyticsService` opens a `RepeatableRead` transaction, reads the database timestamp, and passes `AsOfUtc` to the repository as the upper bound for period queries. `AdminDashboardDto` already contains `Period`, start/end dates, and `AsOf`. The client already uses the `DashboardPage -> DashboardViewModel -> AnalyticsApiRepository -> IApiClient` pipeline along with activation- and session-scoped request cancellation.

## Goals / Non-Goals

**Goals**

- Use a single store-wide read policy and a single Analytics contract for Admin and Manager.
- Support historical date ranges on the real database through the same pipeline while preserving the behavior of the three relative periods.

**Non-Goals**

- New KPI formulas, historical inventory snapshots, new seed data, or Customers permissions.
- Refactoring legacy chart colors. `StitchTokens.Primary` currently remains as fallback in `DashboardPage.CreateRevenueBarBrush` and is out of scope here. New date controls use `AppColors` tokens. Hand-calculated dataset reconciliation belongs to `m3-demo-data-reconciliation`.

## Decisions

### D1. Server authorization and client navigation

Authorize both roles on `AnalyticsController` and retain the current JWT pipeline checks. When updating `docs/API_PERMISSIONS.md`, update both the Analytics row and any sections that still state Manager receives 403. `ApiAuthorizationCoverageTests` continues maintaining expected roles independently from controller metadata. Update `AnalyticsControllerTests` to verify all four roles, adding `SalesStaff` and `WarehouseStaff` to the test auth handler that currently only constructs Admin and Manager.

Split Dashboard and Customers branches in `RoleAccess.CanOpen`. `Destinations`, the route guard, and `Landing` continue sharing the same table. The existing order Overview -> Orders -> Categories and Inventory -> Products with Settings as fallback automatically lands Manager on Overview without special-case routing. Customers remains Admin-only. Update Dashboard's sign-in guidance copy so it applies to both Admin and Manager. One shared endpoint keeps identical snapshots and test coverage for both roles, whereas a separate Manager endpoint would duplicate an identical read contract.

### D2. Bounded custom period on the existing endpoint

Example request: `GET /api/analytics/dashboard?period=custom&from=2026-10-12&to=2026-10-18`. Both endpoints are inclusive in UTC+07. The 31-day limit matches the maximum length of the existing month period and fits the current chart point density.

The controller validates parameter combinations, distinguishes missing from empty or repeated values, parses exact `yyyy-MM-dd` via `DateOnly` and invariant culture, and passes typed criteria into the service. The controller returns `ValidationProblemDetails` for invalid inputs. The service validates the end date against the Vietnam calendar date at the snapshot timestamp, validates safe UTC boundary conversion, and returns a validation result mappable to HTTP 400 before running aggregate queries. Ensure rollback if reads or validation inside the transaction exit abnormally. For example, a 32-day range or future end date must be rejected. For `today`, `last7Days`, `thisMonth`, and default, valid requests continue passing only `period`.

A dedicated demo preset and shifting seed dates relative to run date were considered and rejected. A custom range works with any real transaction history and preserves reproducible seed timestamps. `from`/`to` are tied explicitly to `period=custom` so default and custom ranges are clearly distinguishable in JSON, UI, and HTTP evidence.

### D3. Decoupling filter bounds from snapshot time

From the current database timestamp, the service derives the Vietnam date, report start/end dates, `FromUtc`, and `ToExclusiveUtc`. `ToExclusiveUtc` is the minimum of Vietnam midnight after the end date and `asOf`. The repository accepts the half-open UTC interval and applies the same bounds to total revenue, daily trend, order count, and up to 5 recent orders according to each KPI's timestamp column. Keep parameterized queries and PostgreSQL UTC+07 date grouping. Active products and low stock are read at the transaction snapshot independently of period bounds.

Keep the JSON shape and name `AdminDashboardDto`. Expand `Period` documentation to cover `custom` and document `StartDate`/`EndDate` versus `AsOf`. Days with no events still zero-fill the full range, and lists and KPI rules keep the existing contract. Do not add extra timestamp fields because the two dates plus `AsOf` fully capture behavior. The client validates that returned custom dates match the query before mapping the snapshot.

### D4. Separating draft date inputs from the applied request

Keep the three preset period buttons. In live mode, add a custom-range toggle ("Khoảng ngày") opening start ("Từ ngày") and end ("Đến ngày") inputs formatted as `dd/MM/yyyy` and an Apply ("Áp dụng") command. When first opening the inputs, prefill from the currently displayed snapshot dates. Stack below the period buttons on narrow widths. Use accessible labels, field-level validation messages, declarative bindings/commands, and clear tab order.

The ViewModel keeps draft input strings separate from the immutable committed `DashboardQuery` with `DashboardPeriod.Custom` and two `DateOnly` values on the client. Only applying a valid range triggers a request. Refresh and Retry reuse the committed query. Presets build a query with period only. Every new page activation defaults to the last 7 days (`Last7Days`). Continue using existing request cancellation and session generation guards. The client checks current UTC+07 date for immediate feedback; the service validates against database time, so HTTP 400 still surfaces safely under clock skew.

The client repository formats dates using invariant culture and shares the existing API/DTO. In preview mode, hide the custom-range controls and keep the fixture, banner, and three preset periods. Display the applied live date range and update timestamp. When a custom range spans months, include month in axis labels and thin axis ticks as needed, alongside the existing full daily-values table. Clearly label active-product and low-stock cards as snapshot-time metrics. Because the order list shows the 5 most recently created orders in the period (D3) rather than recent orders up to today, clarify its panel title (e.g., "Đơn mới nhất trong kỳ") for both presets and custom ranges. Explicit manual apply lets users finish editing both dates before sending a request and avoids firing requests while typing.

## Risks / Trade-offs

- [Date mismatch at UTC or Vietnam midnight boundaries] -> Test both boundaries, ranges ending today, historical ranges, and month rollovers with a pinned timestamp. Use a single database timestamp for bounds and metadata.
- [Users mistaking low stock for historical data] -> Display snapshot-time notes next to those metrics even when viewing a custom range.
- [Late responses from a prior account or prior period] -> Retain request/generation guards and test custom refresh, retry, logout, and session replacement.
- [API and client version skew] -> Ship the API before the desktop client. Existing desktop continues working with the three presets once the new backend is deployed. Verify Manager acceptance once both sides are updated.

## Migration Plan

No EF migration or seed changes needed. On apply, run backend and desktop from the integrated build against a dedicated seeded PostgreSQL test database. Log in as `manager1`, select 12–18/10/2026, and observe real data. Record actual `asOf` and environment details. If a release rollback is needed, roll back desktop before the API so the client stops sending `period=custom` and the menu stops exposing Dashboard to Manager once server access is revoked.
