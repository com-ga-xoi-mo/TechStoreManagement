using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Development;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Enums;

namespace TechStore.Client.ViewModels.Dashboard;

public sealed partial class DashboardViewModel : PageViewModel
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private const string DateInputFormat = "dd/MM/yyyy";
    private readonly IAnalyticsRepository _analytics;
    private readonly DashboardPreviewOptions _previewOptions;
    private readonly ISessionStore _sessionStore;
    private bool _hasActivated;
    private DashboardQuery _appliedQuery = new(DashboardPeriod.Last7Days);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLiveReference))]
    private DashboardSnapshot? snapshot;
    [ObservableProperty] private int selectedPeriodIndex = 1;
    [ObservableProperty] private bool showCustomRange;
    [ObservableProperty] private string customFrom = string.Empty;
    [ObservableProperty] private string customTo = string.Empty;
    [ObservableProperty] private string customFromError = string.Empty;
    [ObservableProperty] private string customToError = string.Empty;

    public IReadOnlyList<DashboardPeriodOption> Periods { get; } =
    [
        new(DashboardPeriod.Today, "Hôm nay"),
        new(DashboardPeriod.Last7Days, "7 ngày qua"),
        new(DashboardPeriod.ThisMonth, "Tháng này")
    ];

    public DashboardViewModel(
        IAnalyticsRepository analytics,
        DashboardPreviewOptions previewOptions,
        ISessionStore sessionStore)
    {
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        _previewOptions = previewOptions ?? throw new ArgumentNullException(nameof(previewOptions));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _sessionStore.SessionChanged += OnSessionChanged;
    }

    public DashboardPeriod SelectedPeriod => _appliedQuery.Period;
    public bool IsDemoMode => _previewOptions.Enabled;
    public bool IsLiveMode => !_previewOptions.Enabled;
    public bool ShowPreviewBanner => IsDemoMode;
    public bool ShowLiveReference => IsLiveMode && Snapshot is not null;
    public bool ShowDashboardPanels => IsContent || IsLoading;
    // Kept as a source-compatible alias while the XAML page is replaced in the same change.
    public bool ShowPreviewPanels => ShowDashboardPanels;
    public bool IsUnavailable => Snapshot is null && IsEmpty && IsLiveMode && _sessionStore.Current.Status == SessionStatus.Anonymous;
    public bool CanChangePeriod => !IsLoading;
    public bool CanRefresh => !IsLoading;
    public bool IsSignedOut => IsLiveMode && _sessionStore.Current.Status == SessionStatus.Anonymous && IsEmpty;
    public string EmptyDashboardMessage => IsSignedOut
        ? "Đăng nhập bằng tài khoản Quản trị viên hoặc Quản lý để xem thống kê."
        : "Chưa có dữ liệu trong kỳ đã chọn.";
    public string ReferenceLabel => Snapshot is null
        ? string.Empty
        : IsDemoMode
            ? $"Kỳ {Snapshot.StartDate:dd/MM/yyyy}–{Snapshot.EndDate:dd/MM/yyyy} · Dữ liệu minh họa tại {Snapshot.AsOf.ToOffset(VietnamOffset):dd/MM/yyyy HH:mm} (UTC+07)"
            : $"Kỳ {Snapshot.StartDate:dd/MM/yyyy}–{Snapshot.EndDate:dd/MM/yyyy} · Cập nhật {Snapshot.AsOf.ToOffset(VietnamOffset):dd/MM/yyyy HH:mm} (UTC+07)";
    public string ChartPeakSummary
    {
        get
        {
            if (Snapshot is null || Snapshot.DailyRevenue.Count == 0) return ReferenceLabel;
            var peak = Snapshot.DailyRevenue.MaxBy(point => point.Revenue)!;
            var range = Snapshot.StartDate == Snapshot.EndDate
                ? Snapshot.StartDate.ToString("dddd dd/MM", VietnameseCulture)
                : $"{Snapshot.StartDate:dd/MM}–{Snapshot.EndDate:dd/MM}";
            return $"{range} · Đỉnh {peak.Date.ToString("dddd", VietnameseCulture)}: {FormatNumber(peak.Revenue)} ₫ · Cập nhật {Snapshot.AsOf.ToOffset(TimeSpan.FromHours(7)):HH:mm} UTC+07";
        }
    }
    public string RevenueText => Snapshot is null ? "—" : FormatNumber(Snapshot.Summary.Revenue);
    public string OrdersText => Snapshot?.Summary.OrderCount.ToString("N0", VietnameseCulture) ?? "—";
    public string ProductsText => Snapshot?.Summary.ProductCount.ToString("N0", VietnameseCulture) ?? "—";
    public string LowStockText => Snapshot?.Summary.LowStockCount.ToString("N0", VietnameseCulture) ?? "—";
    public string RevenueTrendText => FormatChange(Snapshot?.Summary.RevenueChangePercent);
    public string RevenueDetailText => Snapshot?.Summary.RevenueChangePercent is null ? string.Empty : "so với kỳ trước";
    public string OrdersTrendText => FormatChange(Snapshot?.Summary.OrderCountChangePercent);
    public string ProductDetailText => Snapshot is null
        ? "Số sản phẩm đang hoạt động tại thời điểm đọc"
        : "Sản phẩm đang hoạt động · số liệu tại thời điểm đọc";
    public string LowStockTrendText => Snapshot is null ? string.Empty : "Theo ngưỡng tồn kho";
    public string LowStockDetailText => Snapshot is null
        ? "Số balance có Quantity ≤ MinStockAlert"
        : "Số balance có Quantity ≤ MinStockAlert · tối đa 5 dòng";
    public string LowStockEmptyText => "Không có balance tồn kho nào dưới ngưỡng.";
    public string RecentOrdersEmptyText => "Không có đơn hàng phát sinh trong kỳ.";
    public bool LowStockIsEmpty => Snapshot is not null && Snapshot.LowStockItems.Count == 0;
    public bool RecentOrdersAreEmpty => Snapshot is not null && Snapshot.RecentOrders.Count == 0;

    public IReadOnlyList<DashboardLowStockRow> LowStockRows => Snapshot?.LowStockItems
        .Select(item => new DashboardLowStockRow(
            item.ProductName,
            item.VariantName ?? "Sản phẩm không có biến thể",
            item.AvailableQuantity.ToString("N0", VietnameseCulture),
            item.MinStockAlert.ToString("N0", VietnameseCulture),
            item.Quantity.ToString("N0", VietnameseCulture),
            item.ReservedQuantity.ToString("N0", VietnameseCulture)))
        .ToArray() ?? [];

    public IReadOnlyList<DashboardDailyRevenueRow> DailyRevenueRows => Snapshot?.DailyRevenue
        .Select(point => new DashboardDailyRevenueRow(
            point.Date.ToString("ddd dd/MM", VietnameseCulture), $"{FormatNumber(point.Revenue)} ₫"))
        .ToArray() ?? [];

    public IReadOnlyList<DashboardRecentOrderRow> RecentOrderRows => Snapshot?.RecentOrders
        .Select(order => new DashboardRecentOrderRow(
            order.OrderCode,
            order.CustomerName ?? "Khách lẻ",
            order.ProductSummary,
            $"{FormatNumber(order.Total)} ₫",
            GetStatusLabel(order.Status),
            order.CreatedAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM HH:mm", VietnameseCulture),
            order.Status))
        .ToArray() ?? [];

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SelectPeriodAsync(int periodIndex, CancellationToken cancellationToken)
    {
        if (periodIndex < 0 || periodIndex >= Periods.Count || periodIndex == SelectedPeriodIndex || IsLoading)
            return;

        ShowCustomRange = false;
        await ApplyQueryAsync(new(Periods[periodIndex].Period), periodIndex, cancellationToken);
    }

    [RelayCommand]
    private void ToggleCustomRange()
    {
        if (IsLiveMode) ShowCustomRange = !ShowCustomRange;
    }

    [RelayCommand]
    private async Task ApplyCustomRangeAsync(CancellationToken cancellationToken)
    {
        if (!IsLiveMode || IsLoading) return;

        var fromValid = DateOnly.TryParseExact(CustomFrom, DateInputFormat, VietnameseCulture, DateTimeStyles.None, out var from);
        var toValid = DateOnly.TryParseExact(CustomTo, DateInputFormat, VietnameseCulture, DateTimeStyles.None, out var to);
        CustomFromError = fromValid ? string.Empty : "Nhập ngày bắt đầu theo dd/MM/yyyy.";
        CustomToError = toValid ? string.Empty : "Nhập ngày kết thúc theo dd/MM/yyyy.";
        if (!fromValid || !toValid) return;

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(VietnamOffset).DateTime);
        if (from == DateOnly.MinValue)
            CustomFromError = "Ngày bắt đầu nằm ngoài khoảng hỗ trợ.";
        if (to > today || to == DateOnly.MaxValue)
            CustomToError = "Chọn ngày kết thúc không muộn hơn hôm nay theo giờ Việt Nam.";
        else if (to < from)
            CustomToError = "Ngày kết thúc phải từ ngày bắt đầu trở đi.";
        else if (to.DayNumber - from.DayNumber >= 31)
            CustomToError = "Chọn khoảng tối đa 31 ngày.";
        if (CustomFromError.Length != 0 || CustomToError.Length != 0) return;

        await ApplyQueryAsync(new(DashboardPeriod.Custom, (from, to)), -1, cancellationToken);
    }

    partial void OnCustomFromChanged(string value)
    {
        CustomFromError = string.Empty;
        CustomToError = string.Empty;
    }

    partial void OnCustomToChanged(string value) => CustomToError = string.Empty;

    private Task<bool> ApplyQueryAsync(DashboardQuery query, int periodIndex, CancellationToken cancellationToken)
    {
        _appliedQuery = query;
        SelectedPeriodIndex = periodIndex;
        OnPropertyChanged(nameof(SelectedPeriod));
        return LoadSnapshotAsync(query, cancellationToken);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!IsLoading) await LoadSnapshotAsync(_appliedQuery, cancellationToken);
    }

    protected override bool ManagesLoadState => true;

    protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken)
    {
        _hasActivated = true;
        return LoadSnapshotAsync(_appliedQuery, cancellationToken);
    }

    protected override Task RetryContentAsync() => LoadSnapshotAsync(_appliedQuery, ActivationToken);

    protected override void OnLatestRequestFailed(Exception exception)
    {
        Snapshot = null;
        ContentState = PageContentState.Error;
    }

    protected override string GetSafeErrorMessage(Exception exception) => exception switch
    {
        ApiException { StatusCode: 401 } when _sessionStore.Current.Status == SessionStatus.Anonymous =>
            "Đăng nhập bằng tài khoản Quản trị viên hoặc Quản lý để xem thống kê.",
        ApiException { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục.",
        ApiException { StatusCode: 403 } => "Bạn không có quyền xem Dashboard Analytics.",
        ApiException { StatusCode: 400 } => "Kỳ thống kê không hợp lệ. Hãy kiểm tra khoảng ngày theo giờ Việt Nam.",
        ApiException { Kind: ApiErrorKind.Timeout } => "Yêu cầu Dashboard quá thời gian chờ. Hãy thử lại.",
        ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối Dashboard API. Hãy kiểm tra kết nối rồi thử lại.",
        ApiException { Kind: ApiErrorKind.InvalidResponse } => "Dashboard API trả dữ liệu không đúng định dạng.",
        _ => "Chưa tải được số liệu Dashboard. Hãy thử lại."
    };

    protected override void OnContentStateChanged()
    {
        OnPropertyChanged(nameof(ReferenceLabel));
        OnPropertyChanged(nameof(ChartPeakSummary));
        OnPropertyChanged(nameof(RevenueText));
        OnPropertyChanged(nameof(OrdersText));
        OnPropertyChanged(nameof(ProductsText));
        OnPropertyChanged(nameof(ProductDetailText));
        OnPropertyChanged(nameof(LowStockText));
        OnPropertyChanged(nameof(LowStockTrendText));
        OnPropertyChanged(nameof(LowStockDetailText));
        OnPropertyChanged(nameof(RevenueTrendText));
        OnPropertyChanged(nameof(RevenueDetailText));
        OnPropertyChanged(nameof(OrdersTrendText));
        OnPropertyChanged(nameof(IsDemoMode));
        OnPropertyChanged(nameof(IsLiveMode));
        OnPropertyChanged(nameof(ShowPreviewBanner));
        OnPropertyChanged(nameof(ShowDashboardPanels));
        OnPropertyChanged(nameof(ShowPreviewPanels));
        OnPropertyChanged(nameof(CanChangePeriod));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(IsUnavailable));
        OnPropertyChanged(nameof(IsSignedOut));
        OnPropertyChanged(nameof(EmptyDashboardMessage));
        OnPropertyChanged(nameof(LowStockRows));
        OnPropertyChanged(nameof(DailyRevenueRows));
        OnPropertyChanged(nameof(RecentOrderRows));
        OnPropertyChanged(nameof(LowStockIsEmpty));
        OnPropertyChanged(nameof(RecentOrdersAreEmpty));
    }

    protected override void OnDeactivated()
    {
        _hasActivated = false;
        _sessionStore.SessionChanged -= OnSessionChanged;
    }

    private async Task<bool> LoadSnapshotAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;
        ContentState = PageContentState.Loading;
        Snapshot = null;
        OnPresentationChanged();

        var sessionGeneration = _sessionStore.Current.Generation;
        return await RunLatestRequestAsync(
            async token => new RequestSnapshot(
                await _analytics.GetDashboardAsync(query, token),
                sessionGeneration),
            result =>
            {
                if (IsLiveMode && _sessionStore.Current.Generation != result.SessionGeneration)
                    return;
                CommitResult(result.Result);
            },
            result => result.Result.IsAvailable,
            () => Snapshot is not null);
    }

    private void CommitResult(DashboardReadResult result)
    {
        Snapshot = result.Snapshot;
        ContentState = result.IsAvailable ? PageContentState.Content : PageContentState.Empty;
        OnPresentationChanged();
    }

    private void OnSessionChanged(object? sender, SessionChangedEventArgs args)
    {
        if (!_hasActivated || !IsLiveMode) return;

        CancelLatestRequest();
        Snapshot = null;
        ErrorMessage = string.Empty;
        if (args.Status == SessionStatus.Expired)
        {
            ErrorMessage = "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục.";
            ContentState = PageContentState.Error;
            OnPresentationChanged();
            return;
        }

        if (args.Status == SessionStatus.Anonymous)
        {
            ContentState = PageContentState.Empty;
            OnPresentationChanged();
            return;
        }

        _ = LoadSnapshotAsync(_appliedQuery, ActivationToken);
    }

    private void OnPresentationChanged()
    {
        OnPropertyChanged(nameof(ReferenceLabel));
        OnPropertyChanged(nameof(ChartPeakSummary));
        OnPropertyChanged(nameof(RevenueText));
        OnPropertyChanged(nameof(OrdersText));
        OnPropertyChanged(nameof(ProductsText));
        OnPropertyChanged(nameof(ProductDetailText));
        OnPropertyChanged(nameof(LowStockText));
        OnPropertyChanged(nameof(LowStockTrendText));
        OnPropertyChanged(nameof(LowStockDetailText));
        OnPropertyChanged(nameof(RevenueTrendText));
        OnPropertyChanged(nameof(RevenueDetailText));
        OnPropertyChanged(nameof(OrdersTrendText));
        OnPropertyChanged(nameof(ShowDashboardPanels));
        OnPropertyChanged(nameof(ShowPreviewPanels));
        OnPropertyChanged(nameof(CanChangePeriod));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(IsUnavailable));
        OnPropertyChanged(nameof(IsSignedOut));
        OnPropertyChanged(nameof(EmptyDashboardMessage));
        OnPropertyChanged(nameof(LowStockRows));
        OnPropertyChanged(nameof(DailyRevenueRows));
        OnPropertyChanged(nameof(RecentOrderRows));
        OnPropertyChanged(nameof(LowStockIsEmpty));
        OnPropertyChanged(nameof(RecentOrdersAreEmpty));
    }

    private static string FormatNumber(decimal value) => value.ToString("N0", VietnameseCulture);

    private static string FormatChange(decimal? value) => value is null
        ? string.Empty
        : $"{(value.Value >= 0 ? "+" : string.Empty)}{value.Value.ToString("0.##", VietnameseCulture)}%";

    private static string GetStatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Chờ thanh toán",
        OrderStatus.Processing => "Đang xử lý",
        OrderStatus.Completed => "Hoàn thành",
        OrderStatus.Cancelled => "Đã hủy",
        OrderStatus.PartiallyRefunded => "Hoàn một phần",
        OrderStatus.Refunded => "Đã hoàn tiền",
        _ => "Trạng thái khác"
    };

    private sealed record RequestSnapshot(DashboardReadResult Result, long SessionGeneration);
}

public sealed record DashboardPeriodOption(DashboardPeriod Period, string Label);
public sealed record DashboardDailyRevenueRow(string DateText, string RevenueText);
public sealed record DashboardLowStockRow(string ProductName, string VariantName, string AvailableText,
    string ThresholdText, string QuantityText, string ReservedText)
{
    public string BalanceSummary => $"Tồn {QuantityText} · giữ {ReservedText} · khả dụng {AvailableText} · ngưỡng {ThresholdText}";
    public string IconGlyph => ProductName.Contains("ipad", StringComparison.OrdinalIgnoreCase)
        ? "\uE7F4"
        : ProductName.Contains("iphone", StringComparison.OrdinalIgnoreCase)
            || ProductName.Contains("galaxy", StringComparison.OrdinalIgnoreCase)
            ? "\uE8EA"
            : "\uE7F8";
}
public sealed record DashboardRecentOrderRow(string OrderCode, string CustomerDisplayName,
    string ProductSummary, string TotalText, string StatusLabel, string CreatedAtText, OrderStatus Status)
{
    public string CustomerAndDate => $"{CustomerDisplayName} · {CreatedAtText}";
    public string TotalAndStatus => $"{TotalText} · {StatusLabel}";
}
