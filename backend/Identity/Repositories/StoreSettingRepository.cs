using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;

namespace TechStore.Api.Identity.Repositories;

public class StoreSettingRepository : IStoreSettingRepository
{
    private readonly AppDbContext _dbContext;

    public StoreSettingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<StoreSetting?> GetCurrentSettingsAsync(bool asNoTracking = false, CancellationToken cancellationToken = default)
    {
        IQueryable<StoreSetting> query = _dbContext.StoreSettings;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(StoreSetting setting)
    {
        _dbContext.StoreSettings.Add(setting);
    }
}
