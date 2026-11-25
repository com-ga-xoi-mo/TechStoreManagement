using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public sealed partial class PosCheckoutPostgresHttpTests
{
    [PostgresFact]
    public async Task ReturnCash_TwoPartialRefunds_RealLedgerPreservesOpeningBalanceAndManagerDrawer()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(1);
        var cashierShift = await world.OpenShiftAsync(world.SalesA, 0m);
        await using var db = world.Database.CreateContext();
        var stock = await AddQuantityStock(world, db, 0);
        var receipt = await PostDto<PurchaseReceiptDto>(world.Warehouse, "/api/inventory/purchase-receipts",
            new CreatePurchaseReceiptRequest
            {
                ClientRequestId = Guid.NewGuid(), SupplierId = await db.Suppliers.Where(s => s.IsActive).Select(s => s.Id).FirstAsync(),
                Lines = [new() { ProductId = stock.ProductId, Quantity = 2, UnitCost = 600_000m }]
            });
        Assert.Equal(PurchaseOrderStatus.Received, receipt.Status);
        Assert.Equal(1_200_000m, receipt.TotalCost);
        var customer = new Customer { FullName = "Refund fixture", Phone = "0900003091", LoyaltyPoints = 491 };
        db.Add(customer);
        db.CustomerLoyaltyPoints.Add(new() { CustomerId = customer.Id, PointsChange = 491, BalanceAfter = 491, Reason = "Legacy opening" });
        await db.SaveChangesAsync();
        var checkout = new PosCheckoutRequest
        {
            CustomerId = customer.Id, PosSessionId = cashierShift.Id, CheckoutKey = Guid.NewGuid(), OrderDiscountAmount = 200_000m,
            CashTendered = 1_980_000m, Items = [new() { ProductId = stock.ProductId, Quantity = 2 }]
        };

        // Act and assert: checkout replay is the same sale and credits eighteen points once.
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", checkout);
        Assert.Equal(sale.Id, (await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", checkout)).Id);
        Assert.Equal(1_980_000m, sale.TotalAmount);
        var credit = await db.CustomerLoyaltyPoints.AsNoTracking().SingleAsync(p => p.OrderId == sale.Id);
        Assert.True(credit.IsSaleAccrual);
        Assert.Equal((18, 509), (credit.PointsChange, credit.BalanceAfter));
        Assert.Equal(CustomerTier.Silver, await db.Customers.Where(c => c.Id == customer.Id).Select(c => c.Tier).SingleAsync());
        var first = await PostDto<ReturnDto>(world.SalesA.Client, "/api/returns", ReturnFor(sale, 1));
        var cash = new SettleReturnRequest("Cash");
        Assert.Equal(HttpStatusCode.Conflict, await PostStatus(world.Manager, $"/api/returns/{first.Id}/settle", cash));
        var drawer = await world.OpenShiftAsync(new PosWorld.Seller(world.Manager, world.ManagerUserId), 2_000_000m);

        // Each successful refund is replayed. Read the DB independently after each commit.
        async Task VerifyRefund(HttpClient manager, ReturnDto request, int balance, CustomerTier tier, OrderStatus status)
        {
            var path = $"/api/returns/{request.Id}/settle";
            var paid = await PostDto<ReturnDto>(manager, path, cash);
            Assert.Equal(paid.RefundedAt, (await PostDto<ReturnDto>(manager, path, cash)).RefundedAt);
            var allocation = Assert.Single(paid.Lines);
            Assert.Equal((900_000m, 100_000m, 90_000m), (allocation.NetAmount, allocation.DiscountAmount, allocation.TaxAmount));
            var payout = await db.Set<CashRefundPayout>().AsNoTracking().SingleAsync(p => p.RefundId == request.Id);
            Assert.Equal(drawer.Id, payout.ShiftId);
            Assert.Equal(990_000m, payout.Amount);
            var entry = await db.CustomerLoyaltyPoints.AsNoTracking().SingleAsync(p => p.RefundCaseId == request.Id);
            Assert.Equal((-9, balance), (entry.PointsChange, entry.BalanceAfter));
            var member = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == customer.Id);
            Assert.Equal((balance, tier), (member.LoyaltyPoints, member.Tier));
            Assert.Equal(status, await db.Orders.Where(o => o.Id == sale.Id).Select(o => o.Status).SingleAsync());
            var profile = (await manager.GetFromJsonAsync<CustomerDto>($"/api/customers/{customer.Id}"))!;
            Assert.Equal((1, 1, sale.TotalAmount, sale.CompletedAt),
                (profile.OrderCount, profile.CompletedOrderCount, profile.TotalSpent, profile.LastPurchaseAt));
            var page = await manager.GetFromJsonAsync<PagedResultDto<CustomerSummaryDto>>($"/api/customers?q={customer.Phone}");
            var summary = Assert.Single(page!.Items);
            Assert.Equal((sale.TotalAmount, sale.CompletedAt), (summary.TotalSpent, summary.LastPurchaseAt));
            var items = await manager.GetFromJsonAsync<PagedResultDto<CustomerPurchasedItemDto>>($"/api/customers/{customer.Id}/purchased-items");
            Assert.Equal(sale.Id, Assert.Single(items!.Items).OrderId);
        }
        await VerifyRefund(world.Manager, first, 500, CustomerTier.Silver, OrderStatus.PartiallyRefunded);

        // Calculate gross profit directly from historical sale and refund amounts and physically restocked units.
        var sold = await db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == sale.Id);
        Assert.Equal(600_000m, sold.UnitCost);
        async Task<decimal> GrossProfit()
        {
            var refundedNet = await db.OrderReturns.Where(r => r.OrderItemId == sold.Id).SumAsync(r => r.NetAmount);
            var restocked = await (from move in db.InventoryMovements
                                   join refund in db.OrderReturns on move.OrderReturnId equals refund.Id
                                   where refund.OrderItemId == sold.Id && move.MovementType == InventoryMovementType.CustomerReturn
                                   select move.QuantityChange).SumAsync();
            var net = sold.TotalPrice - sold.OrderDiscountAmount - refundedNet!.Value;
            return net - (sold.Quantity - restocked) * sold.UnitCost!.Value;
        }
        Assert.Equal(-300_000m, await GrossProfit());

        var paidAt = await db.OrderReturns.Where(r => r.OrderId == sale.Id).Select(r => r.RefundedAt).SingleAsync();
        var nextDay = new DateTimeOffset(paidAt).AddDays(1);
        await using var later = world.Api.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(nextDay))));
        var options = new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") };
        using var warehouse = later.CreateClient(options);
        warehouse.DefaultRequestHeaders.Authorization = world.Warehouse.DefaultRequestHeaders.Authorization;
        using var managerNextDay = later.CreateClient(options);
        managerNextDay.DefaultRequestHeaders.Authorization = world.Manager.DefaultRequestHeaders.Authorization;
        var pending = await warehouse.GetFromJsonAsync<List<ReturnInspectionDto>>("/api/inventory/returns");
        var inspectLine = Assert.Single(pending!, line => line.RequestId == first.Id);
        await PostDto<ReturnInspectionDto>(warehouse, $"/api/inventory/returns/{inspectLine.ReturnLineId}/inspections",
            new InspectReturnRequest(Guid.NewGuid(), 1, "InStock"));
        Assert.Equal(300_000m, await GrossProfit());
        var movement = await db.InventoryMovements.SingleAsync(m => m.InventoryStockId == stock.Id && m.MovementType == InventoryMovementType.CustomerReturn);
        Assert.Equal(nextDay.UtcDateTime, movement.CreatedAt);
        Assert.Equal(1, movement.QuantityChange);
        var last = await PostDto<ReturnDto>(world.Manager, "/api/returns", ReturnFor(sale, 1));
        await VerifyRefund(managerNextDay, last, 491, CustomerTier.Standard, OrderStatus.Refunded);
        Assert.Equal(4, await db.CustomerLoyaltyPoints.CountAsync(p => p.CustomerId == customer.Id));
        Assert.Equal(491, await db.CustomerLoyaltyPoints.Where(p => p.CustomerId == customer.Id && p.OrderId == null).Select(p => p.PointsChange).SingleAsync());
        var closing = await PostDto<PosShiftDto>(managerNextDay, $"/api/pos/shifts/{drawer.Id}/close", new CloseShiftRequest { ClosingBalance = 20_000m });
        Assert.Equal(20_000m, closing.ExpectedClosingBalance);
        Assert.Equal(0m, closing.Discrepancy);
        var originalDrawer = await db.PosSessions.AsNoTracking().SingleAsync(s => s.Id == cashierShift.Id);
        Assert.Equal(1_980_000m, originalDrawer.CashSalesTotal);
        Assert.Equal(0m, originalDrawer.CashRefundTotal);
    }

    [PostgresFact]
    public async Task ReturnLoyalty_OrdersApi_OnlyDeliveryEarnsPointsAndProcessingRefundPreservesThem()
    {
        // Arrange
        await using var world = await PosWorld.CreateAsync(2);
        await using var db = world.Database.CreateContext();
        var customer = new Customer { FullName = "Orders fixture", Phone = "0900003092" };
        db.Add(customer);
        await db.SaveChangesAsync();
        async Task<OrderDto> Reserve(Guid device)
        {
            var draft = await PostDto<OrderDto>(world.SalesA.Client, "/api/orders", new CreateOrderRequest
            {
                CustomerId = customer.Id, PaymentMethod = "Cash",
                Items = [new() { ProductId = world.ProductId, SerialImeiId = device, Quantity = 1 }]
            });
            using var reserved = await world.SalesA.Client.PatchAsJsonAsync($"/api/orders/{draft.Id}/status", new UpdateOrderStatusRequest { TargetStatus = "Processing" });
            reserved.EnsureSuccessStatusCode();
            Assert.False(await db.CustomerLoyaltyPoints.AnyAsync(p => p.OrderId == draft.Id));
            return draft;
        }

        // Act
        var delivered = await Reserve(world.DeviceIds[0]);
        using var complete = await world.SalesA.Client.PatchAsJsonAsync($"/api/orders/{delivered.Id}/status", new UpdateOrderStatusRequest { TargetStatus = "Completed" });
        complete.EnsureSuccessStatusCode();
        var cancelled = await Reserve(world.DeviceIds[1]);
        var request = await PostDto<ReturnDto>(world.Manager, "/api/returns", ReturnFor(cancelled, 1));
        var path = $"/api/returns/{request.Id}";
        await PostDto<ReturnDto>(world.Manager, path + "/settle", new SettleReturnRequest("VietQr"));
        await PostDto<ReturnDto>(world.Manager, path + "/reconcile", new ReconcileReturnRequest("cancel-pre-delivery", Total, true, DateTimeOffset.UtcNow));

        // Assert
        var credit = await db.CustomerLoyaltyPoints.AsNoTracking().SingleAsync(p => p.CustomerId == customer.Id);
        Assert.Equal(delivered.Id, credit.OrderId);
        Assert.Equal((100, 100), (credit.PointsChange, credit.BalanceAfter));
        Assert.Equal(100, await db.Customers.Where(c => c.Id == customer.Id).Select(c => c.LoyaltyPoints).SingleAsync());
        var items = await world.Manager.GetFromJsonAsync<PagedResultDto<CustomerPurchasedItemDto>>($"/api/customers/{customer.Id}/purchased-items");
        var original = Assert.Single(items!.Items);
        Assert.Equal(delivered.Id, original.OrderId);
        Assert.Equal(world.DeviceIds[0], original.SerialImeiId);
        var profile = (await world.Manager.GetFromJsonAsync<CustomerDto>($"/api/customers/{customer.Id}"))!;
        Assert.Equal((2, 1, Total, original.CompletedAt),
            (profile.OrderCount, profile.CompletedOrderCount, profile.TotalSpent, profile.LastPurchaseAt));
    }
}
