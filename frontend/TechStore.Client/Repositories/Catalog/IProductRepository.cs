using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public interface IProductRepository
{
    Task<PagedResult<ProductSummary>> SearchProductsAsync(ProductListCriteria criteria, CancellationToken cancellationToken = default);

    Task<ProductDetail?> GetProductDetailAsync(string productId, CancellationToken cancellationToken = default);

    Task<ProductCreateResult> CreateProductAsync(ProductCreateDraft draft, CancellationToken cancellationToken = default);

    Task<ProductEditData?> GetProductForEditAsync(ProductEditQuery query, CancellationToken cancellationToken = default);

    Task<ProductEditResult> UpdateProductAsync(ProductEditDraft draft, CancellationToken cancellationToken = default);
}
