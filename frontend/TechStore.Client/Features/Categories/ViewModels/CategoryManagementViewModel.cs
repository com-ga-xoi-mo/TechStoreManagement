using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Features.Categories.Models;
using TechStore.Client.Features.Categories.Services;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Shared.Requests;

namespace TechStore.Client.Features.Categories.ViewModels;

public sealed partial class CategoryManagementViewModel(
    ICategoryWorkspaceRepository apiClient, ICategoryDialogRequests dialogs) : ObservableObject
{
    private readonly List<CategoryRow> _allRows = [];
    private readonly Dictionary<Guid, CategoryRow> _byId = [];
    private readonly HashSet<Guid> _collapsed = [];
    private readonly HashSet<Guid> _statusUpdates = [];
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;
    private int _currentPage = 1;

    public const int PageSize = 15;
    public ObservableCollection<CategoryNode> RootCategories { get; } = [];
    public ObservableCollection<CategoryRow> VisibleRows { get; } = [];
    public ObservableCollection<int> PageNumbers { get; } = [];
    public IReadOnlyList<CategoryStatusOption> StatusOptions { get; } =
    [
        new(CategoryStatusFilter.All, "Trạng thái: Tất cả"),
        new(CategoryStatusFilter.Active, "Đang kích hoạt"),
        new(CategoryStatusFilter.Hidden, "Đã ẩn")
    ];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private CategoryStatusFilter _statusFilter;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasLoaded;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _filteredCount;
    [ObservableProperty] private int _eligibleCount;
    [ObservableProperty] private int _pageCount;
    [ObservableProperty] private CategoryWorkspaceTab _selectedTab = CategoryWorkspaceTab.Categories;

    public int CurrentPage => _currentPage;
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < PageCount;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsEmpty => HasLoaded && !IsLoading && !HasError && EligibleCount == 0;
    public string TotalLabel => $"{TotalCount} danh mục";
    public string FooterText => $"Hiển thị {(EligibleCount == 0 ? 0 : (CurrentPage - 1) * PageSize + 1)} - {Math.Min(CurrentPage * PageSize, EligibleCount)} trên tổng số {EligibleCount} danh mục";
    public string EmptyText => TotalCount == 0 ? "Chưa có danh mục để hiển thị." : "Không tìm thấy danh mục phù hợp.";
    public bool IsCategoryTab => SelectedTab == CategoryWorkspaceTab.Categories;
    public bool IsInventoryTab => SelectedTab == CategoryWorkspaceTab.Inventory;
    public bool IsSerialTab => SelectedTab == CategoryWorkspaceTab.SerialImei;

    partial void OnSearchTextChanged(string value) => ResetProjection();
    partial void OnStatusFilterChanged(CategoryStatusFilter value) => ResetProjection();
    partial void OnSelectedTabChanged(CategoryWorkspaceTab value)
    {
        OnPropertyChanged(nameof(IsCategoryTab));
        OnPropertyChanged(nameof(IsInventoryTab));
        OnPropertyChanged(nameof(IsSerialTab));
    }
    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(TotalLabel));
    partial void OnErrorMessageChanged(string? value) => NotifyState();
    partial void OnIsLoadingChanged(bool value) => NotifyState();
    partial void OnHasLoadedChanged(bool value) => NotifyState();

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
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
            var roots = await apiClient.GetNodesAsync(load.Token);
            if (load.IsCancellationRequested || generation != _loadGeneration) return;
            _allRows.Clear();
            _byId.Clear();
            _collapsed.Clear();
            RootCategories.Clear();
            foreach (var root in roots)
            {
                RootCategories.Add(root);
                AddRows(root, null);
            }
            TotalCount = _allRows.Count;
            HasLoaded = true;
            ResetProjection();
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (load.IsCancellationRequested || generation != _loadGeneration) return;
            ErrorMessage = exception is CategoryApiException api ? api.Failure switch
            {
                CategoryApiFailure.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                CategoryApiFailure.Forbidden => "Bạn không có quyền xem danh mục.",
                _ => "Không thể tải danh mục. Vui lòng thử lại."
            } : "Không thể tải danh mục. Vui lòng thử lại.";
            // A failed session must not expose a previous user's rows.
            RootCategories.Clear();
            _allRows.Clear();
            _byId.Clear();
            VisibleRows.Clear();
            TotalCount = 0;
            ResetProjection();
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

    private void AddRows(CategoryNode node, CategoryRow? parent)
    {
        if (_byId.ContainsKey(node.Id)) return;
        var row = new CategoryRow(node, parent);
        _allRows.Add(row);
        _byId.Add(row.Id, row);
        foreach (var child in node.Children) AddRows(child, row);
    }

    private void ResetProjection()
    {
        _currentPage = 1;
        ProjectRows();
    }

    private void ProjectRows()
    {
        var query = SearchText.Trim();
        var filtering = query.Length > 0 || StatusFilter != CategoryStatusFilter.All;
        var matches = _allRows.Where(row => row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            && (StatusFilter == CategoryStatusFilter.All || row.IsActive == (StatusFilter == CategoryStatusFilter.Active))).ToList();
        FilteredCount = matches.Count;
        var forced = new HashSet<Guid>();
        if (filtering)
            foreach (var row in matches)
                for (var parent = row.Parent; parent is not null; parent = parent.Parent) forced.Add(parent.Id);

        foreach (var row in _allRows)
        {
            row.IsForcedOpen = forced.Contains(row.Id);
            row.IsExpanded = row.IsForcedOpen || !_collapsed.Contains(row.Id);
        }
        var eligible = matches.Where(row => filtering || AncestorsExpanded(row)).ToList();
        EligibleCount = eligible.Count;
        PageCount = (EligibleCount + PageSize - 1) / PageSize;
        _currentPage = Math.Clamp(_currentPage, 1, Math.Max(1, PageCount));
        var pageRows = eligible.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        var pageIds = pageRows.Select(row => row.Id).ToHashSet();
        var included = new HashSet<Guid>(pageIds);
        foreach (var row in pageRows)
            for (var parent = row.Parent; parent is not null; parent = parent.Parent) included.Add(parent.Id);
        var output = _allRows.Where(row => included.Contains(row.Id)).ToList();
        foreach (var row in output) row.IsContextOnly = !pageIds.Contains(row.Id);
        Reconcile(VisibleRows, output);
        Reconcile(PageNumbers, Enumerable.Range(1, PageCount).ToList());
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(FooterText));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        NotifyState();
    }

    private bool AncestorsExpanded(CategoryRow row)
    {
        for (var parent = row.Parent; parent is not null; parent = parent.Parent)
            if (_collapsed.Contains(parent.Id)) return false;
        return true;
    }

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

    [RelayCommand]
    private void ToggleExpansion(CategoryRow? row)
    {
        if (row is null || !row.CanToggle || !_byId.ContainsKey(row.Id)) return;
        if (!_collapsed.Add(row.Id)) _collapsed.Remove(row.Id);
        ProjectRows();
    }

    [RelayCommand]
    private void GoToPage(int page)
    {
        _currentPage = Math.Clamp(page, 1, Math.Max(1, PageCount));
        ProjectRows();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousPage() => GoToPage(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextPage() => GoToPage(CurrentPage + 1);

    [RelayCommand] private void AddCategory() => dialogs.Request(new(CategoryDialogAction.Add, null));
    [RelayCommand] private void SelectTab(CategoryWorkspaceTab tab) => SelectedTab = tab;
    [RelayCommand] private void EditCategory(CategoryRow? row) => RequestRowDialog(CategoryDialogAction.Edit, row);
    [RelayCommand] private void DeleteCategory(CategoryRow? row) => RequestRowDialog(CategoryDialogAction.Delete, row);

    [RelayCommand]
    private async Task ToggleStatusAsync(CategoryRow? row)
    {
        if (row is null || !_byId.ContainsKey(row.Id) || !_statusUpdates.Add(row.Id)) return;

        try
        {
            ErrorMessage = null;
            await apiClient.UpdateAsync(row.Id, new UpdateCategoryRequest
            {
                Name = row.Node.Name,
                Slug = row.Node.Slug,
                ParentId = row.Node.ParentId,
                Description = row.Node.Description,
                IsActive = !row.Node.IsActive
            });

            // Reload from the API so the status, filters, counts and hierarchy
            // all reflect the persisted server state instead of an optimistic
            // client-only mutation.
            await LoadCommand.ExecuteAsync(null);
        }
        catch (CategoryApiException exception)
        {
            ErrorMessage = exception.Failure switch
            {
                CategoryApiFailure.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                CategoryApiFailure.Forbidden => "Bạn không có quyền đổi trạng thái danh mục.",
                CategoryApiFailure.BadRequest => exception.Message,
                CategoryApiFailure.Conflict => exception.Message,
                _ => "Không thể cập nhật trạng thái danh mục. Vui lòng thử lại."
            };
            NotifyState();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            ErrorMessage = "Không thể cập nhật trạng thái danh mục. Vui lòng thử lại.";
            NotifyState();
        }
        finally
        {
            _statusUpdates.Remove(row.Id);
        }
    }

    private void RequestRowDialog(CategoryDialogAction action, CategoryRow? row)
    {
        if (row is not null && _byId.ContainsKey(row.Id)) dialogs.Request(new(action, row.Id));
    }
}
