using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;

namespace TechStore.Api.Catalog.Repositories;

public sealed class ProductImageRepository(AppDbContext dbContext) : IProductImageRepository
{
    public void Add(ProductImage image) => dbContext.Set<ProductImage>().Add(image);
    public void Remove(ProductImage image) => dbContext.Set<ProductImage>().Remove(image);
}
