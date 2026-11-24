using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TechStore.Api.Orders.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public class RefundMigrationPostgresTests
{
    [PostgresFact]
    public async Task Migrate_PopulatedM3_PreservesOldPayoutsFlagsUnallocatedSalesAndBackfillsProcessingReservations()
    {
        // Arrange: seed the last M3 schema through its own columns.
        await using var api = await IdentityPostgresApi.CreateAsync();
        await using var db = api.Database.CreateContext();
        var product = await db.Products.Where(p => !p.IsSerialTracked).Select(p => p.Id).FirstAsync();
        var actor = await db.Users.Select(u => u.Id).FirstAsync();
        await db.GetService<IMigrator>().MigrateAsync("20261121170854_PartialPurchaseDeliveries");
        var consistent = Guid.NewGuid();
        var missingShares = Guid.NewGuid();
        var processing = Guid.NewGuid();
        var line = Guid.NewGuid();
        var reservationLine = Guid.NewGuid();
        var receipt = Guid.NewGuid();
        var paidAt = new DateTime(2026, 10, 29, 16, 55, 00, DateTimeKind.Utc);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO orders(id, order_code, cashier_user_id, status, payment_method, subtotal, discount_amount, tax_amount, total_amount)
            VALUES ({consistent}, 'TEST-M3-RETURN-1', {actor}, 'PartiallyRefunded', 'Cash', 20, 0, 0, 20),
                   ({missingShares}, 'TEST-M3-RETURN-2', {actor}, 'Completed', 'Cash', 20, 0, 2, 22),
                   ({processing}, 'TEST-M3-RETURN-3', {actor}, 'Processing', 'Cash', 20, 0, 0, 20);
            INSERT INTO order_items(id, order_id, product_id, product_name, sku, unit_price, quantity, total_price)
            VALUES ({line}, {consistent}, {product}, 'Fixture', 'M3', 10, 2, 20),
                   ({Guid.NewGuid()}, {missingShares}, {product}, 'Fixture', 'M3', 10, 2, 20),
                   ({reservationLine}, {processing}, {product}, 'Fixture', 'M3', 10, 2, 20);
            INSERT INTO order_returns(id, order_id, order_item_id, processed_by_user_id, quantity, reason, refund_amount, refund_method, refunded_at, is_restocked)
            VALUES ({receipt}, {consistent}, {line}, {actor}, 1, 'M3 receipt', 10, 'VietQr', {paidAt}, true);
            """);

        // Act
        await db.Database.MigrateAsync();

        // Assert
        var upgraded = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Returns).SingleAsync(o => o.Id == consistent);
        var legacy = Assert.Single(upgraded.Returns);
        Assert.Equal(receipt, legacy.Id);
        Assert.True(legacy.IsRestocked);
        Assert.Equal(10m, legacy.RefundAmount);
        Assert.Equal(paidAt, legacy.RefundedAt);
        Assert.Null(legacy.NetAmount);
        Assert.Null(legacy.CaseLineId);
        Assert.Null(legacy.TaxAmount);
        Assert.Null(legacy.DiscountAmount);
        var proposed = RefundAllocation.Calculate(upgraded, new Dictionary<Guid, int> { [line] = 1 });
        Assert.Null(proposed.Error);
        Assert.True(proposed.IsFullyReturned);
        Assert.Equal(10m, Assert.Single(proposed.Lines).RefundAmount);
        var inconsistent = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == missingShares);
        Assert.NotNull(RefundAllocation.Calculate(inconsistent, new Dictionary<Guid, int> { [inconsistent.Items.Single().Id] = 1 }).Error);
        using var manager = api.Factory.NewClient();
        await IdentityPostgresApi.LoginAsync(manager, "manager1", "Manager@123");
        var history = await manager.GetFromJsonAsync<OrderReturnsDto>($"/api/returns/order/{consistent}");
        Assert.Null(history!.ReviewReason);
        var receiptDto = Assert.Single(history.LegacyRefunds!);
        Assert.Equal((receipt, line, 1, 10m, paidAt, true), (receiptDto.Id, receiptDto.OrderItemId, receiptDto.Quantity,
            receiptDto.RefundAmount, receiptDto.RefundedAt, receiptDto.IsRestocked));
        var available = Assert.Single(history.Balances);
        Assert.Equal((1, 10m), (available.RemainingQuantity, available.RemainingAmount));
        var held = await manager.GetFromJsonAsync<OrderReturnsDto>($"/api/returns/order/{missingShares}");
        Assert.False(string.IsNullOrWhiteSpace(held!.ReviewReason));
        var heldLine = Assert.Single(held.Balances);
        Assert.Equal((0, 0m), (heldLine.RemainingQuantity, heldLine.RemainingAmount));
        using var rejected = await manager.PostAsJsonAsync("/api/returns", new CreateReturnRequest(Guid.NewGuid(), missingShares,
            "Check old receipt", [new(heldLine.OrderItemId, 1, null)]));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(2, await db.Database.SqlQuery<int>($"""
            SELECT quantity AS "Value" FROM inventory_reservations WHERE order_item_id = {reservationLine} AND state = 'Held'
            """).SingleAsync());
    }
}
