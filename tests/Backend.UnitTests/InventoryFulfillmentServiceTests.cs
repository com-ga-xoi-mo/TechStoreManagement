using FluentAssertions;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

public class InventoryFulfillmentServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime CompletedAt = new(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeSerialImeiRepository _serials = new();
    private readonly FakeInventoryStockRepository _stocks = new();
    private readonly InventoryFulfillmentService _service;

    public InventoryFulfillmentServiceTests()
    {
        _service = new InventoryFulfillmentService(_serials, _stocks);
    }

    private InventoryStock AddStock(Guid productId, Guid? variantId, int quantity, int reserved = 0)
    {
        var stock = new InventoryStock
        {
            ProductId = productId,
            VariantId = variantId,
            Quantity = quantity,
            ReservedQuantity = reserved
        };
        _stocks.Stocks.Add(stock);
        return stock;
    }

    private SerialImei AddDevice(
        Guid productId,
        Guid? variantId = null,
        SerialImeiStatus status = SerialImeiStatus.InStock,
        decimal? unitCost = null,
        int warrantyMonths = 12)
    {
        var device = new SerialImei
        {
            ProductId = productId,
            VariantId = variantId,
            Imei = Random.Shared.NextInt64(100_000_000_000_000, 999_999_999_999_999).ToString(),
            Status = status,
            UnitCost = unitCost,
            WarrantyMonths = warrantyMonths
        };
        _serials.Items.Add(device);
        return device;
    }

    private static FulfillmentLine DeviceLine(SerialImei device, Guid? orderItemId = null) =>
        new(orderItemId ?? Guid.NewGuid(), device.ProductId, device.VariantId, device.Id, 1);

    private static FulfillmentLine QuantityLine(Guid productId, int quantity, Guid? variantId = null, Guid? orderItemId = null) =>
        new(orderItemId ?? Guid.NewGuid(), productId, variantId, null, quantity);

    // ------------------------------------------------------------------
    // Design: staging-only
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_DoesNotDependOnUnitOfWork()
    {
        // Act
        var parameterTypes = typeof(InventoryFulfillmentService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType);

        // Assert
        parameterTypes.Should().NotContain(typeof(IUnitOfWork));
    }

    // ------------------------------------------------------------------
    // CheckAvailabilityAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task CheckAvailability_InStockDeviceWithStock_ReportsNoProblem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var device = AddDevice(productId);
        AddStock(productId, null, 5);

        // Act
        var result = await _service.CheckAvailabilityAsync([DeviceLine(device)]);

        // Assert
        result.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAvailability_SoldDevice_ReportsProblemOnThatLine()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var device = AddDevice(productId, status: SerialImeiStatus.Sold);
        AddStock(productId, null, 5);

        // Act
        var result = await _service.CheckAvailabilityAsync([DeviceLine(device)]);

        // Assert
        result.Problems.Should().ContainSingle().Which.LineIndex.Should().Be(0);
    }

    [Fact]
    public async Task CheckAvailability_UnknownDevice_ReportsProblem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        AddStock(productId, null, 5);
        var line = new FulfillmentLine(Guid.NewGuid(), productId, null, Guid.NewGuid(), 1);

        // Act
        var result = await _service.CheckAvailabilityAsync([line]);

        // Assert
        result.Problems.Should().ContainSingle();
    }

    [Fact]
    public async Task CheckAvailability_DeviceOfAnotherProduct_ReportsProblem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        var device = AddDevice(otherProductId);
        AddStock(productId, null, 5);
        var line = new FulfillmentLine(Guid.NewGuid(), productId, null, device.Id, 1);

        // Act
        var result = await _service.CheckAvailabilityAsync([line]);

        // Assert
        result.Problems.Should().ContainSingle();
    }

    [Fact]
    public async Task CheckAvailability_DeviceOfAnotherVariant_ReportsProblem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var variantA = Guid.NewGuid();
        var variantB = Guid.NewGuid();
        var device = AddDevice(productId, variantA);
        AddStock(productId, variantB, 5);
        var line = new FulfillmentLine(Guid.NewGuid(), productId, variantB, device.Id, 1);

        // Act
        var result = await _service.CheckAvailabilityAsync([line]);

        // Assert
        result.Problems.Should().ContainSingle();
    }

    [Fact]
    public async Task CheckAvailability_NullVariantDeviceForVariantLine_ReportsProblem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var device = AddDevice(productId, null);
        AddStock(productId, variantId, 5);
        var line = new FulfillmentLine(Guid.NewGuid(), productId, variantId, device.Id, 1);

        // Act
        var result = await _service.CheckAvailabilityAsync([line]);

        // Assert
        result.Problems.Should().ContainSingle();
    }

    [Fact]
    public async Task CheckAvailability_VariantDeviceForNullVariantLine_ReportsProblem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var device = AddDevice(productId, variantId);
        AddStock(productId, null, 5);
        var line = new FulfillmentLine(Guid.NewGuid(), productId, null, device.Id, 1);

        // Act
        var result = await _service.CheckAvailabilityAsync([line]);

        // Assert
        result.Problems.Should().ContainSingle();
    }

    [Fact]
    public async Task CheckAvailability_MissingStockRow_ReportsStockNotInitialized()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var device = AddDevice(productId);

        // Act
        var result = await _service.CheckAvailabilityAsync([DeviceLine(device)]);

        // Assert
        result.Problems.Should().ContainSingle().Which.Message.Should().Contain("not initialized");
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(2, true)]
    [InlineData(1, true)]
    public async Task CheckAvailability_NonTrackedQuantity_ComparesWithAvailableUnits(int requested, bool expectedAvailable)
    {
        // Arrange: quantity 5, reserved 3 => 2 available
        var productId = Guid.NewGuid();
        AddStock(productId, null, 5, reserved: 3);

        // Act
        var result = await _service.CheckAvailabilityAsync([QuantityLine(productId, requested)]);

        // Assert
        result.IsAvailable.Should().Be(expectedAvailable);
    }

    [Fact]
    public async Task CheckAvailability_TwoFailingLines_ReportsBothProblems()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var soldDevice = AddDevice(productId, status: SerialImeiStatus.Sold);
        var otherProductId = Guid.NewGuid();
        AddStock(productId, null, 5);
        AddStock(otherProductId, null, 1);

        // Act
        var result = await _service.CheckAvailabilityAsync([DeviceLine(soldDevice), QuantityLine(otherProductId, 5)]);

        // Assert
        result.Problems.Select(p => p.LineIndex).Should().BeEquivalentTo([0, 1]);
    }

    [Fact]
    public async Task CheckAvailability_DoesNotLockOrChangeAnything()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var device = AddDevice(productId);
        var stock = AddStock(productId, null, 5);

        // Act
        await _service.CheckAvailabilityAsync([DeviceLine(device)]);

        // Assert
        _serials.GetForUpdateCallCount.Should().Be(0);
        _stocks.LockedKeys.Should().BeEmpty();
        device.Status.Should().Be(SerialImeiStatus.InStock);
        stock.ReservedQuantity.Should().Be(0);
    }

    // ------------------------------------------------------------------
    // ReserveAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task Reserve_SingleDevice_StagesReservedAndIncrementsReservedQuantity()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var device = AddDevice(productId);
        var stock = AddStock(productId, null, 5);

        // Act
        var result = await _service.ReserveAsync([DeviceLine(device)]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        device.Status.Should().Be(SerialImeiStatus.Reserved);
        stock.ReservedQuantity.Should().Be(1);
        stock.Quantity.Should().Be(5);
        _stocks.Movements.Should().BeEmpty();
    }

    [Fact]
    public async Task Reserve_ThreeDevicesOfOneStockRow_IncrementsReservedByThree()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var devices = Enumerable.Range(0, 3).Select(_ => AddDevice(productId)).ToList();
        var stock = AddStock(productId, null, 5);

        // Act
        var result = await _service.ReserveAsync(devices.Select(d => DeviceLine(d)).ToList());

        // Assert
        result.IsSuccess.Should().BeTrue();
        stock.ReservedQuantity.Should().Be(3);
        devices.Should().OnlyContain(d => d.Status == SerialImeiStatus.Reserved);
    }

    [Theory]
    [InlineData(SerialImeiStatus.Reserved)]
    [InlineData(SerialImeiStatus.Sold)]
    public async Task Reserve_DeviceNotInStock_ConflictAndStagesNothing(SerialImeiStatus status)
    {
        // Arrange
        var productId = Guid.NewGuid();
        var good = AddDevice(productId);
        var bad = AddDevice(productId, status: status);
        var stock = AddStock(productId, null, 5);

        // Act
        var result = await _service.ReserveAsync([DeviceLine(good), DeviceLine(bad)]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Conflict!.LineIndex.Should().Be(1);
        good.Status.Should().Be(SerialImeiStatus.InStock);
        stock.ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Reserve_NonTrackedQuantityExceedsAvailable_ConflictAndStagesNothing()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var stock = AddStock(productId, null, 5, reserved: 4);

        // Act
        var result = await _service.ReserveAsync([QuantityLine(productId, 2)]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        stock.ReservedQuantity.Should().Be(4);
    }

    [Fact]
    public async Task Reserve_NonTrackedQuantity_IncrementsReservedQuantity()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var stock = AddStock(productId, null, 10);

        // Act
        var result = await _service.ReserveAsync([QuantityLine(productId, 4)]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        stock.ReservedQuantity.Should().Be(4);
        stock.Quantity.Should().Be(10);
    }

    [Fact]
    public async Task Reserve_MissingStockRow_Conflict()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var device = AddDevice(productId);

        // Act
        var result = await _service.ReserveAsync([DeviceLine(device)]);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Conflict!.Message.Should().Contain("not initialized");
        device.Status.Should().Be(SerialImeiStatus.InStock);
    }

    [Fact]
    public async Task Reserve_MultipleDevicesAndStocks_LocksInAscendingOrder()
    {
        // Arrange
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var deviceA = AddDevice(productA);
        var deviceB = AddDevice(productB);
        AddStock(productA, null, 5);
        AddStock(productB, null, 5);

        // Act: lines supplied in the "wrong" order on purpose
        var ordered = new[] { productA, productB }.OrderBy(g => g).ToList();
        var lines = ordered.Reverse<Guid>()
            .Select(p => DeviceLine(p == productA ? deviceA : deviceB))
            .ToList();
        var result = await _service.ReserveAsync(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _stocks.LockedKeys.Select(k => k.ProductId).Should().Equal(ordered);
        _serials.LockedIdBatches.Should().ContainSingle();
        _serials.LockedIdBatches[0].Should().BeInAscendingOrder();
    }

    // ------------------------------------------------------------------
    // ConfirmSaleAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task ConfirmSale_ReservedPhone_SellsWithWarrantyAndLedger()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var device = AddDevice(productId, status: SerialImeiStatus.Reserved, warrantyMonths: 12);
        var stock = AddStock(productId, null, 5, reserved: 1);
        var orderItemId = Guid.NewGuid();

        // Act
        var result = await _service.ConfirmSaleAsync([DeviceLine(device, orderItemId)], CompletedAt, UserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        device.Status.Should().Be(SerialImeiStatus.Sold);
        device.WarrantyStartAt.Should().Be(CompletedAt);
        device.WarrantyEndAt.Should().Be(new DateTime(2027, 10, 11, 8, 0, 0, DateTimeKind.Utc));
        stock.Quantity.Should().Be(4);
        stock.ReservedQuantity.Should().Be(0);

        var movement = _stocks.Movements.Should().ContainSingle().Subject;
        movement.MovementType.Should().Be(InventoryMovementType.Sale);
        movement.QuantityChange.Should().Be(-1);
        movement.QuantityAfter.Should().Be(4);
        movement.OrderItemId.Should().Be(orderItemId);
        movement.PerformedByUserId.Should().Be(UserId);
        movement.InventoryStockId.Should().Be(stock.Id);
    }

    [Fact]
    public async Task ConfirmSale_TwoLinesOfOneStockRow_RecordsDecreasingQuantityAfter()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var first = AddDevice(productId, status: SerialImeiStatus.Reserved);
        var second = AddDevice(productId, status: SerialImeiStatus.Reserved);
        AddStock(productId, null, 10, reserved: 2);

        // Act
        var result = await _service.ConfirmSaleAsync([DeviceLine(first), DeviceLine(second)], CompletedAt, UserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _stocks.Movements.Select(m => m.QuantityAfter).Should().Equal(9, 8);
    }

    [Fact]
    public async Task ConfirmSale_DeviceStillInStock_ConflictAndStagesNothing()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var device = AddDevice(productId, status: SerialImeiStatus.InStock);
        var stock = AddStock(productId, null, 5, reserved: 0);

        // Act
        var result = await _service.ConfirmSaleAsync([DeviceLine(device)], CompletedAt, UserId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        device.Status.Should().Be(SerialImeiStatus.InStock);
        stock.Quantity.Should().Be(5);
        _stocks.Movements.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmSale_NonTrackedReservedQuantity_DeductsQuantityAndReserved()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var stock = AddStock(productId, null, 10, reserved: 3);

        // Act
        var result = await _service.ConfirmSaleAsync([QuantityLine(productId, 3)], CompletedAt, UserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        stock.Quantity.Should().Be(7);
        stock.ReservedQuantity.Should().Be(0);
        var movement = _stocks.Movements.Should().ContainSingle().Subject;
        movement.QuantityChange.Should().Be(-3);
        movement.QuantityAfter.Should().Be(7);
    }

    [Fact]
    public async Task ConfirmSale_NonTrackedNotReserved_Conflict()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var stock = AddStock(productId, null, 10, reserved: 0);

        // Act
        var result = await _service.ConfirmSaleAsync([QuantityLine(productId, 3)], CompletedAt, UserId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        stock.Quantity.Should().Be(10);
        _stocks.Movements.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmSale_ReturnsDeviceCostsByOrderItem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var costed = AddDevice(productId, status: SerialImeiStatus.Reserved, unitCost: 21_500_000m);
        var uncosted = AddDevice(productId, status: SerialImeiStatus.Reserved, unitCost: null);
        AddStock(productId, null, 5, reserved: 2);
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();

        // Act
        var result = await _service.ConfirmSaleAsync(
            [DeviceLine(costed, itemA), DeviceLine(uncosted, itemB)], CompletedAt, UserId);

        // Assert
        result.DeviceUnitCosts[itemA].Should().Be(21_500_000m);
        result.DeviceUnitCosts[itemB].Should().BeNull();
    }

    [Fact]
    public async Task ConfirmSale_LineWithoutOrderItemId_Throws()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var device = AddDevice(productId, status: SerialImeiStatus.Reserved);
        AddStock(productId, null, 5, reserved: 1);
        var line = new FulfillmentLine(null, productId, null, device.Id, 1);

        // Act
        var act = () => _service.ConfirmSaleAsync([line], CompletedAt, UserId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ------------------------------------------------------------------
    // Reserve then confirm (cash shortcut) and the serial-tracked invariant
    // ------------------------------------------------------------------

    [Fact]
    public async Task ReserveThenConfirm_OneOfFiveDevices_KeepsSerialTrackedInvariant()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var devices = Enumerable.Range(0, 5).Select(_ => AddDevice(productId)).ToList();
        var stock = AddStock(productId, null, 5);
        var line = DeviceLine(devices[0]);

        // Act
        var reserve = await _service.ReserveAsync([line]);
        var confirm = await _service.ConfirmSaleAsync([line], CompletedAt, UserId);

        // Assert
        reserve.IsSuccess.Should().BeTrue();
        confirm.IsSuccess.Should().BeTrue();
        stock.Quantity.Should().Be(4);
        stock.ReservedQuantity.Should().Be(0);
        devices.Count(d => d.Status == SerialImeiStatus.InStock).Should().Be(4);
        devices[0].Status.Should().Be(SerialImeiStatus.Sold);
    }

    [Fact]
    public async Task Reserve_ThenCaller_OnlyReservationVisibleBeforeConfirm()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var device = AddDevice(productId);
        var stock = AddStock(productId, null, 5);

        // Act
        await _service.ReserveAsync([DeviceLine(device)]);

        // Assert: quantity - reserved is what is still sellable; the device is held
        (stock.Quantity - stock.ReservedQuantity).Should().Be(4);
        device.Status.Should().Be(SerialImeiStatus.Reserved);
    }
}
