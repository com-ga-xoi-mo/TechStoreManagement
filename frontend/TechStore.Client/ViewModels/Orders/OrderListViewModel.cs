using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Orders;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Enums;

namespace TechStore.Client.ViewModels.Orders;

public sealed partial class OrderListViewModel : PageViewModel
{
    private static readonly int[] AllowedPageSizes = [20, 50, 100];

    private readonly IOrderRepository _orders;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IOrderDetailPresenter _detailPresenter;
    private bool _hasActivated;
    private bool _suppressImmediateCriteria;
    private bool _pageOutOfRange;

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private OrderStatusFilter selectedStatusFilter = OrderStatusFilter.All[0];
    [ObservableProperty] private OrderSortOption selectedSort = OrderSortOption.All[0];
    [ObservableProperty] private int selectedPageSize = 20;
    [ObservableProperty] private OrderPage? snapshot;
    [ObservableProperty] private OrderQuery appliedCriteria = new();
    [ObservableProperty] private IReadOnlyList<OrderRow> rows = [];
    [ObservableProperty] private string actionMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangeStatusCommand))]
    private bool isBusy;

    public OrderListViewModel(
        IOrderRepository orders,
        INavigationService navigation,
        IDialogService dialogs,
        IOrderDetailPresenter detailPresenter)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _detailPresenter = detailPresenter ?? throw new ArgumentNullException(nameof(detailPresenter));
    }

    public IReadOnlyList<OrderStatusFilter> StatusFilters => OrderStatusFilter.All;
    public IReadOnlyList<OrderSortOption> SortOptions => OrderSortOption.All;
    public IReadOnlyList<int> PageSizes => AllowedPageSizes;

    public int CurrentPage => Snapshot?.Page ?? AppliedCriteria.Page;
    public int TotalPages => Snapshot?.TotalPages ?? 0;
    public int TotalCount => Snapshot?.TotalCount ?? 0;
    public bool HasActionMessage => !string.IsNullOrWhiteSpace(ActionMessage);
    public bool HasPreviousPage => Snapshot is { Page: > 1 } && !IsLoading;
    public bool HasNextPage => Snapshot is { } page && page.Page < page.TotalPages && !IsLoading;
    public bool ShowPagination => Snapshot is { TotalCount: > 0 };
    public bool ShowEmptyState => IsEmpty && Snapshot is not null;
    public bool ShowInitialLoading => IsLoading && Snapshot is null;
    public string ResultSummary => Snapshot is null
        ? string.Empty
        : $"{TotalCount:N0} đơn · trang {CurrentPage}/{Math.Max(TotalPages, 1)}";

    protected override bool ManagesLoadState => true;

    protected override void OnContentStateChanged()
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowInitialLoading));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        NotifyPagingCommands();
    }

    partial void OnActionMessageChanged(string value) => OnPropertyChanged(nameof(HasActionMessage));

    partial void OnSelectedStatusFilterChanged(OrderStatusFilter value) => ApplyImmediateCriteriaChange();

    partial void OnSelectedSortChanged(OrderSortOption value) => ApplyImmediateCriteriaChange();

    partial void OnSelectedPageSizeChanged(int value) => ApplyImmediateCriteriaChange();

    protected override async Task<bool> LoadContentAsync(CancellationToken cancellationToken)
    {
        _hasActivated = true;
        return await SearchAsync(AppliedCriteria);
    }

    [RelayCommand]
    private Task Search() => ApplyDraftAsync();

    [RelayCommand]
    private Task ClearSearch()
    {
        SearchText = string.Empty;
        return ApplyDraftAsync();
    }

    [RelayCommand]
    private Task ResetFilters()
    {
        _suppressImmediateCriteria = true;
        try
        {
            SearchText = string.Empty;
            SelectedStatusFilter = OrderStatusFilter.All[0];
            SelectedSort = OrderSortOption.All[0];
            SelectedPageSize = 20;
        }
        finally
        {
            _suppressImmediateCriteria = false;
        }

        return ApplyDraftAsync();
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPage() => MoveToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPage() => MoveToPageAsync(CurrentPage + 1);

    [RelayCommand]
    private Task Refresh()
    {
        ActionMessage = string.Empty;
        return SearchAsync(AppliedCriteria);
    }

    [RelayCommand]
    private void NewOrder() => _navigation.Navigate(AppRoute.OrderCreate());

    [RelayCommand(CanExecute = nameof(CanChangeStatus))]
    private async Task ChangeStatus(OrderRowAction? rowAction)
    {
        if (rowAction is null || IsBusy) return;

        var order = rowAction.Order;
        var subject = $"Đơn {order.OrderCode}";
        var reload = false;
        IsBusy = true;
        try
        {
            ActionMessage = string.Empty;
            var confirmed = await _dialogs.ConfirmAsync(BuildConfirmation(order, rowAction.Action), ActivationToken);
            if (confirmed != DialogResult.Confirmed) return;

            await _orders.UpdateStatusAsync(order.Id, rowAction.Action.Target, ActivationToken);
            ActionMessage = $"{subject} đã chuyển sang \"{OrderPresentation.StatusLabel(rowAction.Action.Target)}\".";
            reload = true;
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
            return;
        }
        catch (ApiException exception)
        {
            ActionMessage = OrderApiErrors.Describe(exception, subject);
            // 404 and 409 mean the list is stale; transport failures leave it as it was.
            reload = exception.StatusCode is 404 or 409;
        }
        catch (DialogUnavailableException)
        {
            ActionMessage = "Không thể hiển thị hộp thoại xác nhận. Hãy thử lại.";
        }
        finally
        {
            IsBusy = false;
        }

        if (reload) await SearchAsync(AppliedCriteria);
    }

    [RelayCommand]
    private async Task OpenDetail(OrderRow? row)
    {
        if (row is null) return;

        var subject = $"Đơn {row.Code}";
        try
        {
            ActionMessage = string.Empty;
            var detail = await _orders.GetByIdAsync(row.Order.Id, ActivationToken);
            if (detail is null)
            {
                ActionMessage = $"{subject} không còn tồn tại.";
                await SearchAsync(AppliedCriteria);
                return;
            }

            await _detailPresenter.ShowAsync(detail, ActivationToken);
        }
        catch (OperationCanceledException) when (ActivationToken.IsCancellationRequested)
        {
        }
        catch (ApiException exception)
        {
            ActionMessage = OrderApiErrors.Describe(exception, subject);
        }
    }

    protected override string GetSafeErrorMessage(Exception exception) => exception switch
    {
        ApiException { StatusCode: 400 } => "Bộ lọc không hợp lệ theo Orders API. Hãy kiểm tra các giá trị rồi thử lại.",
        ApiException { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục.",
        ApiException { StatusCode: 403 } => "Bạn không có quyền xem danh sách đơn hàng.",
        ApiException { Kind: ApiErrorKind.Timeout } => "Yêu cầu Orders quá thời gian chờ. Hãy thử lại.",
        ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối Orders API. Hãy kiểm tra kết nối rồi thử lại.",
        ApiException { Kind: ApiErrorKind.InvalidResponse } => "Orders API trả dữ liệu không đúng định dạng.",
        _ => "Không thể tải danh sách đơn hàng. Hãy thử lại."
    };

    protected override void OnLatestRequestFailed(Exception exception)
    {
        ContentState = PageContentState.Error;
        NotifyPagingCommands();
    }

    private bool CanChangeStatus() => !IsBusy;

    private static DialogRequest BuildConfirmation(OrderSummary order, OrderStatusAction action) => action.Target switch
    {
        OrderStatus.Processing => new(
            "Xác nhận thanh toán",
            $"Xác nhận đã nhận thanh toán cho đơn {order.OrderCode}? Thiết bị trong đơn sẽ được giữ chỗ.",
            "Xác nhận",
            "Không"),
        OrderStatus.Completed => new(
            "Hoàn tất đơn",
            $"Hoàn tất đơn {order.OrderCode}? Thiết bị chuyển sang đã bán, tồn kho giảm và bảo hành bắt đầu tính.",
            "Hoàn tất",
            "Không"),
        _ => new(
            "Hủy đơn",
            $"Hủy đơn {order.OrderCode}? Thao tác này không thể hoàn tác.",
            "Hủy đơn",
            "Giữ lại")
    };

    private Task ApplyDraftAsync()
    {
        AppliedCriteria = new OrderQuery
        {
            Status = SelectedStatusFilter.Value,
            Query = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            Sort = SelectedSort.Value,
            PageSize = SelectedPageSize,
            Page = 1
        };
        ActionMessage = string.Empty;
        return SearchAsync(AppliedCriteria);
    }

    private Task MoveToPageAsync(int page)
    {
        if (page < 1 || Snapshot is null || page > Snapshot.TotalPages) return Task.CompletedTask;
        AppliedCriteria = AppliedCriteria with { Page = page };
        return SearchAsync(AppliedCriteria);
    }

    /// <summary>Selections (status, sort, page size) apply at once; the typed keyword waits for submit.</summary>
    private void ApplyImmediateCriteriaChange()
    {
        if (!_hasActivated || _suppressImmediateCriteria || ContentState == PageContentState.Idle) return;
        _ = ApplyImmediateCriteriaChangeAsync();
    }

    private async Task ApplyImmediateCriteriaChangeAsync()
    {
        AppliedCriteria = AppliedCriteria with
        {
            Status = SelectedStatusFilter.Value,
            Sort = SelectedSort.Value,
            PageSize = SelectedPageSize,
            Page = 1
        };
        try
        {
            await SearchAsync(AppliedCriteria);
        }
        catch (ObjectDisposedException)
        {
            // A control notification can race page teardown; the activation owns cancellation and cleanup.
        }
    }

    private async Task<bool> SearchAsync(OrderQuery query, bool allowFallback = true)
    {
        ErrorMessage = string.Empty;
        _pageOutOfRange = false;
        ContentState = PageContentState.Loading;
        NotifyPagingCommands();

        var hasContent = await RunLatestRequestAsync(
            token => _orders.SearchAsync(query, token),
            page => Commit(query, page, allowFallback),
            page => page.Items.Count > 0,
            () => Rows.Count > 0);

        if (_pageOutOfRange && allowFallback)
        {
            // The requested page vanished (e.g. the last order on it was cancelled): go back to page 1 once.
            _pageOutOfRange = false;
            AppliedCriteria = AppliedCriteria with { Page = 1 };
            return await SearchAsync(AppliedCriteria, allowFallback: false);
        }

        return hasContent;
    }

    private void Commit(OrderQuery query, OrderPage page, bool allowFallback)
    {
        if (allowFallback && query.Page > 1 && page.Items.Count == 0 && page.TotalCount > 0)
        {
            _pageOutOfRange = true;
            return;
        }

        Snapshot = page;
        Rows = page.Items.Select(OrderRow.From).ToArray();
        AppliedCriteria = AppliedCriteria with { Page = page.Page };
        ContentState = page.Items.Count > 0 ? PageContentState.Content : PageContentState.Empty;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ResultSummary));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(ShowPagination));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowInitialLoading));
        NotifyPagingCommands();
    }

    private void NotifyPagingCommands()
    {
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }
}
