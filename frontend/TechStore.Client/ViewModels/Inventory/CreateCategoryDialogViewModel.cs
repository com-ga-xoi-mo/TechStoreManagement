using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Inventory;

public sealed record ParentCategoryOption(Guid? Id, string DisplayName, int Level);

public sealed partial class CreateCategoryDialogViewModel : ObservableObject
{
    private static readonly string[] AvailableIcons =
    [
        "folder", "phone", "tablet", "laptop", "headphones", "watch", "speaker", "videocam",
        "memory", "cable", "monitor", "keyboard", "mouse", "gamepad", "camera", "router", "battery"
    ];

    private readonly ICategoryWorkspaceRepository _apiClient;
    private int _iconIndex;

    public ObservableCollection<ParentCategoryOption> ParentOptions { get; } = [];
    public IReadOnlyList<string> IconOptions => AvailableIcons;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _autoSlug = string.Empty;

    [ObservableProperty]
    private Guid? _selectedParentId;

    [ObservableProperty]
    private ParentCategoryOption? _selectedParentOption;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _keepCreating;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    // Client-side preview/supporting fields (strictly not sent to backend)
    [ObservableProperty]
    private string _clientCode = string.Empty;

    [ObservableProperty]
    private string _selectedIcon = "folder";

    public string SelectedIconLabel => SelectedIcon switch
    {
        "folder" => "Danh mục",
        "phone" => "Điện thoại",
        "tablet" => "Máy tính bảng",
        "laptop" => "Laptop",
        "headphones" => "Tai nghe",
        "watch" => "Đồng hồ",
        "speaker" => "Loa",
        "videocam" => "Video camera",
        "memory" => "Linh kiện nhớ",
        "cable" => "Cáp kết nối",
        "monitor" => "Màn hình",
        "keyboard" => "Bàn phím",
        "mouse" => "Chuột",
        "gamepad" => "Thiết bị chơi game",
        "camera" => "Máy ảnh",
        "router" => "Thiết bị mạng",
        "battery" => "Pin / Sạc",
        _ => SelectedIcon
    };

    [ObservableProperty]
    private int _displayOrder = 1;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool CanSave => !IsBusy && !string.IsNullOrWhiteSpace(Name);

    public event EventHandler<CategoryDto>? CategoryCreated;
    public event EventHandler? RequestClose;

    public CreateCategoryDialogViewModel(ICategoryWorkspaceRepository apiClient, IReadOnlyList<CategoryNode>? existingHierarchy = null)
    {
        _apiClient = apiClient;
        LoadParentHierarchy(existingHierarchy);
    }

    public void LoadParentHierarchy(IReadOnlyList<CategoryNode>? hierarchy)
    {
        ParentOptions.Clear();
        ParentOptions.Add(new ParentCategoryOption(null, "-- Danh mục gốc (Cấp 1) --", 0));

        if (hierarchy is not null)
        {
            void Traverse(CategoryNode node, int level)
            {
                var indent = level == 0 ? "" : $"{new string(' ', level * 2)}↳ ";
                ParentOptions.Add(new ParentCategoryOption(node.Id, $"{indent}{node.Name}", level));
                foreach (var child in node.Children)
                {
                    Traverse(child, level + 1);
                }
            }

            foreach (var root in hierarchy)
            {
                Traverse(root, 0);
            }
        }

        SelectedParentOption = ParentOptions.FirstOrDefault(o => o.Id == SelectedParentId) ?? ParentOptions[0];
    }

    partial void OnNameChanged(string value)
    {
        AutoSlug = GenerateSlug(value);
        if (string.IsNullOrWhiteSpace(ClientCode) || ClientCode.StartsWith("CAT-", StringComparison.OrdinalIgnoreCase))
        {
            ClientCode = string.IsNullOrWhiteSpace(AutoSlug) ? "" : $"CAT-{AutoSlug.ToUpperInvariant()}";
        }
    }

    partial void OnSelectedParentOptionChanged(ParentCategoryOption? value)
    {
        SelectedParentId = value?.Id;
    }

    partial void OnSelectedIconChanged(string value)
    {
        _iconIndex = Array.IndexOf(AvailableIcons, value);
        if (_iconIndex < 0) _iconIndex = 0;
        OnPropertyChanged(nameof(SelectedIconLabel));
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand]
    private void CycleIcon()
    {
        _iconIndex = (_iconIndex + 1) % AvailableIcons.Length;
        SelectedIcon = AvailableIcons[_iconIndex];
    }

    [RelayCommand]
    private void SelectIcon(string? icon)
    {
        if (!string.IsNullOrWhiteSpace(icon) && Array.IndexOf(AvailableIcons, icon) >= 0)
            SelectedIcon = icon;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSave) return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            // STRICT DTO CONTRACT: Only Name, Slug, ParentId, Description, IsActive
            var request = new CreateCategoryRequest
            {
                Name = Name.Trim(),
                Slug = string.IsNullOrWhiteSpace(AutoSlug) ? GenerateSlug(Name.Trim()) : AutoSlug,
                ParentId = SelectedParentId,
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                IsActive = IsActive
            };

            var created = await _apiClient.CreateAsync(request, cancellationToken);
            CategoryCreated?.Invoke(this, created);

            if (KeepCreating)
            {
                ResetDraft();
            }
            else
            {
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (CategoryApiException apiException)
        {
            ErrorMessage = apiException.Failure switch
            {
                CategoryApiFailure.Unauthorized => "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.",
                CategoryApiFailure.Forbidden => "Bạn không có quyền thêm danh mục.",
                CategoryApiFailure.Conflict => apiException.Message,
                CategoryApiFailure.BadRequest => apiException.Message,
                _ => "Không thể kết nối đến máy chủ. Vui lòng thử lại."
            };
        }
        catch (Exception)
        {
            ErrorMessage = "Đã xảy ra lỗi không mong muốn khi lưu danh mục. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ResetDraft()
    {
        Name = string.Empty;
        AutoSlug = string.Empty;
        Description = string.Empty;
        ClientCode = string.Empty;
        _iconIndex = 0;
        SelectedIcon = AvailableIcons[0];
        SelectedParentOption = ParentOptions.FirstOrDefault(o => o.Id == null) ?? ParentOptions.FirstOrDefault();
        SelectedParentId = null;
        IsActive = true;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, EventArgs.Empty);

    public static string GenerateSlug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var normalized = text.Trim();
        normalized = normalized.Replace('đ', 'd').Replace('Đ', 'd');

        var formD = normalized.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        var cleaned = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        cleaned = Regex.Replace(cleaned, @"[^a-z0-9]+", "-");
        cleaned = cleaned.Trim('-');
        return cleaned;
    }
}
