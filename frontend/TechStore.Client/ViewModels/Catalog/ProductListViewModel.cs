using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Session;

namespace TechStore.Client.ViewModels.Catalog;

public sealed partial class ProductListViewModel : ObservableObject
{
    private const int PageSize = 8;
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(300);
    private readonly IProductRepository _productRepository;
    private readonly IProductDetailDialogService? _productDetailDialogService;
    private readonly IProductCreateDialogService? _productCreateDialogService;
    private IProductEditDialogService? _productEditDialogService;
    private ICurrentSession? _currentSession;
    private bool _sessionSubscribed;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _searchDebounceCts;
    private long _requestGeneration;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private string? selectedCategoryId = string.Empty;

    [ObservableProperty]
    private string? selectedBrand = string.Empty;

    [ObservableProperty]
    private ProductPriceBand selectedPriceBand = ProductPriceBand.All;

    public int SelectedPriceBandIndex
    {
        get => (int)SelectedPriceBand;
        set
        {
            if (Enum.IsDefined(typeof(ProductPriceBand), value))
            {
                SelectedPriceBand = (ProductPriceBand)value;
            }
        }
    }

    [ObservableProperty]
    private ProductStatus? selectedStockStatus;

    public int SelectedStockStatusIndex
    {
        get => SelectedStockStatus is null ? 0 : (int)SelectedStockStatus.Value + 1;
        set => SelectedStockStatus = value <= 0
            ? null
            : Enum.IsDefined(typeof(ProductStatus), value - 1)
                ? (ProductStatus?)(value - 1)
                : null;
    }

    [ObservableProperty]
    private ProductSortMode selectedSortMode = ProductSortMode.Newest;

    public int SelectedSortModeIndex
    {
        get => (int)SelectedSortMode;
        set
        {
            if (Enum.IsDefined(typeof(ProductSortMode), value))
            {
                SelectedSortMode = (ProductSortMode)value;
            }
        }
    }

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isError;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private int totalItems;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int totalPages = 1;

    [ObservableProperty]
    private bool isCreateSuccess;

    [ObservableProperty]
    private string createSuccessMessage = string.Empty;

    public bool IsProductGridVisible => !IsLoading && !IsError && !IsEmpty;
    public bool CanEditProducts => _currentSession?.HasCapability(ProductEditViewModel.EditCapability) == true;

    public ObservableCollection<ProductCardViewModel> Products { get; } = new();

    public string ResultRangeText => TotalItems == 0
        ? "Hiển thị 0 sản phẩm"
        : $"Hiển thị {(CurrentPage - 1) * PageSize + 1}–{Math.Min(CurrentPage * PageSize, TotalItems)} trong {TotalItems} sản phẩm";

    partial void OnTotalItemsChanged(int value) => OnPropertyChanged(nameof(ResultRangeText));

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(ResultRangeText));
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnTotalPagesChanged(int value) => NextPageCommand.NotifyCanExecuteChanged();

    private bool CanGoToNextPage() => !IsLoading && CurrentPage < TotalPages;

    private bool CanGoToPreviousPage() => !IsLoading && CurrentPage > 1;

    public ProductListViewModel(IProductRepository productRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    public ProductListViewModel(IProductRepository productRepository, IProductDetailDialogService productDetailDialogService)
        : this(productRepository)
    {
        _productDetailDialogService = productDetailDialogService ?? throw new ArgumentNullException(nameof(productDetailDialogService));
    }

    public ProductListViewModel(IProductRepository productRepository, IProductCreateDialogService productCreateDialogService)
        : this(productRepository)
    {
        _productCreateDialogService = productCreateDialogService ?? throw new ArgumentNullException(nameof(productCreateDialogService));
    }

    public ProductListViewModel(
        IProductRepository productRepository,
        IProductDetailDialogService productDetailDialogService,
        IProductCreateDialogService productCreateDialogService)
        : this(productRepository, productDetailDialogService)
    {
        _productCreateDialogService = productCreateDialogService ?? throw new ArgumentNullException(nameof(productCreateDialogService));
    }

    public ProductListViewModel(
        IProductRepository productRepository,
        IProductDetailDialogService productDetailDialogService,
        IProductCreateDialogService productCreateDialogService,
        IProductEditDialogService productEditDialogService,
        ICurrentSession currentSession)
        : this(productRepository, productDetailDialogService, productCreateDialogService)
    {
        _productEditDialogService = productEditDialogService ?? throw new ArgumentNullException(nameof(productEditDialogService));
        _currentSession = currentSession ?? throw new ArgumentNullException(nameof(currentSession));
        SubscribeToSession();
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await ExecuteSearchAsync();
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        CancelPendingSearchDebounce();
        CurrentPage = 1;
        await ExecuteSearchAsync();
    }

    [RelayCommand]
    public async Task RetryAsync()
    {
        await ExecuteSearchAsync();
    }

    [RelayCommand]
    public async Task OpenProductDetailAsync(string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        if (_productDetailDialogService is null)
        {
            throw new InvalidOperationException("Product detail dialog service is not registered.");
        }

        await _productDetailDialogService.ShowAsync(productId);
    }

    [RelayCommand]
    public async Task OpenProductCreateAsync()
    {
        if (_productCreateDialogService is null)
        {
            throw new InvalidOperationException("Product create dialog service is not registered.");
        }

        IsCreateSuccess = false;
        CreateSuccessMessage = string.Empty;
        var result = await _productCreateDialogService.ShowAsync();
        if (result.Outcome != ProductCreateDialogOutcome.Created)
        {
            return;
        }

        await ExecuteSearchAsync();
        IsCreateSuccess = true;
        CreateSuccessMessage = "Đã tạo sản phẩm preview. Dữ liệu chưa được lưu lên server; tồn kho cần nhập qua luồng riêng.";
    }

    [RelayCommand]
    public async Task OpenProductEditAsync(string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        if (!CanEditProducts)
        {
            return;
        }

        if (_productEditDialogService is null)
        {
            throw new InvalidOperationException("Product edit dialog service is not registered.");
        }

        var result = await _productEditDialogService.ShowAsync(productId);
        if (result.Outcome == ProductEditDialogOutcome.Saved)
        {
            await ExecuteSearchAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    public async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await ExecuteSearchAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    public async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await ExecuteSearchAsync();
        }
    }

    partial void OnSearchQueryChanged(string value) => _ = DebounceSearchAsync();

    partial void OnSelectedCategoryIdChanged(string? value) => _ = SearchFromFirstPageAsync();

    partial void OnSelectedBrandChanged(string? value) => _ = SearchFromFirstPageAsync();

    partial void OnSelectedPriceBandChanged(ProductPriceBand value)
    {
        OnPropertyChanged(nameof(SelectedPriceBandIndex));
        _ = SearchFromFirstPageAsync();
    }

    partial void OnSelectedStockStatusChanged(ProductStatus? value)
    {
        OnPropertyChanged(nameof(SelectedStockStatusIndex));
        _ = SearchFromFirstPageAsync();
    }

    partial void OnSelectedSortModeChanged(ProductSortMode value)
    {
        OnPropertyChanged(nameof(SelectedSortModeIndex));
        _ = SearchFromFirstPageAsync();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsProductGridVisible));
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsErrorChanged(bool value) => OnPropertyChanged(nameof(IsProductGridVisible));

    partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(IsProductGridVisible));

    private async Task SearchFromFirstPageAsync()
    {
        CurrentPage = 1;
        await ExecuteSearchAsync();
    }

    private async Task DebounceSearchAsync()
    {
        CancelPendingSearchDebounce();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;

        try
        {
            await Task.Delay(SearchDebounce, token);
            await SearchFromFirstPageAsync();
        }
        catch (OperationCanceledException)
        {
            // A newer text value or an explicit search superseded this debounce.
        }
    }

    private void CancelPendingSearchDebounce()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = null;
    }

    private async Task ExecuteSearchAsync()
    {
        var generation = Interlocked.Increment(ref _requestGeneration);
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        IsLoading = true;
        IsError = false;
        IsEmpty = false;

        try
        {
            var criteria = new ProductListCriteria(
                SearchQuery: SearchQuery,
                CategoryId: SelectedCategoryId,
                Brand: SelectedBrand,
                PriceBand: SelectedPriceBand,
                StockStatus: SelectedStockStatus,
                SortMode: SelectedSortMode,
                PageIndex: CurrentPage - 1,
                PageSize: PageSize);

            var result = await _productRepository.SearchProductsAsync(criteria, token);
            token.ThrowIfCancellationRequested();

            if (!IsCurrentRequest(generation))
            {
                return;
            }

            Products.Clear();
            foreach (var item in result.Items)
            {
                Products.Add(new ProductCardViewModel(item, OpenProductDetailCommand, OpenProductEditCommand, CanEditProducts));
            }

            TotalItems = result.TotalItems;
            TotalPages = Math.Max(1, result.TotalPages);
            CurrentPage = result.PageIndex + 1;
            IsEmpty = result.TotalItems == 0;
            OnPropertyChanged(nameof(ResultRangeText));
        }
        catch (OperationCanceledException)
        {
            // A newer query or route exit superseded this request.
        }
        catch (Exception)
        {
            if (IsCurrentRequest(generation))
            {
                IsError = true;
                IsEmpty = false;
                Products.Clear();
            }
        }
        finally
        {
            if (IsCurrentRequest(generation))
            {
                IsLoading = false;
            }
        }
    }

    private bool IsCurrentRequest(long generation) =>
        generation == Volatile.Read(ref _requestGeneration);

    public void OnNavigatedFrom()
    {
        Interlocked.Increment(ref _requestGeneration);
        CancelPendingSearchDebounce();
        _searchCts?.Cancel();
        UnsubscribeFromSession();
    }

    public void OnNavigatedTo() => SubscribeToSession();

    private void SubscribeToSession()
    {
        if (_currentSession is not null && !_sessionSubscribed)
        {
            _currentSession.SessionChanged += OnSessionChanged;
            _sessionSubscribed = true;
            OnPropertyChanged(nameof(CanEditProducts));
        }
    }

    private void UnsubscribeFromSession()
    {
        if (_currentSession is not null && _sessionSubscribed)
        {
            _currentSession.SessionChanged -= OnSessionChanged;
            _sessionSubscribed = false;
        }
    }

    private void OnSessionChanged(object? sender, CurrentSessionSnapshot snapshot)
    {
        OnPropertyChanged(nameof(CanEditProducts));
        foreach (var product in Products)
        {
            product.IsEditActionVisible = CanEditProducts;
        }
    }
}
