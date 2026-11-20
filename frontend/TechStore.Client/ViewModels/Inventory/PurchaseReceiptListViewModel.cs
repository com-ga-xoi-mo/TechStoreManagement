using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Inventory;

public sealed record SupplierFilterOption(Guid? Id, string Name);

public sealed partial class PurchaseReceiptListViewModel(
    IPurchaseReceiptRepository receiptRepository,
    ISupplierRepository supplierRepository) : ObservableObject
{
    public const int DefaultPageSize = 20;
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;

    public ObservableCollection<PurchaseReceiptSummary> Items { get; } = [];
    public ObservableCollection<int> PageNumbers { get; } = [];
    public ObservableCollection<PageButtonItem> PageButtons { get; } = [];
    public ObservableCollection<SupplierFilterOption> SupplierOptions { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private Guid? _selectedSupplierId;
    // Set by CategoryManagementViewModel from the signed-in role; write actions are hidden and refused without it.
    [ObservableProperty] private bool _canWrite;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasLoaded;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isForbidden;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private int _pageSize = DefaultPageSize;
    [ObservableProperty] private int _totalPages;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEmpty => HasLoaded && !IsLoading && !HasError && Items.Count == 0;
    public bool CanGoPrevious => Page > 1;
    public bool CanGoNext => Page < TotalPages;
    public string TotalLabel => $"{TotalCount:N0} phiếu nhập";
    public string PageLabel => $"Trang {Page} / {Math.Max(1, TotalPages)}";
    public string FooterText => $"Hiển thị {(TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1)} – {Math.Min(Page * PageSize, TotalCount)} trên tổng số {TotalCount:N0} phiếu nhập";

    public event Action<PurchaseReceiptSummary>? ReceiptDetailRequested;
    public event Action? CreateReceiptRequested;

    partial void OnSearchTextChanged(string value) => ReloadAfterFilterChanged();
    partial void OnSelectedSupplierIdChanged(Guid? value) => ReloadAfterFilterChanged();

    private void ReloadAfterFilterChanged()
    {
        Page = 1;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;
        var generation = ++_loadGeneration;

        IsLoading = true;
        ErrorMessage = null;
        IsForbidden = false;
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            if (SupplierOptions.Count == 0)
            {
                await LoadSuppliersAsync(token);
            }

            var request = new PurchaseReceiptFilterRequest
            {
                Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                SupplierId = SelectedSupplierId,
                Page = Page,
                PageSize = PageSize
            };

            var result = await receiptRepository.GetReceiptsAsync(request, token);
            if (token.IsCancellationRequested || generation != _loadGeneration) return;

            if (result.Succeeded)
            {
                Items.Clear();
                foreach (var item in result.Items)
                {
                    Items.Add(item);
                }

                TotalCount = result.TotalCount;
                TotalPages = result.TotalPages;
                Page = result.Page;
                PageSize = result.PageSize;

                UpdatePageNumbers();
                HasLoaded = true;
            }
            else
            {
                if (result.Failure == ReceiptFailureKind.Forbidden)
                {
                    IsForbidden = true;
                    ErrorMessage = "Bạn không có quyền xem phiếu nhập.";
                }
                else
                {
                    ErrorMessage = result.Failure == ReceiptFailureKind.Unauthorized
                        ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
                        : "Không thể tải danh sách phiếu nhập. Vui lòng thử lại.";
                }

                Items.Clear();
                TotalCount = 0;
                TotalPages = 0;
                UpdatePageNumbers();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            if (token.IsCancellationRequested || generation != _loadGeneration) return;
            ErrorMessage = "Không thể tải danh sách phiếu nhập. Vui lòng thử lại.";
            Items.Clear();
            TotalCount = 0;
            TotalPages = 0;
            UpdatePageNumbers();
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(TotalLabel));
                OnPropertyChanged(nameof(PageLabel));
                OnPropertyChanged(nameof(FooterText));
            }
        }
    }

    public async Task LoadSuppliersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var loaded = new List<SupplierItem>();
            for (var page = 1; ; page++)
            {
                var result = await supplierRepository.GetSuppliersAsync(
                    new SupplierFilterRequest { Page = page, PageSize = 100 },
                    cancellationToken);
                if (!result.Succeeded) return;

                loaded.AddRange(result.Items);
                if (result.Items.Count == 0 || page >= result.TotalPages) break;
            }

            SupplierOptions.Clear();
            SupplierOptions.Add(new SupplierFilterOption(null, "Nhà cung cấp: Tất cả"));
            foreach (var s in loaded)
            {
                SupplierOptions.Add(new SupplierFilterOption(s.Id, s.Name));
            }
        }
        catch
        {
            // Supplier dropdown load error is non-fatal for receipts listing
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (!CanGoPrevious) return;
        Page--;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (!CanGoNext) return;
        Page++;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task GoToPageAsync(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > TotalPages || pageNumber == Page) return;
        Page = pageNumber;
        await LoadAsync();
    }

    [RelayCommand]
    private void CreateReceipt()
    {
        if (CanWrite) CreateReceiptRequested?.Invoke();
    }

    [RelayCommand]
    private void ViewDetail(object? parameter)
    {
        if (parameter is PurchaseReceiptSummary summary)
        {
            ReceiptDetailRequested?.Invoke(summary);
        }
    }

    private void UpdatePageNumbers()
    {
        PageNumbers.Clear();
        PageButtons.Clear();

        for (var p = 1; p <= TotalPages; p++)
        {
            PageNumbers.Add(p);
            PageButtons.Add(new PageButtonItem(p, p == Page));
        }
    }
}
