using FluentAssertions;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class ProductServiceStatusTests
{
    private readonly FakeProductRepository _productRepo = new();
    private readonly FakeCategoryRepository _categoryRepo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ProductService _service;

    public ProductServiceStatusTests()
    {
        _service = new ProductService(_productRepo, _categoryRepo, _unitOfWork, new FakeSpecDefinitionRepository());
    }

    [Fact]
    public async Task SetStatusAsync_Deactivate_SetsInactiveSavesOnceAndCommits()
    {
        // Arrange
        var category = ProductCommandTestData.NewActiveCategory();
        _categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id, isActive: true);
        _productRepo.Products[product.Id] = product;

        var request = new UpdateProductStatusRequest { IsActive = false };

        // Act
        var result = await _service.SetStatusAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        product.IsActive.Should().BeFalse();
        product.UpdatedAt.Should().NotBeNull();
        _unitOfWork.SaveChangesCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);
        _unitOfWork.RollbackCount.Should().Be(0);
        result.Data!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SetStatusAsync_Reactivate_KeepsVariantFlags()
    {
        // Arrange
        var category = ProductCommandTestData.NewActiveCategory();
        _categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id, isActive: false);
        var activeVariant = ProductCommandTestData.NewVariant(product, "VAR-ACT", isActive: true);
        var inactiveVariant = ProductCommandTestData.NewVariant(product, "VAR-INACT", isActive: false);
        _productRepo.Products[product.Id] = product;

        var request = new UpdateProductStatusRequest { IsActive = true };

        // Act
        var result = await _service.SetStatusAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        product.IsActive.Should().BeTrue();
        activeVariant.IsActive.Should().BeTrue();
        inactiveVariant.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SetStatusAsync_SameValue_ReturnsSuccessWithoutSaving()
    {
        // Arrange
        var category = ProductCommandTestData.NewActiveCategory();
        _categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id, isActive: true);
        _productRepo.Products[product.Id] = product;

        var request = new UpdateProductStatusRequest { IsActive = true };

        // Act
        var result = await _service.SetStatusAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        _unitOfWork.SaveChangesCount.Should().Be(0);
        product.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task SetStatusAsync_MissingIsActive_ReturnsValidationErrorWithoutTransaction()
    {
        // Arrange
        var request = new UpdateProductStatusRequest { IsActive = null };

        // Act
        var result = await _service.SetStatusAsync(Guid.NewGuid(), request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("isActive");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task SetStatusAsync_UnknownId_ReturnsNotFoundAndRollsBack()
    {
        // Arrange
        var request = new UpdateProductStatusRequest { IsActive = true };

        // Act
        var result = await _service.SetStatusAsync(Guid.NewGuid(), request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.NotFound);
        _unitOfWork.RollbackCount.Should().Be(1);
    }
}
