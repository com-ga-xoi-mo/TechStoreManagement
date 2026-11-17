using FluentAssertions;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

// PrepareAsync: validates and prices a request without storing anything.
public partial class OrderServiceTests
{
    [Fact]
    public async Task PrepareAsync_ValidRequest_BuildsUnsavedPendingOrderPricedFromCatalog()
    {
        // Arrange
        var product = AddProduct();
        var request = RequestOf(TrackedItem(product));

        // Act
        var result = await _service.PrepareAsync(request, ActingUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var order = result.Build!(Now);
        order.Status.Should().Be(OrderStatus.Pending);
        order.CashierUserId.Should().Be(ActingUserId);
        order.TotalAmount.Should().Be(30_000_000m);
        order.Items.Should().ContainSingle();
        _orders.AddedOrders.Should().BeEmpty();
        _unitOfWork.BeginTransactionCount.Should().Be(0);
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task PrepareAsync_EachBuildReturnsAFreshOrder()
    {
        // Arrange
        var product = AddProduct();
        var result = await _service.PrepareAsync(RequestOf(TrackedItem(product)), ActingUserId);

        // Act
        var first = result.Build!(Now);
        var second = result.Build!(Now);

        // Assert
        second.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public async Task PrepareAsync_EmptyItems_ReturnsValidationErrors()
    {
        // Act
        var result = await _service.PrepareAsync(RequestOf(), ActingUserId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_WithoutAvailabilityCheck_DoesNotCallInventory()
    {
        // Arrange
        var product = AddProduct();
        _fulfillment.CheckProblems.Add(new TechStore.Api.Inventory.Services.FulfillmentProblem(0, "sold"));

        // Act
        var result = await _service.PrepareAsync(RequestOf(TrackedItem(product)), ActingUserId, checkAvailability: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _fulfillment.CheckCallCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_DraftOrder_HasNoPosFields()
    {
        // Arrange
        var product = AddProduct();

        // Act
        var result = await _service.CreateAsync(RequestOf(TrackedItem(product)), ActingUserId);

        // Assert
        result.Data!.PosSessionId.Should().BeNull();
        result.Data.CashTendered.Should().BeNull();
        result.Data.ChangeGiven.Should().BeNull();
    }
}
