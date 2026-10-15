using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Identity.Services;

public interface IStoreSettingsService
{
    Task<StoreSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<StoreSettingsDto> UpdateSettingsAsync(UpdateStoreSettingsRequest request, CancellationToken cancellationToken = default);
}
