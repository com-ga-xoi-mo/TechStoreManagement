using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

public class DataSeederTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=techstore_db;Username=techstore_user;Password=TechStorePassword123!")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task DataSeeder_Should_Seed_Data_And_Be_Idempotent()
    {
        await using var dbContext = CreateDbContext();
        var seeder = new DataSeeder(dbContext);

        // Act 1: Run seeding
        await seeder.SeedAsync();

        // Assert: Roles exist
        var rolesCount = await dbContext.Roles.CountAsync();
        rolesCount.Should().Be(4);

        // Assert: Admin user exists with correct password
        var admin = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        admin.Should().NotBeNull();
        admin!.Email.Should().Be("admin@techstore.vn");

        var hasher = new PasswordHasher<User>();
        var verifyResult = hasher.VerifyHashedPassword(admin, admin.PasswordHash, "Admin@123");
        verifyResult.Should().Be(PasswordVerificationResult.Success);

        // Assert: Categories & Products
        var categoriesCount = await dbContext.Categories.CountAsync();
        categoriesCount.Should().BeGreaterThanOrEqualTo(2);

        var productsCount = await dbContext.Products.CountAsync();
        productsCount.Should().BeGreaterThanOrEqualTo(5);

        // Assert: Customers & Vouchers
        var customersCount = await dbContext.Customers.CountAsync();
        customersCount.Should().BeGreaterThanOrEqualTo(2);

        var vouchersCount = await dbContext.Vouchers.CountAsync();
        vouchersCount.Should().BeGreaterThanOrEqualTo(1);

        // Assert: Orders
        var ordersCount = await dbContext.Orders.CountAsync();
        ordersCount.Should().BeGreaterThanOrEqualTo(2);

        // Act 2: Idempotency check - run seeding again
        await seeder.SeedAsync();

        // Assert: Counts remain identical
        var rolesCountAfter = await dbContext.Roles.CountAsync();
        rolesCountAfter.Should().Be(rolesCount);

        var productsCountAfter = await dbContext.Products.CountAsync();
        productsCountAfter.Should().Be(productsCount);
    }
}
