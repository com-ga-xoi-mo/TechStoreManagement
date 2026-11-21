using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Common.Data;

internal interface IDemoSeedRepository
{
    Task AcquireSeedLockAsync(CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, Product>> ReconcileAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductSeed>> FindProductsMissingCoverAsync(
        IReadOnlyDictionary<string, Product> products, CancellationToken cancellationToken);
    void StageCover(Product product, ProductImage image);
}
