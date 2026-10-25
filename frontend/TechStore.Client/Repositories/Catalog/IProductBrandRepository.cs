using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public interface IProductBrandRepository
{
    /// <summary>GET api/products/brands: every brand in use with its product count.</summary>
    Task<IReadOnlyList<BrandOption>> GetBrandsAsync(CancellationToken cancellationToken = default);
}
