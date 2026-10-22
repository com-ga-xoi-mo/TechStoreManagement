using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Repositories;

/// <summary>Stages changes only; the service saves through IUnitOfWork.</summary>
public interface IProductImageRepository
{
    void Add(ProductImage image);
    void Remove(ProductImage image);
}
