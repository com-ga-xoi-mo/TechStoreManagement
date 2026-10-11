using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;

namespace TechStore.Client.ViewModels.Catalog;

public sealed class CategoryTreeRowViewModel
{
    public string Id { get; }
    public string Name { get; }
    public string ParentLabel { get; }
    public string? Description { get; }
    public int ProductCount { get; }
    public bool IsActive { get; }
    public int Depth { get; }
    public bool HasChildren { get; }
    public bool IsExpanded { get; }
    public bool IsContextOnly { get; }
    public bool CanToggleExpansion { get; }
    public string ExpandCollapseLabel => IsExpanded ? $"Thu gọn {Name}" : $"Mở rộng {Name}";
    public string ExpansionGlyph => IsExpanded ? "−" : "+";
    public string Indentation => new string('\u2002', Depth * 2);
    public string AccessibilityLabel =>
        $"Cấp {Depth + 1}. {Name}. {ParentLabel}. {ProductCount} sản phẩm. " +
        (IsActive ? "Đang kích hoạt." : "Đã ẩn.") +
        (HasChildren ? $" { (IsExpanded ? "Đang mở rộng." : "Đang thu gọn.") }" : string.Empty);
    public ICommand? ToggleExpansionCommand { get; }

    public CategoryTreeRowViewModel(
        CategoryNode node,
        string parentLabel,
        int depth,
        bool hasChildren,
        bool isExpanded,
        bool isContextOnly,
        bool canToggleExpansion,
        Action<string>? toggleExpansion)
    {
        Id = node.Id;
        Name = node.Name;
        ParentLabel = parentLabel;
        Description = node.Description;
        ProductCount = node.ProductCount;
        IsActive = node.IsActive;
        Depth = depth;
        HasChildren = hasChildren;
        IsExpanded = isExpanded;
        IsContextOnly = isContextOnly;
        CanToggleExpansion = canToggleExpansion;
        if (canToggleExpansion && toggleExpansion is not null)
        {
            ToggleExpansionCommand = new RelayCommand(() => toggleExpansion(Id));
        }
    }
}

public sealed partial class CategoryListViewModel : ObservableObject
{
    public IReadOnlyList<string> StatusFilterOptions { get; } = ["Tất cả", "Đang kích hoạt", "Đã ẩn"];
    private readonly ICategoryRepository _categoryRepository;
    private readonly HashSet<string> _expandedCategoryIds = new(StringComparer.Ordinal);
    private CategoryTreeSnapshot? _snapshot;
    private CancellationTokenSource? _activeLoadCancellation;
    private Task? _activeLoadTask;
    private int _loadGeneration;

    public ObservableCollection<CategoryTreeRowViewModel> VisibleRows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(CanShowContent))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(CanShowContent))]
    private bool hasLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(CanShowContent))]
    private bool hasError;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string previewMessage = string.Empty;

    [ObservableProperty]
    private int categoryCount;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedStatusFilter = "Tất cả";

    [ObservableProperty]
    private int matchedCategoryCount;

    public bool IsEmpty => HasLoaded && !IsLoading && !HasError && VisibleRows.Count == 0;
    public bool CanShowContent => HasLoaded && !IsLoading && !HasError && VisibleRows.Count > 0;
    public string EmptyMessage => CategoryCount == 0
        ? "Chưa có danh mục."
        : "Không tìm thấy danh mục phù hợp.";

    partial void OnSearchTextChanged(string value) => RebuildRows();
    partial void OnSelectedStatusFilterChanged(string value) => RebuildRows();

    public CategoryListViewModel(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public Task LoadIfNeededAsync()
    {
        if (HasLoaded || IsLoading)
        {
            return _activeLoadTask ?? Task.CompletedTask;
        }

        _activeLoadTask = LoadAsync(CancellationToken.None);
        return _activeLoadTask;
    }

    public void CancelPendingLoad() => _activeLoadCancellation?.Cancel();

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeLoadCancellation = linkedCancellation;
        var generation = ++_loadGeneration;
        IsLoading = true;
        HasError = false;
        ErrorMessage = null;

        try
        {
            var snapshot = await _categoryRepository.GetCategoryTreeAsync(linkedCancellation.Token);
            linkedCancellation.Token.ThrowIfCancellationRequested();
            if (generation != _loadGeneration)
            {
                return;
            }

            _snapshot = snapshot;
            HasLoaded = true;
            CategoryCount = snapshot.CategoryCount;
            PreviewMessage = snapshot.IsPreviewData ? "Dữ liệu danh mục mẫu · Preview" : string.Empty;
            _expandedCategoryIds.Clear();
            AddExpandedParents(snapshot.Roots);
            RebuildRows();
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            // Leaving the category tab or cancelling the command is not a load failure.
        }
        catch (Exception)
        {
            if (generation == _loadGeneration)
            {
                HasError = true;
                ErrorMessage = "Không tải được cây danh mục. Vui lòng thử lại.";
                VisibleRows.Clear();
            }
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsLoading = false;
                _activeLoadCancellation = null;
                _activeLoadTask = null;
            }
        }
    }

    private void AddExpandedParents(IReadOnlyList<CategoryNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Count > 0)
            {
                _expandedCategoryIds.Add(node.Id);
                AddExpandedParents(node.Children);
            }
        }
    }

    private void RebuildRows()
    {
        VisibleRows.Clear();
        if (_snapshot is not null)
        {
            MatchedCategoryCount = CountMatches(_snapshot.Roots);
            foreach (var root in _snapshot.Roots)
            {
                AddVisibleNode(root, parentLabel: "Danh mục gốc", depth: 0);
            }
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CanShowContent));
    }

    private bool AddVisibleNode(CategoryNode node, string parentLabel, int depth)
    {
        var matchingChildren = new List<CategoryNode>();
        foreach (var child in node.Children)
        {
            if (MatchesOrHasMatchingDescendant(child))
            {
                matchingChildren.Add(child);
            }
        }

        var selfMatches = MatchesFilters(node);
        var isContextOnly = HasActiveFilters && !selfMatches;
        if (HasActiveFilters && !selfMatches && matchingChildren.Count == 0)
        {
            return false;
        }

        var hasChildren = HasActiveFilters ? matchingChildren.Count > 0 : node.Children.Count > 0;
        var forcedExpandedByFilter = HasActiveFilters && matchingChildren.Count > 0;
        var isExpanded = hasChildren && (forcedExpandedByFilter || _expandedCategoryIds.Contains(node.Id));
        VisibleRows.Add(new CategoryTreeRowViewModel(
            node,
            parentLabel,
            depth,
            hasChildren,
            isExpanded,
            isContextOnly,
            !HasActiveFilters,
            ToggleExpansion));

        if (isExpanded)
        {
            var children = HasActiveFilters ? matchingChildren : node.Children;
            foreach (var child in children)
            {
                AddVisibleNode(child, node.Name, depth + 1);
            }
        }

        return true;
    }

    private bool MatchesOrHasMatchingDescendant(CategoryNode node) =>
        MatchesFilters(node) || node.Children.Any(MatchesOrHasMatchingDescendant);

    private int CountMatches(IEnumerable<CategoryNode> nodes) => nodes.Sum(node =>
        (MatchesFilters(node) ? 1 : 0) + CountMatches(node.Children));

    private bool MatchesFilters(CategoryNode node)
    {
        var query = SearchText.Trim();
        var matchesName = query.Length == 0 || node.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        var matchesStatus = SelectedStatusFilter switch
        {
            "Đang kích hoạt" => node.IsActive,
            "Đã ẩn" => !node.IsActive,
            _ => true
        };

        return matchesName && matchesStatus;
    }

    private bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText) || SelectedStatusFilter is "Đang kích hoạt" or "Đã ẩn";

    private void ToggleExpansion(string categoryId)
    {
        if (!_expandedCategoryIds.Add(categoryId))
        {
            _expandedCategoryIds.Remove(categoryId);
        }

        RebuildRows();
    }
}
