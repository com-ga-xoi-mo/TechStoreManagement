using FluentAssertions;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class ProductSellingStateTests
{
    [Fact]
    public async Task CreateAsync_VariantFlags_DefaultToNotSelling_AndKeepAnExplicitTrue()
    {
        // Arrange
        var products = new FakeProductRepository();
        var categories = new FakeCategoryRepository();
        var category = ProductCommandTestData.NewActiveCategory();
        categories.Categories[category.Id] = category;
        var service = new ProductService(products, categories, new FakeUnitOfWork(), new FakeSpecDefinitionRepository());
        var request = new CreateProductRequest
        {
            CategoryId = category.Id,
            Name = "iPhone 16",
            Sku = "IP16",
            Brand = "Apple",
            BasePrice = 20000000m,
            CostPrice = 17000000m,
            Variants =
            [
                new CreateProductVariantRequest { VariantName = "Đen 128GB", Sku = "IP16-BK", Price = 20000000m },
                new CreateProductVariantRequest { VariantName = "Trắng 256GB", Sku = "IP16-WH", Price = 23000000m, IsActive = true }
            ]
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.IsActive.Should().BeFalse();
        result.Data.Variants.Select(v => (v.Sku, v.IsActive)).Should().BeEquivalentTo(new[] { ("IP16-BK", false), ("IP16-WH", true) });
    }

    [Fact]
    public async Task UpdateAsync_NewVariantWithIsActiveTrue_SellsAtOnce()
    {
        // Arrange
        var products = new FakeProductRepository();
        var categories = new FakeCategoryRepository();
        var category = ProductCommandTestData.NewActiveCategory();
        categories.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        products.Products[product.Id] = product;
        var service = new ProductService(products, categories, new FakeUnitOfWork(), new FakeSpecDefinitionRepository());
        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants = [new UpdateProductVariantRequest { VariantName = "Xanh", Sku = "MBA-M3-BL", Price = 27990000m, IsActive = true }];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        products.AddedVariants.Single().IsActive.Should().BeTrue();
    }
}
