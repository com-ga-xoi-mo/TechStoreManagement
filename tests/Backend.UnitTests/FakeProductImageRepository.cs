using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;

namespace TechStore.UnitTests;

public sealed class FakeProductImageRepository : IProductImageRepository
{
    public List<ProductImage> Added { get; } = [];
    public List<ProductImage> Removed { get; } = [];
    public void Add(ProductImage image) => Added.Add(image);
    public void Remove(ProductImage image) => Removed.Add(image);
}
