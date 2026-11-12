using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Configurations;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Repositories;
using TechStore.Api.Identity.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed class IdentitySeedPostgresTests
{
    [PostgresFact]
    public async Task SeedAsync_FourDemoIdentitiesAndEditedManager_PreservesCredentialsRolesAndBusinessData()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        await new DataSeeder(new DemoSeedRepository(db), new UnitOfWork(db)).SeedAsync();
        var originalHashes = await db.Users.ToDictionaryAsync(u => u.Username, u => u.PasswordHash);
        foreach (var (username, password) in new[] { ("admin", "Admin@123"), ("cashier1", "Cashier@123"), ("warehouse1", "Warehouse@123"), ("manager1", "Manager@123") })
        {
            await using var loginDb = database.CreateContext();
            var service = new AuthService(new UserRepository(loginDb), new RefreshTokenRepository(loginDb), new UnitOfWork(loginDb), Options.Create(new JwtOptions()), TimeProvider.System);
            Assert.NotNull(await service.LoginAsync(new LoginRequest { Username = username, Password = password }));
        }
        var anchor = await db.Orders.Select(x => new { x.Id, x.CreatedAt, x.TotalAmount }).ToListAsync();
        var manager = await db.Users.Include(u => u.UserRoles).SingleAsync(u => u.Username == "manager1");
        var id = manager.Id;
        manager.FullName = "User edited name";
        manager.Email = "edited@test.vn";
        manager.IsActive = false;
        manager.TokenVersion = 5;
        manager.PasswordHash = new PasswordHasher<User>().HashPassword(manager, "PrivateNewPassword@123");
        var hash = manager.PasswordHash;
        new UserRepository(db).ReplaceRole(manager, await db.Roles.SingleAsync(r => r.Name == RoleType.SalesStaff), DateTime.UtcNow);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await new DataSeeder(new DemoSeedRepository(db), new UnitOfWork(db)).SeedAsync();
        db.ChangeTracker.Clear();
        var saved = await new UserRepository(db).GetByIdWithRolesAsync(id);
        Assert.Equal("edited@test.vn", saved!.Email);
        Assert.Equal("User edited name", saved.FullName);
        Assert.False(saved.IsActive);
        Assert.Equal(5, saved.TokenVersion);
        Assert.Equal(hash, saved.PasswordHash);
        Assert.Equal(RoleType.SalesStaff, AuthenticatedUser.SoleRole(saved));
        Assert.Equal(4, await db.Users.CountAsync());
        foreach (var (username, original) in originalHashes.Where(x => x.Key != "manager1"))
            Assert.Equal(original, await db.Users.Where(u => u.Username == username).Select(u => u.PasswordHash).SingleAsync());
        Assert.Equal(anchor.OrderBy(x => x.Id), (await db.Orders.Select(x => new { x.Id, x.CreatedAt, x.TotalAmount }).ToListAsync()).OrderBy(x => x.Id));
    }
}
