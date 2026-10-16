using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;

namespace TechStore.Api.Identity.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
    {
        var target = normalizedUsername.ToLowerInvariant();
        return await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == target, cancellationToken);
    }

    public async Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public void Add(User user)
    {
        _dbContext.Users.Add(user);
    }
}
