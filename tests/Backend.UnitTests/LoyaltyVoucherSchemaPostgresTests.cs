using Microsoft.EntityFrameworkCore;
using Npgsql;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

// M4 Vy: the database is the last guard for loyalty idempotency and voucher use counts.
public sealed class LoyaltyVoucherSchemaPostgresTests
{
    [PostgresFact]
    public async Task Migrate_DuplicateLoyaltyReferenceKey_IsRejectedByUniqueIndex()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var customer = new Customer { FullName = "Khách A", Phone = "0900000001" };
        db.Customers.Add(customer);
        db.CustomerLoyaltyPoints.Add(Entry(customer.Id, "earn:order-1"));
        await db.SaveChangesAsync();

        // Act
        db.CustomerLoyaltyPoints.Add(Entry(customer.Id, "earn:order-1"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Assert
        Assert.Equal("idx_cust_loyalty_reference_key", ((PostgresException)error.InnerException!).ConstraintName);
    }

    [PostgresFact]
    public async Task Migrate_LoyaltyRowsWithoutReferenceKey_AreNotConstrained()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var customer = new Customer { FullName = "Khách A", Phone = "0900000001" };
        db.Customers.Add(customer);

        // Act: old rows have no key, so any number of them may coexist.
        db.CustomerLoyaltyPoints.AddRange(Entry(customer.Id, null), Entry(customer.Id, null));

        // Assert
        await db.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task Migrate_UsedPlusReservedAboveLimit_IsRejectedByCheck()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var voucher = NewVoucher("SALE", limit: 5);
        voucher.UsedCount = 3;
        voucher.ReservedCount = 2;
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        // Act
        voucher.ReservedCount = 3;
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Assert
        Assert.Equal("ck_vouchers_used_count_valid", ((PostgresException)error.InnerException!).ConstraintName);
    }

    [PostgresFact]
    public async Task Migrate_SecondRedemptionForSameOrder_IsRejectedByUniqueIndex()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var cashier = new User { Username = "cashier", Email = "cashier@test.vn", FullName = "cashier", PasswordHash = "hash" };
        var voucher = NewVoucher("SALE", limit: 5);
        var order = new Order { OrderCode = "ORD-1", CashierUserId = cashier.Id, Subtotal = 100m, TotalAmount = 100m };
        db.AddRange(cashier, voucher, order);
        db.VoucherRedemptions.Add(new VoucherRedemption { VoucherId = voucher.Id, OrderId = order.Id });
        await db.SaveChangesAsync();

        // Act
        db.VoucherRedemptions.Add(new VoucherRedemption { VoucherId = voucher.Id, OrderId = order.Id });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Assert
        Assert.Equal("idx_voucher_redemptions_order_id", ((PostgresException)error.InnerException!).ConstraintName);
    }

    [PostgresFact]
    public async Task Migrate_ExistingOrderGetsNoDiscountSource()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var cashier = new User { Username = "cashier", Email = "cashier@test.vn", FullName = "cashier", PasswordHash = "hash" };
        db.Users.Add(cashier);
        db.Orders.Add(new Order { OrderCode = "ORD-1", CashierUserId = cashier.Id, Subtotal = 100m, TotalAmount = 100m });

        // Act
        await db.SaveChangesAsync();
        var stored = await db.Orders.AsNoTracking().SingleAsync();

        // Assert
        Assert.Equal(OrderDiscountSource.None, stored.DiscountSource);
    }

    private static CustomerLoyaltyPoint Entry(Guid customerId, string? key) => new()
    {
        CustomerId = customerId,
        PointsChange = 10,
        BalanceAfter = 10,
        Reason = "test",
        ReferenceKey = key
    };

    private static Voucher NewVoucher(string code, int limit) => new()
    {
        Code = code,
        DiscountType = VoucherType.Percentage,
        DiscountValue = 10m,
        UsageLimit = limit,
        ExpiresAt = DateTime.UtcNow.AddDays(30)
    };
}
