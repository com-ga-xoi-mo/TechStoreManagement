# Proposal

## Why

The Dashboard needs to display current server aggregates through the existing frontend API/session boundary while matching the supplied visual reference at `docs/sample_dashboard.png`. The current C# Markup page connects the live API but still differs substantially from the sample: its analytics panels stack vertically, the chart is only a line, and recent orders are rendered as text blocks instead of a table. The next apply must close those visual gaps without fabricating fields that the API does not provide.

## What Changes

- Convert `DashboardPage` to Uno C# Markup, preserve the current chart geometry, shared controls, navigation lifecycle and responsive/theme behavior, then remove the Dashboard XAML files.
- Add an Analytics client repository using `IApiClient`, map `AdminDashboardDto` to neutral Dashboard presentation models, and register live as the default source.
- Keep fixture data only behind the explicit preview flag; API failure, authorization failure and empty success remain distinct and never select fixture data as fallback.
- Make period selection and refresh work for live data; replace demo-specific identifiers/labels with API order codes and live snapshot metadata.
- Refine Dashboard and its existing Shell frame against `docs/sample_dashboard.png`: segmented period selection, four compact KPI cards, two-column chart/low-stock row, full-width recent-orders table, sample-like spacing, hierarchy, colors, badges, separators and responsive behavior.
- Keep visible live data truthful: do not hardcode sample KPI values; do not invent customer phone numbers or IMEI values absent from `AdminDashboardDto`. Treat adding those fields as a separate backend/API contract change, outside this change.
- Define Dashboard behavior for loading, valid zero/empty results, retry, 401/403, logout/session expiration, cancellation and late responses.
- Update the existing `frontend-dashboard-preview` and `frontend-shell-navigation` requirements so offline/demo behavior and live Dashboard behavior do not conflict.
- Add headless repository/ViewModel coverage and a final verification plan for PostgreSQL seed → Analytics API → Dashboard refresh, including an order completed after initial load. Record target-specific UI checks and owner acceptance separately.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `frontend-dashboard-preview`: keep explicit fixture preview while adding live-source behavior, error/empty distinctions and metadata requirements.
- `frontend-shell-navigation`: make Dashboard the live default, preserve offline shell startup and define behavior when the Dashboard has no authorized API session.

## Impact

- `frontend/TechStore.Client/Views/Dashboard/`, Dashboard ViewModel and Analytics presentation models/repositories; limited visual adjustments to the existing Shell navigation pane so the supplied full-page reference is represented.
- Frontend UI DI and headless unit tests; no backend API contract or order workflow changes.
- Existing shared `AdminDashboardDto`, `GET /api/analytics/dashboard`, `IApiClient`, bearer/session handling, `PageViewModel`, chart geometry and shared controls.
- OpenSpec deltas for `frontend-dashboard-preview` and `frontend-shell-navigation`.
