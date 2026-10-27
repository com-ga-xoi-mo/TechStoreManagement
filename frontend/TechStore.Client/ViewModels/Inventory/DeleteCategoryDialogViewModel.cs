using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;

namespace TechStore.Client.ViewModels.Inventory;

/// <summary>Headless state and commands for the destructive category confirmation dialog.</summary>
public sealed partial class DeleteCategoryDialogViewModel : ObservableObject
{
    private readonly ICategoryWorkspaceRepository _categoryRepository;

    public Guid CategoryId { get; }
    public Guid TargetId => CategoryId;
    public string CategoryName { get; }
    public string ParentName { get; }
    public bool IsRoot => string.IsNullOrWhiteSpace(ParentName);
    public string LevelName => IsRoot ? "Danh mục gốc" : $"Danh mục con trực thuộc {ParentName}";
    public CategoryIconDescriptor Icon { get; }
    public int ChildCount { get; }
    public int? ProductCount { get; }
    public bool HasChildren => ChildCount > 0;
    public bool HasProducts => ProductCount is > 0;
    public string ChildConstraintText => HasChildren
        ? $"Danh mục này đang chứa {ChildCount:N0} danh mục con trực thuộc."
        : "Danh mục này hiện chưa có danh mục con.";
    public string ProductConstraintText => ProductCount is int count && count > 0
        ? $"Có {count:N0} sản phẩm liên kết thuộc phân loại này trong kho hệ thống."
        : "Không có sản phẩm liên kết thuộc phân loại này trong kho hệ thống.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeleteCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanDelete => !IsBusy;

    public event EventHandler<Guid>? CategoryDeleted;
    public event EventHandler? RequestClose;

    public DeleteCategoryDialogViewModel(
        ICategoryWorkspaceRepository categoryRepository,
        CategoryNode category,
        string? parentName = null)
    {
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        ArgumentNullException.ThrowIfNull(category);

        CategoryId = category.Id;
        CategoryName = category.Name;
        ParentName = parentName ?? string.Empty;
        Icon = CategoryIconResolver.Resolve(category.Name, category.Slug);
        ChildCount = category.Children.Count;
        ProductCount = category.ProductCount;
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    [RelayCommand(CanExecute = nameof(CanDelete))]
    public async Task ConfirmDeleteAsync(CancellationToken cancellationToken = default)
    {
        if (!CanDelete) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _categoryRepository.DeleteAsync(CategoryId, cancellationToken);
            CategoryDeleted?.Invoke(this, CategoryId);
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
                CategoryApiFailure.Forbidden => "Bạn không có quyền xóa danh mục.",
                CategoryApiFailure.BadRequest => "Không thể xóa danh mục vì vẫn còn danh mục con. Vui lòng di chuyển hoặc xóa các danh mục con trước.",
                CategoryApiFailure.Conflict => "Không thể xóa danh mục vì đang có sản phẩm liên kết trong kho hệ thống.",
                CategoryApiFailure.NotFound => "Danh mục không còn tồn tại. Vui lòng tải lại danh sách.",
                _ => "Không thể kết nối đến máy chủ. Vui lòng thử lại."
            };
        }
        catch (Exception)
        {
            ErrorMessage = "Đã xảy ra lỗi khi xóa danh mục. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, EventArgs.Empty);
}
