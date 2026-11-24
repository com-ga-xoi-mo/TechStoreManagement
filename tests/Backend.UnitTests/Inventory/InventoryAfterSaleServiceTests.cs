using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests.Inventory;

public class InventoryAfterSaleServiceTests
{
    private readonly FakeInventoryAfterSaleRepository _repo = new();
    private readonly FakeInventoryStockRepository _stocks = new();
    private readonly FakeSerialImeiRepository _serials = new();
    private readonly DateTime _now = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);
    private readonly Guid _actor = Guid.NewGuid();
    private readonly InventoryAfterSaleService _inventory;

    public InventoryAfterSaleServiceTests() =>
        _inventory = new(_repo, _serials, _stocks, new FixedTimeProvider(_now));

    private (Order Order, FulfillmentLine Line) AddOrder(InventoryStock stock, SerialImei? device, int quantity,
        OrderStatus status = OrderStatus.Completed)
    {
        var order = new Order { Status = status, CompletedAt = status == OrderStatus.Completed ? _now.AddDays(-7) : null };
        var item = new OrderItem { OrderId = order.Id, ProductId = stock.ProductId, VariantId = stock.VariantId,
            SerialImeiId = device?.Id, Quantity = quantity };
        order.Items.Add(item);
        _repo.Orders.Add(order);
        return (order, new FulfillmentLine(item.Id, item.ProductId, item.VariantId, item.SerialImeiId, quantity));
    }

    private InventoryStock AddStock(int quantity, int reserved = 0)
    {
        var stock = new InventoryStock { ProductId = Guid.NewGuid(), Quantity = quantity, ReservedQuantity = reserved };
        _stocks.Stocks.Add(stock);
        return stock;
    }

    private SerialImei Device(InventoryStock stock, SerialImeiStatus status)
    {
        var device = new SerialImei { ProductId = stock.ProductId, VariantId = stock.VariantId, Status = status };
        _serials.Items.Add(device);
        return device;
    }

    [Fact]
    public async Task Release_LegacyProcessingLineAndOtherHoldOnSameSku_ReleasesOnlyOwnerOnce()
    {
        var stock = AddStock(8, 5);
        var (order, line) = AddOrder(stock, null, 2, OrderStatus.Processing);
        var (other, otherLine) = AddOrder(stock, null, 3, OrderStatus.Processing);
        _repo.Add(new InventoryReservation { OrderItemId = otherLine.OrderItemId!.Value, Quantity = 3 });
        var key = Guid.NewGuid();

        Assert.True((await _inventory.CheckReleaseAsync(order.Id, [line])).IsSuccess);
        Assert.False((await _inventory.ReleaseAsync(key, order.Id, [otherLine])).IsSuccess);
        Assert.True((await _inventory.ReleaseAsync(key, order.Id, [line])).IsSuccess);
        Assert.True((await _inventory.ReleaseAsync(key, order.Id, [line])).IsSuccess);
        Assert.False((await _inventory.ReleaseAsync(key, other.Id, [otherLine])).IsSuccess);
        Assert.False((await _inventory.ReleaseAsync(Guid.NewGuid(), order.Id, [line])).IsSuccess);
        Assert.Equal((8, 3), (stock.Quantity, stock.ReservedQuantity));
        Assert.Empty(_stocks.Movements);
        Assert.Equal(ReservationState.Held, _repo.Reservations.Single(r => r.OrderItemId == otherLine.OrderItemId).State);
    }

    [Fact]
    public async Task Release_ReservedSerial_StaysInPhysicalStockAndBecomesAvailable()
    {
        var stock = AddStock(1, 1);
        var device = Device(stock, SerialImeiStatus.Reserved);
        var (order, line) = AddOrder(stock, device, 1, OrderStatus.Pending);
        Assert.False((await _inventory.ReleaseAsync(Guid.NewGuid(), order.Id, [line])).IsSuccess);
        _repo.Add(new InventoryReservation { OrderItemId = line.OrderItemId!.Value, Quantity = 1 });
        Assert.True((await _inventory.ReleaseAsync(Guid.NewGuid(), order.Id, [line])).IsSuccess);
        Assert.Equal((1, 0, SerialImeiStatus.InStock), (stock.Quantity, stock.ReservedQuantity, device.Status));
        Assert.Empty(_stocks.Movements);
    }

    [Theory]
    [InlineData(ReturnDisposition.Restock, SerialImeiStatus.InStock, 1)]
    [InlineData(ReturnDisposition.Defective, SerialImeiStatus.Defective, 0)]
    [InlineData(ReturnDisposition.UnderRepair, SerialImeiStatus.UnderRepair, 0)]
    public async Task Inspect_SerialOutcomeAndRetries_StockAndLedgerMatchDisposition(
        ReturnDisposition outcome, SerialImeiStatus status, int quantity)
    {
        var stock = AddStock(0);
        var device = Device(stock, SerialImeiStatus.Sold);
        var (order, line) = AddOrder(stock, device, 1);
        var inspectionId = Guid.NewGuid();
        var returnId = Guid.NewGuid();
        var refundId = Guid.NewGuid();

        Assert.True((await _inventory.CheckReturnAsync(order.Id, [line])).IsSuccess);
        Assert.Equal(SerialImeiStatus.Sold, device.Status);
        Assert.True((await _inventory.ReceiveReturnAsync(refundId, order.Id, [line])).IsSuccess);
        Assert.True((await _inventory.ReceiveReturnAsync(refundId, order.Id, [line])).IsSuccess);
        Assert.Equal(SerialImeiStatus.Returned, device.Status);
        Assert.Empty(_stocks.Movements);

        var result = await _inventory.InspectAsync(inspectionId, returnId, line, outcome, _actor);
        Assert.True(result.IsSuccess);
        Assert.Equal(result, await _inventory.InspectAsync(inspectionId, returnId, line, outcome, _actor));
        Assert.False((await _inventory.InspectAsync(Guid.NewGuid(), returnId, line, outcome, _actor)).IsSuccess);
        Assert.Equal((quantity, status), (stock.Quantity, device.Status));
        Assert.Equal(quantity, _stocks.Movements.Count);
        if (quantity == 1)
        {
            var movement = Assert.Single(_stocks.Movements);
            Assert.Equal((InventoryMovementType.CustomerReturn, returnId, _now, 1, result.MovementId),
                (movement.MovementType, movement.OrderReturnId!.Value, movement.CreatedAt, movement.QuantityAfter, movement.Id));
        }
        else
        {
            var serialService = new SerialImeiService(_serials, _stocks, new FakeProductRepository(), new FakeUnitOfWork());
            Assert.False((await serialService.RestoreAsync(device.Id, new RestoreDeviceRequest(), _actor)).IsSuccess);
            Assert.False((await serialService.ReportDefectiveAsync(device.Id, new() { Reason = "test" }, _actor)).IsSuccess);
            Assert.Empty(_stocks.Movements);
        }
    }

    [Fact]
    public async Task Inspect_ThreeQuantitySlices_AddsOnlyResellablePortion()
    {
        var stock = AddStock(4);
        var (order, line) = AddOrder(stock, null, 3);
        Assert.True((await _inventory.ReceiveReturnAsync(Guid.NewGuid(), order.Id, [line])).IsSuccess);
        foreach (var outcome in Enum.GetValues<ReturnDisposition>())
        {
            var operation = Guid.NewGuid();
            var slice = Guid.NewGuid();
            var part = line with { Quantity = 1 };
            var success = await _inventory.InspectAsync(operation, slice, part, outcome, _actor);
            Assert.True(success.IsSuccess);
            Assert.Equal(success, await _inventory.InspectAsync(operation, slice, part, outcome, _actor));
            Assert.False((await _inventory.InspectAsync(operation, slice, line, outcome, _actor)).IsSuccess);
        }
        Assert.Equal(5, stock.Quantity);
        Assert.Equal(1, Assert.Single(_stocks.Movements).QuantityChange);
    }

    [Fact]
    public async Task Warranty_RepairAndReturn_RequireSameCaseAndPreserveWarranty()
    {
        var stock = AddStock(0);
        var device = Device(stock, SerialImeiStatus.Sold);
        var (order, line) = AddOrder(stock, device, 1);
        device.WarrantyEndAt = _now.AddMonths(3);
        var caseId = Guid.NewGuid();
        Task<InventoryOperationResult> Act(Guid key, Guid claim, WarrantyAction action) =>
            _inventory.ApplyWarrantyAsync(key, claim, order.Id, line.OrderItemId!.Value, device.Id, action, null, _actor);
        var receive = Guid.NewGuid();
        var deliver = Guid.NewGuid();

        _repo.IsAwaitingBank = true;
        Assert.False((await Act(receive, caseId, WarrantyAction.ReceiveForRepair)).IsSuccess);
        _repo.IsAwaitingBank = false;
        Assert.True((await Act(receive, caseId, WarrantyAction.ReceiveForRepair)).IsSuccess);
        Assert.True((await Act(receive, caseId, WarrantyAction.ReceiveForRepair)).IsSuccess);
        Assert.Equal(SerialImeiStatus.UnderRepair, device.Status);
        Assert.False((await _inventory.ReceiveReturnAsync(Guid.NewGuid(), order.Id, [line])).IsSuccess);
        Assert.False((await Act(deliver, Guid.NewGuid(), WarrantyAction.ReturnToCustomer)).IsSuccess);
        Assert.True((await Act(deliver, caseId, WarrantyAction.ReturnToCustomer)).IsSuccess);
        Assert.True((await Act(deliver, caseId, WarrantyAction.ReturnToCustomer)).IsSuccess);
        Assert.Equal(SerialImeiStatus.Sold, device.Status);
        Assert.Equal(_now.AddMonths(3), device.WarrantyEndAt);
        Assert.Equal(0, stock.Quantity);
        Assert.Empty(_stocks.Movements);
    }

    [Fact]
    public async Task Warranty_Replacement_ChecksVariantTransfersWarrantyAndReturnsSameMovementAndCost()
    {
        var stock = AddStock(1);
        var old = Device(stock, SerialImeiStatus.Sold);
        var replacement = Device(stock, SerialImeiStatus.InStock);
        var (order, line) = AddOrder(stock, old, 1);
        old.WarrantyStartAt = _now.AddMonths(-5);
        old.WarrantyEndAt = _now.AddMonths(7);
        replacement.UnitCost = 25_000m;
        var caseId = Guid.NewGuid();
        var operation = Guid.NewGuid();
        Task<InventoryOperationResult> Replace() => _inventory.ApplyWarrantyAsync(operation, caseId, order.Id, line.OrderItemId!.Value,
            old.Id, WarrantyAction.Replace, replacement.Id, _actor);

        replacement.VariantId = Guid.NewGuid();
        Assert.False((await Replace()).IsSuccess);
        replacement.VariantId = stock.VariantId;
        var success = await Replace();
        Assert.True(success.IsSuccess);
        Assert.Equal(success, await Replace());
        Assert.Equal(25_000m, success.UnitCost);
        Assert.Equal(success.MovementId, Assert.Single(_stocks.Movements).Id);
        Assert.Equal((old.WarrantyStartAt, old.WarrantyEndAt), (replacement.WarrantyStartAt, replacement.WarrantyEndAt));
        Assert.Equal((SerialImeiStatus.Defective, SerialImeiStatus.Sold), (old.Status, replacement.Status));
        Assert.Equal(_serials.Items.Count(x => x.Status is SerialImeiStatus.InStock or SerialImeiStatus.Reserved), stock.Quantity);
        var serialService = new SerialImeiService(_serials, _stocks, new FakeProductRepository(), new FakeUnitOfWork());
        Assert.False((await serialService.RestoreAsync(old.Id, new(), _actor)).IsSuccess);
        Assert.Equal(0, stock.Quantity);
    }

    [Fact]
    public async Task Receive_DeviceResold_DoesNotAcceptPreviousSale()
    {
        var stock = AddStock(0);
        var device = Device(stock, SerialImeiStatus.Sold);
        var (oldOrder, oldLine) = AddOrder(stock, device, 1);
        var (_, newLine) = AddOrder(stock, device, 1);
        device.SaleOrderItemId = newLine.OrderItemId;
        Assert.False((await _inventory.ReceiveReturnAsync(Guid.NewGuid(), oldOrder.Id, [oldLine])).IsSuccess);
        Assert.Equal(SerialImeiStatus.Sold, device.Status);
    }
}
