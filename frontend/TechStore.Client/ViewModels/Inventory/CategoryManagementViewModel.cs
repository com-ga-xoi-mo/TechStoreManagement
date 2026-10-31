using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Services.Inventory;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Inventory;

public sealed partial class CategoryManagementViewModel(
    ICategoryWorkspaceRepository categories,
    ICategoryDialogRequests dialogs,
    InventoryStockViewModel inventory,
    SerialImeiViewModel serials) : PageViewModel
{
    private readonly List<CategoryRow> _allRows = [];
    private readonly Dictionary<Guid, CategoryRow> _byId = [];
    private readonly HashSet<Guid> _collapsed = [];
    private readonly HashSet<Guid> _statusUpdates = [];
    private int _currentPage = 1;

    public const int PageSize = 15;
    public ICategoryWorkspaceRepository Categories => categories;
    public InventoryStockViewModel Inventory { get; } = inventory;
    public SerialImeiViewModel Serials { get; } = serials;
    public ObservableCollection<CategoryNode> RootCategories { get; } = [];
    public ObservableCollection<CategoryRow> VisibleRows { get; } = [];
    public ObservableCollection<int> PageNumbers { get; } = [];
    public ObservableCollection<PageButtonItem> PageButtons { get; } = [];
    public IReadOnlyList<CategoryStatusOption> StatusOptions { get; } =
    [
        new(CategoryStatusFilter.All, "Trạng thái: Tất cả"),
        new(CategoryStatusFilter.Active, "Đang kích hoạt"),
        new(CategoryStatusFilter.Hidden, "Đã ẩn")
    ];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private CategoryStatusFilter _statusFilter;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _filteredCount;
    [ObservableProperty] private int _eligibleCount;
    [ObservableProperty] private int _pageCount;
    [ObservableProperty] private CategoryWorkspaceTab _selectedTab = CategoryWorkspaceTab.Categories;
    [ObservableProperty] private string _statusUpdateError = string.Empty;

    public int CurrentPage => _currentPage;
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < PageCount;
    public bool ShowStatePanel => IsLoading || IsEmpty || IsError;
    public bool HasStatusUpdateError => !string.IsNullOrWhiteSpace(StatusUpdateError);
    public string TotalLabel => $"{TotalCount} danh mục";
    public string PageLabel => $"Trang {CurrentPage} / {Math.Max(1, PageCount)}";
    public string FooterText => $"Hiển thị {(EligibleCount == 0 ? 0 : (CurrentPage - 1) * PageSize + 1)} - {Math.Min(CurrentPage * PageSize, EligibleCount)} trên tổng số {EligibleCount} danh mục";
    public string EmptyText => TotalCount == 0 ? "Chưa có danh mục để hiển thị." : "Không tìm thấy danh mục phù hợp.";
    public bool IsCategoryTab => SelectedTab == CategoryWorkspaceTab.Categories;
    public bool IsInventoryTab => SelectedTab == CategoryWorkspaceTab.Inventory;
    public bool IsSerialTab => SelectedTab == CategoryWorkspaceTab.SerialImei;

    public Task RefreshAsync() => ReloadAsync();

    public void ShowSerialsFor(InventoryStockRow? row)
    {
        if (row is null) return;
        SelectedTab = CategoryWorkspaceTab.SerialImei;
        Serials.FilterByStock(row);
    }

    partial void OnSearchTextChanged(string value) => ResetProjection();
    partial void OnStatusFilterChanged(CategoryStatusFilter value) => ResetProjection();
    partial void OnStatusUpdateErrorChanged(string value) => OnPropertyChanged(nameof(HasStatusUpdateError));
    partial void OnSelectedTabChanged(CategoryWorkspaceTab value)
    {
        OnPropertyChanged(nameof(IsCategoryTab));
        OnPropertyChanged(nameof(IsInventoryTab));
        OnPropertyChanged(nameof(IsSerialTab));
    }
    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(TotalLabel));

    protected override async Task<bool> LoadContentAsync(CancellationToken cancellationToken)
    {
        try
        {
            StatusUpdateError = string.Empty;
            var roots = await categories.GetNodesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
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
            ResetProjection();
            return _allRows.Count > 0;
        }
        catch
        {
            ClearRows();
            throw;
        }
    }

    protected override string GetSafeErrorMessage(Exception exception) => exception is CategoryApiException api
        ? api.Failure switch
        {
            CategoryApiFailure.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
            CategoryApiFailure.Forbidden => "Bạn không có quyền xem danh mục.",
            _ => "Không thể tải danh mục. Vui lòng thử lại."
        }
        : "Không thể tải danh mục. Vui lòng thử lại.";

    protected override void OnDeactivated()
    {
        Inventory.CancelLoading();
        Inventory.CancelExport();
        Serials.CancelLoading();
    }

    protected override void OnContentStateChanged()
    {
        base.OnContentStateChanged();
        OnPropertyChanged(nameof(ShowStatePanel));
        OnPropertyChanged(nameof(EmptyText));
    }

    private void ClearRows()
    {
        RootCategories.Clear();
        _allRows.Clear();
        _byId.Clear();
        VisibleRows.Clear();
        TotalCount = 0;
        ResetProjection();
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
        Reconcile(PageButtons, Enumerable.Range(1, PageCount).Select(p => new PageButtonItem(p, p == CurrentPage)).ToList());
        if (ContentState == PageContentState.Content && EligibleCount == 0)
            ContentState = PageContentState.Empty;
        else if (ContentState == PageContentState.Empty && EligibleCount > 0)
            ContentState = PageContentState.Content;
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(PageLabel));
        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(EmptyText));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
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
    private void GoToPage(object? parameter)
    {
        if (parameter is not int page) return;
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
            StatusUpdateError = string.Empty;
            await categories.UpdateAsync(row.Id, new UpdateCategoryRequest
            {
                Name = row.Node.Name,
                Slug = row.Node.Slug,
                ParentId = row.Node.ParentId,
                Description = row.Node.Description,
                IsActive = !row.Node.IsActive
            }, ActivationToken);

            await ReloadAsync();
        }
        catch (CategoryApiException exception)
        {
            StatusUpdateError = exception.Failure switch
            {
                CategoryApiFailure.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                CategoryApiFailure.Forbidden => "Bạn không có quyền đổi trạng thái danh mục.",
                CategoryApiFailure.BadRequest => exception.Message,
                CategoryApiFailure.Conflict => exception.Message,
                _ => "Không thể cập nhật trạng thái danh mục. Vui lòng thử lại."
            };
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation when leaving the page
        }
        catch
        {
            StatusUpdateError = "Không thể cập nhật trạng thái danh mục. Vui lòng thử lại.";
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
