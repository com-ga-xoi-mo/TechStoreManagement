using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Customers;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Customers;

public sealed partial class CustomerListViewModel : PageViewModel
{
    private static readonly IReadOnlyList<CustomerSummary> NoCustomers = Array.Empty<CustomerSummary>();
    private static readonly string[] TierValues = [string.Empty, "Standard", "Silver", "Gold", "Platinum"];
    private static readonly bool?[] ActivityValues = [true, false, null];
    private readonly ICustomerRepository _customers;
    private CancellationTokenSource? _searchDebounce;

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedTier = string.Empty;
    [ObservableProperty] private int selectedActivityIndex;
    [ObservableProperty] private CustomerPage<CustomerSummary>? snapshot;
    [ObservableProperty] private CustomerQuery appliedCriteria = new() { IsActive = true };
    [ObservableProperty] private bool isCreateDialogOpen;
    [ObservableProperty] private CustomerSummary? selectedCustomer;
    [ObservableProperty] private CustomerProfile? customerToEdit;
    [ObservableProperty] private CustomerSummary? customerToDeactivate;
    [ObservableProperty] private bool isDeactivationConfirmationOpen;
    [ObservableProperty] private string actionErrorMessage = string.Empty;

    public IReadOnlyList<CustomerSummary> Customers => Snapshot?.Items ?? NoCustomers;
    public int CurrentPage => Snapshot?.Page ?? AppliedCriteria.Page;
    public int TotalPages => Snapshot?.TotalPages ?? 0;
    public int TotalCount => Snapshot?.TotalCount ?? 0;
    public string CountLabel => $"{TotalCount:N0} khách hàng";
    public string RangeLabel => TotalCount == 0
        ? "Hiển thị 0 khách hàng"
        : $"Hiển thị {(CurrentPage - 1) * (Snapshot?.PageSize ?? 20) + 1}–{Math.Min(CurrentPage * (Snapshot?.PageSize ?? 20), TotalCount)} trong {TotalCount:N0} khách hàng";
    public string PageLabel => $"Trang {CurrentPage} / {Math.Max(1, TotalPages)}";
    public int SelectedTierIndex
    {
        get => Array.IndexOf(TierValues, SelectedTier);
        set
        {
            if (value >= 0 && value < TierValues.Length && SelectedTier != TierValues[value])
                SelectedTier = TierValues[value];
        }
    }
    public bool HasPreviousPage => Snapshot is { Page: > 1 } && !IsLoading;
    public bool HasNextPage => Snapshot is { } page && page.Page < page.TotalPages && !IsLoading;
    public bool ShowInitialLoading => IsLoading && Snapshot is null;
    public bool ShowEmptyState => IsEmpty && Snapshot is { Items.Count: 0 };
    public bool ShowPagination => Snapshot is { TotalCount: > 0 };
    public bool HasActionError => !string.IsNullOrWhiteSpace(ActionErrorMessage);

    protected override bool ManagesLoadState => true;

    protected override void OnContentStateChanged()
    {
        OnPropertyChanged(nameof(ShowInitialLoading));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowPagination));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(RangeLabel));
        OnPropertyChanged(nameof(PageLabel));
        NotifyPagingCommands();
    }

    public CustomerListViewModel(ICustomerRepository customers) =>
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));

    protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken) => SearchAsync(AppliedCriteria);

    [RelayCommand]
    private Task Search() => ApplySearchAsync();

    [RelayCommand]
    private Task ClearSearch()
    {
        SearchText = string.Empty;
        SelectedTier = string.Empty;
        SelectedActivityIndex = 0;
        return ApplySearchAsync();
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPage() => MoveToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPage() => MoveToPageAsync(CurrentPage + 1);

    [RelayCommand]
    private void OpenCreateDialog()
    {
        ActionErrorMessage = string.Empty;
        IsCreateDialogOpen = true;
    }

    [RelayCommand]
    private void CloseCreateDialog() => IsCreateDialogOpen = false;

    [RelayCommand]
    private void OpenCustomerDetails(CustomerSummary? customer)
    {
        if (customer is null || customer.Id == Guid.Empty) return;
        SelectedCustomer = customer;
    }

    [RelayCommand]
    private async Task LoadCustomerForEditAsync(CustomerSummary? customer, CancellationToken cancellationToken)
    {
        CustomerToEdit = null;
        ActionErrorMessage = string.Empty;
        if (customer is null || customer.Id == Guid.Empty) return;

        try
        {
            CustomerToEdit = await _customers.GetByIdAsync(customer.Id, cancellationToken);
            if (CustomerToEdit is null)
                ActionErrorMessage = "Kh\u00F4ng t\u00ECm th\u1EA5y kh\u00E1ch h\u00E0ng n\u00E0y.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ApiException exception)
        {
            ActionErrorMessage = exception.StatusCode switch
            {
                401 => "Phi\u00EAn truy c\u1EADp \u0111\u00E3 h\u1EBFt h\u1EA1n. H\u00E3y \u0111\u0103ng nh\u1EADp l\u1EA1i.",
                403 => "B\u1EA1n kh\u00F4ng c\u00F3 quy\u1EC1n xem h\u1ED3 s\u01A1 kh\u00E1ch h\u00E0ng.",
                404 => "Kh\u00F4ng t\u00ECm th\u1EA5y kh\u00E1ch h\u00E0ng n\u00E0y.",
                _ => "Kh\u00F4ng th\u1EC3 t\u1EA3i th\u00F4ng tin kh\u00E1ch h\u00E0ng. H\u00E3y th\u1EED l\u1EA1i."
            };
        }
        catch
        {
            ActionErrorMessage = "Kh\u00F4ng th\u1EC3 t\u1EA3i th\u00F4ng tin kh\u00E1ch h\u00E0ng. H\u00E3y th\u1EED l\u1EA1i.";
        }
        OnPropertyChanged(nameof(HasActionError));
    }

    [RelayCommand]
    private void RequestDeactivation(CustomerSummary? customer)
    {
        if (customer is null || customer.Id == Guid.Empty || !customer.IsActive) return;
        CustomerToDeactivate = customer;
        IsDeactivationConfirmationOpen = true;
        ActionErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelDeactivation()
    {
        IsDeactivationConfirmationOpen = false;
        CustomerToDeactivate = null;
        ActionErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmDeactivationAsync(CancellationToken cancellationToken)
    {
        var customer = CustomerToDeactivate;
        if (!IsDeactivationConfirmationOpen || customer is null || !customer.IsActive) return;
        ActionErrorMessage = string.Empty;
        try
        {
            await _customers.DeactivateAsync(customer.Id, cancellationToken);
            IsDeactivationConfirmationOpen = false;
            CustomerToDeactivate = null;
            if (SelectedCustomer?.Id == customer.Id) SelectedCustomer = customer with { IsActive = false };
            await SearchAsync(AppliedCriteria);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ActionErrorMessage = exception switch
            {
                ApiException { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại.",
                ApiException { StatusCode: 403 } => "Bạn không có quyền vô hiệu hóa khách hàng này.",
                ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối máy chủ. Hãy thử lại.",
                _ => "Không thể vô hiệu hóa khách hàng. Hãy thử lại."
            };
        }
        OnPropertyChanged(nameof(HasActionError));
    }

    private async Task ApplySearchAsync()
    {
        CancelDebouncedSearch();
        AppliedCriteria = AppliedCriteria with
        {
            Query = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            Tier = string.IsNullOrWhiteSpace(SelectedTier) ? null : SelectedTier,
            IsActive = ActivityValues[Math.Clamp(SelectedActivityIndex, 0, ActivityValues.Length - 1)],
            Page = 1
        };
        await SearchAsync(AppliedCriteria);
    }

    private Task MoveToPageAsync(int page)
    {
        if (page < 1 || Snapshot is null || page > Snapshot.TotalPages) return Task.CompletedTask;
        AppliedCriteria = AppliedCriteria with { Page = page };
        return SearchAsync(AppliedCriteria);
    }

    private Task<bool> SearchAsync(CustomerQuery query)
    {
        ErrorMessage = string.Empty;
        ContentState = PageContentState.Loading;
        NotifyPagingCommands();
        return RunLatestRequestAsync(
            token => _customers.SearchAsync(query, token),
            page =>
            {
                Snapshot = page;
                AppliedCriteria = AppliedCriteria with { Page = page.Page };
                OnPropertyChanged(nameof(Customers));
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(CountLabel));
                OnPropertyChanged(nameof(RangeLabel));
                OnPropertyChanged(nameof(PageLabel));
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(ShowInitialLoading));
                OnPropertyChanged(nameof(ShowPagination));
                ContentState = page.Items.Count > 0 ? PageContentState.Content : PageContentState.Empty;
                NotifyPagingCommands();
            },
            page => page.Items.Count > 0,
            () => Snapshot is { Items.Count: > 0 });
    }

    private void NotifyPagingCommands()
    {
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value) => ScheduleDebouncedSearch();
    partial void OnSelectedTierChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedTierIndex));
        ScheduleDebouncedSearch();
    }
    partial void OnSelectedActivityIndexChanged(int value) => ScheduleDebouncedSearch();

    private void ScheduleDebouncedSearch()
    {
        if (ContentState == PageContentState.Idle || Snapshot is null) return;
        CancelDebouncedSearch();
        _searchDebounce = CancellationTokenSource.CreateLinkedTokenSource(ActivationToken);
        _ = SearchAfterDelayAsync(_searchDebounce.Token);
    }

    private void CancelDebouncedSearch()
    {
        var debounce = _searchDebounce;
        _searchDebounce = null;
        if (debounce is null) return;
        try { debounce.Cancel(); }
        catch (ObjectDisposedException) { }
        debounce.Dispose();
    }

    private async Task SearchAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            await ApplySearchAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) { }
    }

    protected override void OnDeactivated()
    {
        CancelDebouncedSearch();
    }

    partial void OnActionErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasActionError));

    protected override void OnLatestRequestFailed(Exception exception)
    {
        ContentState = PageContentState.Error;
        NotifyPagingCommands();
    }

    protected override string GetSafeErrorMessage(Exception exception) => exception switch
    {
        ApiException { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục.",
        ApiException { StatusCode: 403 } => "Bạn không có quyền xem danh sách khách hàng.",
        ApiException { Kind: ApiErrorKind.Timeout } => "Yêu cầu khách hàng quá thời gian chờ. Hãy thử lại.",
        ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối máy chủ. Hãy kiểm tra kết nối rồi thử lại.",
        ApiException { Kind: ApiErrorKind.InvalidResponse } => "Máy chủ trả dữ liệu khách hàng không đúng định dạng.",
        _ => "Không thể tải danh sách khách hàng. Hãy thử lại."
    };
}
