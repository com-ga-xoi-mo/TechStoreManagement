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
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InventoryService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public InventoryServiceTests()
    {
        _service = new InventoryService(_stockRepository, _unitOfWork);
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

    [Fact]
    public async Task InitializeStockAsync_Should_Fail_Validation_When_Quantity_Is_Negative()
    {
        // Arrange
        var request = new InitializeStockRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = -5
        };

        // Act
        var result = await _service.InitializeStockAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("quantity");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }
}
