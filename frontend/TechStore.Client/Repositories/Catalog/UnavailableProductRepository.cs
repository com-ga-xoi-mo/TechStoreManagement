using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public sealed class UnavailableProductRepository : IProductRepository
{
    public Task<PagedResult<ProductSummary>> SearchProductsAsync(ProductListCriteria criteria, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new System.InvalidOperationException("Product API integration is not configured. Enable explicit preview mode to use sample data.");
    }

    public Task<ProductDetail?> GetProductDetailAsync(string productId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new System.InvalidOperationException("Product detail API integration is not configured. Enable explicit preview mode to use sample data.");
    }

    public Task<ProductCreateResult> CreateProductAsync(ProductCreateDraft draft, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new System.InvalidOperationException("Product create API integration is not configured. Enable explicit preview mode to use sample data.");
    }

    public Task<ProductEditData?> GetProductForEditAsync(ProductEditQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new System.InvalidOperationException("Product edit API integration is not configured. Enable explicit preview mode to use sample data.");
    }

    public Task<ProductEditResult> UpdateProductAsync(ProductEditDraft draft, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new System.InvalidOperationException("Product edit API integration is not configured. Enable explicit preview mode to use sample data.");
    }
}
