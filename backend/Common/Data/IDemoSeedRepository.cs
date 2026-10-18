namespace TechStore.Api.Common.Data;

internal interface IDemoSeedRepository
{
    Task AcquireSeedLockAsync(CancellationToken cancellationToken);
    Task ReconcileAsync(CancellationToken cancellationToken);
}
