using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class PurchaseReceiptServiceTests
{
    private readonly FakePurchaseOrderRepository _purchaseOrders = new();
    private readonly FakeInventoryStockRepository _stocks = new();
    private readonly FakeSerialImeiRepository _serials = new();
    private readonly FakeProductRepository _products = new();
    private readonly FakeSupplierRepository _suppliers = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly PurchaseReceiptService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public PurchaseReceiptServiceTests()
    {
        _purchaseOrders.SupplierRepository = _suppliers;
        _purchaseOrders.ProductRepository = _products;
        _unitOfWork.OnRollback = () => _purchaseOrders.Rollback();
        _service = new PurchaseReceiptService(
            _purchaseOrders,
            _stocks,
            _serials,
            _products,
            _suppliers,
            _unitOfWork);
    }

    [Fact]
    public async Task CreateAsync_When_ClientRequestId_Already_Exists_Should_Replay_Without_Validation()
    {
        // Arrange
        var clientRequestId = Guid.NewGuid();
        var existingOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PN-20261116-001",
            ClientRequestId = clientRequestId,
            Status = PurchaseOrderStatus.Received,
            TotalCost = 100_000m,
            Supplier = new Supplier { Name = "Old Supplier" }
        };
        _purchaseOrders.PurchaseOrders.Add(existingOrder);

        // Act - request with invalid supplier, missing lines, but existing clientRequestId
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = clientRequestId,
            SupplierId = Guid.Empty
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsReplay.Should().BeTrue();
        result.Data!.Id.Should().Be(existingOrder.Id);
        result.Data.PoNumber.Should().Be("PN-20261116-001");
    }

    [Fact]
    public async Task CreateAsync_Without_ClientRequestId_Should_Return_Validation_Error()
    {
        // Act
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = null
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("clientRequestId");
    }

    [Fact]
    public async Task CreateAsync_With_Invalid_Header_Should_Fail_Validation()
    {
        // Arrange
        var systemSupplier = new Supplier { Id = Guid.NewGuid(), Name = SystemSuppliers.OpeningBalanceName, IsActive = true };
        var inactiveSupplier = new Supplier { Id = Guid.NewGuid(), Name = "Inactive", IsActive = false, Phone = "0123" };
        _suppliers.Suppliers.AddRange([systemSupplier, inactiveSupplier]);

        // Act 1: Inactive supplier, notes too long, empty lines
        var result1 = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = inactiveSupplier.Id,
            SupplierDocumentNumber = new string('A', 101),
            Note = new string('B', 1001),
            Lines = []
        }, _userId);

        // Assert 1
        result1.IsSuccess.Should().BeFalse();
        result1.ValidationErrors.Should().ContainKeys("supplierId", "supplierDocumentNumber", "note", "lines");

        // Act 2: System supplier
        var result2 = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = systemSupplier.Id,
            Lines = [new PurchaseReceiptLineRequest { ProductId = Guid.NewGuid(), Quantity = 1, UnitCost = 1000m }]
        }, _userId);

        // Assert 2
        result2.IsSuccess.Should().BeFalse();
        result2.ValidationErrors.Should().ContainKey("supplierId");
        result2.ValidationErrors!["supplierId"].Should().Contain(x => x.Contains("hệ thống"));
    }

    [Fact]
    public async Task CreateAsync_With_Duplicate_Product_Variant_In_Receipt_Should_Fail_Validation()
    {
        // Arrange
        var prodId = Guid.NewGuid();
        var varId = Guid.NewGuid();
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);

        // Act
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Lines =
            [
                new PurchaseReceiptLineRequest { ProductId = prodId, VariantId = varId, Quantity = 5, UnitCost = 10_000m },
                new PurchaseReceiptLineRequest { ProductId = prodId, VariantId = varId, Quantity = 10, UnitCost = 10_000m }
            ]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("lines[1]");
        result.ValidationErrors!["lines[1]"].Should().Contain(x => x.Contains("trùng"));
    }

    [Fact]
    public async Task CreateAsync_Variant_Required_For_Product_With_Variants()
    {
        // Arrange
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);

        var variant = new ProductVariant { Id = Guid.NewGuid(), VariantName = "128GB Đen", Sku = "IP16-128-BLK", IsActive = true };
        var product = new Product { Id = Guid.NewGuid(), Name = "iPhone 16", Sku = "IP16", Variants = [variant], IsSerialTracked = false };
        _products.Products[product.Id] = product;

        // Act - omitted variantId
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Lines =
            [
                new PurchaseReceiptLineRequest { ProductId = product.Id, VariantId = null, Quantity = 2, UnitCost = 15_000_000m }
            ]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("lines[0].variantId");
    }

    [Fact]
    public async Task CreateAsync_Serial_Tracked_Devices_Count_Must_Match_Quantity()
    {
        // Arrange
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);

        var product = new Product { Id = Guid.NewGuid(), Name = "iPhone 16", Sku = "IP16", IsSerialTracked = true };
        _products.Products[product.Id] = product;

        // Act - quantity 5 but 4 devices
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Lines =
            [
                new PurchaseReceiptLineRequest
                {
                    ProductId = product.Id,
                    Quantity = 5,
                    UnitCost = 18_000_000m,
                    Devices =
                    [
                        new PurchaseReceiptDeviceRequest { Imei = "356789012345671" },
                        new PurchaseReceiptDeviceRequest { Imei = "356789012345672" },
                        new PurchaseReceiptDeviceRequest { Imei = "356789012345673" },
                        new PurchaseReceiptDeviceRequest { Imei = "356789012345674" }
                    ]
                }
            ]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("lines[0].devices");
    }

    [Fact]
    public async Task CreateAsync_Non_Serial_Goods_Cannot_Have_Devices()
    {
        // Arrange
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);

        var product = new Product { Id = Guid.NewGuid(), Name = "Cable", Sku = "CBL", IsSerialTracked = false };
        _products.Products[product.Id] = product;

        // Act
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Lines =
            [
                new PurchaseReceiptLineRequest
                {
                    ProductId = product.Id,
                    Quantity = 1,
                    UnitCost = 50_000m,
                    Devices = [new PurchaseReceiptDeviceRequest { SerialNumber = "SN-001" }]
                }
            ]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("lines[0].devices");
    }

    [Fact]
    public async Task CreateAsync_Bad_Imei_Format_Should_Fail_Validation()
    {
        // Arrange
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);

        var product = new Product { Id = Guid.NewGuid(), Name = "Phone", Sku = "PH", IsSerialTracked = true };
        _products.Products[product.Id] = product;

        // Act - IMEI not 15 digits
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Lines =
            [
                new PurchaseReceiptLineRequest
                {
                    ProductId = product.Id,
                    Quantity = 1,
                    UnitCost = 10_000_000m,
                    Devices = [new PurchaseReceiptDeviceRequest { Imei = "12345" }]
                }
            ]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("lines[0].devices[0].imei");
    }

    [Fact]
    public async Task CreateAsync_Duplicate_Identifiers_In_Payload_Should_Return_Conflict()
    {
        // Arrange
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);

        var product = new Product { Id = Guid.NewGuid(), Name = "Phone", Sku = "PH", IsSerialTracked = true };
        _products.Products[product.Id] = product;

        // Act - same IMEI twice in payload
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Lines =
            [
                new PurchaseReceiptLineRequest
                {
                    ProductId = product.Id,
                    Quantity = 2,
                    UnitCost = 10_000_000m,
                    Devices =
                    [
                        new PurchaseReceiptDeviceRequest { Imei = "356789012345678" },
                        new PurchaseReceiptDeviceRequest { Imei = "356789012345678" }
                    ]
                }
            ]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.DuplicateIdentifiers.Should().Contain("356789012345678");
    }

    [Fact]
    public async Task CreateAsync_Existing_Identifiers_In_Database_Should_Return_Conflict_And_Rollback()
    {
        // Arrange
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);

        var product = new Product { Id = Guid.NewGuid(), Name = "Phone", Sku = "PH", IsSerialTracked = true };
        _products.Products[product.Id] = product;

        _serials.Items.Add(new SerialImei
        {
            ProductId = product.Id,
            Imei = "356789012345678",
            Status = SerialImeiStatus.InStock
        });

        // Act
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Lines =
            [
                new PurchaseReceiptLineRequest
                {
                    ProductId = product.Id,
                    Quantity = 1,
                    UnitCost = 10_000_000m,
                    Devices = [new PurchaseReceiptDeviceRequest { Imei = "356789012345678" }]
                }
            ]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.DuplicateIdentifiers.Should().Contain("356789012345678");
        _unitOfWork.RollbackCount.Should().Be(1);
        _purchaseOrders.PurchaseOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_Valid_Receipt_Records_In_One_Transaction_And_Locks_In_Standard_Order()
    {
        // Arrange
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "FPT Trading", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);

        var prodB = new Product { Id = Guid.NewGuid(), Name = "Prod B", Sku = "B", IsSerialTracked = false };
        var prodA = new Product { Id = Guid.NewGuid(), Name = "Prod A", Sku = "A", IsSerialTracked = false };
        _products.Products[prodB.Id] = prodB;
        _products.Products[prodA.Id] = prodA;

        // Ensure prodA has smaller or larger Guid so we can test ordering
        var smallerProd = prodA.Id.CompareTo(prodB.Id) < 0 ? prodA : prodB;
        var largerProd = prodA.Id.CompareTo(prodB.Id) < 0 ? prodB : prodA;

        // Act: Pass larger first, smaller second in lines
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = supplier.Id,
            SupplierDocumentNumber = "HD-0912",
            Note = "Delivery note",
            Lines =
            [
                new PurchaseReceiptLineRequest { ProductId = largerProd.Id, Quantity = 10, UnitCost = 100_000m },
                new PurchaseReceiptLineRequest { ProductId = smallerProd.Id, Quantity = 20, UnitCost = 50_000m }
            ]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsReplay.Should().BeFalse();
        result.Data!.SupplierName.Should().Be("FPT Trading");
        result.Data.SupplierDocumentNumber.Should().Be("HD-0912");
        result.Data.TotalCost.Should().Be(2_000_000m); // (10 * 100k) + (20 * 50k)
        result.Data.Lines.Should().HaveCount(2);

        _unitOfWork.CommitCount.Should().Be(1);

        // Check lock order in fake stock repository: must be smaller first, then larger
        _stocks.LockedKeys.Should().HaveCount(2);
        _stocks.LockedKeys[0].ProductId.Should().Be(smallerProd.Id);
        _stocks.LockedKeys[1].ProductId.Should().Be(largerProd.Id);

        // Check stocks updated
        _stocks.Stocks.Should().HaveCount(2);
        _stocks.Movements.Should().HaveCount(2);
        _stocks.Movements.Should().AllSatisfy(m =>
        {
            m.MovementType.Should().Be(InventoryMovementType.PurchaseReceipt);
            m.Reason.Should().StartWith("Phiếu nhập PN-");
        });
    }

    [Fact]
    public async Task CreateAsync_DuplicateKeyException_Replays_If_ClientRequestId_Exists()
    {
        // Arrange
        var clientRequestId = Guid.NewGuid();
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);
        var product = new Product { Id = Guid.NewGuid(), Name = "Prod", Sku = "P", IsSerialTracked = false };
        _products.Products[product.Id] = product;

        // When SaveChangesAsync is called, simulate another concurrent thread committing the order first
        _purchaseOrders.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PN-CONCURRENT-001",
            ClientRequestId = clientRequestId,
            Supplier = supplier,
            Status = PurchaseOrderStatus.Received,
            TotalCost = 500_000m
        });
        _unitOfWork.ExceptionToThrowOnSave = new DuplicateKeyException("idx_purchase_orders_client_request_id", "duplicate client request id");

        // Act
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = clientRequestId,
            SupplierId = supplier.Id,
            Lines = [new PurchaseReceiptLineRequest { ProductId = product.Id, Quantity = 5, UnitCost = 100_000m }]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsReplay.Should().BeTrue();
        result.Data!.PoNumber.Should().Be("PN-CONCURRENT-001");
    }

    [Fact]
    public async Task CreateAsync_DuplicateKeyException_Maps_Constraint_To_Conflict_Message()
    {
        // Arrange
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);
        var product = new Product { Id = Guid.NewGuid(), Name = "Prod", Sku = "P", IsSerialTracked = false };
        _products.Products[product.Id] = product;

        _unitOfWork.ExceptionToThrowOnSave = new DuplicateKeyException("idx_inventory_stocks_product_variant", "conflict");

        // Act
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Lines = [new PurchaseReceiptLineRequest { ProductId = product.Id, Quantity = 5, UnitCost = 100_000m }]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.ConflictReason.Should().Contain("Tồn kho vừa được tạo");
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_Should_Apply_Filters_And_Pagination_Including_OB_Receipts()
    {
        // Arrange
        var supplier1 = new Supplier { Id = Guid.NewGuid(), Name = "Supplier One" };
        var supplier2 = new Supplier { Id = Guid.NewGuid(), Name = SystemSuppliers.OpeningBalanceName };

        var r1 = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PN-20261116-001",
            Supplier = supplier1,
            SupplierId = supplier1.Id,
            SupplierDocumentNumber = "DOC-A",
            Status = PurchaseOrderStatus.Received,
            ReceivedAt = DateTime.UtcNow.AddMinutes(-20)
        };
        var r2 = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "OB-20261116-002",
            Supplier = supplier2,
            SupplierId = supplier2.Id,
            Status = PurchaseOrderStatus.Received,
            ReceivedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var rDraft = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PO-DRAFT",
            Supplier = supplier1,
            SupplierId = supplier1.Id,
            Status = PurchaseOrderStatus.Draft
        };

        _purchaseOrders.PurchaseOrders.AddRange([r1, r2, rDraft]);

        // Act 1: Search all
        var all = await _service.SearchAsync(new PurchaseReceiptFilterRequest { Page = 1, PageSize = 10 });
        all.IsSuccess.Should().BeTrue();
        all.Data!.Items.Should().HaveCount(2); // Only Received, excludes Draft
        all.Data.Items.Should().Contain(x => x.PoNumber.StartsWith("OB-")); // Includes OB-
        all.Data.Items[0].PoNumber.Should().Be("OB-20261116-002"); // Newest first

        // Act 2: Filter by supplier
        var bySupplier = await _service.SearchAsync(new PurchaseReceiptFilterRequest { SupplierId = supplier1.Id });
        bySupplier.Data!.Items.Should().ContainSingle(x => x.PoNumber == "PN-20261116-001");

        // Act 3: Filter by search keyword
        var byKeyword = await _service.SearchAsync(new PurchaseReceiptFilterRequest { Search = "DOC-A" });
        byKeyword.Data!.Items.Should().ContainSingle(x => x.PoNumber == "PN-20261116-001");
    }

    [Fact]
    public async Task GetDetailAsync_Should_Return_Detail_Or_NotFound()
    {
        // Arrange
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "FPT" };
        var product = new Product { Id = Guid.NewGuid(), Name = "iPhone", Sku = "IP16", IsSerialTracked = true };
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            PoNumber = "PN-001",
            Supplier = supplier,
            SupplierId = supplier.Id,
            Items =
            [
                new PurchaseOrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Product = product,
                    OrderedQuantity = 1,
                    ReceivedQuantity = 1,
                    UnitCost = 15_000_000m
                }
            ]
        };
        var device = new SerialImei
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Imei = "356789012345678",
            PurchaseOrderId = order.Id,
            Status = SerialImeiStatus.InStock
        };

        _purchaseOrders.PurchaseOrders.Add(order);
        _purchaseOrders.Devices.Add(device);

        // Act 1: Existing id
        var detail = await _service.GetDetailAsync(order.Id);
        detail.IsSuccess.Should().BeTrue();
        detail.Data!.PoNumber.Should().Be("PN-001");
        detail.Data.Lines.Should().ContainSingle();
        detail.Data.Lines[0].Devices.Should().ContainSingle(d => d.Imei == "356789012345678");

        // Act 2: Unknown id
        var notFound = await _service.GetDetailAsync(Guid.NewGuid());
        notFound.IsSuccess.Should().BeFalse();
        notFound.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_ReceiptTotalAboveNumericLimit_FailsValidationWithoutWriting()
    {
        // Arrange: each line fits numeric(18,2), their sum does not.
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);
        var first = new Product { Id = Guid.NewGuid(), Name = "Server A", Sku = "SRV-A", IsSerialTracked = false };
        var second = new Product { Id = Guid.NewGuid(), Name = "Server B", Sku = "SRV-B", IsSerialTracked = false };
        _products.Products[first.Id] = first;
        _products.Products[second.Id] = second;

        // Act
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Lines =
            [
                new PurchaseReceiptLineRequest { ProductId = first.Id, Quantity = 100_000, UnitCost = 99_999_999_999m },
                new PurchaseReceiptLineRequest { ProductId = second.Id, Quantity = 100_000, UnitCost = 99_999_999_999m }
            ]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("lines");
        result.ValidationErrors!["lines"].Should().Contain(x => x.Contains("Tổng tiền phiếu nhập"));
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_NullLineOrDevice_FailsValidationInsteadOfThrowing()
    {
        // Arrange
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "Supplier", Phone = "0123", IsActive = true };
        _suppliers.Suppliers.Add(supplier);

        // Act
        var result = await _service.CreateAsync(new CreatePurchaseReceiptRequest
        {
            ClientRequestId = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Lines =
            [
                null!,
                new PurchaseReceiptLineRequest { ProductId = Guid.NewGuid(), Quantity = 1, UnitCost = 1m, Devices = [null!] }
            ]
        }, _userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKeys("lines[0]", "lines[1].devices[0]");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
    }
}
