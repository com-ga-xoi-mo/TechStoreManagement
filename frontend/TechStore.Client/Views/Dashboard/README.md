# UI-03 Dashboard handoff

## Scope and design reference

This page implements the D01 composition: four KPI cards, three period filters, revenue trend, low-stock list and up to five recent orders. It reuses the UI-00 D31 theme and UI-01 Shell. The implementation uses Uno XAML controls and shapes; `RevenueChartControl` draws bars, grid lines and the trend polyline on a `Canvas`, while its accessible summary exposes dates and amounts as text. KPI cards reflow from four columns to two and then one as the available width narrows. Other Shell destinations remain placeholders.

The mock values are synthetic and are labeled “Dữ liệu mẫu · Preview.” No D01 sample KPI or administrator identity is presented as live data. `DashboardSnapshot` records `GeneratedAt` as the as-of time and `SyntheticPreview` as provenance. The sales figures use completed mock orders in the selected interval; inventory count is distinct mock products, and low stock counts product/variant records whose `Quantity - ReservedQuantity` is at or below `MinimumStockAlert`. The mock has no returns or refunds. These definitions are UI fixture rules only.

## Files and verification

Changed files for UI-03:

- `Models/Analytics/DashboardSnapshot.cs` and `DashboardPeriodHelper.cs`
- `Repositories/Analytics/IAnalyticsRepository.cs`, `FakeAnalyticsRepository.cs`, `UnavailableAnalyticsRepository.cs`, `DashboardPreviewSettings.cs` and `README.md`
- `ViewModels/Dashboard/DashboardViewModel.cs`
- `Views/Dashboard/DashboardPage.xaml`, `DashboardPage.xaml.cs` and this handoff
- `Controls/Dashboard/RevenueChartControl.xaml` and `.xaml.cs`
- `Converters/BooleanToVisibilityConverter.cs`
- Analytics DI registration in `App.xaml.cs` and Dashboard route mapping in `Services/Navigation/UnoFrameAdapter.cs`
- `tests/Frontend.UnitTests/DashboardAnalyticsTests.cs` and the focused test project configuration

Headless tests live in `tests/Frontend.UnitTests/DashboardAnalyticsTests.cs`.

Verified on 2026-10-10:

- `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj` — 22 passed, 0 failed.
- `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` — succeeded, 0 warnings, 0 errors.
- `TECHSTORE_AUTH_MODE=preview TECHSTORE_DASHBOARD_SCENARIO=populated timeout 15s dotnet run --no-build --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` — process started and remained running until the timeout. Dashboard interactions and rendered layout were not exercised in this smoke run.
- The Windows target was not built because this environment is Linux.

## Owner manual acceptance

On 2026-10-10, the project owner reported completing the provided Dashboard walkthrough: period filters, normal/narrow window use, keyboard navigation, and the populated/empty/delayed/error preview scenarios. No specific failure was reported. This records the owner's completion confirmation; it does not claim a Windows-target check. After that confirmation, a `onepoint` chart fixture was added so the sparse-chart case can be tried separately; the owner has not yet tested that new fixture.

Uno emitted the existing RemoteControl/HotDesign dev-server connection and X11 window-manager warnings during the desktop launch. No Dashboard runtime verification is claimed from that process-only smoke run.

## Follow-up API dependency

A separate backend/API integration must settle and document: which order statuses and settlement timestamps count toward revenue; gross versus net totals and refund/return treatment; timezone and period comparison semantics; active product/variant counting; stock threshold and warehouse scope; grouping and data freshness; recent-order paging; and server-enforced RBAC. Add a real repository/API client only after those contracts exist. This preview does not establish backend analytics or real permissions.
