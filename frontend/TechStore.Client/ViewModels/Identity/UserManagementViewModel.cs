using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Identity;
using TechStore.Client.Repositories.Identity;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Identity;

public sealed partial class UserManagementViewModel : PageViewModel, IDisposable
{
    private readonly IUserManagementRepository _repository;
    private readonly ISessionStore _session;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly long _sessionGeneration;
    public ObservableCollection<UserAccount> Accounts { get; } = [];
    public string[] RoleOptions { get; } = ["Admin", "Manager", "SalesStaff", "WarehouseStaff"];
    public string[] RoleFilterOptions { get; } = ["Tất cả vai trò", "Admin", "Manager", "SalesStaff", "WarehouseStaff"];
    public string[] StatusOptions { get; } = ["Tất cả trạng thái", "Hoạt động", "Đã khóa"];
    [ObservableProperty] private string query = string.Empty;
    [ObservableProperty] private int roleFilterIndex;
    [ObservableProperty] private int statusFilterIndex;
    [ObservableProperty] private int page = 1;
    [ObservableProperty] private int totalPages;
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private UserAccount? selectedAccount;
    [ObservableProperty] private string username = string.Empty;
    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private int selectedRoleIndex = 2;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string formError = string.Empty;
    [ObservableProperty] private string notice = string.Empty;
    [ObservableProperty] private string usernameError = string.Empty;
    [ObservableProperty] private string fullNameError = string.Empty;
    [ObservableProperty] private string emailError = string.Empty;
    [ObservableProperty] private string passwordError = string.Empty;
    [ObservableProperty] private string roleError = string.Empty;
    public bool CanManage => _session.Current is { Status: SessionStatus.Active, Role: "Admin" } current && current.Generation == _sessionGeneration;
    public bool IsEditing => SelectedAccount is not null;
    public string FormTitle => IsEditing ? "Chỉnh sửa tài khoản" : "Tạo tài khoản";
    public string PageDescription => $"Trang {Page}/{Math.Max(1, TotalPages)} · {TotalCount} tài khoản";
    public string AccessActionLabel => SelectedAccount?.IsActive == true ? "Khóa tài khoản" : "Mở khóa tài khoản";

    public UserManagementViewModel(IUserManagementRepository repository, ISessionStore session, IDialogService dialogs, INavigationService navigation)
    {
        _repository = repository;
        _session = session;
        _dialogs = dialogs;
        _navigation = navigation;
        _sessionGeneration = session.Current.Generation;
        _session.SessionChanged += OnSessionChanged;
    }

    protected override bool ManagesLoadState => true;
    protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken) => LoadAccountsAsync();
    protected override Task RetryContentAsync() => LoadAccountsAsync();
    protected override void OnLatestRequestFailed(Exception exception) => ContentState = PageContentState.Error;

    private Task<bool> LoadAccountsAsync()
    {
        ContentState = PageContentState.Loading;
        return RunLatestRequestAsync(
        async ct =>
        {
            if (!CanManage) throw new ApiException(ApiErrorKind.Http, "Access denied", 403);
            return await _repository.SearchAsync(new(Query, RoleFilterIndex is > 0 and <= 4 ? RoleOptions[RoleFilterIndex - 1] : null,
                StatusFilterIndex switch { 1 => true, 2 => false, _ => null }, Page), ct);
        }, result =>
        {
            if (!CanManage) return;
            var selectedId = SelectedAccount?.Id;
            Accounts.Clear();
            foreach (var item in result.Items) Accounts.Add(item);
            Page = result.Page;
            TotalPages = result.TotalPages;
            TotalCount = result.TotalCount;
            SelectedAccount = result.Items.FirstOrDefault(item => item.Id == selectedId);
            OnPropertyChanged(nameof(PageDescription));
            ContentState = result.Items.Count == 0 ? PageContentState.Empty : PageContentState.Content;
        }, result => CanManage && result.Items.Count > 0);
    }

    protected override string GetSafeErrorMessage(Exception exception) => exception is ApiException { StatusCode: 401 or 403 }
        ? "Bạn không có quyền quản lý tài khoản. Vui lòng đăng nhập bằng Admin."
        : "Không thể tải tài khoản. Hãy thử lại.";

    [RelayCommand]
    private Task SearchAsync() { Page = 1; return LoadAccountsAsync(); }
    [RelayCommand]
    private Task PreviousPageAsync() { if (Page > 1) Page--; return LoadAccountsAsync(); }
    [RelayCommand]
    private Task NextPageAsync() { if (Page < TotalPages) Page++; return LoadAccountsAsync(); }
    [RelayCommand]
    private void Back() => _navigation.Navigate(ShellDestination.Settings);
    [RelayCommand]
    private void SelectAccount(object? parameter)
    {
        if (!IsBusy && CanManage && parameter is UserAccount account) SelectedAccount = account;
    }

    [RelayCommand]
    private void NewAccount()
    {
        if (IsBusy) return;
        SelectedAccount = null;
        ResetForm();
    }

    [RelayCommand]
    private Task SaveAsync() => MutateAsync(async ct =>
    {
        var updated = SelectedAccount is { } account
            ? await _repository.UpdateAsync(account.Id, new() { FullName = FullName, Email = Email }, ct)
            : await _repository.CreateAsync(new CreateUserRequest { Username = Username, FullName = FullName, Email = Email, Password = Password, Role = SelectedRole() }, ct);
        if (!ct.IsCancellationRequested && CanManage) SelectedAccount = updated;
    });

    // IsBusy serializes writes while access commands stay focusable during confirmation.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ChangeRoleAsync() => SelectedAccount is not { } account ? Task.CompletedTask : MutateAsync(async ct =>
    {
        var role = SelectedRole();
        var self = account.Username == _session.Current.Username && account.Role != role;
        var updated = await _repository.SetRoleAsync(account.Id, role, ct);
        if (ct.IsCancellationRequested || !CanManage) return;
        SelectedAccount = updated;
        if (self) _session.TryExpire(_sessionGeneration);
    }, "Đổi vai trò tài khoản?", "Các phiên hiện có sẽ phải đăng nhập lại.");

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ToggleStatusAsync() => SelectedAccount is not { } account ? Task.CompletedTask : MutateAsync(async ct =>
    {
        var updated = await _repository.SetStatusAsync(account.Id, !account.IsActive, ct);
        if (ct.IsCancellationRequested || !CanManage) return;
        SelectedAccount = updated;
        if (account.Username == _session.Current.Username) _session.TryExpire(_sessionGeneration);
    }, AccessActionLabel + "?", "Thay đổi quyền truy cập và thu hồi các phiên hiện có.");

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ResetPasswordAsync() => SelectedAccount is not { } account ? Task.CompletedTask : MutateAsync(async ct =>
    {
        await _repository.ResetPasswordAsync(account.Id, Password, ct);
        if (account.Username == _session.Current.Username) _session.TryExpire(_sessionGeneration);
    }, "Đặt lại mật khẩu?", "Mật khẩu mới thay thế mật khẩu cũ và thu hồi mọi phiên của tài khoản.");

    private async Task MutateAsync(Func<CancellationToken, Task> action, string? title = null, string? message = null)
    {
        if (IsBusy || !CanManage) return;
        IsBusy = true;
        ClearErrors();
        Notice = string.Empty;
        try
        {
            await RunTrackedRequestAsync(async ct =>
            {
                try
                {
                    if (title is not null && await _dialogs.ConfirmAsync(new(title, message!, "Xác nhận", "Hủy"), ct) != DialogResult.Confirmed) return;
                    ct.ThrowIfCancellationRequested();
                    if (!CanManage) return;
                    await action(ct);
                    if (ct.IsCancellationRequested || !CanManage) return;
                    Password = string.Empty;
                    Notice = "Đã lưu thay đổi tài khoản.";
                    await LoadAccountsAsync();
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
                catch (ApiException error) when (!ct.IsCancellationRequested && CanManage)
                {
                    FormError = error.Detail ?? (error.StatusCode == 409 ? "Thao tác xung đột. Kiểm tra dữ liệu hoặc Admin cuối cùng." : "Không thể lưu tài khoản.");
                    UsernameError = Field(error, "username"); FullNameError = Field(error, "fullName"); EmailError = Field(error, "email");
                    PasswordError = Field(error, "password"); RoleError = Field(error, "role");
                }
                catch (Exception) when (!ct.IsCancellationRequested && CanManage) { FormError = "Không thể kết nối máy chủ. Hãy thử lại."; }
            });
        }
        finally { IsBusy = false; }
    }

    private string SelectedRole() => SelectedRoleIndex is >= 0 and < 4 ? RoleOptions[SelectedRoleIndex]
        : throw new ApiException(ApiErrorKind.Http, "Invalid role", 400, fieldErrors: new Dictionary<string, IReadOnlyList<string>> { ["role"] = ["Cần chọn vai trò."] });

    partial void OnSelectedAccountChanged(UserAccount? value)
    {
        ClearErrors(); Notice = string.Empty; Password = string.Empty;
        Username = value?.Username ?? ""; FullName = value?.FullName ?? ""; Email = value?.Email ?? "";
        SelectedRoleIndex = value?.Role is { } role ? Array.IndexOf(RoleOptions, role) : -1;
        if (value is null) SelectedRoleIndex = 2;
        OnPropertyChanged(nameof(IsEditing)); OnPropertyChanged(nameof(FormTitle)); OnPropertyChanged(nameof(AccessActionLabel));
    }
    private void ResetForm() { Username = ""; FullName = ""; Email = ""; Password = ""; Notice = ""; SelectedRoleIndex = 2; ClearErrors(); }
    private void ClearErrors() { FormError = ""; UsernameError = ""; FullNameError = ""; EmailError = ""; PasswordError = ""; RoleError = ""; }
    private static string Field(ApiException error, string key) => error.FieldErrors.TryGetValue(key, out var errors) ? string.Join(" ", errors) : "";
    private void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (CanManage) return;
        Deactivate(); Accounts.Clear(); SelectedAccount = null; ResetForm();
        OnPropertyChanged(nameof(CanManage));
    }
    protected override void OnDeactivated() => Password = string.Empty;
    public void Dispose() { _session.SessionChanged -= OnSessionChanged; Deactivate(); Accounts.Clear(); ResetForm(); }
}
