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
        var role = new Role { Id = Guid.NewGuid(), Name = RoleType.SalesStaff };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
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
        var role = new Role { Id = Guid.NewGuid(), Name = RoleType.SalesStaff };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
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
    [Theory]
    [InlineData(RoleType.Admin)]
    [InlineData(RoleType.Manager)]
    [InlineData(RoleType.SalesStaff)]
    [InlineData(RoleType.WarehouseStaff)]
    public async Task LoginAsync_EachRole_IssuesVersionAndKeepsPasswordHash(RoleType role)
    {
        var (users, _, work, service) = CreateSut();
        var user = IdentityMigrationPostgresTests.NewAdmin();
        user.UserRoles.Single().Role.Name = role;
        user.TokenVersion = 7;
        var hash = user.PasswordHash;
        users.Add(user);
        var response = await service.LoginAsync(new LoginRequest { Username = " ADMIN ", Password = "Admin@123" });
        Assert.NotNull(response);
        Assert.Equal(role, response.User.Role);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Equal("7", jwt.Claims.Single(c => c.Type == "token_version").Value);
        Assert.Equal(hash, user.PasswordHash);
        Assert.Equal(1, work.CommitCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task LoginAsync_AmbiguousRoles_ReturnsNull(int roleCount)
    {
        var (users, _, _, service) = CreateSut();
        var user = IdentityMigrationPostgresTests.NewAdmin();
        user.UserRoles.Clear();
        for (var i = 0; i < roleCount; i++)
            user.UserRoles.Add(new UserRole { UserId = user.Id, Role = new Role { Name = (RoleType)i } });
        users.Add(user);
        Assert.Null(await service.LoginAsync(new LoginRequest { Username = "admin", Password = "Admin@123" }));
    }

    [Fact]
    public async Task RefreshAsync_ConsumedOrRevokedToken_FailsWithoutRevokingReplacement()
    {
        var (users, tokens, _, service) = CreateSut();
        var user = IdentityMigrationPostgresTests.NewAdmin();
        users.Add(user);
        var login = (await service.LoginAsync(new LoginRequest { Username = "admin", Password = "Admin@123" }))!;
        var refresh = await service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken });
        Assert.NotNull(refresh);
        Assert.Null(await service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken }));
        Assert.Single(tokens.Tokens, t => !t.IsRevoked);
        Assert.NotNull(await service.RefreshAsync(new RefreshTokenRequest { RefreshToken = refresh.RefreshToken }));
        await service.LogoutAsync(user.Id, new LogoutRequest { RefreshToken = "another-user-token" });
        Assert.All(tokens.Tokens, t => Assert.True(t.IsRevoked));
        Assert.Equal(1, user.TokenVersion);
        Assert.False(await service.ValidateIdentityAsync(new AuthenticatedUser(user.Id, RoleType.Admin, 0)));
    }

    [Fact]
    public async Task RefreshAsync_ExpiredOrInactiveOwner_DoesNotIssueTokens()
    {
        var (users, tokens, _, service) = CreateSut();
        var user = IdentityMigrationPostgresTests.NewAdmin();
        users.Add(user);
        var login = (await service.LoginAsync(new LoginRequest { Username = "admin", Password = "Admin@123" }))!;
        tokens.Tokens.Single().ExpiresAt = DateTime.MinValue;
        Assert.Null(await service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken }));
        tokens.Tokens.Single().ExpiresAt = DateTime.MaxValue;
        user.IsActive = false;
        Assert.Null(await service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken }));
        Assert.Single(tokens.Tokens);
    }
}
