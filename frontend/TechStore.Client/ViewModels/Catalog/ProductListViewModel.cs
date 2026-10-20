using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Catalog;

public sealed partial class ProductListViewModel : PageViewModel
{
    private static readonly string[] AllowedSorts =
    [
        "name:asc", "name:desc",
        "price:asc", "price:desc",
        "createdAt:asc", "createdAt:desc"
    ];
    private static readonly int[] AllowedPageSizes = [20, 50, 100];
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly ISessionStore _sessionStore;
    private readonly INavigationService _navigation;
    private bool _categoriesRequested;
    private bool _hasActivated;
    private bool _suppressImmediateCriteria;
    private static readonly IReadOnlyList<ProductSummary> NoProducts = Array.Empty<ProductSummary>();

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string brandText = string.Empty;
    [ObservableProperty] private Guid? selectedCategoryId;
    [ObservableProperty] private string activeFilter = "all";
    [ObservableProperty] private string minPriceText = string.Empty;
    [ObservableProperty] private string maxPriceText = string.Empty;
    [ObservableProperty] private string specsText = string.Empty;
    [ObservableProperty] private string selectedSort = "createdAt:desc";
    [ObservableProperty] private int selectedPageSize = 20;
    [ObservableProperty] private ProductPage? snapshot;
    [ObservableProperty] private ProductQuery? snapshotQuery;
    [ObservableProperty] private ProductQuery appliedCriteria = new();
    [ObservableProperty] private ProductQuery? requestedCriteria;
    [ObservableProperty] private string? validationMessage;
    [ObservableProperty] private string? categoryError;
    [ObservableProperty] private bool isOutOfRange;
    [ObservableProperty] private Guid? focusProductId;
    [ObservableProperty] private bool canManage;
    [ObservableProperty] private string resultContext = string.Empty;
    [ObservableProperty] private IReadOnlyList<CategoryOption> categories = [];

    public IReadOnlyList<int> PageSizes { get; } = AllowedPageSizes;
    public IReadOnlyList<string> SortOptions { get; } = AllowedSorts;
    public IReadOnlyList<string> ActiveOptions { get; } = ["all", "active", "inactive"];
    public int CurrentPage => Snapshot?.Page ?? AppliedCriteria.Page;
    public int TotalPages => Snapshot?.TotalPages ?? 0;
    public int TotalCount => Snapshot?.TotalCount ?? 0;
    public IReadOnlyList<ProductSummary> Products => Snapshot?.Items ?? NoProducts;
    public bool HasSnapshot => Snapshot is not null;
    public bool HasPreviousPage => Snapshot is { Page: > 1 } && !IsLoading;
    public bool HasNextPage => Snapshot is { } page && page.Page < page.TotalPages && !IsLoading;
    public bool HasCategoryError => !string.IsNullOrWhiteSpace(CategoryError);
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);
    public bool ShowEmptyState => IsEmpty && Snapshot is not null && Snapshot.Items.Count == 0 && !IsOutOfRange;
    public bool ShowInitialLoading => IsLoading && Snapshot is null;
    public bool ShowPagination => Snapshot is { TotalCount: > 0 } && !IsOutOfRange;
    public bool HasResultsForPreviousQuery => Snapshot is not null && SnapshotQuery is not null
        && !ProductQueryValueComparer.Equals(SnapshotQuery, RequestedCriteria ?? AppliedCriteria);

    [ObservableProperty] private IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    [ObservableProperty] private string fieldErrorSummary = string.Empty;

    public ProductListViewModel(
        IProductRepository products,
        ICategoryRepository categories,
        ISessionStore sessionStore,
        INavigationService navigation)
    {
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _categories = categories ?? throw new ArgumentNullException(nameof(categories));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _sessionStore.SessionChanged += OnSessionChanged;
        CanManage = CatalogPermissions.CanManage(_sessionStore.Current);
    }

    protected override bool ManagesLoadState => true;

    protected override void OnContentStateChanged()
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowInitialLoading));
        OnPropertyChanged(nameof(ShowPagination));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        NotifyPagingCommands();
    }

    /// <summary>Called by the route factory before activation; it performs no I/O.</summary>
    public void SetRoute(ProductQuery? returnCriteria, Guid? focusProductId)
    {
        if (ContentState != PageContentState.Idle)
            throw new InvalidOperationException("A Catalog route must be configured before activation.");
        var criteria = returnCriteria ?? new ProductQuery();
        ValidateCriteria(criteria);
        AppliedCriteria = criteria;
        RequestedCriteria = criteria;
        CopyToDraft(criteria);
        FocusProductId = focusProductId;
    }

    protected override async Task<bool> LoadContentAsync(CancellationToken cancellationToken)
    {
        _hasActivated = true;
        if (!_categoriesRequested)
        {
            _categoriesRequested = true;
            _ = RunTrackedRequestAsync(LoadCategoriesAsync);
        }

        RequestedCriteria = AppliedCriteria;
        return await SearchAsync(AppliedCriteria, clearOnForbidden: true);
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
            BrandText = string.Empty;
            SelectedCategoryId = null;
            ActiveFilter = "all";
            MinPriceText = string.Empty;
            MaxPriceText = string.Empty;
            SpecsText = string.Empty;
            SelectedSort = "createdAt:desc";
            SelectedPageSize = 20;
        }
        finally
        {
            _suppressImmediateCriteria = false;
        }
        return ApplyDraftAsync();
    }

    [RelayCommand]
    private Task RetryCategories()
    {
        _categoriesRequested = true;
        CategoryError = null;
        OnPropertyChanged(nameof(HasCategoryError));
        return RunTrackedRequestAsync(LoadCategoriesAsync);
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPage() => MoveToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPage() => MoveToPageAsync(CurrentPage + 1);

    [RelayCommand]
    private Task FirstPage() => MoveToPageAsync(1);

    [RelayCommand]
    private Task Refresh() => SearchAsync(AppliedCriteria, clearOnForbidden: true);

    [RelayCommand]
    private void OpenProduct(ProductSummary? product)
    {
        if (product is null || product.Id == Guid.Empty) return;
        _navigation.Navigate(AppRoute.ProductDetail(product.Id, AppliedCriteria, product.Id));
    }

    [RelayCommand]
    private void CreateProduct()
    {
        if (!CanManage) return;
        _navigation.Navigate(AppRoute.ProductCreate(AppliedCriteria));
    }

    public void ClearFocusRequest() => FocusProductId = null;

    private async Task ApplyDraftAsync()
    {
        if (!TryBuildCriteria(out var criteria, out var error))
        {
            ValidationMessage = error;
            OnPropertyChanged(nameof(HasValidationError));
            return;
        }

        ValidationMessage = null;
        OnPropertyChanged(nameof(HasValidationError));
        AppliedCriteria = criteria with { Page = 1 };
        RequestedCriteria = AppliedCriteria;
        FieldErrorSummary = string.Empty;
        OnPropertyChanged(nameof(HasResultsForPreviousQuery));
        await SearchAsync(AppliedCriteria, clearOnForbidden: true);
    }

    private Task MoveToPageAsync(int page)
    {
        if (page < 1 || Snapshot is null || page > Snapshot.TotalPages) return Task.CompletedTask;
        AppliedCriteria = AppliedCriteria with { Page = page };
        RequestedCriteria = AppliedCriteria;
        OnPropertyChanged(nameof(HasResultsForPreviousQuery));
        return SearchAsync(AppliedCriteria, clearOnForbidden: true);
    }

    private Task<bool> SearchAsync(ProductQuery query, bool clearOnForbidden)
    {
        ErrorMessage = string.Empty;
        ContentState = PageContentState.Loading;
        NotifyPagingCommands();
        return RunLatestRequestAsync(
            token => SearchFromRepositoryAsync(query, token, clearOnForbidden),
            result =>
            {
                Snapshot = result;
                OnPropertyChanged(nameof(Products));
                SnapshotQuery = query;
                IsOutOfRange = result.Items.Count == 0 && query.Page > result.TotalPages && query.Page > 1;
                ResultContext = IsOutOfRange ? "Kết quả đã thay đổi; hãy quay lại trang đầu." : string.Empty;
                RequestedCriteria = query with { Page = result.Page };
                AppliedCriteria = AppliedCriteria with { Page = result.Page };
                ContentState = result.Items.Count > 0 ? PageContentState.Content : PageContentState.Empty;
                ErrorMessage = string.Empty;
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(HasSnapshot));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
                OnPropertyChanged(nameof(HasResultsForPreviousQuery));
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(ShowInitialLoading));
                OnPropertyChanged(nameof(ShowPagination));
                NotifyPagingCommands();
            },
            result => result.Items.Count > 0,
            () => Snapshot is { Items.Count: > 0 });
    }

    private void NotifyPagingCommands()
    {
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private async Task<ProductPage> SearchFromRepositoryAsync(
        ProductQuery query,
        CancellationToken cancellationToken,
        bool clearOnForbidden)
    {
        try
        {
            FieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            FieldErrorSummary = string.Empty;
            return await _products.SearchAsync(query, cancellationToken);
        }
        catch (ApiException exception) when ((exception.StatusCode is 401 or 403) && clearOnForbidden)
        {
            ClearSnapshot();
            throw;
        }
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _categories.GetTreeAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Categories = result;
            CategoryError = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            CategoryError = "Không thể tải danh mục. Danh sách sản phẩm vẫn dùng được; hãy tải lại danh mục.";
        }
        finally
        {
            OnPropertyChanged(nameof(HasCategoryError));
        }
    }

    private bool TryBuildCriteria(out ProductQuery query, out string? error)
    {
        error = null;
        decimal? min = ParseOptionalPrice(MinPriceText, "Giá tối thiểu", out error);
        if (error is not null) { query = AppliedCriteria; return false; }
        decimal? max = ParseOptionalPrice(MaxPriceText, "Giá tối đa", out error);
        if (error is not null) { query = AppliedCriteria; return false; }
        if (min is < 0 || max is < 0)
        {
            error = "Giá không được là số âm.";
            query = AppliedCriteria;
            return false;
        }
        if (min.HasValue && max.HasValue && min > max)
        {
            error = "Giá tối thiểu phải nhỏ hơn hoặc bằng giá tối đa.";
            query = AppliedCriteria;
            return false;
        }
        if (!AllowedSorts.Contains(SelectedSort, StringComparer.Ordinal))
        {
            error = "Thứ tự sắp xếp không hợp lệ.";
            query = AppliedCriteria;
            return false;
        }
        if (!AllowedPageSizes.Contains(SelectedPageSize))
        {
            error = "Số sản phẩm mỗi trang không hợp lệ.";
            query = AppliedCriteria;
            return false;
        }
        if (!TryParseSpecs(SpecsText, out var specs, out error))
        {
            query = AppliedCriteria;
            return false;
        }

        bool? isActive = ActiveFilter switch
        {
            "all" => null,
            "active" => true,
            "inactive" => false,
            _ => null
        };
        if (ActiveFilter is not ("all" or "active" or "inactive"))
        {
            error = "Bộ lọc trạng thái không hợp lệ.";
            query = AppliedCriteria;
            return false;
        }

        query = new ProductQuery
        {
            Query = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            Brand = string.IsNullOrWhiteSpace(BrandText) ? null : BrandText.Trim(),
            CategoryId = SelectedCategoryId,
            IsActive = isActive,
            MinPrice = min,
            MaxPrice = max,
            Specs = specs,
            Sort = SelectedSort,
            Page = 1,
            PageSize = SelectedPageSize
        };
        return true;
    }

    private static decimal? ParseOptionalPrice(string text, string label, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out var value)
            && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            error = $"{label} không hợp lệ.";
        return error is null ? value : null;
    }

    private static bool TryParseSpecs(string text, out IReadOnlyList<string> specs, out string? error)
    {
        var result = new List<string>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        error = null;
        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = rawLine.IndexOf('=');
            if (separator <= 0 || separator == rawLine.Length - 1)
            {
                error = "Mỗi thông số phải có dạng key=value.";
                specs = [];
                return false;
            }
            var key = rawLine[..separator].Trim();
            var value = rawLine[(separator + 1)..].Trim();
            if (!Regex.IsMatch(key, "^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant))
            {
                error = "Tên thông số phải dùng snake_case.";
                specs = [];
                return false;
            }
            if (value.Length == 0 || !keys.Add(key))
            {
                error = "Giá trị thông số không được rỗng hoặc lặp key.";
                specs = [];
                return false;
            }
            result.Add($"{key}:{value}");
        }
        specs = result;
        return true;
    }

    private static void ValidateCriteria(ProductQuery criteria)
    {
        if (!AllowedSorts.Contains(criteria.Sort, StringComparer.Ordinal)
            || !AllowedPageSizes.Contains(criteria.PageSize)
            || criteria.Page < 1
            || criteria.MinPrice is < 0
            || criteria.MaxPrice is < 0
            || criteria.MinPrice > criteria.MaxPrice)
            throw new ArgumentException("The route contains invalid Catalog criteria.", nameof(criteria));
    }

    private void CopyToDraft(ProductQuery criteria)
    {
        _suppressImmediateCriteria = true;
        try
        {
        SearchText = criteria.Query ?? string.Empty;
        BrandText = criteria.Brand ?? string.Empty;
        SelectedCategoryId = criteria.CategoryId;
        ActiveFilter = criteria.IsActive switch { true => "active", false => "inactive", null => "all" };
        MinPriceText = criteria.MinPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        MaxPriceText = criteria.MaxPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        SpecsText = string.Join(Environment.NewLine, criteria.Specs.Select(spec =>
        {
            var separator = spec.IndexOf(':');
            return separator < 0 ? spec : string.Concat(spec.AsSpan(0, separator), "=", spec.AsSpan(separator + 1));
        }));
        SelectedSort = criteria.Sort;
        SelectedPageSize = criteria.PageSize;
        }
        finally
        {
            _suppressImmediateCriteria = false;
        }
    }

    partial void OnSelectedSortChanged(string value) => ApplyImmediateCriteriaChange();
    partial void OnSelectedPageSizeChanged(int value) => ApplyImmediateCriteriaChange();

    private void ApplyImmediateCriteriaChange()
    {
        if (!_hasActivated || _suppressImmediateCriteria || ContentState == PageContentState.Idle) return;
        _ = ApplyImmediateCriteriaChangeAsync();
    }

    private async Task ApplyImmediateCriteriaChangeAsync()
    {
        AppliedCriteria = AppliedCriteria with
        {
            Sort = SelectedSort,
            PageSize = SelectedPageSize,
            Page = 1
        };
        RequestedCriteria = AppliedCriteria;
        OnPropertyChanged(nameof(HasResultsForPreviousQuery));
        try
        {
            await SearchAsync(AppliedCriteria, clearOnForbidden: true);
        }
        catch (ObjectDisposedException)
        {
            // A control notification can race page teardown; the activation owns cancellation and cleanup.
        }
    }

    protected override string GetSafeErrorMessage(Exception exception) => exception switch
    {
        ApiException { StatusCode: 400 } => "Bộ lọc không hợp lệ theo Catalog API. Hãy kiểm tra các giá trị rồi thử lại.",
        ApiException { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục.",
        ApiException { StatusCode: 403 } => "Bạn không có quyền xem danh sách sản phẩm.",
        ApiException { Kind: ApiErrorKind.Timeout } => "Yêu cầu Catalog quá thời gian chờ. Hãy thử lại.",
        ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối Catalog API. Hãy kiểm tra kết nối rồi thử lại.",
        ApiException { Kind: ApiErrorKind.InvalidResponse } => "Catalog API trả dữ liệu không đúng định dạng.",
        _ => "Không thể tải danh sách sản phẩm. Hãy thử lại."
    };

    protected override void OnLatestRequestFailed(Exception exception)
    {
        FieldErrors = exception is ApiException { StatusCode: 400 } api
            ? api.FieldErrors
            : new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["q"] = "Từ khóa",
            ["brand"] = "Thương hiệu",
            ["categoryId"] = "Danh mục",
            ["isActive"] = "Trạng thái",
            ["minPrice"] = "Giá tối thiểu",
            ["maxPrice"] = "Giá tối đa",
            ["spec"] = "Thông số",
            ["sort"] = "Sắp xếp",
            ["page"] = "Trang",
            ["pageSize"] = "Số sản phẩm mỗi trang"
        };
        var fields = FieldErrors.Keys.Where(labels.ContainsKey).Select(key => labels[key]).Distinct().ToArray();
        FieldErrorSummary = fields.Length == 0 ? string.Empty : $"Kiểm tra: {string.Join(", ", fields)}.";
        ContentState = PageContentState.Error;
        NotifyPagingCommands();
    }

    protected override void OnDeactivated() => _sessionStore.SessionChanged -= OnSessionChanged;

    private void OnSessionChanged(object? sender, SessionChangedEventArgs args)
    {
        CanManage = CatalogPermissions.CanManage(_sessionStore.Current);
        CancelTrackedRequests();
        ClearSnapshot();
        AppliedCriteria = new ProductQuery();
        RequestedCriteria = AppliedCriteria;
        CopyToDraft(AppliedCriteria);
        FocusProductId = null;
        Categories = [];
        _categoriesRequested = false;
        CategoryError = null;
        OnPropertyChanged(nameof(HasCategoryError));
        FieldErrorSummary = string.Empty;
        ContentState = args.Status == SessionStatus.Expired ? PageContentState.Error : PageContentState.Idle;
        ErrorMessage = args.Status == SessionStatus.Expired
            ? "Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục."
            : string.Empty;
    }

    private void ClearSnapshot()
    {
        Snapshot = null;
        OnPropertyChanged(nameof(Products));
        SnapshotQuery = null;
        IsOutOfRange = false;
        ResultContext = string.Empty;
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(HasSnapshot));
        OnPropertyChanged(nameof(HasPreviousPage));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(HasResultsForPreviousQuery));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowInitialLoading));
        OnPropertyChanged(nameof(ShowPagination));
        NotifyPagingCommands();
    }
}
