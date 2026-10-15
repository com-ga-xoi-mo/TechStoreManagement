using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Identity;

public sealed class StoreSettingsRepository : IStoreSettingsRepository
{
    private readonly IApiClient _apiClient;

    public StoreSettingsRepository(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<StoreSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetAsync<StoreSettingsDto>("api/settings/store", cancellationToken);
    }

    public async Task<StoreSettingsDto> UpdateSettingsAsync(UpdateStoreSettingsRequest request, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PutAsync<UpdateStoreSettingsRequest, StoreSettingsDto>("api/settings/store", request, cancellationToken);
    }
}
