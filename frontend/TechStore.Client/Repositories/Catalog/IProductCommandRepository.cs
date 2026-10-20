using TechStore.Client.Models.Catalog;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Catalog;

public interface IProductCommandRepository
{
    /// <summary>POST api/products. 400/409 reach the caller as ApiException.</summary>
    Task<ProductDetail> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>PUT api/products/{id}. 400/404/409 reach the caller as ApiException.</summary>
    Task<ProductDetail> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>PATCH api/products/{id}/status.</summary>
    Task<ProductDetail> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>DELETE api/products/{id}; 204 is success.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
