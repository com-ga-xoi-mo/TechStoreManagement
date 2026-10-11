using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Services.Inventory;
using TechStore.Client.Services.Dialog;

namespace TechStore.Client.ViewModels.Inventory;

public sealed partial class StockListViewModel : ObservableObject
{
    public const int RowsPerPage = 8;
    public IReadOnlyList<InventoryStockStatus> StatusOptions { get; } =
    [
        InventoryStockStatus.All,
        InventoryStockStatus.Available,
        InventoryStockStatus.LowStock,
        InventoryStockStatus.OutOfStock
    ];

    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IInventoryChangeNotifier? _changeNotifier;
    private readonly IStockDetailDialogService? _stockDetailDialogService;
    private readonly IStockAdjustmentDialogService? _stockAdjustmentDialogService;
    private CancellationTokenSource? _activeRequest;
    private Task? _categoryLoadTask;
    private Task? _activeLoadTask;
    private bool _hasActivated;
    private int _requestGeneration;

    public ObservableCollection<StockBalance> Items { get; } = [];
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    private bool hasLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    private bool hasError;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isPreviewData;

    [ObservableProperty]
    private bool isCategoryLoading;

    [ObservableProperty]
    private bool hasCategoryError;

    [ObservableProperty]
    private string? categoryErrorMessage;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string? selectedCategoryId = string.Empty;

    [ObservableProperty]
    private InventoryStockStatus selectedStatus = InventoryStockStatus.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FirstItemNumber))]
    [NotifyPropertyChangedFor(nameof(LastItemNumber))]
    [NotifyPropertyChangedFor(nameof(CanGoToPreviousPage))]
    [NotifyPropertyChangedFor(nameof(CanGoToNextPage))]
    [NotifyPropertyChangedFor(nameof(PaginationLabel))]
    private int pageNumber = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(FirstItemNumber))]
    [NotifyPropertyChangedFor(nameof(LastItemNumber))]
    [NotifyPropertyChangedFor(nameof(CanGoToPreviousPage))]
    [NotifyPropertyChangedFor(nameof(CanGoToNextPage))]
    [NotifyPropertyChangedFor(nameof(PaginationLabel))]
    private int totalItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(FirstItemNumber))]
    [NotifyPropertyChangedFor(nameof(LastItemNumber))]
    [NotifyPropertyChangedFor(nameof(CanGoToPreviousPage))]
    [NotifyPropertyChangedFor(nameof(CanGoToNextPage))]
    [NotifyPropertyChangedFor(nameof(PaginationLabel))]
    private int pageSize = RowsPerPage;

    public bool IsEmpty => HasLoaded && !IsLoading && !HasError && Items.Count == 0;
    public bool HasItems => HasLoaded && !IsLoading && !HasError && Items.Count > 0;
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
    public int FirstItemNumber => TotalItems == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
    public int LastItemNumber => TotalItems == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalItems);
    public bool CanGoToPreviousPage => PageNumber > 1;
    public bool CanGoToNextPage => PageNumber < TotalPages;
    public string PaginationLabel => TotalItems == 0
        ? "Không có sản phẩm tồn kho"
        : $"Hiển thị {FirstItemNumber}–{LastItemNumber} trên {TotalItems} · Trang {PageNumber}/{TotalPages}";

    public StockListViewModel(
        IInventoryRepository inventoryRepository,
        ICategoryRepository categoryRepository,
        IInventoryChangeNotifier? changeNotifier = null,
        IStockDetailDialogService? stockDetailDialogService = null,
        IStockAdjustmentDialogService? stockAdjustmentDialogService = null)
    {
        _inventoryRepository = inventoryRepository;
        _categoryRepository = categoryRepository;
        _changeNotifier = changeNotifier;
        _stockDetailDialogService = stockDetailDialogService;
        _stockAdjustmentDialogService = stockAdjustmentDialogService;
    }

    partial void OnSearchTextChanged(string value) => CriteriaChanged();
    partial void OnSelectedCategoryIdChanged(string? value) => CriteriaChanged();
    partial void OnSelectedStatusChanged(InventoryStockStatus value) => CriteriaChanged();

    public async Task LoadIfNeededAsync()
    {
        _hasActivated = true;
        SubscribeToInventoryChanges();
        await LoadCategoriesIfNeededAsync();
        if (IsLoading)
        {
            await WaitForCurrentQueryAsync();
        }
        else if (!HasLoaded)
        {
            await StartStockLoadAsync();
        }
    }

    public Task WaitForCurrentQueryAsync() => _activeLoadTask ?? Task.CompletedTask;

    public void CancelPendingLoad()
    {
        _activeRequest?.Cancel();
        _activeRequest = null;
        _requestGeneration++;
        IsLoading = false;
    }

    public void OnNavigatedFrom()
    {
        CancelPendingLoad();
        _hasActivated = false;
        if (_changeNotifier is not null)
        {
            _changeNotifier.StockChanged -= OnStockChanged;
        }
    }

    private void SubscribeToInventoryChanges()
    {
        if (_changeNotifier is not null)
        {
            _changeNotifier.StockChanged -= OnStockChanged;
            _changeNotifier.StockChanged += OnStockChanged;
        }
    }

    private void OnStockChanged(object? sender, StockBalanceKey changedKey)
    {
        HasLoaded = false;
        if (_hasActivated)
        {
            _ = StartStockLoadAsync();
        }
    }

    [RelayCommand]
    private Task RetryAsync() => StartStockLoadAsync();

    [RelayCommand]
    private Task OpenDetailsAsync(StockBalance? balance)
    {
        if (balance is null || _stockDetailDialogService is null)
        {
            return Task.CompletedTask;
        }

        return _stockDetailDialogService.ShowAsync(new StockBalanceKey(balance.ProductId, balance.VariantId));
    }

    [RelayCommand]
    private Task OpenAdjustmentAsync(StockBalance? balance)
    {
        if (balance is null || _stockAdjustmentDialogService is null)
        {
            return Task.CompletedTask;
        }

        return _stockAdjustmentDialogService.ShowAsync(balance, IsPreviewData);
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private Task PreviousPageAsync() => LoadPageAsync(PageNumber - 1);

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private Task NextPageAsync() => LoadPageAsync(PageNumber + 1);

    [RelayCommand]
    private async Task RetryCategoriesAsync()
    {
        _categoryLoadTask = null;
        await LoadCategoriesIfNeededAsync(force: true);
    }

    private void CriteriaChanged()
    {
        if (!_hasActivated)
        {
            return;
        }

        PageNumber = 1;
        _ = StartStockLoadAsync();
    }

    private async Task LoadPageAsync(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > TotalPages)
        {
            return;
        }

        PageNumber = pageNumber;
        await StartStockLoadAsync();
    }

    private Task StartStockLoadAsync()
    {
        _activeLoadTask = LoadStockAsync();
        return _activeLoadTask;
    }

    private async Task LoadStockAsync()
    {
        _activeRequest?.Cancel();
        _activeRequest?.Dispose();
        var request = new CancellationTokenSource();
        _activeRequest = request;
        var generation = ++_requestGeneration;
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        HasLoaded = false;
        Items.Clear();

        try
        {
            var result = await _inventoryRepository.GetStockPageAsync(
                new StockListQuery(SearchText, SelectedCategoryId, SelectedStatus, PageNumber, PageSize),
                request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (generation != _requestGeneration)
            {
                return;
            }

            foreach (var item in result.Items)
            {
                Items.Add(item);
            }

            TotalItems = result.TotalItems;
            PageNumber = result.PageNumber;
            PageSize = result.PageSize;
            IsPreviewData = result.IsPreviewData;
            HasLoaded = true;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            // Superseded stock queries are expected during filter changes/navigation.
        }
        catch (InventoryRepositoryUnavailableException)
        {
            if (generation == _requestGeneration)
            {
                HasError = true;
                ErrorMessage = "API tồn kho chưa được cấu hình. Không có dữ liệu thật; bật chế độ Preview để xem dữ liệu mẫu.";
                Items.Clear();
                TotalItems = 0;
            }
        }
        catch (Exception)
        {
            if (generation == _requestGeneration)
            {
                HasError = true;
                ErrorMessage = "Không tải được danh sách tồn kho. Vui lòng thử lại.";
                Items.Clear();
                TotalItems = 0;
            }
        }
        finally
        {
            if (generation == _requestGeneration)
            {
                IsLoading = false;
                _activeRequest = null;
            }

            request.Dispose();

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasItems));
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }
    }

    private Task LoadCategoriesIfNeededAsync(bool force = false)
    {
        if (!force && CategoryOptions.Count > 0)
        {
            return Task.CompletedTask;
        }

        if (_categoryLoadTask is not null)
        {
            return _categoryLoadTask;
        }

        _categoryLoadTask = LoadCategoriesAsync();
        return _categoryLoadTask;
    }

    private async Task LoadCategoriesAsync()
    {
        IsCategoryLoading = true;
        HasCategoryError = false;
        CategoryErrorMessage = null;
        try
        {
            var categories = await _categoryRepository.GetCategoriesAsync();
            CategoryOptions.Clear();
            CategoryOptions.Add(new CategoryOption(string.Empty, "Tất cả danh mục"));
            foreach (var category in categories.OrderBy(option => option.Name, StringComparer.CurrentCulture))
            {
                CategoryOptions.Add(category);
            }
        }
        catch (Exception)
        {
            HasCategoryError = true;
            CategoryErrorMessage = "Không tải được danh mục lọc. Vui lòng thử lại.";
        }
        finally
        {
            IsCategoryLoading = false;
            _categoryLoadTask = null;
        }
    }
}
