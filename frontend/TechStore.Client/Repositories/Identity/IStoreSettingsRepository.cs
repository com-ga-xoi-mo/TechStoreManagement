using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Identity;

public interface IStoreSettingsRepository
{
    Task<StoreSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<StoreSettingsDto> UpdateSettingsAsync(UpdateStoreSettingsRequest request, CancellationToken cancellationToken = default);
}
