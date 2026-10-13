using TechStore.Client.Models.Identity;
using TechStore.Client.Repositories.Identity;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Identity;
using Xunit;

namespace TechStore.Client.ViewModels.Identity.Tests;

public sealed class LoginViewModelTests
{
    [Theory]
    [InlineData("locked", LoginResultStatus.LockedOut)]
    [InlineData("inactive", LoginResultStatus.Inactive)]
    [InlineData("network", LoginResultStatus.NetworkError)]
    [InlineData("unknown", LoginResultStatus.WrongCredentials)]
    public async Task FakeRepository_ReturnsDistinctFailures(string username, LoginResultStatus expected)
    {
        var result = await new FakeAuthRepository().LoginAsync(username, "sample");

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Session);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task Submit_EmptyFields_ShowsErrorsWithoutRepositoryCall()
    {
        var repository = new RecordingRepository();
        var navigation = new RecordingNavigation();
        var vm = new LoginViewModel(repository, new CurrentSessionProvider(), navigation,
            new AuthModeProvider(AuthMode.ExplicitPreview));
        LoginInputField? focused = null;
        vm.FocusRequested += field => focused = field;

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal(0, repository.CallCount);
        Assert.Equal(LoginInputField.Username, focused);
        Assert.True(vm.IsUsernameErrorVisible);
        Assert.True(vm.IsPasswordErrorVisible);
        Assert.Contains("tên đăng nhập", vm.UsernameError);
        Assert.Contains("mật khẩu", vm.PasswordError);
    }

    [Fact]
    public async Task Submit_BlankUsername_FocusesUsernameAndDoesNotCallRepository()
    {
        var repository = new RecordingRepository();
        var vm = new LoginViewModel(repository, new CurrentSessionProvider(), new RecordingNavigation(),
            new AuthModeProvider(AuthMode.ExplicitPreview));
        vm.Username = "   ";
        vm.Password = "sample";
        LoginInputField? focused = null;
        vm.FocusRequested += field => focused = field;

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal(0, repository.CallCount);
        Assert.Equal(LoginInputField.Username, focused);
        Assert.True(vm.IsUsernameErrorVisible);
    }

    [Fact]
    public async Task Submit_RejectedCredentials_KeepsUsernameAndClearsPassword()
    {
        var vm = new LoginViewModel(new FakeAuthRepository(), new CurrentSessionProvider(),
            new RecordingNavigation(), new AuthModeProvider(AuthMode.ExplicitPreview));
        vm.Username = "admin";
        vm.Password = "wrong";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal("admin", vm.Username);
        Assert.Equal(string.Empty, vm.Password);
        Assert.True(vm.IsErrorVisible);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Submit_MockSuccess_EntersPreviewOnce()
    {
        var session = new CurrentSessionProvider();
        var navigation = new RecordingNavigation();
        var vm = new LoginViewModel(new FakeAuthRepository(), session, navigation,
            new AuthModeProvider(AuthMode.ExplicitPreview));
        vm.Username = "admin";
        vm.Password = "Admin@123";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal(SessionStatus.Preview, session.Current.Status);
        Assert.Equal(1, navigation.ShellCount);
        Assert.Equal(string.Empty, vm.Password);
    }

    [Fact]
    public async Task Submit_WithoutAuthApi_DoesNotCallRepositoryOrEnterShell()
    {
        var repository = new RecordingRepository();
        var session = new CurrentSessionProvider();
        var navigation = new RecordingNavigation();
        var vm = new LoginViewModel(repository, session, navigation,
            new AuthModeProvider(AuthMode.Real));
        vm.Username = "admin";
        vm.Password = "Admin@123";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal(0, repository.CallCount);
        Assert.Equal(0, navigation.ShellCount);
        Assert.Equal(SessionStatus.Unknown, session.Current.Status);
        Assert.Contains("Auth API", vm.ErrorMessage);
        Assert.Equal(string.Empty, vm.Password);
    }

    [Fact]
    public async Task Submit_WhilePending_DoesNotStartSecondRepositoryCall()
    {
        var repository = new PendingRepository();
        var vm = new LoginViewModel(repository, new CurrentSessionProvider(), new RecordingNavigation(),
            new AuthModeProvider(AuthMode.ExplicitPreview));
        vm.Username = "admin";
        vm.Password = "sample";

        var first = vm.LoginCommand.ExecuteAsync(null);
        var second = vm.LoginCommand.ExecuteAsync(null);
        Assert.Equal(1, repository.CallCount);
        Assert.True(vm.IsLoading);

        repository.Complete();
        await Task.WhenAll(first, second);
        Assert.False(vm.IsLoading);
    }

    private sealed class RecordingRepository : IAuthRepository
    {
        public int CallCount { get; private set; }

        public Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new LoginResult(LoginResultStatus.WrongCredentials, "Rejected", null));
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingNavigation : IRootNavigationService
    {
        public int ShellCount { get; private set; }
        public void ShowShell() => ShellCount++;
        public void ShowLogin() { }
    }

    private sealed class PendingRepository : IAuthRepository
    {
        private readonly TaskCompletionSource<LoginResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }

        public Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return _completion.Task;
        }

        public void Complete() => _completion.SetResult(
            new LoginResult(LoginResultStatus.WrongCredentials, "Rejected", null));

        public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
