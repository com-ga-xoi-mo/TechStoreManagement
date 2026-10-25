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
    private readonly FakeSerialImeiRepository _serialRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InventoryService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public InventoryServiceTests()
    {
        _service = new InventoryService(_stockRepository, _productRepository, _serialRepository, _unitOfWork);
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
    public async Task GetStocksAsync_Should_Map_CategoryAndEffectiveSellingPrice()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Điện thoại" };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "iPhone 15",
            Sku = "IP15",
            Category = category,
            BasePrice = 24_000_000m
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            VariantName = "256 GB",
            Sku = "IP15-256",
            Price = 24_990_000m,
            Product = product
        };
        product.Variants.Add(variant);
        _stockRepository.Stocks.Add(new InventoryStock
        {
            ProductId = product.Id,
            VariantId = variant.Id,
            Product = product,
            Variant = variant,
            Quantity = 8
        });
        _stockRepository.Serials.AddRange(
        [
            new SerialImei { Id = Guid.NewGuid(), ProductId = product.Id, VariantId = variant.Id },
            new SerialImei { Id = Guid.NewGuid(), ProductId = product.Id, VariantId = variant.Id },
            new SerialImei { Id = Guid.NewGuid(), ProductId = product.Id, VariantId = variant.Id }
        ]);

        var result = await _service.GetStocksAsync(new InventoryStockFilterRequest { Page = 1, PageSize = 20 });

        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().ContainSingle();
        result.Data.Items[0].CategoryName.Should().Be("Điện thoại");
        result.Data.Items[0].SellingPrice.Should().Be(24_990_000m);
        result.Data.Items[0].TotalSerialsCount.Should().Be(3);
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

    [Fact]
    public async Task DeleteStockAsync_EmptyStock_DeletesStockAndCommits()
    {
        var stock = new InventoryStock
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 0,
            ReservedQuantity = 0
        };
        _stockRepository.Stocks.Add(stock);

        var result = await _service.DeleteStockAsync(stock.Id);

        result.IsSuccess.Should().BeTrue();
        _stockRepository.Stocks.Should().BeEmpty();
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteStockAsync_ReservedStock_ReturnsConflictAndKeepsRow()
    {
        var stock = new InventoryStock
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 5,
            ReservedQuantity = 1
        };
        _stockRepository.Stocks.Add(stock);

        var result = await _service.DeleteStockAsync(stock.Id);

        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.ConflictReason.Should().Contain("giữ chỗ");
        _stockRepository.Stocks.Should().ContainSingle();
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteStockAsync_WithMovementHistory_ReturnsConflictAndKeepsRow()
    {
        var stock = new InventoryStock
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 0
        };
        _stockRepository.Stocks.Add(stock);
        _stockRepository.Movements.Add(new InventoryMovement { InventoryStockId = stock.Id });

        var result = await _service.DeleteStockAsync(stock.Id);

        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.ConflictReason.Should().Contain("lịch sử");
        _stockRepository.Stocks.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteStockAsync_SoldSerial_ReturnsConflictAndKeepsSerial()
    {
        var productId = Guid.NewGuid();
        var stock = new InventoryStock
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = 1
        };
        _stockRepository.Stocks.Add(stock);
        _serialRepository.Items.Add(new SerialImei
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Status = SerialImeiStatus.Sold
        });

        var result = await _service.DeleteStockAsync(stock.Id);

        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        _serialRepository.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task AdjustStockAsync_Increase_UpdatesQuantityAndWritesAdjustmentMovement()
    {
        var stock = new InventoryStock { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = 10, ReservedQuantity = 2 };
        _stockRepository.Stocks.Add(stock);

        var result = await _service.AdjustStockAsync(stock.Id, new AdjustStockRequest
        {
            QuantityChange = 5, Reason = "Kiểm kê", Note = "Bổ sung thực tế"
        }, _userId);

        result.IsSuccess.Should().BeTrue();
        stock.Quantity.Should().Be(15);
        _stockRepository.Movements.Should().ContainSingle();
        _stockRepository.Movements[0].MovementType.Should().Be(InventoryMovementType.Adjustment);
        _stockRepository.Movements[0].QuantityChange.Should().Be(5);
        _stockRepository.Movements[0].QuantityAfter.Should().Be(15);
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task AdjustStockAsync_DecreaseBelowReserved_ReturnsConflict()
    {
        var stock = new InventoryStock { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = 10, ReservedQuantity = 8 };
        _stockRepository.Stocks.Add(stock);

        var result = await _service.AdjustStockAsync(stock.Id, new AdjustStockRequest
        {
            QuantityChange = -3, Reason = "Kiểm kê"
        }, _userId);

        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        stock.Quantity.Should().Be(10);
        _stockRepository.Movements.Should().BeEmpty();
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task AdjustStockAsync_CheckConstraintViolation_ReturnsConflict()
    {
        // Arrange
        var stock = new InventoryStock { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = 10, ReservedQuantity = 0 };
        _stockRepository.Stocks.Add(stock);
        _unitOfWork.ExceptionToThrowOnSave = new TechStore.Api.Common.Data.CheckConstraintViolationException("chk_inventory_stocks_quantity");

        // Act
        var result = await _service.AdjustStockAsync(stock.Id, new AdjustStockRequest
        {
            QuantityChange = -1, Reason = "Kiểm kê"
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task GetStockDetailAsync_MapsRecentMovementAndAvailableQuantity()
    {
        var product = new Product { Id = Guid.NewGuid(), Name = "Phone", Sku = "P-1" };
        var stock = new InventoryStock { Id = Guid.NewGuid(), ProductId = product.Id, Product = product, Quantity = 12, ReservedQuantity = 3 };
        _stockRepository.Stocks.Add(stock);
        _stockRepository.Movements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(), InventoryStockId = stock.Id, MovementType = InventoryMovementType.Adjustment,
            QuantityChange = 2, QuantityAfter = 12, Reason = "Kiểm kê", CreatedAt = DateTime.UtcNow
        });

        var result = await _service.GetStockDetailAsync(stock.Id);

        result.IsSuccess.Should().BeTrue();
        result.Data!.AvailableQuantity.Should().Be(9);
        result.Data.RecentMovements.Should().ContainSingle();
        result.Data.RecentMovements[0].QuantityBefore.Should().Be(10);
    }

    [Fact]
    public async Task GetStocksAsync_StatusFilters_AreDisjoint()
    {
        // Arrange
        var pOut = new Product { Id = Guid.NewGuid(), Name = "Out", Sku = "OUT" };
        var pLow = new Product { Id = Guid.NewGuid(), Name = "Low", Sku = "LOW" };
        var pIn = new Product { Id = Guid.NewGuid(), Name = "In", Sku = "IN" };

        _stockRepository.Stocks.AddRange(
        [
            new InventoryStock { Id = Guid.NewGuid(), ProductId = pOut.Id, Product = pOut, Quantity = 2, ReservedQuantity = 2, MinStockAlert = 5 },
            new InventoryStock { Id = Guid.NewGuid(), ProductId = pLow.Id, Product = pLow, Quantity = 5, ReservedQuantity = 2, MinStockAlert = 5 },
            new InventoryStock { Id = Guid.NewGuid(), ProductId = pIn.Id, Product = pIn, Quantity = 10, ReservedQuantity = 1, MinStockAlert = 5 }
        ]);

        // Act
        var outRes = await _service.GetStocksAsync(new InventoryStockFilterRequest { OutOfStock = true });
        var lowRes = await _service.GetStocksAsync(new InventoryStockFilterRequest { LowStock = true });
        var inRes = await _service.GetStocksAsync(new InventoryStockFilterRequest { InStock = true });

        // Assert
        outRes.Data!.Items.Should().ContainSingle().Which.ProductId.Should().Be(pOut.Id);
        outRes.Data.Items[0].IsLowStock.Should().BeFalse();

        lowRes.Data!.Items.Should().ContainSingle().Which.ProductId.Should().Be(pLow.Id);
        lowRes.Data.Items[0].IsLowStock.Should().BeTrue();

        inRes.Data!.Items.Should().ContainSingle().Which.ProductId.Should().Be(pIn.Id);
        inRes.Data.Items[0].IsLowStock.Should().BeFalse();

        (outRes.Data.TotalCount + lowRes.Data.TotalCount + inRes.Data.TotalCount).Should().Be(3);
    }
}
