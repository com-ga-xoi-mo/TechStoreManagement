using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Inventory;

public enum SupplierStatusFilter
{
    All,
    Active,
    Inactive
}

public sealed record SupplierStatusOption(SupplierStatusFilter Value, string Label);

public sealed partial class SupplierListViewModel(ISupplierRepository supplierRepository) : ObservableObject
{
    public const int DefaultPageSize = 20;
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;

    public ObservableCollection<SupplierItem> Items { get; } = [];
    public ObservableCollection<int> PageNumbers { get; } = [];
    public ObservableCollection<PageButtonItem> PageButtons { get; } = [];

    public IReadOnlyList<SupplierStatusOption> StatusOptions { get; } =
    [
        new(SupplierStatusFilter.All, "Trạng thái: Tất cả"),
        new(SupplierStatusFilter.Active, "Đang sử dụng"),
        new(SupplierStatusFilter.Inactive, "Ngừng sử dụng")
    ];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private SupplierStatusFilter _statusFilter = SupplierStatusFilter.All;
    // Set by CategoryManagementViewModel from the signed-in role; write actions are hidden and refused without it.
    [ObservableProperty] private bool _canWrite;
    [ObservableProperty] private bool _isLoading;
    // Retrying cannot help when the role may not read suppliers, so the view hides "Thử lại".
    [ObservableProperty] private bool _isForbidden;
    [ObservableProperty] private bool _hasLoaded;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private int _pageSize = DefaultPageSize;
    [ObservableProperty] private int _totalPages;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEmpty => HasLoaded && !IsLoading && !HasError && Items.Count == 0;
    public bool CanGoPrevious => Page > 1;
    public bool CanGoNext => Page < TotalPages;
    public string TotalLabel => $"{TotalCount:N0} nhà cung cấp";
    public string PageLabel => $"Trang {Page} / {Math.Max(1, TotalPages)}";
    public string FooterText => $"Hiển thị {(TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1)} – {Math.Min(Page * PageSize, TotalCount)} trên tổng số {TotalCount:N0} nhà cung cấp";

    public event Action<SupplierItem?>? SupplierEditorRequested;
    public event Action<SupplierItem>? SupplierStatusRequested;

    partial void OnSearchTextChanged(string value) => ReloadAfterFilterChanged();
    partial void OnStatusFilterChanged(SupplierStatusFilter value) => ReloadAfterFilterChanged();

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
        IsForbidden = false;
        ErrorMessage = null;
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            bool? isActive = StatusFilter switch
            {
                SupplierStatusFilter.Active => true,
                SupplierStatusFilter.Inactive => false,
                _ => null
            };

            var request = new SupplierFilterRequest
            {
                Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                IsActive = isActive,
                Page = Page,
                PageSize = PageSize
            };

            var result = await supplierRepository.GetSuppliersAsync(request, token);
            if (generation != _loadGeneration) return;

            if (result.Succeeded)
            {
                Items.Clear();
                foreach (var item in result.Items)
                {
                    Items.Add(item);
                }

                TotalCount = result.TotalCount;
                TotalPages = result.TotalPages;
                HasLoaded = true;
                UpdatePageNumbers();
            }
            else
            {
                IsForbidden = result.Failure == SupplierFailureKind.Forbidden;
                ErrorMessage = DescribeLoadFailure(result.Failure);
                Items.Clear();
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception)
        {
            if (generation == _loadGeneration)
            {
                ErrorMessage = DescribeLoadFailure(SupplierFailureKind.Network);
                Items.Clear();
            }
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

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

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
    private void CreateSupplier()
    {
        if (CanWrite) SupplierEditorRequested?.Invoke(null);
    }

    [RelayCommand]
    private void EditSupplier(object? parameter)
    {
        if (CanWrite && parameter is SupplierItem item)
        {
            SupplierEditorRequested?.Invoke(item);
        }
    }

    [RelayCommand]
    private void ToggleStatus(object? parameter)
    {
        if (CanWrite && parameter is SupplierItem item)
        {
            SupplierStatusRequested?.Invoke(item);
        }
    }

    internal static string DescribeLoadFailure(SupplierFailureKind failure) => failure switch
    {
        SupplierFailureKind.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
        SupplierFailureKind.Forbidden => "Bạn không có quyền xem danh sách nhà cung cấp.",
        _ => "Không thể tải danh sách nhà cung cấp. Vui lòng thử lại."
    };

    // Called by the page after the user confirms. Returns null on success, otherwise a message to show.
    public async Task<string?> ChangeStatusAsync(SupplierItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var result = await supplierRepository.SetStatusAsync(item.Id, !item.IsActive, cancellationToken);
        if (result.Succeeded)
        {
            await LoadAsync();
            return null;
        }

        return result.Failure switch
        {
            SupplierFailureKind.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
            SupplierFailureKind.Forbidden => "Bạn không có quyền thay đổi trạng thái nhà cung cấp.",
            SupplierFailureKind.NotFound => "Nhà cung cấp không còn tồn tại.",
            SupplierFailureKind.Conflict or SupplierFailureKind.BadRequest => result.Message ?? "Không thể cập nhật trạng thái nhà cung cấp.",
            _ => "Không thể kết nối máy chủ. Vui lòng thử lại."
        };
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
