using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public interface IProductImageRepository
{
    /// <summary>POST api/products/{id}/images; HTTP 400/401/403/404/409/503 reach the caller as ApiException.</summary>
    Task<ProductImage> UploadAsync(Guid productId, ImageUpload upload, CancellationToken cancellationToken = default);
    /// <summary>DELETE api/products/{id}/images/{imageId}; HTTP 401/403/404 reach the caller as ApiException.</summary>
    Task DeleteAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default);
    /// <summary>PUT api/products/{id}/images/layout; HTTP 400/401/403/404 reach the caller as ApiException.</summary>
    Task<IReadOnlyList<ProductImage>> SaveLayoutAsync(Guid productId, IReadOnlyList<ImageLayoutEntry> entries, CancellationToken cancellationToken = default);
}
