using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using TechStore.Client.Models.Customers;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Customers;

public sealed partial class CustomerDetailViewModel : PageViewModel
{
    private const int PageSize = 10;
    private readonly ICustomerRepository _customers;
    private Guid _customerId;
    private long _loadVersion;

    [ObservableProperty] private CustomerProfile? profile;
    [ObservableProperty] private CustomerPage<CustomerLoyaltyTransaction>? pointHistory;
    [ObservableProperty] private CustomerPage<CustomerOrder>? orderHistory;
    [ObservableProperty] private CustomerPage<CustomerPurchasedItem>? purchasedItems;
    [ObservableProperty] private int pointsPage = 1;
    [ObservableProperty] private int ordersPage = 1;
    [ObservableProperty] private int devicesPage = 1;
    [ObservableProperty] private bool isPointsLoading;
    [ObservableProperty] private bool isOrdersLoading;
    [ObservableProperty] private bool isDevicesLoading;
    [ObservableProperty] private string selectedHistoryTab = "orders";
    [ObservableProperty] private bool isChangingActivity;
    [ObservableProperty] private bool activityChanged;
    [ObservableProperty] private string activityErrorMessage = string.Empty;

    public bool HasProfile => Profile is not null;
    public bool CanDeactivate => Profile?.IsActive == true && !IsChangingActivity;
    public bool CanActivate => Profile?.IsActive == false && !IsChangingActivity;
    public bool HasActivityError => !string.IsNullOrWhiteSpace(ActivityErrorMessage);
    public string FullName => Profile?.FullName ?? string.Empty;
    public string Phone => Profile?.Phone ?? string.Empty;
    public string EmailLabel => string.IsNullOrWhiteSpace(Profile?.Email) ? "Chưa có email" : Profile.Email;
    public string Address => Profile?.Address ?? string.Empty;
    public bool HasAddress => !string.IsNullOrWhiteSpace(Profile?.Address);
    public string TierCode => CustomerTierPresentation.FromTier(Profile?.Tier).Tier;
    public string TierLabel => Profile is null
        ? string.Empty
        : CustomerTierPresentation.FromTier(Profile.Tier).BadgeLabel;
    public string LoyaltyPointsLabel => $"★  {Profile?.LoyaltyPoints.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) ?? "0"} điểm tích lũy";
    public string TotalSpentLabel => $"{Profile?.TotalSpent.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) ?? "0"} ₫";
    public string OrderCountLabel => $"{Profile?.OrderCount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) ?? "0"} đơn";
    public string OrderCountCaption
    {
        get
        {
            if (Profile is not { } profile) return string.Empty;
            var caption = $"{profile.CompletedOrderCount} thành công · {profile.CancelledOrderCount} đơn hủy";
            var other = Math.Max(0, profile.OrderCount - profile.CompletedOrderCount - profile.CancelledOrderCount);
            return other > 0 ? $"{caption} · {other} đơn khác" : caption;
        }
    }
    public string RegistrationDateLabel => Profile?.CreatedAt.ToLocalTime()
        .ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN")) ?? string.Empty;
    public string MembershipCaption
    {
        get
        {
            if (Profile is not { } profile) return string.Empty;
            if (!profile.IsActive) return "Đã vô hiệu hóa";
            var created = profile.CreatedAt.ToLocalTime();
            var months = Math.Max(0, (DateTime.Now.Year - created.Year) * 12 + DateTime.Now.Month - created.Month);
            return $"Thành viên {months} tháng";
        }
    }
    public string ActivityStatusLabel => Profile?.IsActive == true
        ? "Khách hàng đang hoạt động"
        : Profile is null ? string.Empty : "Khách hàng đã vô hiệu hóa";
    public bool IsOrdersTabSelected => SelectedHistoryTab == "orders";
    public bool IsDevicesTabSelected => SelectedHistoryTab == "devices";
    public bool IsPointsTabSelected => SelectedHistoryTab == "points";
    public IReadOnlyList<CustomerOrder> Orders => OrderHistory?.Items ?? Array.Empty<CustomerOrder>();
    public IReadOnlyList<CustomerPurchasedItem> Devices => PurchasedItems?.Items ?? Array.Empty<CustomerPurchasedItem>();
    public IReadOnlyList<CustomerLoyaltyTransaction> PointTransactions => PointHistory?.Items ?? Array.Empty<CustomerLoyaltyTransaction>();
    public string OrderHistoryCountLabel => OrderHistory?.TotalCount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) ?? "0";
    public string DeviceCountLabel => PurchasedItems?.TotalCount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) ?? "0";
    public string PointTransactionCountLabel => PointHistory?.TotalCount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) ?? "0";
    public bool HasNoOrders => OrderHistory is { TotalCount: 0 };
    public bool HasNoDevices => PurchasedItems is { TotalCount: 0 };
    public bool HasNoPointTransactions => PointHistory is { TotalCount: 0 };
    public string OrdersPageLabel => FormatPageLabel(OrderHistory);
    public string DevicesPageLabel => FormatPageLabel(PurchasedItems);
    public string PointsPageLabel => FormatPageLabel(PointHistory);
    public bool CanPreviousOrdersPage => !IsOrdersLoading && OrdersPage > 1;
    public bool CanNextOrdersPage => !IsOrdersLoading && OrderHistory is { } orders && OrdersPage < orders.TotalPages;
    public bool CanPreviousDevicesPage => !IsDevicesLoading && DevicesPage > 1;
    public bool CanNextDevicesPage => !IsDevicesLoading && PurchasedItems is { } devices && DevicesPage < devices.TotalPages;
    public bool CanPreviousPointsPage => !IsPointsLoading && PointsPage > 1;
    public bool CanNextPointsPage => !IsPointsLoading && PointHistory is { } points && PointsPage < points.TotalPages;

    public CustomerDetailViewModel(ICustomerRepository customers) =>
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));

    partial void OnProfileChanged(CustomerProfile? value)
    {
        OnPropertyChanged(nameof(HasProfile));
        OnPropertyChanged(nameof(CanDeactivate));
        OnPropertyChanged(nameof(CanActivate));
        OnPropertyChanged(nameof(FullName));
        OnPropertyChanged(nameof(Phone));
        OnPropertyChanged(nameof(EmailLabel));
        OnPropertyChanged(nameof(Address));
        OnPropertyChanged(nameof(HasAddress));
        OnPropertyChanged(nameof(TierCode));
        OnPropertyChanged(nameof(TierLabel));
        OnPropertyChanged(nameof(LoyaltyPointsLabel));
        OnPropertyChanged(nameof(TotalSpentLabel));
        OnPropertyChanged(nameof(OrderCountLabel));
        OnPropertyChanged(nameof(OrderCountCaption));
        OnPropertyChanged(nameof(RegistrationDateLabel));
        OnPropertyChanged(nameof(MembershipCaption));
        OnPropertyChanged(nameof(ActivityStatusLabel));
        ActivateCustomerCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedHistoryTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsOrdersTabSelected));
        OnPropertyChanged(nameof(IsDevicesTabSelected));
        OnPropertyChanged(nameof(IsPointsTabSelected));
    }

    partial void OnOrderHistoryChanged(CustomerPage<CustomerOrder>? value)
    {
        OnPropertyChanged(nameof(Orders));
        OnPropertyChanged(nameof(OrderHistoryCountLabel));
        OnPropertyChanged(nameof(HasNoOrders));
        OnPropertyChanged(nameof(OrdersPageLabel));
        OnPropertyChanged(nameof(CanPreviousOrdersPage));
        OnPropertyChanged(nameof(CanNextOrdersPage));
    }

    partial void OnPurchasedItemsChanged(CustomerPage<CustomerPurchasedItem>? value)
    {
        OnPropertyChanged(nameof(Devices));
        OnPropertyChanged(nameof(DeviceCountLabel));
        OnPropertyChanged(nameof(HasNoDevices));
        OnPropertyChanged(nameof(DevicesPageLabel));
        OnPropertyChanged(nameof(CanPreviousDevicesPage));
        OnPropertyChanged(nameof(CanNextDevicesPage));
    }

    partial void OnPointHistoryChanged(CustomerPage<CustomerLoyaltyTransaction>? value)
    {
        OnPropertyChanged(nameof(PointTransactions));
        OnPropertyChanged(nameof(PointTransactionCountLabel));
        OnPropertyChanged(nameof(HasNoPointTransactions));
        OnPropertyChanged(nameof(PointsPageLabel));
        OnPropertyChanged(nameof(CanPreviousPointsPage));
        OnPropertyChanged(nameof(CanNextPointsPage));
    }

    partial void OnOrdersPageChanged(int value)
    {
        OnPropertyChanged(nameof(OrdersPageLabel));
        OnPropertyChanged(nameof(CanPreviousOrdersPage));
        OnPropertyChanged(nameof(CanNextOrdersPage));
    }

    partial void OnDevicesPageChanged(int value)
    {
        OnPropertyChanged(nameof(DevicesPageLabel));
        OnPropertyChanged(nameof(CanPreviousDevicesPage));
        OnPropertyChanged(nameof(CanNextDevicesPage));
    }

    partial void OnPointsPageChanged(int value)
    {
        OnPropertyChanged(nameof(PointsPageLabel));
        OnPropertyChanged(nameof(CanPreviousPointsPage));
        OnPropertyChanged(nameof(CanNextPointsPage));
    }

    partial void OnIsOrdersLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPreviousOrdersPage));
        OnPropertyChanged(nameof(CanNextOrdersPage));
    }

    partial void OnIsDevicesLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPreviousDevicesPage));
        OnPropertyChanged(nameof(CanNextDevicesPage));
    }

    partial void OnIsPointsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPreviousPointsPage));
        OnPropertyChanged(nameof(CanNextPointsPage));
    }

    partial void OnIsChangingActivityChanged(bool value)
    {
        OnPropertyChanged(nameof(CanDeactivate));
        OnPropertyChanged(nameof(CanActivate));
        ActivateCustomerCommand.NotifyCanExecuteChanged();
    }

    partial void OnActivityErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasActivityError));

    [RelayCommand] private void ShowOrdersTab() => SelectedHistoryTab = "orders";
    [RelayCommand] private void ShowDevicesTab() => SelectedHistoryTab = "devices";
    [RelayCommand] private void ShowPointsTab() => SelectedHistoryTab = "points";

    private static string FormatPageLabel<T>(CustomerPage<T>? page) =>
        $"Trang {page?.Page ?? 1} / {Math.Max(1, page?.TotalPages ?? 1)}";

    public async Task LoadAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));
        _customerId = customerId;
        var version = Interlocked.Increment(ref _loadVersion);
        Profile = null;
        PointHistory = null;
        OrderHistory = null;
        PurchasedItems = null;
        PointsPage = OrdersPage = DevicesPage = 1;
        ActivityChanged = false;
        ActivityErrorMessage = string.Empty;
        ContentState = PageContentState.Loading;
        ErrorMessage = string.Empty;
        try
        {
            var profileTask = _customers.GetByIdAsync(customerId, cancellationToken);
            var pointsTask = _customers.GetLoyaltyHistoryAsync(customerId, 1, PageSize, cancellationToken);
            var ordersTask = _customers.GetOrderHistoryAsync(customerId, 1, PageSize, cancellationToken);
            var devicesTask = _customers.GetPurchasedItemsAsync(customerId, 1, PageSize, cancellationToken);
            await Task.WhenAll(profileTask, pointsTask, ordersTask, devicesTask);
            if (version != Volatile.Read(ref _loadVersion)) return;
            Profile = await profileTask;
            if (Profile is null)
            {
                ContentState = PageContentState.Error;
                ErrorMessage = "Không tìm thấy khách hàng.";
                return;
            }
            PointHistory = await pointsTask;
            OrderHistory = await ordersTask;
            PurchasedItems = await devicesTask;
            PointsPage = OrdersPage = DevicesPage = 1;
            ContentState = PageContentState.Content;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ApiException exception)
        {
            if (version != Volatile.Read(ref _loadVersion)) return;
            ContentState = PageContentState.Error;
            ErrorMessage = exception.StatusCode switch
            {
                401 => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại.",
                403 => "Bạn không có quyền xem hồ sơ khách hàng.",
                404 => "Không tìm thấy khách hàng.",
                _ => "Không thể tải hồ sơ khách hàng. Hãy thử lại."
            };
        }
        catch
        {
            if (version != Volatile.Read(ref _loadVersion)) return;
            ContentState = PageContentState.Error;
            ErrorMessage = "Không thể tải hồ sơ khách hàng. Hãy thử lại.";
        }
    }

    [RelayCommand]
    private Task Reload(CancellationToken cancellationToken) =>
        _customerId == Guid.Empty ? Task.CompletedTask : LoadAsync(_customerId, cancellationToken);

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private async Task ActivateCustomerAsync(CancellationToken cancellationToken)
    {
        if (Profile is not { IsActive: false } profile || IsChangingActivity) return;
        ActivityErrorMessage = string.Empty;
        IsChangingActivity = true;
        try
        {
            await _customers.ActivateAsync(profile.Id, cancellationToken);
            Profile = profile with { IsActive = true };
            ActivityChanged = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ActivityErrorMessage = exception switch
            {
                ApiException { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại.",
                ApiException { StatusCode: 403 } => "Bạn không có quyền kích hoạt lại khách hàng này.",
                ApiException { StatusCode: 404 } => "Không tìm thấy khách hàng.",
                ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối máy chủ. Hãy thử lại.",
                _ => "Không thể kích hoạt lại khách hàng. Hãy thử lại."
            };
        }
        finally
        {
            IsChangingActivity = false;
        }
    }

    [RelayCommand]
    private Task PreviousPointsPageAsync() => LoadPointsPageAsync(PointsPage - 1);
    [RelayCommand]
    private Task NextPointsPageAsync() => LoadPointsPageAsync(PointsPage + 1);
    [RelayCommand]
    private Task PreviousOrdersPageAsync() => LoadOrdersPageAsync(OrdersPage - 1);
    [RelayCommand]
    private Task NextOrdersPageAsync() => LoadOrdersPageAsync(OrdersPage + 1);
    [RelayCommand]
    private Task PreviousDevicesPageAsync() => LoadDevicesPageAsync(DevicesPage - 1);
    [RelayCommand]
    private Task NextDevicesPageAsync() => LoadDevicesPageAsync(DevicesPage + 1);

    private Task LoadPointsPageAsync(int page) => LoadHistoryPageAsync(page, PointHistory?.TotalPages ?? 0,
        () => IsPointsLoading = true, () => IsPointsLoading = false,
        async token => PointHistory = await _customers.GetLoyaltyHistoryAsync(_customerId, page, PageSize, token),
        () => PointsPage = page);

    private Task LoadOrdersPageAsync(int page) => LoadHistoryPageAsync(page, OrderHistory?.TotalPages ?? 0,
        () => IsOrdersLoading = true, () => IsOrdersLoading = false,
        async token => OrderHistory = await _customers.GetOrderHistoryAsync(_customerId, page, PageSize, token),
        () => OrdersPage = page);

    private Task LoadDevicesPageAsync(int page) => LoadHistoryPageAsync(page, PurchasedItems?.TotalPages ?? 0,
        () => IsDevicesLoading = true, () => IsDevicesLoading = false,
        async token => PurchasedItems = await _customers.GetPurchasedItemsAsync(_customerId, page, PageSize, token),
        () => DevicesPage = page);

    private async Task LoadHistoryPageAsync(int page, int totalPages, Action start, Action finish,
        Func<CancellationToken, Task> load, Action commitPage)
    {
        if (page < 1 || page > totalPages || _customerId == Guid.Empty) return;
        start();
        ErrorMessage = string.Empty;
        try
        {
            await load(ActivationToken);
            commitPage();
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            ErrorMessage = "Không thể tải thêm lịch sử. Hãy thử lại.";
        }
        finally { finish(); }
    }
}
