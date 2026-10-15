using FluentAssertions;
using TechStore.Client.Repositories.Identity;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Identity;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.Client.UnitTests.Identity;

public class LoginViewModelTests
{
    private sealed class FakeAuthRepository : IAuthRepository
    {
        public bool ShouldSucceed { get; set; } = true;
        public LoginRequest? LastRequest { get; private set; }

        public Task<LoginResponseDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (ShouldSucceed)
            {
                return Task.FromResult(new LoginResponseDto
                {
                    AccessToken = "fake_access_token",
                    RefreshToken = "fake_refresh_token",
                    User = new UserProfileDto
                    {
                        Id = Guid.NewGuid(),
                        Username = request.Username,
                        FullName = "Admin User",
                        Role = RoleType.Admin
                    }
                });
            }

            throw new Exception("401 Unauthorized");
        }

        public Task<UserProfileDto> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UserProfileDto());
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public AppRoute CurrentRoute { get; private set; } = AppRoute.Root(ShellDestination.Dashboard);
        public ShellDestination CurrentDestination => CurrentRoute.Destination;
        public ShellDestination? LastNavigatedDestination { get; private set; }

        public event EventHandler<ShellDestination>? DestinationChanged;
        public event EventHandler<AppRoute>? RouteChanged;

        public NavigationOutcome Navigate(ShellDestination destination) => Navigate(AppRoute.Root(destination));

        public NavigationOutcome Navigate(AppRoute route)
        {
            LastNavigatedDestination = route.Destination;
            CurrentRoute = route;
            DestinationChanged?.Invoke(this, route.Destination);
            RouteChanged?.Invoke(this, route);
            return NavigationOutcome.Navigated;
        }
    }

    [Fact]
    public async Task LoginAsync_EmptyUsernameOrPassword_SetsValidationErrors()
    {
        // Arrange
        var fakeRepo = new FakeAuthRepository();
        var fakeNav = new FakeNavigationService();
        var vm = new LoginViewModel(fakeRepo, fakeNav);

        // Act
        await vm.LoginCommand.ExecuteAsync(null);

        // Assert
        vm.UsernameError.Should().NotBeNullOrWhiteSpace();
        vm.PasswordError.Should().NotBeNullOrWhiteSpace();
        fakeNav.LastNavigatedDestination.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_NavigatesToDashboard()
    {
        // Arrange
        var fakeRepo = new FakeAuthRepository { ShouldSucceed = true };
        var fakeNav = new FakeNavigationService();
        var vm = new LoginViewModel(fakeRepo, fakeNav)
        {
            Username = "admin",
            Password = "Admin@123"
        };

        // Act
        await vm.LoginCommand.ExecuteAsync(null);

        // Assert
        vm.HasError.Should().BeFalse();
        vm.ErrorMessage.Should().BeEmpty();
        fakeNav.LastNavigatedDestination.Should().Be(ShellDestination.Dashboard);
        fakeRepo.LastRequest.Should().NotBeNull();
        fakeRepo.LastRequest!.Username.Should().Be("admin");
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_DisplaysErrorMessage()
    {
        // Arrange
        var fakeRepo = new FakeAuthRepository { ShouldSucceed = false };
        var fakeNav = new FakeNavigationService();
        var vm = new LoginViewModel(fakeRepo, fakeNav)
        {
            Username = "wrong_user",
            Password = "wrong_password"
        };

        // Act
        await vm.LoginCommand.ExecuteAsync(null);

        // Assert
        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Contain("không đúng");
        fakeNav.LastNavigatedDestination.Should().BeNull();
    }

    [Fact]
    public void TogglePasswordVisibility_FlipsState()
    {
        // Arrange
        var fakeRepo = new FakeAuthRepository();
        var fakeNav = new FakeNavigationService();
        var vm = new LoginViewModel(fakeRepo, fakeNav);

        vm.IsPasswordVisible.Should().BeFalse();

        // Act
        vm.TogglePasswordVisibilityCommand.Execute(null);

        // Assert
        vm.IsPasswordVisible.Should().BeTrue();
    }
}
