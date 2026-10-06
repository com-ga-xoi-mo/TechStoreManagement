using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

public class OrderEntityTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy_test;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void OrderEntities_Should_Initialize_With_Defaults()
    {
        var order = new Order
        {
            OrderCode = "ORD-202610-0001",
            CashierUserId = Guid.NewGuid(),
            Subtotal = 1000000m,
            TotalAmount = 1000000m
        };
        order.Id.Should().NotBeEmpty();
        order.Status.Should().Be(OrderStatus.Pending);
        order.PaymentMethod.Should().Be(PaymentMethod.Cash);
        order.Items.Should().NotBeNull();
        order.Returns.Should().NotBeNull();

        var item = new OrderItem
        {
            OrderId = order.Id,
            ProductId = Guid.NewGuid(),
            ProductName = "iPhone 15 Pro Max",
            Sku = "IP15PM-256",
            UnitPrice = 30000000m,
            TotalPrice = 30000000m,
            Quantity = 1
        };
        item.Id.Should().NotBeEmpty();
        item.DiscountAmount.Should().Be(0);

        var orderReturn = new OrderReturn
        {
            OrderId = order.Id,
            OrderItemId = item.Id,
            ProcessedByUserId = Guid.NewGuid(),
            Quantity = 1,
            Reason = "Defective screen",
            RefundAmount = 30000000m,
            RefundMethod = PaymentMethod.Cash
        };
        orderReturn.Id.Should().NotBeEmpty();
        orderReturn.IsRestocked.Should().BeFalse();
    }

    [Fact]
    public void OrderModel_Should_Configure_Tables_And_CompositeKeys()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        var orderEntity = model.FindEntityType(typeof(Order));
        orderEntity.Should().NotBeNull();
        orderEntity!.GetTableName().Should().Be("orders");

        var orderItemEntity = model.FindEntityType(typeof(OrderItem));
        orderItemEntity.Should().NotBeNull();
        orderItemEntity!.GetTableName().Should().Be("order_items");

        var orderReturnEntity = model.FindEntityType(typeof(OrderReturn));
        orderReturnEntity.Should().NotBeNull();
        orderReturnEntity!.GetTableName().Should().Be("order_returns");

        var itemOrderUnique = orderItemEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "uq_order_items_id_order");
        itemOrderUnique.Should().NotBeNull();
        itemOrderUnique!.IsUnique.Should().BeTrue();
    }
}
