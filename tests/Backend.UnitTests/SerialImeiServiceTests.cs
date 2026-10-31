using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class SerialImeiServiceTests
{
    private readonly FakeSerialImeiRepository _serialRepository = new();
    private readonly FakeInventoryStockRepository _stockRepository = new();
    private readonly FakeProductRepository _productRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly SerialImeiService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public SerialImeiServiceTests()
    {
        _service = new SerialImeiService(_serialRepository, _stockRepository, _productRepository, _unitOfWork);
    }

    [Fact]
    public async Task GetSerialsAsync_Should_Return_Paged_Serials_And_Filter_By_Status()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _serialRepository.Items.Add(new SerialImei
        {
            ProductId = productId,
            SerialNumber = "SN001",
            Status = SerialImeiStatus.InStock
        });
        _serialRepository.Items.Add(new SerialImei
        {
            ProductId = productId,
            SerialNumber = "SN002",
            Status = SerialImeiStatus.Sold
        });

        var request = new SerialImeiFilterRequest
        {
            ProductId = productId,
            Status = SerialImeiStatus.InStock,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _service.GetSerialsAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items[0].SerialNumber.Should().Be("SN001");
        result.Data.Items[0].Status.Should().Be(SerialImeiStatus.InStock);
    }

    [Fact]
    public async Task BatchImportAsync_Should_Create_Stock_When_Absent_Accumulate_Quantity_And_Set_QuantityAfter_Correctly()
    {
        // Arrange: chưa có stock row cho sản phẩm này
        var productId = Guid.NewGuid();
        _productRepository.Products.Add(productId, new Product { Id = productId, Name = "Product A" });
        var request = new BatchImportSerialImeiRequest
        {
            ProductId = productId,
            DefaultUnitCost = 1_000_000m,
            Items =
            [
                new SerialImeiImportItemRequest { SerialNumber = "SN101", Imei = "123456789012345" },
                new SerialImeiImportItemRequest { SerialNumber = "SN102", Imei = "123456789012346" }
            ],
            Reason = "First batch import"
        };

        // Act
        var result = await _service.BatchImportAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ImportedCount.Should().Be(2);

        _unitOfWork.BeginTransactionCount.Should().Be(1);
        _unitOfWork.SaveChangesCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);

        // BƯỚC 1 & 2: Kiểm tra stock row được tạo mới và Quantity được cộng dồn đúng 2
        _stockRepository.Stocks.Should().HaveCount(1);
        var stock = _stockRepository.Stocks[0];
        stock.ProductId.Should().Be(productId);
        stock.Quantity.Should().Be(2);

        // BƯỚC 3: Kiểm tra InventoryMovement được tạo SAU KHI Quantity đã cộng xong và QuantityAfter = 2
        _stockRepository.Movements.Should().HaveCount(1);
        var movement = _stockRepository.Movements[0];
        movement.MovementType.Should().Be(InventoryMovementType.OpeningBalance);
        movement.QuantityChange.Should().Be(2);
        movement.QuantityAfter.Should().Be(2); // Phải bằng stock.Quantity sau khi cộng
        movement.PerformedByUserId.Should().Be(_userId);

        _serialRepository.Items.Should().HaveCount(2);
        _serialRepository.Items.Should().OnlyContain(i => i.Status == SerialImeiStatus.InStock);
    }

    [Fact]
    public async Task BatchImportAsync_Should_Accumulate_Existing_Stock_Quantity_And_Set_QuantityAfter_Correctly()
    {
        // Arrange: đã có stock row với Quantity = 10
        var productId = Guid.NewGuid();
        _productRepository.Products.Add(productId, new Product { Id = productId, Name = "Product B" });
        var existingStock = new InventoryStock
        {
            ProductId = productId,
            Quantity = 10,
            MinStockAlert = 5
        };
        _stockRepository.Stocks.Add(existingStock);

        var request = new BatchImportSerialImeiRequest
        {
            ProductId = productId,
            DefaultUnitCost = 1_000_000m,
            Items =
            [
                new SerialImeiImportItemRequest { SerialNumber = "SN201", Imei = "987654321098765" },
                new SerialImeiImportItemRequest { SerialNumber = "SN202", Imei = "987654321098766" },
                new SerialImeiImportItemRequest { SerialNumber = "SN203", Imei = "987654321098767" }
            ]
        };

        // Act
        var result = await _service.BatchImportAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ImportedCount.Should().Be(3);

        // Stock quantity được tích lũy từ 10 + 3 = 13
        existingStock.Quantity.Should().Be(13);

        // Movement QuantityAfter nhận đúng giá trị sau tích lũy = 13
        _stockRepository.Movements.Should().HaveCount(1);
        var movement = _stockRepository.Movements[0];
        movement.QuantityChange.Should().Be(3);
        movement.QuantityAfter.Should().Be(13);
    }

    [Fact]
    public async Task BatchImportAsync_Should_Return_Conflict_When_Duplicate_Identifiers_In_Database()
    {
        // Arrange: DB đã có Serial SN999
        var productId = Guid.NewGuid();
        _productRepository.Products.Add(productId, new Product { Id = productId, Name = "Product C" });
        _serialRepository.Items.Add(new SerialImei
        {
            ProductId = productId,
            SerialNumber = "SN999"
        });

        var request = new BatchImportSerialImeiRequest
        {
            ProductId = productId,
            DefaultUnitCost = 1_000_000m,
            Items =
            [
                new SerialImeiImportItemRequest { SerialNumber = "SN999", Imei = "111112222233333" }
            ]
        };

        // Act
        var result = await _service.BatchImportAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.ConflictingSerials.Should().Contain("SN999");
        _unitOfWork.RollbackCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task BatchImportAsync_Should_Return_Conflict_When_Duplicate_Identifiers_Within_Payload()
    {
        // Arrange: payload tự chứa 2 serial trùng nhau
        var productId = Guid.NewGuid();
        _productRepository.Products.Add(productId, new Product { Id = productId, Name = "Product D" });
        var request = new BatchImportSerialImeiRequest
        {
            ProductId = productId,
            DefaultUnitCost = 1_000_000m,
            Items =
            [
                new SerialImeiImportItemRequest { SerialNumber = "DUPE-SN" },
                new SerialImeiImportItemRequest { SerialNumber = "DUPE-SN" }
            ]
        };

        // Act
        var result = await _service.BatchImportAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.ConflictingSerials.Should().Contain("DUPE-SN");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task BatchImportAsync_Should_Fail_Validation_When_Imei_Is_Not_15_Digits()
    {
        // Arrange: IMEI 10 chữ số (không hợp lệ)
        var productId = Guid.NewGuid();
        _productRepository.Products.Add(productId, new Product { Id = productId, Name = "Product E" });
        var request = new BatchImportSerialImeiRequest
        {
            ProductId = productId,
            DefaultUnitCost = 1_000_000m,
            Items =
            [
                new SerialImeiImportItemRequest { Imei = "12345" }
            ]
        };

        // Act
        var result = await _service.BatchImportAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("items[0].imei");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task BatchImportAsync_Should_Fail_Validation_When_Items_Count_Exceeds_500()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _productRepository.Products.Add(productId, new Product { Id = productId, Name = "Product Max" });
        var items = Enumerable.Range(1, 501)
            .Select(i => new SerialImeiImportItemRequest { SerialNumber = $"SN-{i}" })
            .ToList();

        var request = new BatchImportSerialImeiRequest
        {
            ProductId = productId,
            DefaultUnitCost = 1_000_000m,
            Items = items
        };

        // Act
        var result = await _service.BatchImportAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("items");
        result.ValidationErrors!["items"].Should().Contain("Batch import size cannot exceed 500 items per request.");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task BatchImportAsync_Should_Fail_Validation_When_Product_Does_Not_Exist()
    {
        // Arrange
        var nonExistentProductId = Guid.NewGuid();
        var request = new BatchImportSerialImeiRequest
        {
            ProductId = nonExistentProductId,
            DefaultUnitCost = 1_000_000m,
            Items = [new SerialImeiImportItemRequest { SerialNumber = "SN-001" }]
        };

        // Act
        var result = await _service.BatchImportAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("productId");
        result.ValidationErrors!["productId"].Should().Contain($"Product with ID '{nonExistentProductId}' does not exist.");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task BatchImportAsync_Should_Fail_Validation_When_Variant_Does_Not_Belong_To_Product()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var mismatchedVariantId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            Name = "Product V",
            Variants = new List<ProductVariant>
            {
                new() { Id = Guid.NewGuid(), ProductId = productId, Sku = "SKU-V1" }
            }
        };
        _productRepository.Products.Add(productId, product);

        var request = new BatchImportSerialImeiRequest
        {
            ProductId = productId,
            DefaultUnitCost = 1_000_000m,
            VariantId = mismatchedVariantId,
            Items = [new SerialImeiImportItemRequest { SerialNumber = "SN-002" }]
        };

        // Act
        var result = await _service.BatchImportAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("variantId");
        result.ValidationErrors!["variantId"].Should().Contain($"Variant with ID '{mismatchedVariantId}' does not belong to product '{productId}'.");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task BatchImportAsync_Should_Require_A_Cost_For_Every_Device()
    {
        // Arrange: no item cost and no defaultUnitCost
        var productId = Guid.NewGuid();
        _productRepository.Products.Add(productId, new Product { Id = productId, Name = "Product C" });
        var request = new BatchImportSerialImeiRequest
        {
            ProductId = productId,
            Items =
            [
                new SerialImeiImportItemRequest { SerialNumber = "SN-C1", UnitCost = 21_500_000m },
                new SerialImeiImportItemRequest { SerialNumber = "SN-C2" }
            ]
        };

        // Act
        var result = await _service.BatchImportAsync(request, _userId);

        // Assert: only the second item is rejected, and nothing is written
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("items[1].unitCost").And.NotContainKey("items[0].unitCost");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
        _serialRepository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchImportAsync_Should_Store_The_Item_Cost_Or_Else_The_Default()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _productRepository.Products.Add(productId, new Product { Id = productId, Name = "Product D" });
        var request = new BatchImportSerialImeiRequest
        {
            ProductId = productId,
            DefaultUnitCost = 20_000_000m,
            Items =
            [
                new SerialImeiImportItemRequest { SerialNumber = "SN-D1", UnitCost = 21_500_000m },
                new SerialImeiImportItemRequest { SerialNumber = "SN-D2" }
            ]
        };

        // Act
        var result = await _service.BatchImportAsync(request, _userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _serialRepository.Items.Single(i => i.SerialNumber == "SN-D1").UnitCost.Should().Be(21_500_000m);
        _serialRepository.Items.Single(i => i.SerialNumber == "SN-D2").UnitCost.Should().Be(20_000_000m);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(1_000_000_000_001)]
    public async Task BatchImportAsync_Should_Reject_A_Cost_Outside_Range(long cost)
    {
        // Arrange
        var productId = Guid.NewGuid();
        _productRepository.Products.Add(productId, new Product { Id = productId, Name = "Product E" });
        var request = new BatchImportSerialImeiRequest
        {
            ProductId = productId,
            Items = [new SerialImeiImportItemRequest { SerialNumber = "SN-E1", UnitCost = cost }]
        };

        // Act
        var result = await _service.BatchImportAsync(request, _userId);

        // Assert
        result.ValidationErrors.Should().ContainKey("items[0].unitCost");
        _serialRepository.Items.Should().BeEmpty();
    }
}
