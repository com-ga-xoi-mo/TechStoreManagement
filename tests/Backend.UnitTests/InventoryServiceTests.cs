using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class InventoryServiceTests
{
    private readonly FakeInventoryStockRepository _stockRepository = new();
    private readonly FakeProductRepository _productRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InventoryService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public InventoryServiceTests()
    {
        _service = new InventoryService(_stockRepository, _productRepository, _unitOfWork);
    }

    [Fact]
    public async Task GetStocksAsync_Should_Return_Paged_Stocks_And_Apply_Filters()
    {
        // Arrange
        var prod1 = new Product { Id = Guid.NewGuid(), Name = "iPhone 15", Sku = "IP15" };
        var prod2 = new Product { Id = Guid.NewGuid(), Name = "MacBook Pro", Sku = "MBP" };

        _stockRepository.Stocks.Add(new InventoryStock
        {
            ProductId = prod1.Id,
            Product = prod1,
            Quantity = 10,
            MinStockAlert = 5
        });
        _stockRepository.Stocks.Add(new InventoryStock
        {
            ProductId = prod2.Id,
            Product = prod2,
            Quantity = 3,
            MinStockAlert = 5
        });

        var request = new InventoryStockFilterRequest
        {
            ProductId = prod1.Id,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _service.GetStocksAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items[0].ProductId.Should().Be(prod1.Id);
    }

    [Fact]
    public async Task GetStocksAsync_Should_Filter_By_LowStock()
    {
        // Arrange
        var prod1 = new Product { Id = Guid.NewGuid(), Name = "iPhone 15", Sku = "IP15" };
        var prod2 = new Product { Id = Guid.NewGuid(), Name = "MacBook Pro", Sku = "MBP" };

        _stockRepository.Stocks.Add(new InventoryStock
        {
            ProductId = prod1.Id,
            Product = prod1,
            Quantity = 10,
            MinStockAlert = 5
        });
        _stockRepository.Stocks.Add(new InventoryStock
        {
            ProductId = prod2.Id,
            Product = prod2,
            Quantity = 2,
            MinStockAlert = 5
        });

        var request = new InventoryStockFilterRequest
        {
            LowStock = true
        };

        // Act
        var result = await _service.GetStocksAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items[0].ProductId.Should().Be(prod2.Id);
        result.Data.Items[0].IsLowStock.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeStockAsync_Should_Succeed_And_Create_Stock_And_OpeningBalance_Movement()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _productRepository.Products.Add(productId, new Product { Id = productId, Name = "Test Product" });
        var request = new InitializeStockRequest
        {
            ProductId = productId,
            Quantity = 50,
            MinStockAlert = 10,
            Reason = "Initial inventory import"
        };

        // Act
        var result = await _service.InitializeStockAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Quantity.Should().Be(50);
        result.Data.MinStockAlert.Should().Be(10);

        _unitOfWork.BeginTransactionCount.Should().Be(1);
        _unitOfWork.SaveChangesCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);

        _stockRepository.Stocks.Should().HaveCount(1);
        _stockRepository.Movements.Should().HaveCount(1);

        var movement = _stockRepository.Movements[0];
        movement.MovementType.Should().Be(InventoryMovementType.OpeningBalance);
        movement.QuantityChange.Should().Be(50);
        movement.QuantityAfter.Should().Be(50);
        movement.PerformedByUserId.Should().Be(_userId);
    }

    [Fact]
    public async Task InitializeStockAsync_Should_Rollback_And_Return_Conflict_When_Stock_Already_Exists()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _productRepository.Products.Add(productId, new Product { Id = productId, Name = "Existing Product" });
        _stockRepository.Stocks.Add(new InventoryStock
        {
            ProductId = productId,
            Quantity = 20
        });

        var request = new InitializeStockRequest
        {
            ProductId = productId,
            Quantity = 30
        };

        // Act
        var result = await _service.InitializeStockAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.ConflictReason.Should().Contain(productId.ToString());

        _unitOfWork.BeginTransactionCount.Should().Be(1);
        _unitOfWork.RollbackCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    public async Task InitializeStockAsync_Should_Fail_Validation_When_Quantity_Is_Zero_Or_Negative(int quantity)
    {
        // Arrange
        var prodId = Guid.NewGuid();
        _productRepository.Products.Add(prodId, new Product { Id = prodId, Name = "Test Product" });
        var request = new InitializeStockRequest
        {
            ProductId = prodId,
            Quantity = quantity
        };

        // Act
        var result = await _service.InitializeStockAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("quantity");
        result.ValidationErrors!["quantity"].Should().Contain("Opening quantity must be greater than zero.");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task InitializeStockAsync_Should_Fail_Validation_When_Product_Does_Not_Exist()
    {
        // Arrange
        var nonExistentProductId = Guid.NewGuid();
        var request = new InitializeStockRequest
        {
            ProductId = nonExistentProductId,
            Quantity = 10
        };

        // Act
        var result = await _service.InitializeStockAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("productId");
        result.ValidationErrors!["productId"].Should().Contain($"Product with ID '{nonExistentProductId}' does not exist.");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task InitializeStockAsync_Should_Fail_Validation_When_Variant_Does_Not_Belong_To_Product()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var mismatchedVariantId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            Name = "Product A",
            Variants = new List<ProductVariant>
            {
                new() { Id = Guid.NewGuid(), ProductId = productId, Sku = "SKU-1" }
            }
        };
        _productRepository.Products.Add(productId, product);

        var request = new InitializeStockRequest
        {
            ProductId = productId,
            VariantId = mismatchedVariantId,
            Quantity = 10
        };

        // Act
        var result = await _service.InitializeStockAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("variantId");
        result.ValidationErrors!["variantId"].Should().Contain($"Variant with ID '{mismatchedVariantId}' does not belong to product '{productId}'.");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }
}
