using TechStore.Api.Identity.Entities;

namespace TechStore.Api.Identity.Repositories;

public interface IStoreSettingRepository
{
    Task<StoreSetting?> GetCurrentSettingsAsync(bool asNoTracking = false, CancellationToken cancellationToken = default);
    void Add(StoreSetting setting);
}
