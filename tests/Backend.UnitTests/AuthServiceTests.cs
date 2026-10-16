using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TechStore.Api.Identity.Configurations;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class AuthServiceTests
{
    private (FakeUserRepository UserRepository, FakeRefreshTokenRepository RefreshTokenRepository, FakeUnitOfWork UnitOfWork, AuthService AuthService) CreateSut()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var jwtOptions = Options.Create(new JwtOptions
        {
            SecretKey = "Test_Secret_Key_At_Least_32_Bytes_Long_2026!",
            Issuer = "TechStore.Test",
            Audience = "TechStore.Client.Test",
            ExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7
        });

        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        var authService = new AuthService(userRepository, refreshTokenRepository, unitOfWork, jwtOptions, timeProvider);

        return (userRepository, refreshTokenRepository, unitOfWork, authService);
    }

    [Fact]
    public async Task LoginAsync_ValidAdminCredentials_ReturnsTokenAndProfile()
    {
        // Arrange
        var (userRepository, refreshTokenRepository, unitOfWork, authService) = CreateSut();
        var passwordHasher = new PasswordHasher<User>();

        var role = new Role { Id = Guid.NewGuid(), Name = RoleType.Admin };

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            FullName = "Administrator",
            Email = "admin@techstore.vn",
            IsActive = true
        };
        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Admin@123");
        adminUser.UserRoles.Add(new UserRole
        {
            UserId = adminUser.Id,
            RoleId = role.Id,
            User = adminUser,
            Role = role
        });
        userRepository.Add(adminUser);

        // Act
        var result = await authService.LoginAsync(new LoginRequest
        {
            Username = "admin",
            Password = "Admin@123"
        });

        // Assert
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.User.Should().NotBeNull();
        result.User.Username.Should().Be("admin");
        result.User.Role.Should().Be(RoleType.Admin);
        unitOfWork.SaveChangesCount.Should().BeGreaterThan(0);

        // Verify refresh token was saved in repository
        var savedToken = refreshTokenRepository.Tokens.FirstOrDefault(t => t.UserId == adminUser.Id);
        savedToken.Should().NotBeNull();
        savedToken!.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ReturnsNull()
    {
        // Arrange
        var (userRepository, _, _, authService) = CreateSut();
        var passwordHasher = new PasswordHasher<User>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "cashier",
            FullName = "Cashier User",
            Email = "cashier@techstore.vn",
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "CorrectPassword123");
        userRepository.Add(user);

        // Act
        var result = await authService.LoginAsync(new LoginRequest
        {
            Username = "cashier",
            Password = "WrongPassword"
        });

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsNull()
    {
        // Arrange
        var (userRepository, _, _, authService) = CreateSut();
        var passwordHasher = new PasswordHasher<User>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "disabled_user",
            FullName = "Disabled User",
            Email = "disabled@techstore.vn",
            IsActive = false
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "Password123");
        userRepository.Add(user);

        // Act
        var result = await authService.LoginAsync(new LoginRequest
        {
            Username = "disabled_user",
            Password = "Password123"
        });

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileAsync_ExistingUser_ReturnsUserProfileDto()
    {
        // Arrange
        var (userRepository, _, _, authService) = CreateSut();
        var role = new Role { Id = Guid.NewGuid(), Name = RoleType.Manager };

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "manager1",
            FullName = "Manager Nguyen",
            Email = "manager@techstore.vn",
            IsActive = true
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, User = user, Role = role });
        userRepository.Add(user);

        // Act
        var profile = await authService.GetProfileAsync(user.Id);

        // Assert
        profile.Should().NotBeNull();
        profile!.Username.Should().Be("manager1");
        profile.FullName.Should().Be("Manager Nguyen");
        profile.Role.Should().Be(RoleType.Manager);
    }

    [Fact]
    public async Task LogoutAsync_ValidRefreshToken_RevokesToken()
    {
        // Arrange
        var (userRepository, refreshTokenRepository, unitOfWork, authService) = CreateSut();
        var passwordHasher = new PasswordHasher<User>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            FullName = "Test User",
            Email = "test@techstore.vn",
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "Password123");
        userRepository.Add(user);

        // Login first to generate refresh token
        var loginResult = await authService.LoginAsync(new LoginRequest
        {
            Username = "testuser",
            Password = "Password123"
        });
        loginResult.Should().NotBeNull();

        // Act: Logout with refresh token
        var logoutSuccess = await authService.LogoutAsync(user.Id, new LogoutRequest
        {
            RefreshToken = loginResult!.RefreshToken
        });

        // Assert
        logoutSuccess.Should().BeTrue();
        unitOfWork.SaveChangesCount.Should().BeGreaterThan(0);

        var tokenInRepo = refreshTokenRepository.Tokens.FirstOrDefault(t => t.UserId == user.Id);
        tokenInRepo.Should().NotBeNull();
        tokenInRepo!.IsRevoked.Should().BeTrue();
    }
}
