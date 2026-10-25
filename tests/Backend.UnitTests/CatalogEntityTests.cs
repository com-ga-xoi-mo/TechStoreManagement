using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using Xunit;

namespace TechStore.UnitTests;

public class CatalogEntityTests
{
    [Fact]
    public void ProductImage_Model_HasCompositeVariantForeignKey()
    {
        // Arrange
        using var context = CreateDbContext();
        var entity = context.Model.FindEntityType(typeof(ProductImage))!;
        // Act
        var foreignKey = entity.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(ProductVariant));
        // Assert
        foreignKey.Properties.Select(p => p.Name).Should().Equal("VariantId", "ProductId");
        foreignKey.PrincipalKey.Properties.Select(p => p.Name).Should().Equal("Id", "ProductId");
        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void ProductImage_Model_HasRequiredIndexes()
    {
        // Arrange
        using var context = CreateDbContext();
        // Act
        var indexes = context.Model.FindEntityType(typeof(ProductImage))!.GetIndexes().ToList();
        // Assert
        indexes.Should().HaveCount(3);
        indexes.Single(i => i.GetDatabaseName() == "idx_product_images_product_id").Properties.Select(p => p.Name)
            .Should().Equal("ProductId", "SortOrder");
        indexes.Single(i => i.GetDatabaseName() == "idx_product_images_variant_id").Properties.Select(p => p.Name)
            .Should().Equal("VariantId", "ProductId");
        indexes.Single(i => i.GetDatabaseName() == "uq_product_images_object_key").IsUnique.Should().BeTrue();
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy_test;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void CatalogEntities_Should_Initialize_With_Expected_Defaults()
    {
        var category = new Category { Name = "Smartphones", Slug = "smartphones" };
        category.Id.Should().NotBeEmpty();
        category.IsActive.Should().BeTrue();
        category.SubCategories.Should().NotBeNull();
        category.Products.Should().NotBeNull();

        var product = new Product
        {
            CategoryId = category.Id,
            Name = "iPhone 15 Pro Max",
            Sku = "IP15PM-256",
            Brand = "Apple",
            BasePrice = 30000000m,
            CostPrice = 25000000m
        };
        product.Id.Should().NotBeEmpty();
        product.Specs.Should().Be("{}");
        product.IsSerialTracked.Should().BeTrue();
        product.IsActive.Should().BeTrue();
        product.Variants.Should().NotBeNull();

        var variant = new ProductVariant
        {
            ProductId = product.Id,
            VariantName = "Natural Titanium 256GB",
            Sku = "IP15PM-256-NT",
            Price = 30000000m
        };
        variant.Id.Should().NotBeEmpty();
        variant.Specs.Should().Be("{}");
        variant.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CatalogModel_Should_Configure_Tables_Indexes_And_CompositeKey()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        var categoryEntity = model.FindEntityType(typeof(Category));
        categoryEntity.Should().NotBeNull();
        categoryEntity!.GetTableName().Should().Be("categories");

        var productEntity = model.FindEntityType(typeof(Product));
        productEntity.Should().NotBeNull();
        productEntity!.GetTableName().Should().Be("products");

        var variantEntity = model.FindEntityType(typeof(ProductVariant));
        variantEntity.Should().NotBeNull();
        variantEntity!.GetTableName().Should().Be("product_variants");

        // Verify composite unique key on product_variants (Id, ProductId)
        var compositeIndex = variantEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "uq_product_variants_id_product");
        compositeIndex.Should().NotBeNull();
        compositeIndex!.IsUnique.Should().BeTrue();
    }
}
