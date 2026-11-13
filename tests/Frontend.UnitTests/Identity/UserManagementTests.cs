using System.Net;
using System.Net.Http.Json;
using TechStore.Client.Models.Identity;
using TechStore.Client.Repositories.Identity;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.UnitTests.Infrastructure;
using TechStore.Client.ViewModels.Identity;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Identity;

public sealed class UserManagementTests
{
    [Fact]
    public async Task Repository_MapsFiltersAndMetadata_AndAcceptsPasswordReset204()
    {
        var id = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler((request, _) => Task.FromResult(request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new PagedResultDto<UserAccountDto>(
                [new() { Id = id, Username = "employee", FullName = "Employee", Email = "e@test.vn", Role = "WarehouseStaff", IsActive = true }], 2, 20, 21, 2)) }
            : new HttpResponseMessage(HttpStatusCode.NoContent)));
        using var client = new HttpClient(handler);
        var repository = new UserManagementRepository(new ApiClient(client, new()));
        var result = await repository.SearchAsync(new(" a&b ", "WarehouseStaff", true, 2));
        Assert.Equal("api/users?page=2&pageSize=20&q=a%26b&role=WarehouseStaff&isActive=true", handler.Requests.Single().RequestUri!.PathAndQuery.TrimStart('/'));
        Assert.Equal("Nhân viên kho", Assert.Single(result.Items).RoleLabel);
        await repository.ResetPasswordAsync(id, "NewPassword@123");
        Assert.Equal(HttpMethod.Post, handler.Requests.Last().Method);
        Assert.Contains("reset-password", handler.Requests.Last().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ViewModel_SearchAndFormErrors_PreserveInputAndAllowRetry()
    {
        var repository = new Accounts();
        var session = AdminSession();
        using var vm = new UserManagementViewModel(repository, session, new Dialogs(), new NavigationService(new Preparer()));
        await vm.ActivateAsync();
        Assert.True(vm.IsContent);
        vm.NewAccountCommand.Execute(null);
        vm.Username = "duplicate"; vm.FullName = "Input"; vm.Email = "input@test.vn"; vm.Password = "Password@123";
        repository.Failure = new ApiException(ApiErrorKind.Http, "Bad request", 400, fieldErrors: new Dictionary<string, IReadOnlyList<string>> { ["username"] = ["Đã tồn tại"] });
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("duplicate", vm.Username);
        Assert.Equal("Input", vm.FullName);
        Assert.Equal("Đã tồn tại", vm.UsernameError);
        repository.Failure = null;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("", vm.Password);
        Assert.NotEmpty(vm.Notice);
    }

    [Fact]
    public async Task NonAdminDirectRoute_DoesNotLoadAccounts_AndKeepsSixDestinations()
    {
        var repository = new Accounts();
        var session = new InMemorySessionStore();
        session.SetSession("sales", "refresh", "sales", "Sales", "SalesStaff");
        using var vm = new UserManagementViewModel(repository, session, new Dialogs(), new NavigationService(new Preparer()));
        await vm.ActivateAsync();
        Assert.Equal(0, repository.SearchCount);
        Assert.True(vm.IsError);
        Assert.Empty(vm.Accounts);
        Assert.Equal(ShellDestination.Settings, AppRoute.UserManagement().Destination);
        Assert.Equal(6, ShellDestinations.All.Count);
    }

    [Fact]
    public async Task LogoutDuringLoadAndMutation_DiscardsLateResultsAndClearsPasswords()
    {
        var repository = new Accounts { SearchGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = AdminSession();
        using var vm = new UserManagementViewModel(repository, session, new Dialogs(), new NavigationService(new Preparer()));
        var load = vm.ActivateAsync();
        vm.Password = "PrivatePassword";
        session.Clear();
        repository.SearchGate.SetResult(repository.Result);
        await load;
        Assert.Empty(vm.Accounts);
        Assert.Null(vm.SelectedAccount);
        Assert.Equal("", vm.Password);
    }

    [Fact]
    public async Task RoleLossDuringLoad_CancelsActivationAndDiscardsLateAccounts()
    {
        var repository = new Accounts { SearchGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = AdminSession();
        using var vm = new UserManagementViewModel(repository, session, new Dialogs(), new NavigationService(new Preparer()));
        var load = vm.ActivateAsync();
        vm.Password = "PrivatePassword";

        session.SetSession("manager", "refresh", "admin", "Admin", "Manager");
        repository.SearchGate.SetResult(repository.Result);
        await load;

        Assert.False(vm.CanManage);
        Assert.Empty(vm.Accounts);
        Assert.Empty(vm.Password);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task Mutation_DoubleSubmitAndCancelledConfirmation_DoNotWriteTwice()
    {
        var repository = new Accounts();
        var dialogs = new Dialogs { Result = DialogResult.Cancelled };
        using var vm = new UserManagementViewModel(repository, AdminSession(), dialogs, new NavigationService(new Preparer()));
        await vm.ActivateAsync();
        vm.SelectedAccount = repository.Result.Items.Single();
        await vm.ToggleStatusCommand.ExecuteAsync(null);
        Assert.Equal(0, repository.WriteCount);
        vm.Password = "Secret";
        vm.NewAccountCommand.Execute(null);
        Assert.Equal("", vm.Password);
        vm.Dispose();
        Assert.Empty(vm.Accounts);
    }

    [Fact]
    public async Task LoadingFailure_RetryEmptyThenContent_AndInvalidRoleDoesNotWrite()
    {
        var repository = new Accounts { SearchFailure = new HttpRequestException("offline") };
        using var vm = new UserManagementViewModel(repository, AdminSession(), new Dialogs(), new NavigationService(new Preparer()));
        await vm.ActivateAsync();
        Assert.True(vm.IsError);
        repository.SearchFailure = null;
        repository.Empty = true;
        await vm.RetryCommand.ExecuteAsync(null);
        Assert.True(vm.IsEmpty);
        repository.Empty = false;
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.True(vm.IsContent);
        vm.SelectedRoleIndex = -1;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, repository.WriteCount);
        Assert.NotEmpty(vm.RoleError);
    }

    [Fact]
    public async Task PendingMutation_BlocksOtherWritesAndSelection_AndCannotRestoreAfterLogout()
    {
        var repository = new Accounts { WriteGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = AdminSession();
        using var vm = new UserManagementViewModel(repository, session, new Dialogs(), new NavigationService(new Preparer()));
        await vm.ActivateAsync();
        vm.SelectedAccount = repository.Result.Items.Single();
        var save = vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        await vm.ToggleStatusCommand.ExecuteAsync(null);
        vm.NewAccountCommand.Execute(null);
        Assert.Equal(1, repository.WriteCount);
        Assert.NotNull(vm.SelectedAccount);
        session.Clear();
        repository.WriteGate.SetResult(repository.Result.Items.Single());
        await save;
        Assert.Null(vm.SelectedAccount);
        Assert.Empty(vm.Accounts);
        Assert.Empty(vm.Password);
        Assert.Empty(vm.Notice);
    }

    [Fact]
    public async Task ConfirmationPending_RepeatedAccessCommandsRemainFocusableButDoNotQueueWrites()
    {
        var repository = new Accounts();
        var dialogs = new Dialogs { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = new UserManagementViewModel(repository, AdminSession(), dialogs, new NavigationService(new Preparer()));
        await vm.ActivateAsync();
        vm.SelectedAccount = repository.Result.Items.Single();
        var first = vm.ChangeRoleCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.True(vm.ChangeRoleCommand.CanExecute(null));
        await vm.ChangeRoleCommand.ExecuteAsync(null);
        await vm.ToggleStatusCommand.ExecuteAsync(null);
        await vm.ResetPasswordCommand.ExecuteAsync(null);
        Assert.Equal(1, dialogs.ConfirmCount);
        Assert.Equal(0, repository.WriteCount);

        dialogs.Gate.SetResult(DialogResult.Cancelled);
        await first;
        Assert.False(vm.IsBusy);
        Assert.Equal(0, repository.WriteCount);
    }

    private static InMemorySessionStore AdminSession()
    {
        var store = new InMemorySessionStore();
        store.SetSession("admin", "refresh", "admin", "Admin", "Admin");
        return store;
    }
    private sealed class Preparer : IDestinationPreparer { public bool Prepare(AppRoute route) => true; }
    private sealed class Dialogs : IDialogService
    {
        public DialogResult Result { get; set; } = DialogResult.Confirmed;
        public TaskCompletionSource<DialogResult>? Gate { get; init; }
        public int ConfirmCount { get; private set; }
        public Task<DialogResult> ConfirmAsync(DialogRequest request, CancellationToken cancellationToken = default)
        {
            ConfirmCount++;
            return Gate?.Task ?? Task.FromResult(Result);
        }
        public Task<DialogResult> InformAsync(DialogRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Result);
    }
    private sealed class Accounts : IUserManagementRepository
    {
        public int SearchCount { get; private set; }
        public int WriteCount { get; private set; }
        public Exception? Failure { get; set; }
        public Exception? SearchFailure { get; set; }
        public bool Empty { get; set; }
        public TaskCompletionSource<UserAccountSearchResult>? SearchGate { get; init; }
        public TaskCompletionSource<UserAccount>? WriteGate { get; init; }
        public UserAccountSearchResult Result { get; } = new([new(Guid.NewGuid(), "employee", "Employee", "e@test.vn", "SalesStaff", true, DateTime.UtcNow, null)], 1, 1, 1);
        public Task<UserAccountSearchResult> SearchAsync(UserAccountQuery query, CancellationToken ct = default)
        {
            SearchCount++;
            if (SearchFailure is not null) return Task.FromException<UserAccountSearchResult>(SearchFailure);
            return SearchGate?.Task ?? Task.FromResult(Empty ? new UserAccountSearchResult([], 1, 0, 0) : Result);
        }
        private Task<UserAccount> Write()
        { WriteCount++; return Failure is null ? WriteGate?.Task ?? Task.FromResult(Result.Items.Single()) : Task.FromException<UserAccount>(Failure); }
        public Task<UserAccount> CreateAsync(CreateUserRequest request, CancellationToken ct = default) => Write();
        public Task<UserAccount> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default) => Write();
        public Task<UserAccount> SetStatusAsync(Guid id, bool active, CancellationToken ct = default) => Write();
        public Task<UserAccount> SetRoleAsync(Guid id, string role, CancellationToken ct = default) => Write();
        public async Task ResetPasswordAsync(Guid id, string password, CancellationToken ct = default) => _ = await Write();
    }
}
