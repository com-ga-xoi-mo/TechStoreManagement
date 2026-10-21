using FluentAssertions;
using TechStore.Api.Catalog.Services;
using Xunit;

namespace TechStore.UnitTests;

public class ProductServiceDeleteTests
{
    private readonly FakeProductRepository _productRepo = new();
    private readonly FakeCategoryRepository _categoryRepo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ProductService _service;

    public ProductServiceDeleteTests()
    {
        _service = new ProductService(_productRepo, _categoryRepo, _unitOfWork, new FakeSpecDefinitionRepository());
    }

    [Fact]
    public async Task DeleteAsync_UnusedProduct_RemovesAndCommits()
    {
        // Arrange
        var category = ProductCommandTestData.NewActiveCategory();
        _categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        _productRepo.Products[product.Id] = product;

        // Act
        var result = await _service.DeleteAsync(product.Id);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        result.Data.Should().BeNull();
        _productRepo.RemovedProducts.Should().Contain(product);
        _productRepo.Products.Should().NotContainKey(product.Id);
        _unitOfWork.SaveChangesCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_ProductInUse_ReturnsConflictAndKeepsProduct()
    {
        // Arrange
        var category = ProductCommandTestData.NewActiveCategory();
        _categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        _productRepo.Products[product.Id] = product;
        _productRepo.InUseProductIds.Add(product.Id);

        // Act
        var result = await _service.DeleteAsync(product.Id);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Conflict);
        result.ConflictMessage.Should().Contain("Deactivate");
        _productRepo.RemovedProducts.Should().BeEmpty();
        _productRepo.Products.Should().ContainKey(product.Id);
        _unitOfWork.SaveChangesCount.Should().Be(0);
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await _service.DeleteAsync(Guid.NewGuid());

        // Assert
        result.Status.Should().Be(ProductCommandStatus.NotFound);
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_SaveThrows_RollsBackAndRethrows()
    {
        // Arrange
        var category = ProductCommandTestData.NewActiveCategory();
        _categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        _productRepo.Products[product.Id] = product;
        _unitOfWork.ExceptionToThrowOnSave = new InvalidOperationException("DB error");

        // Act
        var act = () => _service.DeleteAsync(product.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.RollbackCount.Should().Be(1);
    }
}
