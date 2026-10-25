using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Features.Categories.Models;
using TechStore.Client.Features.Inventory.Models;
using TechStore.Client.Features.Inventory.Services;
using TechStore.Client.Services.Api;
using TechStore.Shared.Requests;

namespace TechStore.Client.Features.Inventory.ViewModels;

public sealed partial class InventoryStockViewModel(
    IInventoryApiClient apiClient,
    IInventoryReportExporter? reportExporter = null,
    ICategoryApiClient? categoryApiClient = null) : ObservableObject
{
    public const int DefaultPageSize = 20;

    private readonly List<InventoryStockRow> _loadedRows = [];
    private readonly IInventoryReportExporter _reportExporter = reportExporter ?? new CsvInventoryReportExporter();
    private readonly ICategoryApiClient? _categoryApiClient = categoryApiClient;
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;
    private bool _serverFilterApplied;

    public ObservableCollection<InventoryStockRow> StockItems { get; } = [];
    public ObservableCollection<InventoryStockRow> VisibleItems { get; } = [];
    public ObservableCollection<int> PageNumbers { get; } = [];

    public IReadOnlyList<InventoryStockStatusOption> StatusOptions { get; } =
    [
        new(InventoryStockStatusFilter.All, "Trạng thái tồn: Tất cả"),
        new(InventoryStockStatusFilter.InStock, "Còn hàng"),
        new(InventoryStockStatusFilter.LowStock, "Sắp hết hàng"),
        new(InventoryStockStatusFilter.OutOfStock, "Hết hàng")
    ];

    /// <summary>Categories are populated from the inventory response; no catalog names are fabricated in the client.</summary>
    public ObservableCollection<string> CategoryOptions { get; } = ["Danh mục: Tất cả"];
    /// <summary>Warehouses are not yet a dimension in the inventory API/schema, so only the actual aggregate warehouse is shown.</summary>
    public IReadOnlyList<string> WarehouseOptions { get; } = ["Kho: Kho Tổng"];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private InventoryStockStatusFilter _stockStatusFilter;
    [ObservableProperty] private string _selectedCategory = "Danh mục: Tất cả";
    [ObservableProperty] private string _selectedWarehouse = "Kho: Kho Tổng";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasLoaded;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private int _pageSize = DefaultPageSize;
    [ObservableProperty] private int _totalPages;
    [ObservableProperty] private int _filteredCount;
    [ObservableProperty] private bool _isSuccessToastVisible;
    [ObservableProperty] private string _successToastMessage = string.Empty;
    [ObservableProperty] private bool _isExporting;

    private CancellationTokenSource? _toastDismissCancellation;

    public bool IsCategoryFilterAvailable => CategoryOptions.Count > 1;
    public bool IsWarehouseFilterAvailable => false;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEmpty => HasLoaded && !IsLoading && !HasError && VisibleItems.Count == 0;
    public bool CanGoPrevious => Page > 1;
    public bool CanGoNext => Page < TotalPages;
    public string TotalLabel => $"{TotalCount:N0} sản phẩm tồn kho";
    public string FooterText
    {
        get
        {
            var total = _serverFilterApplied ? TotalCount : (FilteredCount == 0 ? TotalCount : FilteredCount);
            var hasRows = _serverFilterApplied ? TotalCount > 0 : FilteredCount > 0;
            var first = hasRows ? (Page - 1) * PageSize + 1 : 0;
            var last = _serverFilterApplied ? Math.Min(Page * PageSize, TotalCount) : (Page - 1) * PageSize + FilteredCount;
            return $"Hiển thị {first} – {last} trên tổng số {total:N0} sản phẩm";
        }
    }
    public string EmptyText => TotalCount == 0 ? "Chưa có tồn kho để hiển thị." : "Không tìm thấy tồn kho phù hợp.";

    public async Task ShowSuccessToastAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _toastDismissCancellation?.Cancel();
        _toastDismissCancellation?.Dispose();
        var dismiss = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _toastDismissCancellation = dismiss;
        SuccessToastMessage = message;
        IsSuccessToastVisible = true;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), dismiss.Token);
            if (!dismiss.IsCancellationRequested)
                IsSuccessToastVisible = false;
        }
        catch (OperationCanceledException) when (dismiss.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_toastDismissCancellation, dismiss))
            {
                _toastDismissCancellation = null;
                dismiss.Dispose();
            }
        }
    }

    public void DismissSuccessToast()
    {
        _toastDismissCancellation?.Cancel();
        IsSuccessToastVisible = false;
    }

    [RelayCommand]
    private void DismissToast() => DismissSuccessToast();

    public event Action<InventoryStockAction>? ActionRequested;
    public event Action<InventoryStockDeleteRequest>? DeleteRequested;
    public event Action<InventoryStockRow>? StockDetailRequested;
    public event Action<InventoryStockRow>? StockAdjustmentRequested;

    partial void OnSearchTextChanged(string value) { ProjectVisibleItems(); ReloadServerFilterIfPaged(); }
    partial void OnStockStatusFilterChanged(InventoryStockStatusFilter value) { ProjectVisibleItems(); ReloadServerFilterIfPaged(); }
    partial void OnSelectedCategoryChanged(string value) { ProjectVisibleItems(); ReloadServerFilterIfPaged(); }
    partial void OnSelectedWarehouseChanged(string value) => ProjectVisibleItems();
    partial void OnErrorMessageChanged(string? value) => NotifyState();
    partial void OnIsLoadingChanged(bool value) => NotifyState();
    partial void OnHasLoadedChanged(bool value) => NotifyState();
    partial void OnTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(TotalLabel));
        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(EmptyText));
    }
    partial void OnPageChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(FooterText));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        RefreshPageNumbers();
    }
    partial void OnTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoNext));
        NextPageCommand.NotifyCanExecuteChanged();
        RefreshPageNumbers();
    }
    partial void OnFilteredCountChanged(int value) => OnPropertyChanged(nameof(FooterText));
    partial void OnIsExportingChanged(bool value) => ExportReportCommand.NotifyCanExecuteChanged();

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        CancelLoading();
        var generation = ++_loadGeneration;
        using var load = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = load;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await apiClient.GetStocksAsync(new InventoryStockFilterRequest
            {
                Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                CategoryName = IsCategoryFilterAvailable && SelectedCategory != "Danh mục: Tất cả" ? SelectedCategory : null,
                Page = Math.Max(1, Page),
                PageSize = Math.Clamp(PageSize, 1, 100),
                LowStock = StockStatusFilter == InventoryStockStatusFilter.LowStock ? true : null,
                OutOfStock = StockStatusFilter == InventoryStockStatusFilter.OutOfStock ? true : null,
                InStock = StockStatusFilter == InventoryStockStatusFilter.InStock ? true : null
            }, load.Token);
            if (load.IsCancellationRequested || generation != _loadGeneration) return;

            _serverFilterApplied = !string.IsNullOrWhiteSpace(SearchText)
                || (IsCategoryFilterAvailable && SelectedCategory != "Danh mục: Tất cả")
                || StockStatusFilter != InventoryStockStatusFilter.All;

            _loadedRows.Clear();
            foreach (var item in result.Items ?? []) _loadedRows.Add(new InventoryStockRow(item));
            CategoryOptions.Clear();
            CategoryOptions.Add("Danh mục: Tất cả");
            if (_categoryApiClient is not null)
            {
                try
                {
                    var categories = await _categoryApiClient.GetTreeAsync(load.Token);
                    foreach (var category in FlattenCategoryNames(categories))
                        if (!CategoryOptions.Contains(category, StringComparer.OrdinalIgnoreCase)) CategoryOptions.Add(category);
                }
                catch (OperationCanceledException) when (load.IsCancellationRequested) { throw; }
                catch { /* inventory data remains usable when the category facet is unavailable */ }
            }
            foreach (var category in _loadedRows.Select(row => row.CategoryName)
                         .Where(category => category is not "—")
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(category => category, StringComparer.OrdinalIgnoreCase))
                if (!CategoryOptions.Contains(category, StringComparer.OrdinalIgnoreCase)) CategoryOptions.Add(category);
            OnPropertyChanged(nameof(IsCategoryFilterAvailable));
            Reconcile(StockItems, _loadedRows);
            Page = Math.Max(1, result.Page);
            PageSize = Math.Clamp(result.PageSize, 1, 100);
            TotalCount = Math.Max(0, result.TotalCount);
            TotalPages = Math.Max(0, result.TotalPages);
            HasLoaded = true;
            ProjectVisibleItems();
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (load.IsCancellationRequested || generation != _loadGeneration) return;
            ErrorMessage = exception is ApiException apiException
                ? apiException.Kind switch
                {
                    ApiErrorKind.Http when apiException.StatusCode == 401 => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                    ApiErrorKind.Http when apiException.StatusCode == 403 => "Bạn không có quyền xem tồn kho.",
                    ApiErrorKind.Timeout => "Tải tồn kho quá thời gian. Vui lòng thử lại.",
                    ApiErrorKind.Network => "Không thể kết nối máy chủ tồn kho.",
                    _ => "Không thể tải tồn kho. Vui lòng thử lại."
                }
                : "Không thể tải tồn kho. Vui lòng thử lại.";
            _loadedRows.Clear();
            _serverFilterApplied = false;
            Reconcile(StockItems, []);
            Reconcile(VisibleItems, []);
            NotifyState();
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                _loadCancellation = null;
                IsLoading = false;
            }
        }
    }

    public void CancelLoading()
    {
        ++_loadGeneration;
        _loadCancellation?.Cancel();
        _loadCancellation = null;
        IsLoading = false;
    }

    [RelayCommand]
    private async Task GoToPageAsync(int page)
    {
        if (TotalPages == 0) return;
        var next = Math.Clamp(page, 1, TotalPages);
        if (next == Page && HasLoaded) return;
        Page = next;
        await LoadAsync(CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private Task PreviousPageAsync() => GoToPageAsync(Page - 1);

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private Task NextPageAsync() => GoToPageAsync(Page + 1);

    [RelayCommand] private void ImportStock() => ActionRequested?.Invoke(InventoryStockAction.Import);

    [RelayCommand(CanExecute = nameof(CanExportReport))]
    private async Task ExportReportAsync(CancellationToken cancellationToken)
    {
        var snapshot = VisibleItems.ToArray();
        if (snapshot.Length == 0)
        {
            await ShowSuccessToastAsync("Không có dữ liệu tồn kho phù hợp để xuất.", cancellationToken);
            return;
        }

        IsExporting = true;
        SuccessToastMessage = "Đang tạo báo cáo tồn kho...";
        IsSuccessToastVisible = true;
        try
        {
            var result = await _reportExporter.ExportAsync(snapshot, cancellationToken);
            await ShowSuccessToastAsync(
                $"Đã xuất {result.RowCount:N0} dòng tồn kho vào {System.IO.Path.GetFileName(result.FilePath)}.",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            await ShowSuccessToastAsync("Không thể xuất báo cáo tồn kho. Vui lòng thử lại.", cancellationToken);
        }
        finally
        {
            IsExporting = false;
            ExportReportCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanExportReport() => !IsExporting;

    [RelayCommand]
    private void ViewSerials(InventoryStockRow? row)
    {
        if (row is null) return;
        StockDetailRequested?.Invoke(row);
        ActionRequested?.Invoke(InventoryStockAction.ViewSerials);
    }

    [RelayCommand]
    private void AdjustStock(InventoryStockRow? row)
    {
        if (row is null) return;
        StockAdjustmentRequested?.Invoke(row);
        ActionRequested?.Invoke(InventoryStockAction.Adjust);
    }
    [RelayCommand]
    private void DeleteStock(InventoryStockRow? row)
    {
        if (row is null) return;
        DeleteRequested?.Invoke(row.ToDeleteRequest());
        ActionRequested?.Invoke(InventoryStockAction.Delete);
    }

    private void ProjectVisibleItems()
    {
        var query = SearchText.Trim();
        var filtered = _loadedRows.Where(row =>
            (query.Length == 0 || row.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))
            && (!IsCategoryFilterAvailable || SelectedCategory == "Danh mục: Tất cả" || row.CategoryName.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
            && (StockStatusFilter == InventoryStockStatusFilter.All || row.Status == ToStatus(StockStatusFilter)))
            .ToList();
        FilteredCount = filtered.Count;
        Reconcile(VisibleItems, filtered);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(FooterText));
    }

    private void ReloadServerFilterIfPaged()
    {
        if (HasLoaded && TotalPages > 1)
            _ = LoadCommand.ExecuteAsync(null);
    }

    private static IEnumerable<string> FlattenCategoryNames(IEnumerable<CategoryNode> roots)
    {
        foreach (var root in roots)
        {
            if (!string.IsNullOrWhiteSpace(root.Name)) yield return root.Name;
            foreach (var child in FlattenCategoryNames(root.Children)) yield return child;
        }
    }

    private void RefreshPageNumbers()
    {
        PageNumbers.Clear();
        for (var page = 1; page <= TotalPages; page++) PageNumbers.Add(page);
    }

    private static InventoryStockStatus ToStatus(InventoryStockStatusFilter value) => value switch
    {
        InventoryStockStatusFilter.InStock => InventoryStockStatus.InStock,
        InventoryStockStatusFilter.LowStock => InventoryStockStatus.LowStock,
        InventoryStockStatusFilter.OutOfStock => InventoryStockStatus.OutOfStock,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static void Reconcile<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
    {
        for (var index = 0; index < desired.Count; index++)
        {
            if (index < target.Count && EqualityComparer<T>.Default.Equals(target[index], desired[index])) continue;
            var existing = target.IndexOf(desired[index]);
            if (existing >= 0) target.Move(existing, index);
            else target.Insert(index, desired[index]);
        }
        while (target.Count > desired.Count) target.RemoveAt(target.Count - 1);
    }
}
