using System.Net;
using TechStore.Client.Controls.Charts;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Development;
using TechStore.Client.Services.Session;
using TechStore.Client.UnitTests.Infrastructure;
using TechStore.Client.UnitTests.Repositories.Analytics;
using TechStore.Client.ViewModels.Dashboard;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.UnitTests.ViewModels.Dashboard;

public sealed class FinancialReportViewModelTests
{
    private static readonly DateTimeOffset AfterMidnight = new(2024, 10, 31, 17, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Admin", false, true)]
    [InlineData("Manager", false, true)]
    [InlineData("SalesStaff", false, false)]
    [InlineData("WarehouseStaff", false, false)]
    [InlineData("Unknown", false, false)]
    [InlineData(null, false, false)]
    [InlineData("Admin", true, false)]
    public async Task Open_GatesRoleAndSourceAndLeavesDefaultDashboardIntact(string? role, bool preview, bool allowed)
    {
        var dashboardRepository = new TrackingDashboard();
        var reportRepository = new DeferredReports();
        var dashboard = await ActivateAsync(reportRepository, MakeSession(role), preview: preview, dashboard: dashboardRepository);
        try
        {
            var report = dashboard.FinancialReport;
            Assert.Equal(allowed, report.IsAvailable);
            Assert.True(report.ShowOverview);
            Assert.Empty(reportRepository.Requests);
            var open = report.OpenCommand.ExecuteAsync(null);
            Assert.Equal(allowed, report.IsOpen);
            if (allowed) Assert.Single(reportRepository.Requests).Succeed();
            else Assert.Empty(reportRepository.Requests);
            await open;

            Assert.Equal(DashboardPeriod.Last7Days, Assert.Single(dashboardRepository.Queries).Period);
            Assert.Equal(preview, dashboard.ShowPreviewBanner);
            Assert.True(dashboard.IsContent);
        }
        finally { dashboard.Deactivate(); }
    }

    [Theory]
    [InlineData(-1, "25/10/2024", "31/10/2024", "01/11/2024")]
    [InlineData(0, "26/10/2024", "01/11/2024", "02/11/2024")]
    public async Task Apply_UsesVietnamMidnightForDefaultAndValidEndDate(int minuteOffset, string from, string today, string future)
    {
        var repository = new DeferredReports();
        var dashboard = await ActivateAsync(repository, MakeSession("Admin"), AfterMidnight.AddMinutes(minuteOffset));
        try
        {
            var report = dashboard.FinancialReport;
            await OpenAsync(report, repository);
            Assert.Equal(from, report.From);
            Assert.Equal(today, report.To);
            Assert.Equal(6, repository.Requests[0].Range.To.DayNumber - repository.Requests[0].Range.From.DayNumber);

            report.To = future;
            await report.ApplyCommand.ExecuteAsync(null);
            Assert.Contains("giờ Việt Nam", report.ToError);
            Assert.Single(repository.Requests);

            report.From = report.To = today;
            var valid = report.ApplyCommand.ExecuteAsync(null);
            Assert.Equal(repository.Requests[^1].Range.From, repository.Requests[^1].Range.To);
            repository.Requests[^1].Succeed();
            await valid;
            Assert.Empty(report.ToError);
            Assert.Contains(today, report.Presentation!.Reference);
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task Apply_ValidatesDraftAndReloadUsesAppliedRangeAfterErrorsAndReopening()
    {
        var repository = new DeferredReports();
        var dashboard = await ActivateAsync(repository, MakeSession("Admin"));
        try
        {
            var report = dashboard.FinancialReport;
            await dashboard.SelectPeriodCommand.ExecuteAsync(0);
            var overview = dashboard.Snapshot;
            await OpenAsync(report, repository);
            var original = report.Snapshot;
            report.From = "lỗi";
            await report.ApplyCommand.ExecuteAsync(null);
            Assert.NotEmpty(report.FromError);
            report.From = "01/11/2024";
            report.To = "31/10/2024";
            await report.ApplyCommand.ExecuteAsync(null);
            Assert.Contains("ngày bắt đầu", report.ToError);
            report.From = "01/10/2024";
            report.To = "01/11/2024";
            await report.ApplyCommand.ExecuteAsync(null);
            Assert.Contains("31", report.ToError);
            Assert.Same(original, report.Snapshot);
            Assert.Single(repository.Requests);

            report.To = "31/10/2024";
            var apply = report.ApplyCommand.ExecuteAsync(null);
            var range = repository.Requests[^1].Range;
            Assert.Equal(31, range.To.DayNumber - range.From.DayNumber + 1);
            Assert.Null(report.Snapshot);
            repository.Requests[^1].Succeed();
            await apply;
            Assert.Equal(range, report.Snapshot!.Range);

            report.From = "02/10/2024";
            var refresh = report.ReloadCommand.ExecuteAsync(null);
            Assert.Equal(range, repository.Requests[^1].Range);
            repository.Requests[^1].Fail(new ApiException(ApiErrorKind.Timeout, "debug timeout"));
            await refresh;
            Assert.Equal("Thử lại", report.ReloadLabel);
            var retry = report.ReloadCommand.ExecuteAsync(null);
            Assert.Equal(range, repository.Requests[^1].Range);
            repository.Requests[^1].Succeed();
            await retry;
            report.From = "lỗi";
            await report.ApplyCommand.ExecuteAsync(null);
            Assert.NotEmpty(report.FromError);
            report.CloseCommand.Execute(null);
            Assert.True(report.ShowOverview);
            Assert.Same(overview, dashboard.Snapshot);
            Assert.Null(report.Snapshot);
            await OpenAsync(report, repository);
            Assert.Equal(range, report.Snapshot!.Range);
            Assert.Equal("01/10/2024", report.From);
            Assert.Equal("31/10/2024", report.To);
            Assert.Empty(report.FromError);
            Assert.Empty(report.ToError);
            Assert.Equal(DashboardPeriod.Today, dashboard.SelectedPeriod);
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task OpenAndClose_SwitchPanelsGuardUnavailableAndResetOnSessionChange()
    {
        var repository = new DeferredReports();
        var store = MakeSession("Admin");
        var dashboard = await ActivateAsync(repository, store);
        try
        {
            var report = dashboard.FinancialReport;
            Assert.True(report.ShowOverview);
            Assert.False(report.IsOpen);

            var open = report.OpenCommand.ExecuteAsync(null);
            Assert.True(report.IsOpen);
            Assert.False(report.ShowOverview);
            Assert.Single(repository.Requests);
            repository.Requests[0].Succeed();
            await open;

            report.CloseCommand.Execute(null);
            Assert.False(report.IsOpen);
            Assert.True(report.ShowOverview);

            open = report.OpenCommand.ExecuteAsync(null);
            Assert.True(report.IsOpen);
            var reset = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            report.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FinancialReportViewModel.ShowOverview) && report.ShowOverview) reset.TrySetResult();
            };
            store.SetSession("replacement", "refresh", "second", "Second", "Manager");
            await reset.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(report.IsOpen);
            Assert.True(report.ShowOverview);
            repository.Requests[1].Succeed();
            await open;

            store.SetSession("blocked", "refresh", "third", "Third", "SalesStaff");
            await report.OpenCommand.ExecuteAsync(null);
            Assert.False(report.IsOpen);
            Assert.True(report.ShowOverview);
            Assert.Equal(2, repository.Requests.Count);
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task FocusRequested_OpenCloseAndReloadAskForPredictableTargets()
    {
        var repository = new DeferredReports();
        var store = MakeSession("Admin");
        var dashboard = await ActivateAsync(repository, store);
        try
        {
            var report = dashboard.FinancialReport;
            var requests = new List<FinancialReportFocusTarget>();
            report.FocusRequested += (_, target) => requests.Add(target);

            var open = report.OpenCommand.ExecuteAsync(null);
            Assert.Equal([FinancialReportFocusTarget.SelectedTab], requests);
            repository.Requests[0].Succeed();
            await open;
            Assert.Single(requests);

            var reload = report.ReloadCommand.ExecuteAsync(null);
            Assert.Single(requests);
            repository.Requests[1].Fail(new ApiException(ApiErrorKind.Network, "offline"));
            await reload;
            Assert.Equal([FinancialReportFocusTarget.SelectedTab, FinancialReportFocusTarget.Reload], requests);

            var retry = report.ReloadCommand.ExecuteAsync(null);
            repository.Requests[2].Succeed();
            await retry;
            Assert.Equal(FinancialReportFocusTarget.Reload, requests[^1]);
            Assert.Equal(3, requests.Count);

            report.CloseCommand.Execute(null);
            Assert.Equal(FinancialReportFocusTarget.SelectedTab, requests[^1]);
            Assert.Equal(4, requests.Count);

            // Closing an already closed report and an automatic reset on sign-out never steal focus.
            report.CloseCommand.Execute(null);
            Assert.Equal(4, requests.Count);
            var reopen = report.OpenCommand.ExecuteAsync(null);
            Assert.Equal(5, requests.Count);
            store.SetSession("replacement", "refresh", "second", "Second", "Manager");
            repository.Requests[3].Succeed();
            await reopen;
            Assert.Equal(5, requests.Count);
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task RangeLabel_StaysVisibleWhileLoadingWithoutStaleFigures()
    {
        var repository = new DeferredReports();
        var dashboard = await ActivateAsync(repository, MakeSession("Admin"));
        try
        {
            var report = dashboard.FinancialReport;
            Assert.Empty(report.RangeLabel);

            var open = report.OpenCommand.ExecuteAsync(null);
            Assert.True(report.IsLoading);
            Assert.Null(report.Snapshot);
            Assert.StartsWith("Kỳ ", report.RangeLabel);
            Assert.DoesNotContain("Cập nhật", report.RangeLabel);
            repository.Requests[0].Succeed();
            await open;
            Assert.Contains("Cập nhật", report.RangeLabel);
            Assert.Equal(report.Presentation!.Reference, report.RangeLabel);

            report.From = "12/10/2024";
            report.To = "14/10/2024";
            var apply = report.ApplyCommand.ExecuteAsync(null);
            Assert.Null(report.Snapshot);
            Assert.Equal("Kỳ 12/10/2024–14/10/2024", report.RangeLabel);
            repository.Requests[1].Fail(new ApiException(ApiErrorKind.Network, "offline"));
            await apply;
            Assert.True(report.IsError);
            Assert.Equal("Kỳ 12/10/2024–14/10/2024", report.RangeLabel);

            report.CloseCommand.Execute(null);
            Assert.Empty(report.RangeLabel);
        }
        finally { dashboard.Deactivate(); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Apply_OlderResultCannotChangeNewLoadingOrSnapshot(bool olderFails, bool olderFinishesLast)
    {
        var repository = new DeferredReports();
        var dashboard = await ActivateAsync(repository, MakeSession("Admin"));
        try
        {
            var report = dashboard.FinancialReport;
            var requestA = report.OpenCommand.ExecuteAsync(null);
            report.From = "12/10/2024";
            report.To = "14/10/2024";
            var requestB = report.ApplyCommand.ExecuteAsync(null);
            var older = repository.Requests[0];
            var newer = repository.Requests[1];
            Assert.True(older.Token.IsCancellationRequested);
            Assert.False(newer.Token.IsCancellationRequested);
            if (olderFinishesLast)
            {
                newer.Succeed();
                await requestB;
            }
            if (olderFails) older.Fail(new ApiException(ApiErrorKind.Network, "stale network error"));
            else older.Succeed();
            await requestA;
            Assert.Equal(!olderFinishesLast, report.IsLoading);
            Assert.Empty(report.ErrorMessage);
            if (!olderFinishesLast)
            {
                Assert.Null(report.Snapshot);
                newer.Succeed();
                await requestB;
            }
            Assert.Equal(newer.Range, report.Snapshot!.Range);
            Assert.Contains("12/10/2024–14/10/2024", report.Presentation!.Reference);
        }
        finally { dashboard.Deactivate(); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Manager")]
    [InlineData("WarehouseStaff")]
    public async Task SessionChange_ClearsReportAndDiscardsLateResponse(string? replacement)
    {
        var repository = new DeferredReports();
        var store = MakeSession("Admin");
        var dashboard = await ActivateAsync(repository, store);
        try
        {
            var report = dashboard.FinancialReport;
            var pending = report.OpenCommand.ExecuteAsync(null);
            if (replacement is null) store.Clear();
            else store.SetSession("replacement", "refresh", "second", "Second", replacement);
            Assert.True(repository.Requests[0].Token.IsCancellationRequested);
            repository.Requests[0].Succeed();
            await pending;
            Assert.False(report.IsOpen);
            Assert.False(report.IsAvailable);
            Assert.Empty(report.From);
            Assert.Empty(report.To);
            Assert.Empty(report.ErrorMessage);
            Assert.Null(report.Snapshot);
            await report.OpenCommand.ExecuteAsync(null);
            Assert.Single(repository.Requests);
        }
        finally { dashboard.Deactivate(); }

        if (replacement == "Manager")
        {
            var next = await ActivateAsync(repository, store);
            try
            {
                Assert.True(next.FinancialReport.ShowOverview);
                Assert.True(next.FinancialReport.IsAvailable);
                Assert.Null(next.FinancialReport.Snapshot);
            }
            finally { next.Deactivate(); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Leave_PageOrWindowCancelsWorkAndLeaseWaitsForLateResult(bool closeWindow)
    {
        var repository = new DeferredReports();
        var dashboard = MakeDashboard(repository, MakeSession("Admin"));
        using var window = new CancellationTokenSource();
        var scopeDisposed = false;
        await using var lease = dashboard.CreateActivationLease(() =>
        {
            scopeDisposed = true;
            return ValueTask.CompletedTask;
        }, window.Token);
        lease.Start(dashboard.ActivateAsync);
        var report = dashboard.FinancialReport;
        var pending = report.OpenCommand.ExecuteAsync(null);

        if (closeWindow) window.Cancel();
        var cleanup = lease.Deactivate();
        Assert.False(scopeDisposed);
        Assert.True(repository.Requests[0].Token.IsCancellationRequested);
        repository.Requests[0].Succeed();
        await pending;
        await cleanup;
        Assert.True(scopeDisposed);
        Assert.Null(report.Snapshot);
        Assert.False(report.IsLoading);
    }

    [Fact]
    public async Task CloseReport_DoesNotCancelAnIndependentDashboardRequest()
    {
        var repository = new DeferredReports();
        var m3 = new TrackingDashboard { Delay = true };
        var dashboard = MakeDashboard(repository, MakeSession("Admin"), dashboard: m3);
        try
        {
            var overviewRequest = dashboard.ActivateAsync();
            var report = dashboard.FinancialReport;
            var open = report.OpenCommand.ExecuteAsync(null);
            report.CloseCommand.Execute(null);
            Assert.True(repository.Requests[0].Token.IsCancellationRequested);
            Assert.False(m3.Token.IsCancellationRequested);
            repository.Requests[0].Succeed();
            await open;
            Assert.Null(report.Snapshot);
            m3.Finish();
            await overviewRequest;
            Assert.True(dashboard.IsEmpty);
        }
        finally { dashboard.Deactivate(); }
    }

    [Theory]
    [InlineData(ApiErrorKind.Http, 400, "Kỳ báo cáo")]
    [InlineData(ApiErrorKind.Http, 403, "không có quyền")]
    [InlineData(ApiErrorKind.Http, 503, "Chưa tải")]
    [InlineData(ApiErrorKind.Timeout, null, "quá thời gian")]
    [InlineData(ApiErrorKind.Network, null, "Không thể kết nối")]
    [InlineData(ApiErrorKind.InvalidResponse, null, "không đúng định dạng")]
    public async Task Error_PreservesSessionAndCanRetry(ApiErrorKind kind, int? status, string message)
    {
        var repository = new DeferredReports();
        var session = MakeSession("Manager");
        var dashboard = await ActivateAsync(repository, session);
        try
        {
            var report = dashboard.FinancialReport;
            var pending = report.OpenCommand.ExecuteAsync(null);
            var first = repository.Requests[0];
            first.Fail(new ApiException(kind, "server diagnostics", status));
            await pending;
            Assert.True(report.IsError);
            Assert.Contains(message, report.ErrorMessage);
            Assert.DoesNotContain("server diagnostics", report.ErrorMessage);
            Assert.Null(report.Snapshot);
            Assert.Equal(SessionStatus.Active, session.Current.Status);

            var retry = report.ReloadCommand.ExecuteAsync(null);
            Assert.Equal(first.Range, repository.Requests[1].Range);
            repository.Requests[1].Succeed();
            await retry;
            Assert.Equal(PageContentState.Empty, report.State);
            Assert.NotEmpty(report.Presentation!.Daily);
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task Unauthorized_UsesBearerHandlerAndClearsTheReportWithoutReplay()
    {
        var session = MakeSession("Admin");
        using var response = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = new HttpClient(new BearerTokenHandler(session) { InnerHandler = response });
        var dashboard = await ActivateAsync(new AnalyticsApiRepository(new ApiClient(http, new())), session);
        try
        {
            await dashboard.FinancialReport.OpenCommand.ExecuteAsync(null);
            Assert.Equal(SessionStatus.Expired, session.Current.Status);
            Assert.False(dashboard.FinancialReport.IsOpen);
            Assert.Null(dashboard.FinancialReport.Snapshot);
            Assert.Equal("Bearer", Assert.Single(response.Requests).Authorization!.Scheme);
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task Presentation_ShowsSampleDateAndKeepsUnknownDistinctFromZeroAndEmpty()
    {
        var snapshot = await FinancialReportApiRepositoryTests.ReadAsync(FinancialReportApiRepositoryTests.SalesJson);
        var view = new FinancialReportPresentation(snapshot);
        Assert.Contains("15/10/2024 09:00 (UTC+07)", view.Reference);
        Assert.Contains("480 ₫", view.Summary[0].Values.Select(value => value.Text));
        Assert.Contains("Chưa tính được", view.Summary[2].Values.Select(value => value.Text));
        Assert.Equal("0 ₫", view.Daily[1].Sales);
        Assert.Equal("Chưa tính được", view.Daily[1].Refunds);
        Assert.Equal("Chưa tính được", view.Lists[3].EmptyMessage);
        Assert.Equal("-1.500,25 ₫", FinancialReportPresentation.Money(-1500.25m));
        Assert.Equal("Chưa tính được", view.NetSalesKpi.Value);
        Assert.Equal("370 ₫", view.CashFlowKpi.Value);
        Assert.Equal("3 đơn đã giao · 5 sản phẩm", view.WarrantyAndOrdersKpi.Detail);
        Assert.Same(view.Summary[0], view.SalesSection);
        Assert.Same(view.Summary[1], view.CashFlowSection);
        Assert.Same(view.Summary[2], view.CostProfitSection);
        Assert.Same(view.Summary[3], view.WarrantySection);
        Assert.Same(view.Lists[0], view.TopQuantityList);
        Assert.Same(view.Lists[1], view.TopRevenueList);
        Assert.Same(view.Lists[2], view.CashierList);
        Assert.Same(view.Lists[3], view.ShiftList);
        Assert.Equal("Chưa tính được", view.Daily[1].NetProfit);
        Assert.False(view.IsEmpty);
        var request = new DeferredRequest(snapshot.Range, CancellationToken.None);
        request.Succeed();
        var empty = await request.Completion.Task;
        Assert.True(new FinancialReportPresentation(empty).IsEmpty);
        Assert.False(new FinancialReportPresentation(empty with { CashByShift = null }).IsEmpty);
        Assert.False(new FinancialReportPresentation(empty with { Summary = empty.Summary with { WarrantyCost = null } }).IsEmpty);
    }

    [Theory]
    [InlineData(-1, FinancialReportPreset.Today, "31/10/2024", "31/10/2024")]
    [InlineData(0, FinancialReportPreset.Today, "01/11/2024", "01/11/2024")]
    [InlineData(-1, FinancialReportPreset.Last7Days, "25/10/2024", "31/10/2024")]
    [InlineData(0, FinancialReportPreset.Last7Days, "26/10/2024", "01/11/2024")]
    [InlineData(-1, FinancialReportPreset.ThisMonth, "01/10/2024", "31/10/2024")]
    [InlineData(0, FinancialReportPreset.ThisMonth, "01/11/2024", "01/11/2024")]
    public async Task Preset_UsesVietnamMidnightAndLoadsImmediately(int minuteOffset, FinancialReportPreset preset, string from, string to)
    {
        // Arrange
        var repository = new DeferredReports();
        var dashboard = await ActivateAsync(repository, MakeSession("Admin"), AfterMidnight.AddMinutes(minuteOffset));
        try
        {
            var report = dashboard.FinancialReport;
            await OpenAsync(report, repository);

            // Act
            var pending = RunPreset(report, preset).ExecuteAsync(null);

            // Assert
            Assert.Equal(2, repository.Requests.Count);
            Assert.True(report.IsLoading);
            Assert.Equal(from, report.From);
            Assert.Equal(to, report.To);
            Assert.Equal(preset, report.SelectedPreset);
            var range = repository.Requests[^1].Range;
            Assert.Equal(from, range.From.ToString("dd/MM/yyyy"));
            Assert.Equal(to, range.To.ToString("dd/MM/yyyy"));
            Assert.InRange(range.To.DayNumber - range.From.DayNumber + 1, 1, 31);
            repository.Requests[^1].Succeed();
            await pending;
            Assert.Equal(range, report.Snapshot!.Range);
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task Preset_BecomesTheAppliedRangeSoReloadIgnoresDraftDates()
    {
        // Arrange
        var repository = new DeferredReports();
        var dashboard = await ActivateAsync(repository, MakeSession("Admin"));
        try
        {
            var report = dashboard.FinancialReport;
            await OpenAsync(report, repository);
            Assert.Equal(FinancialReportPreset.Last7Days, report.SelectedPreset);

            // Act
            var preset = report.ThisMonthCommand.ExecuteAsync(null);
            var presetRange = repository.Requests[^1].Range;
            repository.Requests[^1].Succeed();
            await preset;
            report.From = "02/10/2024";
            report.To = "03/10/2024";
            var reload = report.ReloadCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(3, repository.Requests.Count);
            Assert.Equal(presetRange, repository.Requests[^1].Range);
            repository.Requests[^1].Succeed();
            await reload;
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task Preset_CancelsInFlightRequestAndIgnoresItsLateResponse()
    {
        // Arrange
        var repository = new DeferredReports();
        var dashboard = await ActivateAsync(repository, MakeSession("Admin"));
        try
        {
            var report = dashboard.FinancialReport;
            var open = report.OpenCommand.ExecuteAsync(null);

            // Act
            var today = report.TodayCommand.ExecuteAsync(null);
            var older = repository.Requests[0];
            var newer = repository.Requests[1];

            // Assert
            Assert.True(older.Token.IsCancellationRequested);
            Assert.False(newer.Token.IsCancellationRequested);
            older.Succeed();
            await open;
            Assert.True(report.IsLoading);
            Assert.Null(report.Snapshot);
            newer.Succeed();
            await today;
            Assert.Equal(newer.Range, report.Snapshot!.Range);
            Assert.Equal(newer.Range.From, newer.Range.To);
            Assert.False(report.IsLoading);
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task Preset_ClearsFieldErrorsAndReplacesInvalidDraftDates()
    {
        // Arrange
        var repository = new DeferredReports();
        var dashboard = await ActivateAsync(repository, MakeSession("Admin"));
        try
        {
            var report = dashboard.FinancialReport;
            await OpenAsync(report, repository);
            report.From = "lỗi";
            report.To = "cũng lỗi";
            await report.ApplyCommand.ExecuteAsync(null);
            Assert.NotEmpty(report.FromError);
            Assert.NotEmpty(report.ToError);

            // Act
            var pending = report.Last7DaysCommand.ExecuteAsync(null);
            repository.Requests[^1].Succeed();
            await pending;

            // Assert
            Assert.Empty(report.FromError);
            Assert.Empty(report.ToError);
            Assert.Equal("26/10/2024", report.From);
            Assert.Equal("01/11/2024", report.To);
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task SelectedPreset_FollowsAppliedDatesAndClearsForCustomRanges()
    {
        // Arrange
        var repository = new DeferredReports();
        var dashboard = await ActivateAsync(repository, MakeSession("Admin"));
        try
        {
            var report = dashboard.FinancialReport;
            Assert.Equal(FinancialReportPreset.None, report.SelectedPreset);
            await OpenAsync(report, repository);
            Assert.True(report.IsLast7DaysPreset);
            Assert.False(report.IsTodayPreset);
            Assert.False(report.IsThisMonthPreset);

            // Act and assert
            report.From = "12/10/2024";
            report.To = "14/10/2024";
            var custom = report.ApplyCommand.ExecuteAsync(null);
            Assert.Equal(FinancialReportPreset.None, report.SelectedPreset);
            repository.Requests[^1].Succeed();
            await custom;

            report.From = report.To = "01/11/2024";
            var same = report.ApplyCommand.ExecuteAsync(null);
            Assert.True(report.IsTodayPreset);
            repository.Requests[^1].Succeed();
            await same;
            report.From = "lỗi";
            await report.ApplyCommand.ExecuteAsync(null);
            Assert.True(report.IsTodayPreset);
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task PresetAndChartCommands_AreGuardedWhenReportIsClosed()
    {
        // Arrange
        var repository = new DeferredReports();
        var dashboard = await ActivateAsync(repository, MakeSession("Admin"));
        try
        {
            var report = dashboard.FinancialReport;

            // Act
            await report.TodayCommand.ExecuteAsync(null);
            await report.Last7DaysCommand.ExecuteAsync(null);
            await report.ThisMonthCommand.ExecuteAsync(null);
            report.ShowCashFlowChartCommand.Execute(null);

            // Assert
            Assert.Empty(repository.Requests);
            Assert.Equal(FinancialReportPreset.None, report.SelectedPreset);
            Assert.True(report.IsSalesChart);
        }
        finally { dashboard.Deactivate(); }
    }

    [Fact]
    public async Task ChartMode_SwitchesBetweenSalesAndCashFlowAndResetsOnSessionChange()
    {
        // Arrange
        var repository = new DeferredReports();
        var store = MakeSession("Admin");
        var dashboard = await ActivateAsync(repository, store);
        try
        {
            var report = dashboard.FinancialReport;
            await OpenAsync(report, repository);
            var changed = new List<string?>();
            report.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            // Act
            report.ShowCashFlowChartCommand.Execute(null);

            // Assert
            Assert.Equal(FinancialChartMode.CashFlow, report.ChartMode);
            Assert.True(report.IsCashFlowChart);
            Assert.False(report.IsSalesChart);
            Assert.Contains(nameof(FinancialReportViewModel.IsSalesChart), changed);
            Assert.Contains(nameof(FinancialReportViewModel.IsCashFlowChart), changed);
            Assert.Single(repository.Requests);

            report.ShowSalesChartCommand.Execute(null);
            Assert.Equal(FinancialChartMode.Sales, report.ChartMode);

            report.ShowCashFlowChartCommand.Execute(null);
            var reset = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            report.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FinancialReportViewModel.ShowOverview) && report.ShowOverview) reset.TrySetResult();
            };
            store.SetSession("replacement", "refresh", "second", "Second", "Manager");
            await reset.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(FinancialChartMode.Sales, report.ChartMode);
            Assert.Equal(FinancialReportPreset.None, report.SelectedPreset);
        }
        finally { dashboard.Deactivate(); }
    }

    private static CommunityToolkit.Mvvm.Input.IAsyncRelayCommand RunPreset(FinancialReportViewModel report, FinancialReportPreset preset) => preset switch
    {
        FinancialReportPreset.Today => report.TodayCommand,
        FinancialReportPreset.ThisMonth => report.ThisMonthCommand,
        _ => report.Last7DaysCommand
    };

    private static InMemorySessionStore MakeSession(string? role)
    {
        var store = new InMemorySessionStore();
        if (role is not null) store.SetSession("token", "refresh", "first", "First", role);
        return store;
    }

    private static DashboardViewModel MakeDashboard(IFinancialReportRepository report, ISessionStore store,
        DateTimeOffset? clock = null, bool preview = false, TrackingDashboard? dashboard = null) =>
        new(dashboard ?? new TrackingDashboard(), new DashboardPreviewOptions(preview), store, report,
            new FixedClock(clock ?? AfterMidnight));

    private static async Task<DashboardViewModel> ActivateAsync(IFinancialReportRepository report, ISessionStore store,
        DateTimeOffset? clock = null, bool preview = false, TrackingDashboard? dashboard = null)
    {
        var viewModel = MakeDashboard(report, store, clock, preview, dashboard);
        await viewModel.ActivateAsync();
        return viewModel;
    }

    private static async Task OpenAsync(FinancialReportViewModel report, DeferredReports repository)
    {
        var pending = report.OpenCommand.ExecuteAsync(null);
        repository.Requests[^1].Succeed();
        await pending;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TrackingDashboard : IAnalyticsRepository
    {
        private readonly TaskCompletionSource<DashboardReadResult> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<DashboardQuery> Queries { get; } = [];
        public bool Delay { get; init; }
        public CancellationToken Token { get; private set; }

        public Task<DashboardReadResult> GetDashboardAsync(DashboardQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            Token = cancellationToken;
            return Delay ? _pending.Task : new FixtureAnalyticsRepository().GetDashboardAsync(query, cancellationToken);
        }

        public void Finish() => _pending.SetResult(DashboardReadResult.Unavailable);
    }

    private sealed class DeferredReports : IFinancialReportRepository
    {
        public List<DeferredRequest> Requests { get; } = [];

        public Task<FinancialReportSnapshot> GetFinancialReportAsync(FinancialReportRange range, CancellationToken cancellationToken = default)
        {
            var request = new DeferredRequest(range, cancellationToken);
            Requests.Add(request);
            return request.Completion.Task;
        }
    }

    private sealed record DeferredRequest(FinancialReportRange Range, CancellationToken Token)
    {
        public TaskCompletionSource<FinancialReportSnapshot> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Succeed()
        {
            var totals = new FinancialReportTotals(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var days = Enumerable.Range(0, Range.To.DayNumber - Range.From.DayNumber + 1)
                .Select(offset => new FinancialReportDay(Range.From.AddDays(offset), totals)).ToArray();
            Completion.SetResult(new(Range, AfterMidnight, days, totals, [], [], [], []));
        }

        public void Fail(Exception error) => Completion.SetException(error);
    }
}
