using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Identity;

namespace TechStore.Client.Repositories.Identity;

public sealed class FakeAuthRepository : IAuthRepository
{
    // Hardcoded preview accounts for testing UI states
    // In a real implementation, this goes to ApiClient
    
    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        // Simulate network delay
        await Task.Delay(800, cancellationToken);

        return username.ToLowerInvariant() switch
        {
            "admin" when password == "Admin@123" => new LoginResult(
                LoginResultStatus.Success, 
                null, 
                new SessionSnapshot("Administrator", new HashSet<string> { "catalog:read", "inventory:read", "orders:read", "customers:read" })
            ),
            "locked" => new LoginResult(LoginResultStatus.LockedOut, "Tài khoản đã bị khóa do đăng nhập sai nhiều lần.", null),
            "inactive" => new LoginResult(LoginResultStatus.Inactive, "Tài khoản hiện đang bị vô hiệu hóa.", null),
            "error" => throw new Exception("Simulated unexpected exception"),
            "network" => new LoginResult(LoginResultStatus.NetworkError, "Không thể kết nối đến máy chủ. Vui lòng kiểm tra mạng.", null),
            _ => new LoginResult(LoginResultStatus.WrongCredentials, "Tên đăng nhập hoặc mật khẩu không chính xác.", null)
        };
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
