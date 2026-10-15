using TechStore.Api.Identity.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Identity.Services;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(Guid userId, LogoutRequest request, CancellationToken cancellationToken = default);
}
