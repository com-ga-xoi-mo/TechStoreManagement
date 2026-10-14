using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public interface IProductRepository
{
    Task<ProductPage> SearchAsync(ProductQuery query, CancellationToken cancellationToken = default);
    Task<ProductDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
