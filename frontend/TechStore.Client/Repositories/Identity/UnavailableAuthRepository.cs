using TechStore.Client.Models.Identity;

namespace TechStore.Client.Repositories.Identity;

/// <summary>
/// Fails closed until a verified backend auth contract is available.
/// </summary>
public sealed class UnavailableAuthRepository : IAuthRepository
{
    public Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new LoginResult(
            LoginResultStatus.NetworkError,
            "Đăng nhập chưa khả dụng vì Auth API chưa được tích hợp.",
            null));
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
