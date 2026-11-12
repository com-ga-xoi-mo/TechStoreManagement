using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Repositories;
using TechStore.Api.Identity.Services;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

internal static class TestJwtAuthentication
{
    public static IServiceCollection AddStoreJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddStoreAuthentication(configuration);
        services.AddTestIdentities();
        return services;
    }

    public static void AddTestIdentities(this IServiceCollection services)
    {
        services.RemoveAll<IUserRepository>();
        services.AddSingleton<FakeUserRepository>();
        services.AddSingleton<IUserRepository>(sp => sp.GetRequiredService<FakeUserRepository>());
        services.RemoveAll<IRefreshTokenRepository>();
        services.AddSingleton<IRefreshTokenRepository, FakeRefreshTokenRepository>();
        services.RemoveAll<IUnitOfWork>();
        services.AddScoped<IUnitOfWork, FakeUnitOfWork>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IAuthService, AuthService>();
    }

    public static Guid AddIdentity(IServiceProvider services, string role, Guid? id = null)
    {
        var repository = services.GetRequiredService<FakeUserRepository>();
        var userId = id ?? Guid.NewGuid();
        if (repository.Users.ContainsKey(userId)) return userId;
        var assignedRole = new Role { Id = Guid.NewGuid(), Name = Enum.Parse<RoleType>(role) };
        var user = new User { Id = userId, Username = "http-" + userId, Email = userId + "@test.vn", FullName = role };
        user.UserRoles.Add(new UserRole { UserId = userId, RoleId = assignedRole.Id, Role = assignedRole });
        repository.Add(user);
        return userId;
    }
}
