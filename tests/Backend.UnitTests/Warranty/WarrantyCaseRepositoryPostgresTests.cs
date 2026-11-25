using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Warranty.Entities;
using TechStore.Api.Warranty.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public sealed partial class PosCheckoutPostgresHttpTests
{
    [PostgresFact]
    public async Task WarrantyRepository_FindSale_UsesCurrentOwnerAndFallsBackForLegacyDevices()
    {
        await using var world = await PosWorld.CreateAsync(3);
        await using var db = world.Database.CreateContext();
        var devices = await db.SerialImeis.Where(d => world.DeviceIds.Contains(d.Id)).OrderBy(d => d.Imei).ToListAsync();
        var formerOwner = new Customer { FullName = "Former owner", Phone = "0900000101" };
        var currentOwner = new Customer { FullName = "Current owner", Phone = "0900000102" };
        var originalOwner = new Customer { FullName = "Original owner", Phone = "0900000103" };
        db.AddRange(formerOwner, currentOwner, originalOwner);
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        OrderItem Sale(int serial, string code, Customer owner, OrderStatus status, DateTime? completedAt)
        {
            var order = new Order
            {
                OrderCode = code,
                CustomerId = owner.Id,
                CashierUserId = world.ManagerUserId,
                Status = status,
                CompletedAt = completedAt
            };
            var item = new OrderItem
            {
                Order = order,
                ProductId = world.ProductId,
                SerialImeiId = devices[serial].Id,
                ProductName = "Warranty product",
                Sku = "WARRANTY"
            };
            db.Add(item);
            return item;
        }

        Sale(0, "OLD-SALE", formerOwner, OrderStatus.Refunded, start);
        var current = Sale(0, "CURRENT-SALE", currentOwner, OrderStatus.Completed, start.AddDays(1));
        Sale(1, "REPLACEMENT-PRIOR-SALE", formerOwner, OrderStatus.Refunded, start.AddDays(2));
        var original = Sale(0, "REPLACEMENT-ORIGINAL-SALE", originalOwner, OrderStatus.Completed, start);
        Sale(2, "LEGACY-CANCELLED", formerOwner, OrderStatus.Cancelled, null);
        var legacy = Sale(2, "LEGACY-DELIVERED", currentOwner, OrderStatus.Completed, start);
        devices[0].SaleOrderItemId = current.Id;
        devices[1].SaleOrderItemId = original.Id;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repo = new WarrantyCaseRepository(db, new InventoryAfterSaleRepository(db));
        foreach (var (index, expected) in new[] { (0, current), (1, original), (2, legacy) })
        {
            var device = await repo.FindDeviceByCodeAsync(devices[index].Imei!, default);
            Assert.NotNull(device);
            var result = await repo.FindSaleAsync(device, default);
            Assert.NotNull(result);
            Assert.Equal(expected.Id, result.Id);
            Assert.Equal(expected.Order.CustomerId, result.Order.Customer!.Id);
        }
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [PostgresFact]
    public async Task WarrantyRepository_SearchTreatsPatternCharactersLiterally_AndDatabasePreventsSecondOpenCase()
    {
        await using var world = await PosWorld.CreateAsync(2);
        var shift = await world.OpenShiftAsync(world.SalesA, 0m);
        var checkout = Checkout(world, shift.Id, world.DeviceIds[0], 2 * Total);
        checkout.Items!.Add(new() { ProductId = world.ProductId, SerialImeiId = world.DeviceIds[1], Quantity = 1 });
        var sale = await PostDto<OrderDto>(world.SalesA.Client, "/api/pos/checkout", checkout);
        await using var db = world.Database.CreateContext();
        var repo = new WarrantyCaseRepository(db, new InventoryAfterSaleRepository(db));
        var work = new UnitOfWork(db);
        var now = DateTime.UtcNow;
        var devices = await db.SerialImeis.Where(d => world.DeviceIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);

        WarrantyCase AddClaim(int serial, string code, string phone)
        {
            var device = devices[world.DeviceIds[serial]];
            var item = sale.Items.Single(i => i.SerialImeiId == device.Id);
            var caseId = Guid.NewGuid();
            var op = Guid.NewGuid();
            db.Add(InventoryOperationLog.Warranty(op, caseId, sale.Id, item.Id, device.Id,
                WarrantyAction.ReceiveForRepair, null, world.ManagerUserId, now));
            var claim = new WarrantyCase
            {
                Id = caseId,
                CaseCode = code,
                OrderId = sale.Id,
                OrderItemId = item.Id,
                DeviceId = device.Id,
                OpenedById = world.ManagerUserId,
                OpenedAt = now,
                WarrantyStartAt = device.WarrantyStartAt!.Value,
                WarrantyEndAt = device.WarrantyEndAt!.Value,
                ReceiveOperationId = op,
                Status = WarrantyCaseStatus.Received,
                ContactPhone = phone
            };
            repo.Add(claim);
            return claim;
        }

        var literal = AddClaim(0, "BH-202611-0001", "123%_456");
        AddClaim(1, "BH-202611-0002", "12300456");
        repo.AddEvent(new WarrantyCaseEvent
        {
            CaseId = literal.Id,
            ActorId = world.ManagerUserId,
            ToStatus = WarrantyCaseStatus.Received,
            CreatedAt = now
        });
        await work.SaveChangesAsync();

        foreach (var search in new[] { "%", "_" })
        {
            var result = await repo.SearchAsync(new(WarrantyCaseStatus.Received, search, 1, 20), default);
            Assert.Equal(1, result.TotalCount);
            Assert.Equal(literal.Id, Assert.Single(result.Items).Claim.Id);
        }
        Assert.Equal("BH-202611-0002", await repo.GetLastCaseCodeAsync("BH-202611-", default));
        Assert.Equal(literal.Id, Assert.Single(await repo.CasesForDeviceAsync(literal.DeviceId, default)).Id);
        Assert.Single((await repo.GetDetailAsync(literal.Id, default))!.Claim.Events);

        AddClaim(0, "BH-202611-0003", "12300000");
        var duplicate = await Assert.ThrowsAsync<DuplicateKeyException>(() => work.SaveChangesAsync());
        Assert.Equal("uq_warranty_cases_open_device", duplicate.ConstraintName);
    }
}
