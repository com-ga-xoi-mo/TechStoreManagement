using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Repositories;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public class FakeUserRepository : IUserRepository
{
    public Dictionary<Guid, User> Users { get; } = new();
    public List<User> AddedUsers { get; } = [];

    public Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
    {
        var target = normalizedUsername.ToLowerInvariant();
        var user = Users.Values.FirstOrDefault(u => u.Username.Trim().ToLowerInvariant() == target);
        return Task.FromResult(user);
    }

    public Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Users.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        GetByIdWithRolesAsync(userId, cancellationToken);

    public Task LockAdministrationAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<Role> GetRoleForAssignmentAsync(RoleType role, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Role { Id = Guid.NewGuid(), Name = role });

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.Count(u => u.IsActive && u.UserRoles.Count == 1 && u.UserRoles.Single().Role.Name == RoleType.Admin));

    public Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(UserSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = Users.Values.AsEnumerable();
        if (criteria.Keyword is { } text) query = query.Where(u => new[] { u.Username, u.FullName, u.Email }.Any(x => x.Contains(text, StringComparison.OrdinalIgnoreCase)));
        if (criteria.Role is { } role) query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == role));
        if (criteria.IsActive is { } active) query = query.Where(u => u.IsActive == active);
        var rows = query.OrderBy(u => u.Username).ThenBy(u => u.Id).ToList();
        return Task.FromResult<(IReadOnlyList<User>, int)>((rows.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToList(), rows.Count));
    }

    public void ReplaceRole(User user, Role role, DateTime assignedAt)
    {
        user.UserRoles.Clear();
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role, AssignedAt = assignedAt });
    }

    public void Add(User user)
    {
        Users[user.Id] = user;
        AddedUsers.Add(user);
    }
}
