using FluentAssertions;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

// UC-ORD-04: UpdateStatusAsync
public partial class OrderServiceTests
{
    [Fact]
    public async Task UpdateStatusAsync_PendingToProcessing_ReservesAndMarksPaid()
    {
        // Arrange
        var (order, _, _) = AddOrder(OrderStatus.Pending);

        // Act
        var result = await _service.UpdateStatusAsync(order.Id, Target("Processing"), ActingUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be("Processing");
        order.Status.Should().Be(OrderStatus.Processing);
        order.PaidAt.Should().Be(Now);
        order.CompletedAt.Should().BeNull();
        _fulfillment.CallLog.Should().Equal("reserve");
        _unitOfWork.BeginTransactionCount.Should().Be(1);
        _unitOfWork.SaveChangesCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);
        _unitOfWork.RollbackCount.Should().Be(0);
        _orders.GetForUpdateCallCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStatusAsync_ProcessingToCompleted_ConfirmsSaleAndKeepsPaidAt()
    {
        // Arrange
        var paidEarlier = Now.AddMinutes(-30);
        var (order, item, _) = AddOrder(OrderStatus.Processing, paidAt: paidEarlier);

        // Act
        var result = await _service.UpdateStatusAsync(order.Id, Target("completed"), ActingUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Completed);
        order.PaidAt.Should().Be(paidEarlier);
        order.CompletedAt.Should().Be(Now);
        _fulfillment.CallLog.Should().Equal("confirm");
        _fulfillment.LastCompletedAt.Should().Be(Now);
        _fulfillment.LastPerformedBy.Should().Be(ActingUserId);
        _fulfillment.LastConfirmedLines!.Single().OrderItemId.Should().Be(item.Id);
    }

    [Fact]
    public async Task UpdateStatusAsync_Completion_UsesDeviceCostWhenAvailable()
    {
        // Arrange
        var (order, item, _) = AddOrder(OrderStatus.Processing, paidAt: Now, costPrice: 22_000_000m);
        _fulfillment.DeviceCostsByOrderItem[item.Id] = 21_500_000m;

        // Act
        await _service.UpdateStatusAsync(order.Id, Target("Completed"), ActingUserId);

        // Assert
        item.UnitCost.Should().Be(21_500_000m);
    }

    [Fact]
    public async Task UpdateStatusAsync_Completion_FallsBackToProductCostWhenDeviceHasNone()
    {
        // Arrange
        var (order, item, _) = AddOrder(OrderStatus.Processing, paidAt: Now, costPrice: 22_000_000m);
        _fulfillment.DeviceCostsByOrderItem[item.Id] = null;

        // Act
        await _service.UpdateStatusAsync(order.Id, Target("Completed"), ActingUserId);

        // Assert
        item.UnitCost.Should().Be(22_000_000m);
    }

    [Fact]
    public async Task UpdateStatusAsync_CashShortcut_ReservesThenConfirmsInOneTransaction()
    {
        // Arrange
        var (order, item, _) = AddOrder(OrderStatus.Pending);
        _fulfillment.DeviceCostsByOrderItem[item.Id] = 21_500_000m;

        // Act
        var result = await _service.UpdateStatusAsync(order.Id, Target("Completed"), ActingUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fulfillment.CallLog.Should().Equal("reserve", "confirm");
        order.Status.Should().Be(OrderStatus.Completed);
        order.PaidAt.Should().Be(Now);
        order.CompletedAt.Should().Be(Now);
        order.PaidAt.Should().Be(order.CompletedAt);
        item.UnitCost.Should().Be(21_500_000m);
        _unitOfWork.BeginTransactionCount.Should().Be(1);
        _unitOfWork.SaveChangesCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStatusAsync_PendingToCancelled_TouchesNoInventory()
    {
        // Arrange
        var (order, _, _) = AddOrder(OrderStatus.Pending);

        // Act
        var result = await _service.UpdateStatusAsync(order.Id, Target("Cancelled"), ActingUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.PaidAt.Should().BeNull();
        _fulfillment.CallLog.Should().BeEmpty();
        _unitOfWork.SaveChangesCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStatusAsync_ProcessingToCancelled_IsAConflictAndChangesNothing()
    {
        // Arrange
        var (order, _, _) = AddOrder(OrderStatus.Processing, paidAt: Now);

        // Act
        var result = await _service.UpdateStatusAsync(order.Id, Target("Cancelled"), ActingUserId);

        // Assert
        result.IsConflict.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Processing);
        _unitOfWork.SaveChangesCount.Should().Be(0);
        _unitOfWork.RollbackCount.Should().Be(1);
        _fulfillment.CallLog.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateStatusAsync_CompletedToCompleted_IsAConflictWithNoSecondSale()
    {
        // Arrange
        var (order, _, _) = AddOrder(OrderStatus.Completed, paidAt: Now);

        // Act
        var result = await _service.UpdateStatusAsync(order.Id, Target("Completed"), ActingUserId);

        // Assert
        result.IsConflict.Should().BeTrue();
        _fulfillment.CallLog.Should().BeEmpty();
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownOrder_ReturnsNotFoundAndRollsBack()
    {
        // Act
        var result = await _service.UpdateStatusAsync(Guid.NewGuid(), Target("Completed"), ActingUserId);

        // Assert
        result.Found.Should().BeFalse();
        _unitOfWork.RollbackCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Theory]
    [InlineData("Refunded")]
    [InlineData("Pending")]
    [InlineData("nonsense")]
    [InlineData("1")]
    [InlineData(null)]
    public async Task UpdateStatusAsync_DisallowedTargetStatus_ReportsTargetStatusWithoutOpeningATransaction(string? status)
    {
        // Arrange
        var (order, _, _) = AddOrder(OrderStatus.Pending);

        // Act
        var result = await _service.UpdateStatusAsync(order.Id, Target(status), ActingUserId);

        // Assert
        result.ValidationErrors.Should().ContainKey("targetStatus");
        _unitOfWork.BeginTransactionCount.Should().Be(0);
        _orders.GetForUpdateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateStatusAsync_ProductDeactivatedBeforePayment_IsAConflictAndReservesNothing()
    {
        // Arrange
        var (order, _, _) = AddOrder(OrderStatus.Pending, productActive: false);

        // Act
        var result = await _service.UpdateStatusAsync(order.Id, Target("Processing"), ActingUserId);

        // Assert
        result.IsConflict.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Pending);
        _fulfillment.ReserveCallCount.Should().Be(0);
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStatusAsync_ProductDeactivatedAfterPayment_DoesNotBlockCompletion()
    {
        // Arrange
        var (order, _, _) = AddOrder(OrderStatus.Processing, productActive: false, paidAt: Now);

        // Act
        var result = await _service.UpdateStatusAsync(order.Id, Target("Completed"), ActingUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateStatusAsync_ReserveConflict_RollsBackAndLeavesOrderPending()
    {
        // Arrange
        var (order, _, _) = AddOrder(OrderStatus.Pending);
        _fulfillment.ReserveResult = FulfillmentResult.Failed(new FulfillmentProblem(0, "Device is Sold; expected InStock."));

        // Act
        var result = await _service.UpdateStatusAsync(order.Id, Target("Completed"), ActingUserId);

        // Assert
        result.IsConflict.Should().BeTrue();
        result.ConflictReason.Should().Contain("IP15P").And.Contain("Sold");
        order.Status.Should().Be(OrderStatus.Pending);
        order.PaidAt.Should().BeNull();
        _fulfillment.ConfirmCallCount.Should().Be(0);
        _unitOfWork.SaveChangesCount.Should().Be(0);
        _unitOfWork.RollbackCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateStatusAsync_ConfirmConflictAfterReserve_RollsBackAndLeavesOrderUntouched()
    {
        // Arrange
        var (order, item, _) = AddOrder(OrderStatus.Pending);
        _fulfillment.ConfirmConflict = new FulfillmentProblem(
            0, "Stock is not reserved for this sale.", FulfillmentProblemKind.InsufficientStock);

        // Act
        var result = await _service.UpdateStatusAsync(order.Id, Target("Completed"), ActingUserId);

        // Assert
        result.IsConflict.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Pending);
        order.PaidAt.Should().BeNull();
        order.CompletedAt.Should().BeNull();
        item.UnitCost.Should().BeNull();
        _unitOfWork.SaveChangesCount.Should().Be(0);
        _unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStatusAsync_SaveFailure_RollsBackAndPropagatesTheException()
    {
        // Arrange
        var (order, _, _) = AddOrder(OrderStatus.Pending);
        _unitOfWork.SaveExceptionQueue.Enqueue(new InvalidOperationException("boom"));

        // Act
        var act = () => _service.UpdateStatusAsync(order.Id, Target("Processing"), ActingUserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.RollbackCount.Should().Be(1);
        _unitOfWork.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateStatusAsync_PassesTheOrderLinesToInventory()
    {
        // Arrange
        var (order, item, product) = AddOrder(OrderStatus.Pending);

        // Act
        await _service.UpdateStatusAsync(order.Id, Target("Processing"), ActingUserId);

        // Assert
        var line = _fulfillment.LastReservedLines!.Should().ContainSingle().Subject;
        line.OrderItemId.Should().Be(item.Id);
        line.ProductId.Should().Be(product.Id);
        line.SerialImeiId.Should().Be(item.SerialImeiId);
        line.Quantity.Should().Be(1);
    }
}
