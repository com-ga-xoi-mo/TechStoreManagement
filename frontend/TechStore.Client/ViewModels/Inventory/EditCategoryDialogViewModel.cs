using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Inventory;

/// <summary>Headless draft and validation state for editing one category.</summary>
public sealed partial class EditCategoryDialogViewModel : ObservableObject
{
    public const string RootParentLabel = "-- Danh mục gốc (Không có danh mục cha) --";
    public const int MaxNameLength = 200;

    private readonly ICategoryWorkspaceRepository _categoryRepository;
    private readonly CategoryNode _category;
    private readonly HashSet<Guid> _excludedParentIds = [];
    private readonly HashSet<Guid> _availableParentIds = [];
    private bool _initializingParent;
    private bool _explicitParentSelection;
    private string? _originalSlug;

    public Guid CategoryId => _category.Id;
    public ObservableCollection<ParentCategoryOption> ParentOptions { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private Guid? _selectedParentId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private ParentCategoryOption? _selectedParentOption;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _currentParentUnavailable;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsRoot => SelectedParentId is null && !CurrentParentUnavailable;
    public string LevelBadgeText => IsRoot ? "Danh mục gốc" : "Danh mục con";
    public string ParentHint => IsRoot
        ? "Đây là danh mục cấp 1, không phụ thuộc danh mục khác."
        : "Chọn danh mục cha để thay đổi cấp phân loại của danh mục này.";
    public string ContextTitle => IsRoot ? "Thông tin danh mục gốc" : "Đường dẫn phân cấp";
    public string ContextText => IsRoot
        ? BuildRootContext()
        : BuildChildContext();
    public bool CanSave => !IsBusy
        && !string.IsNullOrWhiteSpace(Name)
        && Name.Trim().Length <= MaxNameLength
        && IsValidParentSelection();

    public event EventHandler<CategoryDto>? CategoryUpdated;
    public event EventHandler? RequestClose;

    public EditCategoryDialogViewModel(
        ICategoryWorkspaceRepository categoryRepository,
        CategoryNode category,
        IReadOnlyList<CategoryNode>? hierarchy = null)
    {
        _categoryRepository = categoryRepository;
        _category = category;
        _name = category.Name;
        _description = category.Description ?? string.Empty;
        _isActive = category.IsActive;
        _originalSlug = string.IsNullOrWhiteSpace(category.Slug) ? null : category.Slug;

        var roots = hierarchy ?? [category];
        BuildExcludedIds(category, _excludedParentIds);
        var currentParentId = category.ParentId ?? FindParentId(roots, category.Id);
        _selectedParentId = currentParentId;
        BuildParentOptions(roots, currentParentId);
        _currentParentUnavailable = currentParentId is not null && !_availableParentIds.Contains(currentParentId.Value);
        _initializingParent = true;
        _selectedParentOption = ParentOptions.FirstOrDefault(option => option.Id == currentParentId);
        _initializingParent = false;

        NotifyPresentationState();
    }

    partial void OnNameChanged(string value)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(value) || value.Trim().Length > MaxNameLength
            ? $"Tên danh mục phải có từ 1 đến {MaxNameLength} ký tự."
            : null;
        NotifyPresentationState();
    }

    partial void OnSelectedParentIdChanged(Guid? value)
    {
        NotifyPresentationState();
    }

    partial void OnSelectedParentOptionChanged(ParentCategoryOption? value)
    {
        if (!_initializingParent)
        {
            _explicitParentSelection = true;
            CurrentParentUnavailable = false;
        }

        if (!EqualityComparer<Guid?>.Default.Equals(SelectedParentId, value?.Id))
            SelectedParentId = value?.Id;
        NotifyPresentationState();
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnCurrentParentUnavailableChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRoot));
        OnPropertyChanged(nameof(LevelBadgeText));
        OnPropertyChanged(nameof(ParentHint));
        OnPropertyChanged(nameof(ContextTitle));
        OnPropertyChanged(nameof(ContextText));
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSave) return;

        var trimmedName = Name.Trim();
        if (trimmedName.Length == 0 || trimmedName.Length > MaxNameLength)
        {
            ErrorMessage = $"Tên danh mục phải có từ 1 đến {MaxNameLength} ký tự.";
            return;
        }

        if (!IsValidParentSelection())
        {
            ErrorMessage = "Danh mục cha đã chọn không hợp lệ hoặc có thể tạo vòng lặp.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var request = new UpdateCategoryRequest
            {
                Name = trimmedName,
                Slug = _originalSlug,
                ParentId = SelectedParentId,
                Description = Description,
                IsActive = IsActive
            };

            var updated = await _categoryRepository.UpdateAsync(CategoryId, request, cancellationToken);
            CategoryUpdated?.Invoke(this, updated);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CategoryApiException exception)
        {
            ErrorMessage = exception.Failure switch
            {
                CategoryApiFailure.Unauthorized => "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.",
                CategoryApiFailure.Forbidden => "Bạn không có quyền sửa danh mục.",
                CategoryApiFailure.NotFound => exception.Message,
                CategoryApiFailure.BadRequest => exception.Message,
                CategoryApiFailure.Conflict => exception.Message,
                _ => "Không thể kết nối đến máy chủ. Vui lòng thử lại."
            };
        }
        catch (Exception)
        {
            ErrorMessage = "Đã xảy ra lỗi khi lưu danh mục. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, EventArgs.Empty);

    private bool IsValidParentSelection()
    {
        if (CurrentParentUnavailable && !_explicitParentSelection) return false;
        if (SelectedParentId is null) return true;
        return _availableParentIds.Contains(SelectedParentId.Value)
            && !_excludedParentIds.Contains(SelectedParentId.Value)
            && ParentOptions.Any(option => option.Id == SelectedParentId);
    }

    private void BuildParentOptions(IReadOnlyList<CategoryNode> roots, Guid? currentParentId)
    {
        ParentOptions.Clear();
        ParentOptions.Add(new ParentCategoryOption(null, RootParentLabel, 0));
        var visited = new HashSet<Guid>();
        foreach (var root in roots)
            TraverseParentOption(root, 0, visited);

        _availableParentIds.RemoveWhere(id => !_excludedParentIds.Contains(id) && id == _category.Id);
    }

    private void TraverseParentOption(CategoryNode node, int level, HashSet<Guid> visited)
    {
        if (!visited.Add(node.Id)) return;
        if (!_excludedParentIds.Contains(node.Id))
        {
            _availableParentIds.Add(node.Id);
            var indent = level == 0 ? string.Empty : $"{new string(' ', level * 2)}↳ ";
            ParentOptions.Add(new ParentCategoryOption(node.Id, $"{indent}{node.Name}", level + 1));
        }

        foreach (var child in node.Children)
            TraverseParentOption(child, level + 1, visited);
    }

    private static void BuildExcludedIds(CategoryNode category, HashSet<Guid> excluded)
    {
        var visited = new HashSet<Guid>();
        void Visit(CategoryNode node)
        {
            if (!visited.Add(node.Id)) return;
            excluded.Add(node.Id);
            foreach (var child in node.Children) Visit(child);
        }

        Visit(category);
    }

    private static Guid? FindParentId(IReadOnlyList<CategoryNode> roots, Guid targetId)
    {
        var visited = new HashSet<Guid>();
        Guid? Search(IEnumerable<CategoryNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!visited.Add(node.Id)) continue;
                foreach (var child in node.Children)
                {
                    if (child.Id == targetId) return node.Id;
                    var nested = Search([child]);
                    if (nested is not null) return nested;
                }
            }
            return null;
        }

        return Search(roots);
    }

    private string BuildRootContext()
    {
        var productText = _category.ProductCount is int productCount
            ? $" và {productCount:N0} sản phẩm."
            : " và các sản phẩm liên kết.";

        if (_category.Children.Count == 0)
            return $"Đang chứa 0 danh mục con{productText}";

        var names = _category.Children.Select(child => child.Name).Take(4).ToArray();
        var suffix = _category.Children.Count > names.Length ? ", ..." : string.Empty;
        return $"Đang chứa {_category.Children.Count} danh mục con ({string.Join(", ", names)}{suffix}){productText}";
    }

    private string BuildChildContext()
    {
        var parent = FindNodeName(SelectedParentId);
        return parent is null
            ? $"Đường dẫn phân cấp: {Name}"
            : $"Đường dẫn phân cấp: {parent} > {Name}";
    }

    private string? FindNodeName(Guid? id)
    {
        if (id is null) return null;
        return ParentOptions.FirstOrDefault(option => option.Id == id)?.DisplayName.Trim(' ', '↳');
    }

    private void NotifyPresentationState()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(IsRoot));
        OnPropertyChanged(nameof(LevelBadgeText));
        OnPropertyChanged(nameof(ParentHint));
        OnPropertyChanged(nameof(ContextTitle));
        OnPropertyChanged(nameof(ContextText));
    }
}
