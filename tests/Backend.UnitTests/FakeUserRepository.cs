using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Repositories;

namespace TechStore.UnitTests;

public class FakeUserRepository : IUserRepository
{
    public Dictionary<Guid, User> Users { get; } = new();
    public List<User> AddedUsers { get; } = [];

    public Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
    {
        var target = normalizedUsername.ToLowerInvariant();
        var user = Users.Values.FirstOrDefault(u => u.Username.ToLower() == target);
        return Task.FromResult(user);
    }

    public Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Users.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    public void Add(User user)
    {
        Users[user.Id] = user;
        AddedUsers.Add(user);
    }
}
