using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Repositories;

namespace TechStore.UnitTests;

public class FakeStoreSettingRepository : IStoreSettingRepository
{
    public StoreSetting? CurrentSetting { get; set; }
    public List<StoreSetting> AddedSettings { get; } = [];

    public Task<StoreSetting?> GetCurrentSettingsAsync(bool asNoTracking = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CurrentSetting);
    }

    public void Add(StoreSetting setting)
    {
        CurrentSetting = setting;
        AddedSettings.Add(setting);
    }
}
