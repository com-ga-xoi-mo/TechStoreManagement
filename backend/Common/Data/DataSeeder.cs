using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Storage;

namespace TechStore.Api.Common.Data;

internal sealed class DataSeeder(IDemoSeedRepository repository, IUnitOfWork unitOfWork, IObjectStorage storage) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var attemptedKeys = new List<string>();
        var commitStarted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await repository.AcquireSeedLockAsync(cancellationToken);
            var products = await repository.ReconcileAsync(cancellationToken);
            var missing = await repository.FindProductsMissingCoverAsync(products, cancellationToken);
            foreach (var seed in missing)
            {
                var product = products[seed.Sku];
                var bytes = await ReadCoverAsync(seed, cancellationToken);
                var file = ProductImageFileInspector.Inspect(bytes, out var error);
                if (file?.ContentType != "image/png")
                    throw new InvalidOperationException($"Invalid demo cover for {seed.Sku}: {error ?? "PNG required."}");

                var key = $"products/{product.Id:N}/{Guid.NewGuid():N}{file.Extension}";
                attemptedKeys.Add(key);
                var url = await storage.PutAsync(key, bytes, file.ContentType, cancellationToken);
                repository.StageCover(product, new ProductImage
                {
                    Id = DemoSeedManifest.Id($"image:{seed.Sku}"),
                    ProductId = product.Id,
                    ObjectKey = key,
                    Url = url,
                    ContentType = file.ContentType,
                    SizeBytes = bytes.Length,
                    SortOrder = 0,
                    CreatedAt = DemoSeedManifest.AnchorUtc
                });
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            commitStarted = true;
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            // A failed commit acknowledgement does not prove the database rolled back.
            if (!commitStarted)
            {
                foreach (var key in attemptedKeys)
                    await storage.TryDeleteAsync(key, CancellationToken.None);
            }
            throw;
        }
    }

    private static async Task<byte[]> ReadCoverAsync(ProductSeed seed, CancellationToken cancellationToken)
    {
        var name = DemoSeedManifest.CoverResourcesByCategory.GetValueOrDefault(seed.CategorySlug);
        using var stream = name is null ? null : typeof(DemoSeedManifest).Assembly.GetManifestResourceStream(name);
        if (stream is null)
            throw new InvalidOperationException($"Missing demo cover resource for {seed.Sku} ({seed.CategorySlug}).");
        if (stream.Length > ProductImageFileInspector.MaxBytes)
            throw new InvalidOperationException($"Demo cover for {seed.Sku} exceeds the catalog image size limit.");

        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }
}
