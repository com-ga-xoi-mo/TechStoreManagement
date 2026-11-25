using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Vouchers;
using TechStore.Client.Repositories.Vouchers;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Vouchers;

public sealed partial class VoucherListViewModel : PageViewModel
{
    private static readonly IReadOnlyList<VoucherRow> NoRows = Array.Empty<VoucherRow>();
    private static readonly bool?[] StatusValues = [null, true, false];
    private readonly IVoucherRepository _vouchers;
    private readonly INavigationService _navigation;
    private readonly ISessionStore? _session;
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource? _searchDebounce;

    public static IReadOnlyList<string> StatusOptions { get; } = ["Tất cả trạng thái", "Đang bật", "Đã tắt"];

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private int selectedStatusIndex;
    [ObservableProperty] private VoucherPage? snapshot;
    [ObservableProperty] private VoucherQuery appliedCriteria = new();
    [ObservableProperty] private IReadOnlyList<VoucherRow> rows = NoRows;
    [ObservableProperty] private string actionErrorMessage = string.Empty;

    /// <summary>Raised when the list wants the editor dialog: a voucher to edit, or null for a new one.</summary>
    public event Action<VoucherItem?>? VoucherEditorRequested;

    public VoucherListViewModel(
        IVoucherRepository vouchers,
        INavigationService navigation,
        ISessionStore? session = null,
        TimeProvider? timeProvider = null)
    {
        _vouchers = vouchers ?? throw new ArgumentNullException(nameof(vouchers));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _session = session;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Voucher pages are for Admin and Manager; the server answers 403 to anyone else.</summary>
    public bool CanManageVouchers => _session is null
        || (_session.Current.Status == SessionStatus.Active && RoleAccess.CanManageVouchers(_session.Current.Role));
    public int CurrentPage => Snapshot?.Page ?? AppliedCriteria.Page;
    public int TotalPages => Snapshot?.TotalPages ?? 0;
    public int TotalCount => Snapshot?.TotalCount ?? 0;
    public string CountLabel => $"{TotalCount:N0} voucher";
    public string PageLabel => $"Trang {CurrentPage} / {Math.Max(1, TotalPages)}";
    public string RangeLabel => TotalCount == 0
        ? "Hiển thị 0 voucher"
        : $"Hiển thị {(CurrentPage - 1) * (Snapshot?.PageSize ?? 20) + 1}–{Math.Min(CurrentPage * (Snapshot?.PageSize ?? 20), TotalCount)} trong {TotalCount:N0} voucher";
    public bool HasPreviousPage => Snapshot is { Page: > 1 } && !IsLoading;
    public bool HasNextPage => Snapshot is { } page && page.Page < page.TotalPages && !IsLoading;
    public bool ShowInitialLoading => IsLoading && Snapshot is null;
    public bool ShowEmptyState => IsEmpty;
    public bool ShowTable => Rows.Count > 0;
    public bool ShowPagination => Snapshot is { TotalCount: > 0 };
    public bool HasActionError => !string.IsNullOrWhiteSpace(ActionErrorMessage);

    protected override bool ManagesLoadState => true;

    protected override void OnContentStateChanged()
    {
        OnPropertyChanged(nameof(ShowInitialLoading));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        NotifyPagingCommands();
    }

    protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken) => SearchAsync(AppliedCriteria);

    [RelayCommand]
    private void Back() => _navigation.Navigate(ShellDestination.Customers);

    [RelayCommand]
    private Task Search() => ApplySearchAsync();

    [RelayCommand]
    private Task ClearSearch()
    {
        SearchText = string.Empty;
        SelectedStatusIndex = 0;
        return ApplySearchAsync();
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPage() => MoveToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPage() => MoveToPageAsync(CurrentPage + 1);

    [RelayCommand]
    private void OpenCreate()
    {
        ActionErrorMessage = string.Empty;
        VoucherEditorRequested?.Invoke(null);
    }

    // The row command parameter can briefly be the page's DataContext while a row is removed, so it is checked, not cast.
    [RelayCommand]
    private void OpenEdit(object? parameter)
    {
        if (parameter is not VoucherRow row) return;
        ActionErrorMessage = string.Empty;
        VoucherEditorRequested?.Invoke(row.Voucher);
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is not VoucherRow row) return;
        ActionErrorMessage = string.Empty;
        try
        {
            var saved = await _vouchers.SetActiveAsync(row.Voucher.Id, row.CanActivate, cancellationToken);
            ReplaceRow(saved);
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
                ApiException { StatusCode: 403 } => "Bạn không có quyền thay đổi voucher.",
                ApiException { StatusCode: 404 } => "Voucher không còn tồn tại. Danh sách đã được tải lại.",
                ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối máy chủ. Hãy thử lại.",
                _ => "Không thể đổi trạng thái voucher. Hãy thử lại."
            };
            if (exception is ApiException { StatusCode: 404 }) await SearchAsync(AppliedCriteria);
        }
    }

    /// <summary>Reloads the current page after the editor saved, so the list shows the change without a manual reload.</summary>
    public Task NotifySavedAsync() => SearchAsync(AppliedCriteria);

    private void ReplaceRow(VoucherItem saved)
    {
        if (Snapshot is null) return;
        var items = Snapshot.Items.Select(item => item.Id == saved.Id ? saved : item).ToArray();
        Snapshot = Snapshot with { Items = items };
        Rows = BuildRows(items);
    }

    private async Task ApplySearchAsync()
    {
        CancelDebouncedSearch();
        AppliedCriteria = AppliedCriteria with
        {
            Query = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            IsActive = StatusValues[Math.Clamp(SelectedStatusIndex, 0, StatusValues.Length - 1)],
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

    private Task<bool> SearchAsync(VoucherQuery query)
    {
        ErrorMessage = string.Empty;
        ContentState = PageContentState.Loading;
        NotifyPagingCommands();
        return RunLatestRequestAsync(
            token => _vouchers.SearchAsync(query, token),
            page =>
            {
                Snapshot = page;
                AppliedCriteria = AppliedCriteria with { Page = page.Page };
                Rows = BuildRows(page.Items);
                NotifyPageChanged();
                ContentState = page.Items.Count > 0 ? PageContentState.Content : PageContentState.Empty;
            },
            page => page.Items.Count > 0,
            () => Snapshot is { Items.Count: > 0 });
    }

    private IReadOnlyList<VoucherRow> BuildRows(IReadOnlyList<VoucherItem> items)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return items.Select(item => VoucherRow.From(item, now)).ToArray();
    }

    private void NotifyPageChanged()
    {
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(RangeLabel));
        OnPropertyChanged(nameof(PageLabel));
        OnPropertyChanged(nameof(ShowPagination));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        NotifyPagingCommands();
    }

    private void NotifyPagingCommands()
    {
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnRowsChanged(IReadOnlyList<VoucherRow> value) => OnPropertyChanged(nameof(ShowTable));
    partial void OnActionErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasActionError));
    partial void OnSearchTextChanged(string value) => ScheduleDebouncedSearch();
    partial void OnSelectedStatusIndexChanged(int value) => ScheduleDebouncedSearch();

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

    protected override void OnDeactivated() => CancelDebouncedSearch();

    protected override void OnLatestRequestFailed(Exception exception)
    {
        // A failed search must not leave rows from an earlier answer on screen.
        Snapshot = null;
        Rows = NoRows;
        NotifyPageChanged();
        ContentState = PageContentState.Error;
    }

    protected override string GetSafeErrorMessage(Exception exception) => exception switch
    {
        ApiException { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục.",
        ApiException { StatusCode: 403 } => "Bạn không có quyền xem danh sách voucher.",
        ApiException { Kind: ApiErrorKind.Timeout } => "Yêu cầu voucher quá thời gian chờ. Hãy thử lại.",
        ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối máy chủ. Hãy kiểm tra kết nối rồi thử lại.",
        ApiException { Kind: ApiErrorKind.InvalidResponse } => "Máy chủ trả dữ liệu voucher không đúng định dạng.",
        _ => "Không thể tải danh sách voucher. Hãy thử lại."
    };
}
