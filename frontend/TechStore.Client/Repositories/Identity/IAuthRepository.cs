using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Identity;

namespace TechStore.Client.Repositories.Identity;

public interface IAuthRepository
{
    Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
