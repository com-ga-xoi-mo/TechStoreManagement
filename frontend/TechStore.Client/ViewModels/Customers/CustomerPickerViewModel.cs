using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Customers;
using TechStore.Client.Repositories.Customers;
using TechStore.Client.Services.Api;

namespace TechStore.Client.ViewModels.Customers;

/// <summary>
/// Picks the customer of a sale, or none for a walk-in guest. Shared by order creation and the POS screen;
/// it lives inside another page, so the host calls <see cref="Cancel"/> when that page deactivates.
/// </summary>
public sealed partial class CustomerPickerViewModel : ObservableObject
{
    public const int ResultLimit = 8;
    public const int MinimumSearchLength = 2;
    private static readonly IReadOnlyList<CustomerSummary> NoResults = Array.Empty<CustomerSummary>();
    private readonly ICustomerRepository _customers;
    private CancellationTokenSource? _search;
    private long _searchVersion;
    private bool _suppressSchedule;

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private IReadOnlyList<CustomerSummary> results = NoResults;
    [ObservableProperty] private bool isSearching;
    [ObservableProperty] private string message = string.Empty;
    [ObservableProperty] private CustomerChoice? selectedCustomer;
    [ObservableProperty] private bool isCreating;

    public CustomerPickerViewModel(ICustomerRepository customers, CustomerFormViewModel form)
    {
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        Form = form ?? throw new ArgumentNullException(nameof(form));
    }

    /// <summary>The existing create form, reused for "Khách mới".</summary>
    public CustomerFormViewModel Form { get; }

    public Guid? SelectedCustomerId => SelectedCustomer?.Id;
    public bool HasSelection => SelectedCustomer is not null;
    public bool HasResults => Results.Count > 0;
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public string SelectionLabel => SelectedCustomer is { } customer
        ? $"{customer.FullName} · {customer.Phone}"
        : "Khách lẻ";
    public string SelectionTierLabel => SelectedCustomer is { } customer
        ? CustomerTierPresentation.FromTier(customer.Tier).BadgeLabel
        : "Không tích điểm";

    partial void OnSelectedCustomerChanged(CustomerChoice? value)
    {
        OnPropertyChanged(nameof(SelectedCustomerId));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(SelectionTierLabel));
    }

    partial void OnResultsChanged(IReadOnlyList<CustomerSummary> value) => OnPropertyChanged(nameof(HasResults));

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnSearchTextChanged(string value) => ScheduleSearch();

    [RelayCommand]
    private Task Search() => SearchNowAsync(SearchText);

    // Row buttons bind CommandParameter to their item; while a row is removed WinUI can briefly hand over the
    // page DataContext instead, so the parameter is checked rather than typed.
    [RelayCommand]
    private void Select(object? parameter)
    {
        if (parameter is not CustomerSummary { Id: var id } customer || id == Guid.Empty) return;
        SelectedCustomer = new CustomerChoice(customer.Id, customer.FullName, customer.Phone, customer.Tier);
        CancelSearch();
        Results = NoResults;
        Message = string.Empty;
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private void Clear()
    {
        SelectedCustomer = null;
        Message = string.Empty;
    }

    [RelayCommand]
    private void StartCreate()
    {
        Form.Initialize(null);
        var text = SearchText.Trim();
        if (text.Length > 0 && text.All(char.IsDigit)) Form.Phone = text;
        else Form.FullName = text;
        IsCreating = true;
    }

    [RelayCommand]
    private void CancelCreate() => IsCreating = false;

    [RelayCommand]
    private async Task SaveNew()
    {
        await Form.SaveCommand.ExecuteAsync(null);
        if (Form.SavedProfile is { } created)
        {
            IsCreating = false;
            SelectedCustomer = new CustomerChoice(created.Id, created.FullName, created.Phone, created.Tier);
            Message = string.Empty;
            return;
        }

        if (!Form.PhoneAlreadyUsed) return;

        // Somebody already owns this phone: show that customer instead of a second profile.
        var phone = Form.Phone.Trim();
        IsCreating = false;
        CancelSearch();
        SetSearchTextWithoutScheduling(phone);
        await SearchNowAsync(phone);
        Message = "Số điện thoại này đã có khách hàng. Hãy chọn trong danh sách.";
    }

    /// <summary>Stops a pending search, e.g. when the host page is left.</summary>
    public void Cancel() => CancelSearch();

    /// <summary>Back to a walk-in guest with an empty search, e.g. after the order was created.</summary>
    public void Reset()
    {
        CancelSearch();
        SetSearchTextWithoutScheduling(string.Empty);
        Results = NoResults;
        Message = string.Empty;
        SelectedCustomer = null;
        IsCreating = false;
    }

    private void SetSearchTextWithoutScheduling(string value)
    {
        _suppressSchedule = true;
        try { SearchText = value; }
        finally { _suppressSchedule = false; }
    }

    private void ScheduleSearch()
    {
        if (_suppressSchedule) return;
        var token = RestartSearchToken();
        _ = SearchAfterDelayAsync(SearchText, token);
    }

    private async Task SearchAfterDelayAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            await RunSearchAsync(text, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private Task SearchNowAsync(string text) => RunSearchAsync(text, RestartSearchToken());

    private async Task RunSearchAsync(string text, CancellationToken cancellationToken)
    {
        var query = text.Trim();
        var version = Interlocked.Increment(ref _searchVersion);
        if (query.Length < MinimumSearchLength)
        {
            Results = NoResults;
            Message = string.Empty;
            IsSearching = false;
            return;
        }

        IsSearching = true;
        Message = string.Empty;
        try
        {
            var page = await _customers.SearchAsync(
                new CustomerQuery { Query = query, IsActive = true, Page = 1, PageSize = ResultLimit },
                cancellationToken);
            if (version != Volatile.Read(ref _searchVersion)) return;
            Results = page.Items;
            Message = page.Items.Count == 0 ? "Không tìm thấy khách hàng. Bấm \"Khách mới\" để tạo." : string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (version != Volatile.Read(ref _searchVersion)) return;
            Results = NoResults;
            Message = exception switch
            {
                ApiException { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại.",
                ApiException { StatusCode: 403 } => "Bạn không có quyền tìm khách hàng.",
                ApiException { Kind: ApiErrorKind.Network or ApiErrorKind.Timeout } => "Không thể kết nối máy chủ. Hãy thử lại.",
                _ => "Không thể tìm khách hàng. Hãy thử lại."
            };
        }
        finally
        {
            if (version == Volatile.Read(ref _searchVersion)) IsSearching = false;
        }
    }

    private CancellationToken RestartSearchToken()
    {
        CancelSearch();
        _search = new CancellationTokenSource();
        return _search.Token;
    }

    private void CancelSearch()
    {
        var search = _search;
        _search = null;
        if (search is null) return;
        try { search.Cancel(); }
        catch (ObjectDisposedException) { }
        search.Dispose();
    }
}
