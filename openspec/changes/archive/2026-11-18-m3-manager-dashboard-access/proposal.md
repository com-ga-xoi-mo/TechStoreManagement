# Proposal

## Why

M3 Sprint 2 assigns Binh to open the live Dashboard to Manager. Currently the API and Shell only allow Admin into Overview, and periods anchored to the current date mean demo order history around 18/10/2026 may fall outside the window during demos.

## What Changes

- Grant store-wide Dashboard read access to Admin and Manager. Synchronize the API, permissions matrix, navigation menu, and route guard. After sign-in, Manager lands on Overview following the existing navigation order.
- Add an optional `period=custom` range with two Vietnam calendar dates `from` and `to` (up to 31 days) on the endpoint and live Dashboard. Preserve the three existing periods and the `last7Days` default. Demo acceptance selects 12–18/10/2026 against the seeded database.
- Reuse `AdminDashboardDto` and existing KPI definitions. The custom period filters revenue and orders by report dates, while active products and low stock continue to reflect snapshot read time. Preview remains separately enabled via configuration.
- Hand off evidence of `manager1` login, live Dashboard reading, and four-role HTTP checks. Reconcile fully against the hand-calculated dataset in `m3-demo-data-reconciliation`.

## Capabilities

### New Capabilities

No new capabilities.

### Modified Capabilities

- `admin-analytics-api`: Access permissions, custom date-range contract, and Vietnam period boundaries.
- `api-role-authorization`: Dashboard permission in the API matrix and role verification scenarios.
- `frontend-dashboard-preview`: Live Dashboard for Manager, custom date-range selection, refresh/retry, and preview data source isolation.
- `frontend-shell-navigation`: Role-based menu, landing page, and Overview route guard.

## Impact

- Backend Analytics, API authorization coverage suite, `docs/API_PERMISSIONS.md`, and Analytics Swagger/OpenAPI descriptions.
- Shared DTO keeps its name and JSON shape while extending valid `period` values. Desktop updates `RoleAccess`, Analytics models/repository, `DashboardViewModel`, and `DashboardPage`.
- Refactoring chart color `StitchTokens`, Customers permissions, and seed data are out of scope. Apply uses a seeded test database to verify the live read path.
