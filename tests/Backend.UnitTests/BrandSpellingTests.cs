using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class BrandSpellingTests
{
    private static (FakeProductRepository Products, ProductService Service, Category Category) CreateContext(params string[] existingBrands)
    {
        var products = new FakeProductRepository();
        var categories = new FakeCategoryRepository();
        var category = ProductCommandTestData.NewActiveCategory();
        categories.Categories[category.Id] = category;
        var i = 0;
        foreach (var brand in existingBrands)
        {
            var product = ProductCommandTestData.NewProduct(category.Id, sku: $"EXIST-{i++}");
            product.Brand = brand;
            products.Products[product.Id] = product;
        }
        return (products, new ProductService(products, categories, new FakeUnitOfWork(), new FakeSpecDefinitionRepository()), category);
    }

    private static CreateProductRequest NewRequest(Guid categoryId, string brand) => new()
    {
        CategoryId = categoryId,
        Name = "iPhone 16",
        Sku = "IP16-NEW",
        Brand = brand,
        BasePrice = 20000000m,
        CostPrice = 18000000m
    };

    [Theory]
    [InlineData("apple", "Apple")]
    [InlineData("  APPLE ", "Apple")]
    [InlineData(" Razer ", "Razer")]
    public async Task CreateAsync_ReusesTheExistingSpelling(string typed, string stored)
    {
        // Arrange
        var (products, service, category) = CreateContext("Apple", "Apple", "Logitech");

        // Act
        var result = await service.CreateAsync(NewRequest(category.Id, typed));

        // Assert
        result.IsSuccess.Should().BeTrue();
        products.AddedProducts.Single().Brand.Should().Be(stored);
        result.Data!.Brand.Should().Be(stored);
    }

    [Fact]
    public async Task CreateAsync_SeveralSpellings_TheMostUsedWins()
    {
        // Arrange
        var (products, service, category) = CreateContext("apple", "Apple", "Apple");

        // Act
        await service.CreateAsync(NewRequest(category.Id, "APPLE"));

        // Assert
        products.AddedProducts.Single().Brand.Should().Be("Apple");
    }

    [Fact]
    public async Task UpdateAsync_OnlyProductOfABrand_CanChangeItsLetterCase()
    {
        // Arrange
        var (products, service, category) = CreateContext("Logitech");
        var product = ProductCommandTestData.NewProduct(category.Id);
        product.Brand = "apple";
        products.Products[product.Id] = product;
        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Brand = "Apple";

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        product.Brand.Should().Be("Apple");
    }

    [Fact]
    public async Task UpdateAsync_OtherProductsSpelling_IsReused()
    {
        // Arrange
        var (products, service, category) = CreateContext("Apple", "Apple");
        var product = ProductCommandTestData.NewProduct(category.Id);
        product.Brand = "apple";
        products.Products[product.Id] = product;
        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Brand = "APPLE";

        // Act
        await service.UpdateAsync(product.Id, request);

        // Assert
        product.Brand.Should().Be("Apple");
    }

    [Fact]
    public async Task GetBrandsAsync_MergesSpellingsWithTheirCounts()
    {
        // Arrange
        var (_, service, _) = CreateContext("Apple", "Apple", "apple", "logitech");

        // Act
        var brands = await service.GetBrandsAsync();

        // Assert
        brands.Select(b => (b.Name, b.ProductCount)).Should().Equal(("Apple", 3), ("logitech", 1));
    }
}
