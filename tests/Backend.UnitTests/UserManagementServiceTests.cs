using Microsoft.AspNetCore.Identity;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed class UserManagementServiceTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakeRefreshTokenRepository _tokens = new();
    private readonly FakeUnitOfWork _work = new();
    private readonly UserManagementService _service;
    private readonly AuthenticatedUser _actor;

    public UserManagementServiceTests()
    {
        var user = IdentityMigrationPostgresTests.NewAdmin();
        _users.Add(user);
        _actor = new(user.Id, RoleType.Admin, 0);
        _service = new(_users, _tokens, _work, TimeProvider.System);
    }

    [Fact]
    public async Task CreateAsync_InvalidFields_ReportsTogetherWithoutTransaction()
    {
        var result = await _service.CreateAsync(new CreateUserRequest(), _actor);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(5, result.Errors!.Count);
        Assert.Equal(0, _work.BeginTransactionCount);
    }

    [Theory]
    [InlineData("1", 8)]
    [InlineData("Unknown", 128)]
    [InlineData("Admin", 7)]
    [InlineData("Admin", 129)]
    public async Task CreateAsync_InvalidRoleOrPasswordBoundary_IsRejected(string role, int passwordLength)
    {
        var request = ValidRequest();
        request.Role = role;
        request.Password = new string('a', passwordLength);
        Assert.Equal(400, (await _service.CreateAsync(request, _actor)).StatusCode);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(128)]
    public async Task CreateAsync_ValidPasswordBoundary_HashesWithoutTrimming(int length)
    {
        var request = ValidRequest();
        request.Password = " " + new string('a', length - 2) + " ";
        var result = await _service.CreateAsync(request, _actor);
        Assert.Equal(201, result.StatusCode);
        var user = _users.Users[result.Data!.Id];
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.Password));
    }

    [Fact]
    public async Task CreateAsync_DuplicateAtSave_RollsBackWithoutCommit()
    {
        _work.ExceptionToThrowOnSave = new DuplicateKeyException("idx_users_normalized_username");
        var result = await _service.CreateAsync(ValidRequest(), _actor);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(1, _work.RollbackCount);
        Assert.Equal(0, _work.CommitCount);
    }

    [Fact]
    public async Task StatusAndRole_NoOp_KeepTokenVersionAndTimestamp()
    {
        var user = _users.Users[_actor.Id];
        var timestamp = user.UpdatedAt;
        Assert.Equal(200, (await _service.SetStatusAsync(user.Id, new() { IsActive = true }, _actor)).StatusCode);
        Assert.Equal(200, (await _service.SetRoleAsync(user.Id, new() { Role = "Admin" }, _actor)).StatusCode);
        Assert.Equal(0, user.TokenVersion);
        Assert.Equal(timestamp, user.UpdatedAt);
    }

    [Fact]
    public async Task LastAdmin_RejectionsKeepIdentityAndTokens()
    {
        var user = _users.Users[_actor.Id];
        var hash = user.PasswordHash;
        Assert.Equal(409, (await _service.SetStatusAsync(user.Id, new() { IsActive = false }, _actor)).StatusCode);
        Assert.Equal(409, (await _service.SetRoleAsync(user.Id, new() { Role = "SalesStaff" }, _actor)).StatusCode);
        Assert.True(user.IsActive);
        Assert.Equal(RoleType.Admin, AuthenticatedUser.SoleRole(user));
        Assert.Equal(hash, user.PasswordHash);
        Assert.Equal(0, user.TokenVersion);
        Assert.Equal(0, _work.SaveChangesCount);
    }

    [Fact]
    public async Task ActorRevokedWhileWaiting_IsRejectedBeforeMutation()
    {
        _users.Users[_actor.Id].TokenVersion = 1;
        var result = await _service.CreateAsync(ValidRequest(), _actor);
        Assert.Equal(401, result.StatusCode);
        Assert.Single(_users.Users);
        Assert.Equal(0, _work.SaveChangesCount);
    }

    [Fact]
    public async Task SearchAsync_CombinedFiltersAndUnknownRole_AreHandled()
    {
        await _service.CreateAsync(ValidRequest(), _actor);
        var result = await _service.SearchAsync(new() { Q = "nhân", Role = "salesstaff", IsActive = true });
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.Equal("SalesStaff", Assert.Single(result.Data.Items).Role);
        Assert.Equal(400, (await _service.SearchAsync(new() { Role = "2" })).StatusCode);
        Assert.Equal(404, (await _service.GetByIdAsync(Guid.NewGuid())).StatusCode);
    }

    private static CreateUserRequest ValidRequest() => new()
    {
        Username = "employee", FullName = "Nhân viên", Email = "employee@test.vn", Password = "Employee@123", Role = "SalesStaff"
    };
}
