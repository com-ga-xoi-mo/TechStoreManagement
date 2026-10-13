using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;

namespace TechStore.Client.ViewModels.Dashboard;

public sealed record DashboardKpiCard(string Title, string Value, string Detail);
public sealed record DashboardChartLabel(string Text);
public sealed record DashboardLowStockRow(string ProductName, string VariantName, string QuantityLabel);
public sealed record DashboardRecentOrderRow(string OrderCode, string CustomerName, string ProductSummary,
    string AmountText, string CreatedAtText, string Status);

public sealed partial class DashboardViewModel : ObservableObject
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    private readonly IAnalyticsRepository _analyticsRepository;
    private CancellationTokenSource? _loadingCts;
    private long _requestVersion;
    private bool _isActive;

    [ObservableProperty]
    private DashboardPeriod selectedPeriod = DashboardPeriod.Last7Days;

    [ObservableProperty]
    private DashboardSnapshot? snapshot;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public bool HasData => Snapshot is not null && !IsError;
    public bool IsEmpty => Snapshot is { HasSales: false } && !IsLoading && !IsError;
    public bool HasNoLowStockItems => Snapshot is not null && LowStockItems.Count == 0 && !IsLoading && !IsError;
    public bool IsRecentOrdersEmpty => Snapshot is not null && RecentOrders.Count == 0 && !IsLoading && !IsError;
    public ObservableCollection<DashboardKpiCard> KpiCards { get; } = new();
    public DashboardKpiCard RevenueCard { get; private set; } = new("Tổng doanh thu", "—", "Đang tải dữ liệu");
    public DashboardKpiCard OrderCountCard { get; private set; } = new("Số đơn hàng", "—", "Đang tải dữ liệu");
    public DashboardKpiCard ProductCountCard { get; private set; } = new("Tổng sản phẩm", "—", "Tồn kho hiện tại");
    public DashboardKpiCard LowStockCountCard { get; private set; } = new("Cảnh báo tồn kho", "—", "Tồn kho hiện tại");
    public ObservableCollection<DashboardLowStockRow> LowStockItems { get; } = new();
    public ObservableCollection<DashboardRecentOrderRow> RecentOrders { get; } = new();
    public ObservableCollection<DashboardChartLabel> ChartPointLabels { get; } = new();
    public bool IsTodaySelected => SelectedPeriod == DashboardPeriod.Today;
    public bool IsLast7DaysSelected => SelectedPeriod == DashboardPeriod.Last7Days;
    public bool IsThisMonthSelected => SelectedPeriod == DashboardPeriod.ThisMonth;
    public string ChartSummary => Snapshot is null
        ? string.Empty
        : string.Join(" · ", Snapshot.RevenuePoints.Select(point =>
            $"{point.BucketStart.ToString(Snapshot.Period == DashboardPeriod.Today ? "HH:mm" : "dd/MM", VietnameseCulture)}: {FormatCurrency(point.Revenue)}"));
    public string SelectedPeriodLabel => SelectedPeriod switch
    {
        DashboardPeriod.Today => "Hôm nay",
        DashboardPeriod.Last7Days => "7 ngày qua",
        DashboardPeriod.ThisMonth => "Tháng này",
        _ => string.Empty
    };

    public string RevenueText => FormatCurrency(Snapshot?.Revenue ?? 0m);
    public string OrderCountText => (Snapshot?.CompletedOrderCount ?? 0).ToString("N0", VietnameseCulture);
    public string ProductCountText => (Snapshot?.TotalActiveProducts ?? 0).ToString("N0", VietnameseCulture);
    public string LowStockCountText => (Snapshot?.LowStockCount ?? 0).ToString("N0", VietnameseCulture);
    public string AverageOrderValueText => Snapshot?.AverageOrderValue is decimal amount
        ? $"TB {FormatCurrency(amount)} / đơn"
        : "Chưa có đơn hoàn thành";
    public string RevenueComparisonText => FormatComparison(Snapshot?.RevenueChangePercent);
    public string OrderCountComparisonText => FormatComparison(Snapshot?.OrderCountChangePercent);
    public string AsOfText => Snapshot is null
        ? string.Empty
        : $"Tồn kho tại {DashboardPeriodHelper.ToHoChiMinhTime(Snapshot.GeneratedAt).ToString("HH:mm", VietnameseCulture)}";
    public string RangeText => Snapshot is null
        ? string.Empty
        : $"{Snapshot.RangeStart.ToString("dd/MM", VietnameseCulture)} – {Snapshot.RangeEndExclusive.AddTicks(-1).ToString("dd/MM", VietnameseCulture)}";

    public DashboardViewModel(IAnalyticsRepository analyticsRepository)
    {
        _analyticsRepository = analyticsRepository;
    }

    partial void OnSelectedPeriodChanged(DashboardPeriod value)
    {
        OnPropertyChanged(nameof(SelectedPeriodLabel));
        OnPropertyChanged(nameof(IsTodaySelected));
        OnPropertyChanged(nameof(IsLast7DaysSelected));
        OnPropertyChanged(nameof(IsThisMonthSelected));
    }

    partial void OnSnapshotChanged(DashboardSnapshot? value)
    {
        UpdateKpiCards(value);
        NotifyDashboardProperties();
    }

    public Task InitializeAsync() => ActivateAsync();

    public async Task ActivateAsync()
    {
        _isActive = true;
        await LoadDataAsync(SelectedPeriod).ConfigureAwait(true);
    }

    public void Deactivate()
    {
        _isActive = false;
        Interlocked.Increment(ref _requestVersion);
        _loadingCts?.Cancel();
        IsLoading = false;
    }

    [RelayCommand]
    private async Task ChangePeriodAsync(DashboardPeriod period)
    {
        if (SelectedPeriod == period && Snapshot is not null && !IsError)
        {
            return;
        }

        SelectedPeriod = period;
        await LoadDataAsync(period).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        await LoadDataAsync(SelectedPeriod).ConfigureAwait(true);
    }

    private async Task LoadDataAsync(DashboardPeriod period)
    {
        _loadingCts?.Cancel();
        _loadingCts?.Dispose();
        var cts = new CancellationTokenSource();
        _loadingCts = cts;
        var requestVersion = Interlocked.Increment(ref _requestVersion);
        var cancellationToken = cts.Token;

        IsLoading = true;
        IsError = false;
        ErrorMessage = string.Empty;
        Snapshot = null;
        NotifyDashboardProperties();

        try
        {
            var result = await _analyticsRepository
                .GetDashboardSnapshotAsync(period, cancellationToken)
                .ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_isActive || requestVersion != Volatile.Read(ref _requestVersion))
            {
                return;
            }

            if (result.Period != period)
            {
                throw new InvalidOperationException("Repository returned a snapshot for a different period.");
            }

            Snapshot = result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer range selection or page navigation superseded this request.
        }
        catch (Exception)
        {
            if (_isActive && requestVersion == Volatile.Read(ref _requestVersion))
            {
                IsError = true;
                ErrorMessage = "Không thể tải dữ liệu tổng quan. Vui lòng thử lại.";
            }
        }
        finally
        {
            if (_isActive && requestVersion == Volatile.Read(ref _requestVersion))
            {
                IsLoading = false;
                NotifyDashboardProperties();
            }
        }
    }

    private void NotifyDashboardProperties()
    {
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoLowStockItems));
        OnPropertyChanged(nameof(IsRecentOrdersEmpty));
        OnPropertyChanged(nameof(SelectedPeriodLabel));
        OnPropertyChanged(nameof(RevenueText));
        OnPropertyChanged(nameof(OrderCountText));
        OnPropertyChanged(nameof(ProductCountText));
        OnPropertyChanged(nameof(LowStockCountText));
        OnPropertyChanged(nameof(AverageOrderValueText));
        OnPropertyChanged(nameof(RevenueComparisonText));
        OnPropertyChanged(nameof(OrderCountComparisonText));
        OnPropertyChanged(nameof(AsOfText));
        OnPropertyChanged(nameof(RangeText));
        OnPropertyChanged(nameof(ChartSummary));
    }

    private void UpdateKpiCards(DashboardSnapshot? value)
    {
        KpiCards.Clear();
        LowStockItems.Clear();
        RecentOrders.Clear();
        ChartPointLabels.Clear();
        if (value is null)
        {
            RevenueCard = new("Tổng doanh thu", "—", "Đang tải dữ liệu");
            OrderCountCard = new("Số đơn hàng", "—", "Đang tải dữ liệu");
            ProductCountCard = new("Tổng sản phẩm", "—", "Tồn kho hiện tại");
            LowStockCountCard = new("Cảnh báo tồn kho", "—", "Tồn kho hiện tại");
            AddKpiCards();
            return;
        }

        RevenueCard = new("Tổng doanh thu", FormatCurrency(value.Revenue), RevenueComparisonText);
        OrderCountCard = new("Số đơn hàng", OrderCountText, AverageOrderValueText);
        ProductCountCard = new("Tổng sản phẩm", ProductCountText, "Đang kinh doanh · hiện tại");
        LowStockCountCard = new("Cảnh báo tồn kho", LowStockCountText, "Sản phẩm dưới ngưỡng an toàn · hiện tại");
        AddKpiCards();
        foreach (var item in value.LowStockItems)
        {
            LowStockItems.Add(new DashboardLowStockRow(
                item.ProductName,
                item.VariantName,
                $"{item.AvailableQuantity:N0} {item.Unit}"));
        }

        foreach (var order in value.RecentOrders)
        {
            RecentOrders.Add(new DashboardRecentOrderRow(
                order.OrderCode,
                order.CustomerName,
                order.ProductSummary,
                FormatCurrency(order.TotalAmount),
                DashboardPeriodHelper.ToHoChiMinhTime(order.CreatedAt).ToString("dd/MM/yyyy HH:mm", VietnameseCulture),
                order.Status));
        }

        foreach (var point in value.RevenuePoints)
        {
            var label = point.BucketStart.ToString(value.Period == DashboardPeriod.Today ? "HH:mm" : "dd/MM", VietnameseCulture);
            ChartPointLabels.Add(new DashboardChartLabel($"{label}: {FormatCurrency(point.Revenue)}"));
        }
    }

    private void AddKpiCards()
    {
        KpiCards.Add(RevenueCard);
        KpiCards.Add(OrderCountCard);
        KpiCards.Add(ProductCountCard);
        KpiCards.Add(LowStockCountCard);
        OnPropertyChanged(nameof(RevenueCard));
        OnPropertyChanged(nameof(OrderCountCard));
        OnPropertyChanged(nameof(ProductCountCard));
        OnPropertyChanged(nameof(LowStockCountCard));
    }

    private static string FormatCurrency(decimal amount) =>
        $"{amount.ToString("N0", VietnameseCulture)} ₫";

    private static string FormatComparison(decimal? percent) => percent is decimal value
        ? $"{value:+0.0;-0.0;0.0}% so với kỳ trước"
        : "Chưa có kỳ so sánh";
}
