using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace TechStore.UnitTests;

// M4 Vy: the loyalty and voucher migration keeps what is already stored (balances, ledger, used voucher counts, orders).
public sealed class LoyaltyVoucherMigrationPostgresTests
{
    private const string Previous = "20261121170854_PartialPurchaseDeliveries";

    [PostgresFact]
    public async Task Migrate_DatabaseWithOldLoyaltyVoucherAndOrderRows_KeepsEveryValue()
    {
        // Arrange: the schema before this change, filled with old-style rows.
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using (var db = database.CreateContext())
            await db.GetService<IMigrator>().MigrateAsync(Previous);
        var customerId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                INSERT INTO users(id,username,email,full_name,password_hash) VALUES (@user,'cashier','cashier@test.vn','Cashier','hash');
                INSERT INTO customers(id,full_name,phone,loyalty_points,tier) VALUES (@customer,'Khách cũ','0900000009',700,'Silver');
                INSERT INTO customer_loyalty_points(customer_id,points_change,balance_after,reason) VALUES (@customer,500,500,'Old purchase'),(@customer,200,700,'Old purchase 2');
                INSERT INTO vouchers(id,code,discount_type,discount_value,usage_limit,used_count,expires_at) VALUES (@voucher,'OLD10','Percentage',10,5,3, now() + interval '30 days');
                INSERT INTO orders(id,order_code,cashier_user_id,customer_id,voucher_id,subtotal,discount_amount,total_amount) VALUES (@order,'ORD-202611-0001',@user,@customer,@voucher,2000000,200000,1800000);
                """, connection);
            command.Parameters.AddWithValue("user", userId);
            command.Parameters.AddWithValue("customer", customerId);
            command.Parameters.AddWithValue("voucher", voucherId);
            command.Parameters.AddWithValue("order", orderId);
            await command.ExecuteNonQueryAsync();
        }

        // Act
        await using (var db = database.CreateContext())
            await db.Database.MigrateAsync();

        // Assert
        await using var verify = database.CreateContext();
        var customer = await verify.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        Assert.Equal(700, customer.LoyaltyPoints);
        var ledger = await verify.CustomerLoyaltyPoints.AsNoTracking().Where(p => p.CustomerId == customerId).OrderBy(p => p.BalanceAfter).ToListAsync();
        Assert.Equal([500, 700], ledger.Select(p => p.BalanceAfter));
        Assert.All(ledger, p => Assert.Null(p.ReferenceKey));
        var voucher = await verify.Vouchers.AsNoTracking().SingleAsync(v => v.Id == voucherId);
        Assert.Equal((3, 0, 5), (voucher.UsedCount, voucher.ReservedCount, voucher.UsageLimit));
        var order = await verify.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal((200_000m, voucherId), (order.DiscountAmount, order.VoucherId));
        Assert.Equal(TechStore.Shared.Enums.OrderDiscountSource.None, order.DiscountSource);
        Assert.Equal(0, await verify.VoucherRedemptions.CountAsync());
    }

    [PostgresFact]
    public async Task Migrate_OldVoucherAtItsLimit_StillRespectsTheNewCheckOnHolds()
    {
        // Arrange: a voucher with every use consumed before the change.
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using (var db = database.CreateContext())
            await db.GetService<IMigrator>().MigrateAsync(Previous);
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "INSERT INTO vouchers(code,discount_type,discount_value,usage_limit,used_count,expires_at) VALUES ('FULL','Percentage',10,5,5, now() + interval '30 days')", connection);
            await command.ExecuteNonQueryAsync();
        }

        await using (var db = database.CreateContext())
            await db.Database.MigrateAsync();

        // Act
        await using var verify = database.CreateContext();
        var voucher = await verify.Vouchers.SingleAsync(v => v.Code == "FULL");
        voucher.ReservedCount = 1;
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => verify.SaveChangesAsync());

        // Assert
        Assert.Equal("ck_vouchers_used_count_valid", ((PostgresException)error.InnerException!).ConstraintName);
    }
}
