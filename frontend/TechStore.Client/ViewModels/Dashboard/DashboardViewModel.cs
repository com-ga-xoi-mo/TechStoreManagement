using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.Services.Development;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Dashboard;

public sealed partial class DashboardViewModel(
    IAnalyticsRepository analytics,
    DashboardPreviewOptions previewOptions) : PageViewModel
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    [ObservableProperty]
    private DashboardSnapshot? snapshot;

    [ObservableProperty]
    private bool isUnavailable;

    [ObservableProperty]
    private int selectedPeriodIndex = 1;

    public IReadOnlyList<DashboardPeriodOption> Periods { get; } =
    [
        new(DashboardPeriod.Today, "Hôm nay"),
        new(DashboardPeriod.Last7Days, "7 ngày qua"),
        new(DashboardPeriod.ThisMonth, "Tháng này")
    ];

    public DashboardPeriod SelectedPeriod => Periods[SelectedPeriodIndex].Period;
    public bool IsDemoMode => previewOptions.Enabled;
    public bool ShowPreviewPanels => IsContent || IsLoading;
    public bool CanChangePeriod => IsDemoMode && !IsLoading && Snapshot is not null;
    public string ReferenceLabel => Snapshot is null
        ? string.Empty
        : $"Kỳ {Snapshot.StartDate:dd/MM/yyyy}–{Snapshot.EndDate:dd/MM/yyyy} · Dữ liệu tại {Snapshot.AsOf:dd/MM/yyyy HH:mm} (UTC+07)";
    public string RevenueText => Snapshot is null ? "—" : FormatNumber(Snapshot.Summary.Revenue);
    public string OrdersText => Snapshot?.Summary.OrderCount.ToString("N0", VietnameseCulture) ?? "—";
    public string ProductsText => Snapshot?.Summary.ProductCount.ToString("N0", VietnameseCulture) ?? "—";
    public string LowStockText => Snapshot?.Summary.LowStockCount.ToString("N0", VietnameseCulture) ?? "—";
    public string RevenueTrendText => FormatChange(Snapshot?.Summary.RevenueChangePercent);
    public string OrdersTrendText => FormatChange(Snapshot?.Summary.OrderCountChangePercent);
    public string ProductDetailText => Snapshot is null
        ? "Chưa có dữ liệu Analytics"
        : "Sản phẩm · ảnh chụp tại thời điểm dữ liệu";
    public string LowStockTrendText => Snapshot is null ? string.Empty : "Cần kiểm tra";
    public string LowStockDetailText => Snapshot is null
        ? "Chưa có dữ liệu tồn kho"
        : "Dưới ngưỡng minh họa · tối đa 5 dòng";
    public IReadOnlyList<DashboardLowStockRow> LowStockRows => Snapshot?.LowStockItems
        .Select(item => new DashboardLowStockRow(item.ProductName, item.VariantDescription,
            item.AvailableQuantity.ToString("N0", VietnameseCulture), item.ReorderThreshold.ToString("N0", VietnameseCulture)))
        .ToArray() ?? [];
    public IReadOnlyList<DashboardDailyRevenueRow> DailyRevenueRows => Snapshot?.DailyRevenue
        .Select(point => new DashboardDailyRevenueRow(
            point.Date.ToString("ddd dd/MM", VietnameseCulture), $"{FormatNumber(point.Revenue)} ₫"))
        .ToArray() ?? [];
    public IReadOnlyList<DashboardRecentOrderRow> RecentOrderRows => Snapshot?.RecentOrders
        .Select(order => new DashboardRecentOrderRow(
            $"#{order.DemoDisplayId}", order.CustomerDisplayName, order.ProductSummary,
            order.SerialLabel is null ? string.Empty : $"Số sê-ri: {order.SerialLabel}",
            $"{FormatNumber(order.Total)} ₫", GetStatusLabel(order.Status),
            order.CreatedAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM HH:mm", VietnameseCulture)))
        .ToArray() ?? [];

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SelectPeriodAsync(int periodIndex, CancellationToken cancellationToken)
    {
        if (periodIndex < 0 || periodIndex >= Periods.Count || periodIndex == SelectedPeriodIndex)
            return;

        SelectedPeriodIndex = periodIndex;
        OnPropertyChanged(nameof(SelectedPeriod));
        await LoadSnapshotAsync(Periods[periodIndex].Period, cancellationToken);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RefreshAsync(CancellationToken cancellationToken) =>
        await LoadSnapshotAsync(SelectedPeriod, cancellationToken);

    protected override bool ManagesLoadState => true;

    protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken) =>
        LoadSnapshotAsync(SelectedPeriod, cancellationToken);

    protected override Task RetryContentAsync() => LoadSnapshotAsync(SelectedPeriod, ActivationToken);

    protected override void OnLatestRequestFailed(Exception exception)
    {
        Snapshot = null;
        IsUnavailable = false;
        ContentState = PageContentState.Error;
    }

    protected override string GetSafeErrorMessage(Exception exception) =>
        "Chưa tải được số liệu Dashboard. Hãy thử lại.";

    protected override void OnContentStateChanged()
    {
        OnPropertyChanged(nameof(ReferenceLabel));
        OnPropertyChanged(nameof(RevenueText));
        OnPropertyChanged(nameof(OrdersText));
        OnPropertyChanged(nameof(ProductsText));
        OnPropertyChanged(nameof(ProductDetailText));
        OnPropertyChanged(nameof(LowStockText));
        OnPropertyChanged(nameof(LowStockTrendText));
        OnPropertyChanged(nameof(LowStockDetailText));
        OnPropertyChanged(nameof(RevenueTrendText));
        OnPropertyChanged(nameof(OrdersTrendText));
        OnPropertyChanged(nameof(IsDemoMode));
        OnPropertyChanged(nameof(ShowPreviewPanels));
        OnPropertyChanged(nameof(CanChangePeriod));
        OnPropertyChanged(nameof(LowStockRows));
        OnPropertyChanged(nameof(DailyRevenueRows));
        OnPropertyChanged(nameof(RecentOrderRows));
    }

    private async Task<bool> LoadSnapshotAsync(DashboardPeriod period, CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;
        ContentState = PageContentState.Loading;
        IsUnavailable = false;
        Snapshot = null;
        return await RunLatestRequestAsync(
            token => GetDashboardAsync(period, token),
            CommitResult,
            result => result.IsAvailable,
            () => Snapshot is not null);
    }

    private Task<DashboardReadResult> GetDashboardAsync(DashboardPeriod period, CancellationToken cancellationToken) =>
        analytics.GetDashboardAsync(new DashboardQuery(period), cancellationToken);

    private void CommitResult(DashboardReadResult result)
    {
        Snapshot = result.Snapshot;
        IsUnavailable = !result.IsAvailable;
        OnPropertyChanged(nameof(ReferenceLabel));
        OnPropertyChanged(nameof(RevenueText));
        OnPropertyChanged(nameof(OrdersText));
        OnPropertyChanged(nameof(ProductsText));
        OnPropertyChanged(nameof(ProductDetailText));
        OnPropertyChanged(nameof(LowStockText));
        OnPropertyChanged(nameof(LowStockTrendText));
        OnPropertyChanged(nameof(LowStockDetailText));
        OnPropertyChanged(nameof(RevenueTrendText));
        OnPropertyChanged(nameof(OrdersTrendText));
        OnPropertyChanged(nameof(IsDemoMode));
        OnPropertyChanged(nameof(ShowPreviewPanels));
        OnPropertyChanged(nameof(CanChangePeriod));
        OnPropertyChanged(nameof(LowStockRows));
        OnPropertyChanged(nameof(DailyRevenueRows));
        OnPropertyChanged(nameof(RecentOrderRows));
        ContentState = result.IsAvailable ? PageContentState.Content : PageContentState.Empty;
    }

    private static string FormatNumber(decimal value) => value.ToString("N0", VietnameseCulture);

    private static string FormatChange(decimal? value) => value is null
        ? string.Empty
        : $"{(value.Value >= 0 ? "+" : string.Empty)}{value.Value.ToString("0.##", VietnameseCulture)}%";

    private static string GetStatusLabel(TechStore.Shared.Enums.OrderStatus status) => status switch
    {
        TechStore.Shared.Enums.OrderStatus.Pending => "Chờ thanh toán",
        TechStore.Shared.Enums.OrderStatus.Processing => "Đang xử lý",
        TechStore.Shared.Enums.OrderStatus.Completed => "Hoàn thành",
        TechStore.Shared.Enums.OrderStatus.Cancelled => "Đã hủy",
        TechStore.Shared.Enums.OrderStatus.PartiallyRefunded => "Hoàn một phần",
        TechStore.Shared.Enums.OrderStatus.Refunded => "Đã hoàn tiền",
        _ => "Trạng thái khác"
    };

}

public sealed record DashboardPeriodOption(DashboardPeriod Period, string Label);
public sealed record DashboardDailyRevenueRow(string DateText, string RevenueText);
public sealed record DashboardLowStockRow(string ProductName, string VariantDescription, string AvailableText, string ThresholdText);
public sealed record DashboardRecentOrderRow(string DemoDisplayId, string CustomerDisplayName, string ProductSummary,
    string SerialLabel, string TotalText, string StatusLabel, string CreatedAtText);
