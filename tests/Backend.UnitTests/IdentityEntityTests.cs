using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

public class IdentityEntityTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy_test;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void IdentityEntities_Should_Initialize_With_Expected_Defaults()
    {
        var user = new User
        {
            Username = "admin",
            Email = "admin@techstore.vn",
            FullName = "Administrator",
            PasswordHash = "hash123"
        };
        user.Id.Should().NotBeEmpty();
        user.IsActive.Should().BeTrue();
        user.UserRoles.Should().NotBeNull();
        user.RefreshTokens.Should().NotBeNull();

        var role = new Role
        {
            Name = RoleType.Admin,
            Description = "Full admin"
        };
        role.Id.Should().NotBeEmpty();
        role.Name.Should().Be(RoleType.Admin);

        var userRole = new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id
        };
        userRole.UserId.Should().Be(user.Id);
        userRole.RoleId.Should().Be(role.Id);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "sha256hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        refreshToken.Id.Should().NotBeEmpty();
        refreshToken.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void IdentityModel_Should_Configure_Tables_And_PartialIndexes()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        var userEntity = model.FindEntityType(typeof(User));
        userEntity.Should().NotBeNull();
        userEntity!.GetTableName().Should().Be("users");

        var roleEntity = model.FindEntityType(typeof(Role));
        roleEntity.Should().NotBeNull();
        roleEntity!.GetTableName().Should().Be("roles");

        var userRoleEntity = model.FindEntityType(typeof(UserRole));
        userRoleEntity.Should().NotBeNull();
        userRoleEntity!.GetTableName().Should().Be("user_roles");
        userRoleEntity!.FindPrimaryKey()!.Properties.Select(p => p.Name)
            .Should().BeEquivalentTo(new[] { "UserId", "RoleId" });

        var tokenEntity = model.FindEntityType(typeof(RefreshToken));
        tokenEntity.Should().NotBeNull();
        tokenEntity!.GetTableName().Should().Be("refresh_tokens");

        var activeIndex = tokenEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "idx_refresh_tokens_active");
        activeIndex.Should().NotBeNull();
        activeIndex!.GetFilter().Should().Be("is_revoked = false");
    }
}
