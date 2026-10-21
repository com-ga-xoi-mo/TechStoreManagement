namespace TechStore.Api.Common.Data;

internal sealed class DataSeeder(IDemoSeedRepository repository, IUnitOfWork unitOfWork) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await repository.AcquireSeedLockAsync(cancellationToken);
            await repository.ReconcileAsync(cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
