using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Repositories;
using TechStore.Api.Common.Data;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public sealed class IdentityMigrationPostgresTests
{
    [PostgresFact]
    public async Task Migrate_FreshDatabase_CreatesVersionAndNormalizedIndexes()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var admin = NewAdmin();
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        Assert.Equal(0, admin.TokenVersion);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_indexes WHERE tablename='users' AND indexname IN ('idx_users_normalized_username','idx_users_normalized_email')", connection);
        Assert.Equal(2L, await command.ExecuteScalarAsync());
        db.Users.Add(new User { Username = " ADMIN ", Email = "other@test.vn", FullName = "Duplicate", PasswordHash = "hash" });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23505", ((PostgresException)error.InnerException!).SqlState);
    }

    [PostgresFact]
    public async Task Migrate_LegacyIdentity_PreservesHashAndRejectsDuplicatesWithoutPartialMigration()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        const string previous = "20261022182049_AddProductImages";
        await using (var db = database.CreateContext())
            await db.GetService<IMigrator>().MigrateAsync(previous);
        var admin = NewAdmin();
        var hash = admin.PasswordHash;
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                INSERT INTO roles(id,name) VALUES (@role,'Admin');
                INSERT INTO users(id,username,email,full_name,password_hash) VALUES (@id,'admin','admin@test.vn','Admin',@hash);
                INSERT INTO user_roles(user_id,role_id) VALUES (@id,@role);
                INSERT INTO users(username,email,full_name,password_hash) VALUES (' ADMIN ','duplicate@test.vn','Other','unchanged');
                """, connection);
            command.Parameters.AddWithValue("role", admin.UserRoles.Single().RoleId);
            command.Parameters.AddWithValue("id", admin.Id);
            command.Parameters.AddWithValue("hash", hash);
            await command.ExecuteNonQueryAsync();
        }
        await using (var db = database.CreateContext())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
            Assert.Contains("duplicate normalized", error.MessageText);
        }
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("DELETE FROM users WHERE username=' ADMIN '", connection);
            await command.ExecuteNonQueryAsync();
        }
        await using (var db = database.CreateContext())
        {
            await db.Database.MigrateAsync();
            var upgraded = await db.Users.SingleAsync();
            Assert.Equal(admin.Id, upgraded.Id);
            Assert.Equal(hash, upgraded.PasswordHash);
            Assert.Equal(0, upgraded.TokenVersion);
        }
    }

    [PostgresFact]
    public async Task Migrate_PopulatedWithoutUsableAdmin_StopsWithoutRewritingCredentials()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using (var db = database.CreateContext())
            await db.GetService<IMigrator>().MigrateAsync("20261022182049_AddProductImages");
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand("INSERT INTO users(username,email,full_name,password_hash) VALUES ('employee','employee@test.vn','Employee','original')", connection))
            await command.ExecuteNonQueryAsync();
        await using (var db = database.CreateContext())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
            Assert.Contains("no active sole-role Admin", error.MessageText);
        }
        await using var check = new NpgsqlCommand("SELECT password_hash FROM users WHERE username='employee'", connection);
        Assert.Equal("original", await check.ExecuteScalarAsync());
    }

    [PostgresFact]
    public async Task Repository_ReadsDoNotTrack_AndUserLockBlocksAnotherTransaction()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var user = NewAdmin();
        await using (var db = database.CreateContext())
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        await using var first = database.CreateContext();
        var repository = new UserRepository(first);
        Assert.Equal(user.Id, (await repository.GetByNormalizedUsernameAsync(" ADMIN "))!.Id);
        var search = await repository.SearchAsync(new UserSearchCriteria("Admin", RoleType.Admin, true, 1, 20));
        Assert.Single(search.Items);
        Assert.Equal(1, search.TotalCount);
        Assert.Empty(first.ChangeTracker.Entries());
        var firstWork = new UnitOfWork(first);
        await firstWork.BeginTransactionAsync();
        await repository.GetForUpdateAsync(user.Id);
        await using var second = database.CreateContext();
        await second.Database.OpenConnectionAsync();
        var pid = ((NpgsqlConnection)second.Database.GetDbConnection()).ProcessID;
        var secondWork = new UnitOfWork(second);
        await secondWork.BeginTransactionAsync();
        var pending = new UserRepository(second).GetForUpdateAsync(user.Id);
        await using var observer = new NpgsqlConnection(database.ConnectionString);
        await observer.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT wait_event_type='Lock' FROM pg_stat_activity WHERE pid=@pid", observer);
            command.Parameters.AddWithValue("pid", pid);
            if (await command.ExecuteScalarAsync(timeout.Token) is true) break;
            await Task.Delay(10, timeout.Token);
        }
        Assert.False(pending.IsCompleted);
        await firstWork.CommitAsync();
        Assert.Equal(user.Id, (await pending.WaitAsync(timeout.Token))!.Id);
        await secondWork.RollbackAsync();
    }

    internal static User NewAdmin()
    {
        var role = new Role { Id = Guid.NewGuid(), Name = RoleType.Admin };
        var user = new User { Id = Guid.NewGuid(), Username = "admin", Email = "admin@test.vn", FullName = "Admin" };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Admin@123");
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        return user;
    }
}
