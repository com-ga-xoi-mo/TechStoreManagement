using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Configurations;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Repositories;
using TechStore.Api.Identity.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed class IdentityConcurrencyPostgresTests
{
    private static readonly IOptions<JwtOptions> Options = Microsoft.Extensions.Options.Options.Create(new JwtOptions());

    [PostgresFact]
    public async Task RefreshAsync_TwoIndependentTransactions_ConsumeTokenExactlyOnce()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var user = await SeedAsync(database);
        var login = await LoginAsync(database);
        var barrier = new LockBarrier();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        async Task<TechStore.Shared.DTOs.LoginResponseDto?> Refresh()
        {
            await using var db = Context(database, barrier);
            return await Auth(db).RefreshAsync(new() { RefreshToken = login.RefreshToken }, timeout.Token);
        }
        var responses = await Task.WhenAll(Refresh(), Refresh());
        var winner = Assert.Single(responses, response => response is not null)!;
        await using var verify = database.CreateContext();
        Assert.Equal(1, await verify.RefreshTokens.CountAsync(t => !t.IsRevoked));
        Assert.Null(await Auth(verify).RefreshAsync(new() { RefreshToken = login.RefreshToken }));
        Assert.NotNull(await Auth(verify).RefreshAsync(new() { RefreshToken = winner.RefreshToken }));
    }

    [InlineData(false)]
    [InlineData(true)]
    [PostgresIdentityTheory]
    public async Task Administration_TwoLastAdminsChangedConcurrently_PreservesUsableAdmin(bool lockAccount)
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var first = await SeedAsync(database);
        Guid secondId;
        await using (var db = database.CreateContext())
        {
            var second = new User { Username = "second-admin", Email = "second@test.vn", PasswordHash = first.PasswordHash, FullName = "Second" };
            var role = await db.Roles.SingleAsync(r => r.Name == RoleType.Admin);
            second.UserRoles.Add(new UserRole { UserId = second.Id, RoleId = role.Id });
            db.Users.Add(second);
            await db.SaveChangesAsync();
            secondId = second.Id;
        }
        var barrier = new LockBarrier();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        async Task<UserOperationResult<TechStore.Shared.DTOs.UserAccountDto>> Change(Guid id)
        {
            await using var db = Context(database, barrier);
            var service = Management(db);
            var actor = new AuthenticatedUser(id, RoleType.Admin, 0);
            return lockAccount ? await service.SetStatusAsync(id, new() { IsActive = false }, actor, timeout.Token)
                : await service.SetRoleAsync(id, new() { Role = "SalesStaff" }, actor, timeout.Token);
        }
        var results = await Task.WhenAll(Change(first.Id), Change(secondId));
        Assert.Single(results, r => r.StatusCode == 200);
        Assert.Single(results, r => r.StatusCode == 409);
        await using var verify = database.CreateContext();
        Assert.Equal(1, await new UserRepository(verify).CountActiveAdminsAsync());
    }

    [PostgresFact]
    public async Task CreateAsync_ConcurrentNormalizedDuplicate_RollsBackLosingUserAndRole()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var admin = await SeedAsync(database);
        var barrier = new LockBarrier();
        async Task<UserOperationResult<TechStore.Shared.DTOs.UserAccountDto>> Create(string username, string email)
        {
            await using var db = Context(database, barrier);
            return await Management(db).CreateAsync(new() { Username = username, Email = email, FullName = "Employee", Password = "Password@123", Role = "SalesStaff" },
                new(admin.Id, RoleType.Admin, 0));
        }
        var results = await Task.WhenAll(Create("Employee", "one@test.vn"), Create(" EMPLOYEE ", "two@test.vn"));
        Assert.Single(results, r => r.StatusCode == 201);
        Assert.Single(results, r => r.StatusCode == 409);
        await using var verify = database.CreateContext();
        Assert.Equal(2, await verify.Users.CountAsync());
        Assert.Equal(2, await verify.UserRoles.CountAsync());
    }

    [PostgresFact]
    public async Task RefreshAsync_RacesAccountLock_NoUsableTokenSurvivesLockCommit()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var admin = await SeedAsync(database);
        Guid employeeId;
        await using (var db = database.CreateContext())
        {
            var result = await Management(db).CreateAsync(new() { Username = "employee", Email = "employee@test.vn", FullName = "Employee", Password = "Password@123", Role = "SalesStaff" }, new(admin.Id, RoleType.Admin, 0));
            employeeId = result.Data!.Id;
        }
        TechStore.Shared.DTOs.LoginResponseDto login;
        await using (var db = database.CreateContext()) login = (await Auth(db).LoginAsync(new() { Username = "employee", Password = "Password@123" }))!;
        var barrier = new LockBarrier();
        await using var refreshDb = Context(database, barrier);
        await using var lockDb = Context(database, barrier);
        var refreshTask = Auth(refreshDb).RefreshAsync(new() { RefreshToken = login.RefreshToken });
        var lockTask = Management(lockDb).SetStatusAsync(employeeId, new() { IsActive = false }, new(admin.Id, RoleType.Admin, 0));
        await Task.WhenAll(refreshTask, lockTask).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(200, (await lockTask).StatusCode);
        await using var verify = database.CreateContext();
        Assert.False(await Auth(verify).ValidateIdentityAsync(new(employeeId, RoleType.SalesStaff, 0)));
        Assert.Equal(0, await verify.RefreshTokens.CountAsync(t => t.UserId == employeeId && !t.IsRevoked));
        if (await refreshTask is { } pair)
            Assert.Null(await Auth(verify).RefreshAsync(new() { RefreshToken = pair.RefreshToken }));
    }

    [PostgresFact]
    public async Task RefreshAsync_SaveFails_RollsBackConsumedAndReplacementTokens()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await SeedAsync(database);
        var login = await LoginAsync(database);
        await using (var db = database.CreateContext())
        {
            var work = new FailingSaveUnitOfWork(new UnitOfWork(db));
            var service = new AuthService(new UserRepository(db), new RefreshTokenRepository(db), work, Options, TimeProvider.System);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshAsync(new() { RefreshToken = login.RefreshToken }));
            Assert.Empty(db.ChangeTracker.Entries());
        }
        await using var verify = database.CreateContext();
        Assert.Single(await verify.RefreshTokens.ToListAsync());
        Assert.NotNull(await Auth(verify).RefreshAsync(new() { RefreshToken = login.RefreshToken }));
    }

    [PostgresFact]
    public async Task Administration_ActorRevokedWhileWaiting_RechecksVersionAfterLock()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var admin = await SeedAsync(database);
        await using var first = database.CreateContext();
        var repository = new UserRepository(first);
        var work = new UnitOfWork(first);
        await work.BeginTransactionAsync();
        await repository.LockAdministrationAsync();
        var locked = (await repository.GetForUpdateAsync(admin.Id))!;
        locked.TokenVersion++;
        await work.SaveChangesAsync();
        await using var second = database.CreateContext();
        await second.Database.OpenConnectionAsync();
        var pid = ((Npgsql.NpgsqlConnection)second.Database.GetDbConnection()).ProcessID;
        var pending = Management(second).CreateAsync(new() { Username = "late-employee", FullName = "Employee", Email = "late@test.vn", Password = "Password@123", Role = "SalesStaff" }, new(admin.Id, RoleType.Admin, 0));
        await using var observer = new Npgsql.NpgsqlConnection(database.ConnectionString);
        await observer.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var command = new Npgsql.NpgsqlCommand("SELECT wait_event_type='Lock' FROM pg_stat_activity WHERE pid=@pid", observer);
            command.Parameters.AddWithValue("pid", pid);
            if (await command.ExecuteScalarAsync(timeout.Token) is true) break;
            await Task.Delay(10, timeout.Token);
        }
        Assert.False(pending.IsCompleted);
        await work.CommitAsync();
        Assert.Equal(401, (await pending.WaitAsync(timeout.Token)).StatusCode);
        await using var verify = database.CreateContext();
        Assert.Single(await verify.Users.ToListAsync());
    }

    private static AuthService Auth(AppDbContext db) => new(new UserRepository(db), new RefreshTokenRepository(db), new UnitOfWork(db), Options, TimeProvider.System);
    private static UserManagementService Management(AppDbContext db) => new(new UserRepository(db), new RefreshTokenRepository(db), new UnitOfWork(db), TimeProvider.System);
    private static AppDbContext Context(PostgresDatabase database, LockBarrier barrier) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(database.ConnectionString).AddInterceptors(new FirstLockInterceptor(barrier)).Options);

    private static async Task<User> SeedAsync(PostgresDatabase database)
    {
        await using var db = database.CreateContext();
        var admin = IdentityMigrationPostgresTests.NewAdmin();
        db.Users.Add(admin);
        foreach (var role in Enum.GetValues<RoleType>().Where(role => role != RoleType.Admin)) db.Roles.Add(new Role { Name = role });
        await db.SaveChangesAsync();
        return admin;
    }

    private static async Task<TechStore.Shared.DTOs.LoginResponseDto> LoginAsync(PostgresDatabase database)
    {
        await using var db = database.CreateContext();
        return (await Auth(db).LoginAsync(new() { Username = "admin", Password = "Admin@123" }))!;
    }

    private sealed class LockBarrier
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;
        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrived) == 2) _ready.SetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private sealed class FirstLockInterceptor(LockBarrier barrier) : DbCommandInterceptor
    {
        private bool _arrived;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!_arrived && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                _arrived = true;
                await barrier.ArriveAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class FailingSaveUnitOfWork(UnitOfWork inner) : IUnitOfWork
    {
        public Task BeginTransactionAsync(CancellationToken ct = default) => inner.BeginTransactionAsync(ct);
        public Task BeginTransactionAsync(System.Data.IsolationLevel level, CancellationToken ct = default) => inner.BeginTransactionAsync(level, ct);
        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            await inner.SaveChangesAsync(ct);
            throw new InvalidOperationException("Injected save failure");
        }
        public Task CommitAsync(CancellationToken ct = default) => inner.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => inner.RollbackAsync(ct);
    }
}

public sealed class PostgresIdentityTheoryAttribute : TheoryAttribute
{
    public PostgresIdentityTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TECHSTORE_TEST_CONNECTION")))
            Skip = "Set TECHSTORE_TEST_CONNECTION to run concurrent PostgreSQL tests.";
    }
}
