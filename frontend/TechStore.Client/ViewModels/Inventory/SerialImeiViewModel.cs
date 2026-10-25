using TechStore.Client.Repositories.Inventory;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Inventory;

public enum SerialImeiStatusFilter
{
    All,
    InStock,
    Reserved,
    Sold,
    Returned,
    UnderRepair,
    Defective
}

public sealed record SerialImeiStatusOption(SerialImeiStatusFilter Value, string Label);

public sealed partial class SerialImeiViewModel(IInventoryRepository inventoryRepository) : ObservableObject
{
    public const int DefaultPageSize = 20;
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;

    public ObservableCollection<SerialImeiDto> Items { get; } = [];
    public ObservableCollection<int> PageNumbers { get; } = [];
    public ObservableCollection<PageButtonItem> PageButtons { get; } = [];
    public IReadOnlyList<SerialImeiStatusOption> StatusOptions { get; } =
    [
        new(SerialImeiStatusFilter.All, "Trạng thái: Tất cả"),
        new(SerialImeiStatusFilter.InStock, "Trong kho"),
        new(SerialImeiStatusFilter.Reserved, "Đang giữ"),
        new(SerialImeiStatusFilter.Sold, "Đã bán"),
        new(SerialImeiStatusFilter.Returned, "Đã trả"),
        new(SerialImeiStatusFilter.UnderRepair, "Đang sửa chữa"),
        new(SerialImeiStatusFilter.Defective, "Lỗi")
    ];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private SerialImeiStatusFilter _statusFilter;
    [ObservableProperty] private Guid? _productFilterId;
    [ObservableProperty] private Guid? _variantFilterId;
    [ObservableProperty] private string _productFilterLabel = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasLoaded;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private int _pageSize = DefaultPageSize;
    [ObservableProperty] private int _totalPages;

    public bool HasProductFilter => ProductFilterId.HasValue;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEmpty => HasLoaded && !IsLoading && !HasError && Items.Count == 0;
    public bool CanGoPrevious => Page > 1;
    public bool CanGoNext => Page < TotalPages;
    public string TotalLabel => $"{TotalCount:N0} Serial / IMEI";
    public string FooterText => $"Hiển thị {(TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1)} – {Math.Min(Page * PageSize, TotalCount)} trên tổng số {TotalCount:N0} Serial / IMEI";

    partial void OnSearchTextChanged(string value) => ReloadAfterFilterChanged();
    partial void OnStatusFilterChanged(SerialImeiStatusFilter value) => ReloadAfterFilterChanged();
    partial void OnProductFilterIdChanged(Guid? value) => OnPropertyChanged(nameof(HasProductFilter));
    partial void OnErrorMessageChanged(string? value) => NotifyState();
    partial void OnIsLoadingChanged(bool value) => NotifyState();
    partial void OnHasLoadedChanged(bool value) => NotifyState();
    partial void OnTotalCountChanged(int value) { OnPropertyChanged(nameof(TotalLabel)); OnPropertyChanged(nameof(FooterText)); }
    partial void OnPageChanged(int value) { OnPropertyChanged(nameof(CanGoPrevious)); OnPropertyChanged(nameof(CanGoNext)); OnPropertyChanged(nameof(FooterText)); RefreshPageNumbers(); PreviousPageCommand.NotifyCanExecuteChanged(); NextPageCommand.NotifyCanExecuteChanged(); }
    partial void OnPageSizeChanged(int value) => OnPropertyChanged(nameof(FooterText));
    partial void OnTotalPagesChanged(int value) { OnPropertyChanged(nameof(CanGoNext)); RefreshPageNumbers(); NextPageCommand.NotifyCanExecuteChanged(); }

    public void FilterByStock(InventoryStockRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        ProductFilterId = row.ProductId;
        VariantFilterId = row.VariantId;
        ProductFilterLabel = string.IsNullOrWhiteSpace(row.VariantName)
            ? $"{row.ProductName} ({row.Sku})"
            : $"{row.ProductName} - {row.VariantName} ({row.Sku})";
        Page = 1;
        _ = LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ClearProductFilter()
    {
        if (!HasProductFilter) return;
        ProductFilterId = null;
        VariantFilterId = null;
        ProductFilterLabel = string.Empty;
        ReloadAfterFilterChanged();
    }

    private void ReloadAfterFilterChanged()
    {
        if (!HasLoaded && !IsLoading) return;
        Page = 1;
        _ = LoadCommand.ExecuteAsync(null);
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        CancelLoading();
        var generation = ++_loadGeneration;
        using var load = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = load;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await inventoryRepository.GetSerialsAsync(new SerialImeiFilterRequest
            {
                Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                ProductId = ProductFilterId,
                VariantId = VariantFilterId,
                Status = ToStatus(StatusFilter),
                Page = Math.Max(1, Page),
                PageSize = Math.Clamp(PageSize, 1, 100)
            }, load.Token);
            if (load.IsCancellationRequested || generation != _loadGeneration) return;
            Items.Clear();
            foreach (var item in result.Items ?? []) Items.Add(item);
            Page = Math.Max(1, result.Page);
            PageSize = Math.Clamp(result.PageSize, 1, 100);
            TotalCount = Math.Max(0, result.TotalCount);
            TotalPages = Math.Max(0, result.TotalPages);
            HasLoaded = true;
            NotifyState();
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (load.IsCancellationRequested || generation != _loadGeneration) return;
            ErrorMessage = exception is ApiException apiException && apiException.StatusCode == 403
                ? "Bạn không có quyền xem Serial / IMEI."
                : "Không thể tải danh sách Serial / IMEI. Vui lòng thử lại.";
            Items.Clear();
            TotalCount = 0;
            TotalPages = 0;
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
    private async Task GoToPageAsync(object? parameter)
    {
        if (parameter is not int page || TotalPages == 0) return;
        Page = Math.Clamp(page, 1, TotalPages);
        await LoadAsync(CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private Task PreviousPageAsync() => GoToPageAsync(Page - 1);

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private Task NextPageAsync() => GoToPageAsync(Page + 1);

    private static SerialImeiStatus? ToStatus(SerialImeiStatusFilter filter) => filter == SerialImeiStatusFilter.All
        ? null
        : (SerialImeiStatus)((int)filter - 1);

    internal static string StatusText(SerialImeiStatus status) => status switch
    {
        SerialImeiStatus.InStock => "Trong kho",
        SerialImeiStatus.Reserved => "Đang giữ",
        SerialImeiStatus.Sold => "Đã bán",
        SerialImeiStatus.Returned => "Đã trả",
        SerialImeiStatus.UnderRepair => "Đang sửa chữa",
        SerialImeiStatus.Defective => "Lỗi",
        _ => status.ToString()
    };

    private static void Reconcile(ObservableCollection<int> target, IEnumerable<int> desired)
    {
        target.Clear();
        foreach (var value in desired) target.Add(value);
    }

    private void RefreshPageNumbers()
    {
        Reconcile(PageNumbers, Enumerable.Range(1, TotalPages));
        PageButtons.Clear();
        for (var page = 1; page <= TotalPages; page++)
            PageButtons.Add(new PageButtonItem(page, page == Page));
    }
}
