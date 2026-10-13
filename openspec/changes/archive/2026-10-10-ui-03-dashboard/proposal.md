# Proposal

## Why

After UI-02 preview login, the Shell's “Tổng quan” route still shows a generic placeholder. UI-03 turns that route into the first reviewable business screen using an explicit mock analytics source while the backend reporting contract remains unavailable.

## What Changes

- Replace only the Dashboard placeholder with a native Uno page based on D01: four KPI cards, Hôm nay / 7 ngày qua / Tháng này filters, revenue chart, low-stock list and five recent orders. Keep the other Shell routes as their existing placeholders.
- Load one time-range-consistent dashboard snapshot through `IAnalyticsRepository` and a DI-selected mock. Show loading, empty, error and retry states; label all displayed figures as preview data. Use D31/UI-00 styles and a native XAML chart rather than embedding D01 HTML or adding a chart package without target verification.
- Define provisional metric semantics and date boundaries for the mock so cards, chart and lists reconcile. Record the authoritative aggregation, timezone, low-stock threshold, status and API response contract as prerequisites of a separate real-integration change; do not imply the mock validates backend analytics.
- Keep links to order, catalog and inventory details unavailable until those destination screens exist. No linked business screen, HTTP ApiClient, JWT work or backend change is included.

## Capabilities

### New Capabilities

- `client-dashboard`: Desktop analytics preview, synchronized period filtering, chart and summary lists, data states and provenance.

### Modified Capabilities

- `client-navigation-shell`: The Dashboard route opens its real UI-03 page after preview login, while the other unimplemented routes retain placeholders and Shell behavior.

## Impact

Planning affects `Views/Dashboard`, `ViewModels/Dashboard`, `Models/Analytics`, `Repositories/Analytics`, DI in `App.xaml.cs`, and the Dashboard mapping in `Services/Navigation/UnoFrameAdapter.cs`. It reuses archived UI-00 theme, UI-01 Shell/navigation and UI-02 explicit preview session. Backend `Customers/README.md` describes an AnalyticsController, but no controller, endpoint or shared dashboard DTO exists in current source. Real analytics aggregation, contract and authorization remain a separate dependency.
