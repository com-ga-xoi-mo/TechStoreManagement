using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;
using TechStore.Api.Customers.Services;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

// M4 Vy on real PostgreSQL: row locks, idempotent ledger keys and rollback, with one transaction per caller.
public sealed class LoyaltyVoucherServicesPostgresTests
{
    [PostgresFact]
    public async Task Loyalty_M4ReferenceSale_Earns18ThenReverses9And9AndIgnoresARepeatedReturn()
    {
        // Arrange: 2 x 1,000,000 with a 200,000 order discount, so 1,800,000 goods before VAT.
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var (customerId, orderIds) = await SeedAsync(database, orders: 1);
        var order = CompletedOrder(orderIds[0], customerId, subtotal: 2_000_000m, discount: 200_000m);
        var firstReturn = Guid.NewGuid();
        var secondReturn = Guid.NewGuid();

        // Act
        var earned = await InTransactionAsync(database, (db, _) => Loyalty(db).AwardForOrderAsync(order));
        var first = await InTransactionAsync(database, (db, _) => Loyalty(db).ReverseForReturnAsync(order, firstReturn, 900_000m));
        var repeated = await InTransactionAsync(database, (db, _) => Loyalty(db).ReverseForReturnAsync(order, firstReturn, 900_000m));
        var second = await InTransactionAsync(database, (db, _) => Loyalty(db).ReverseForReturnAsync(order, secondReturn, 0m));

        // Assert
        Assert.Equal((LoyaltyOutcome.Applied, 18, 18), (earned.Outcome, earned.PointsChange, earned.BalanceAfter));
        Assert.Equal((LoyaltyOutcome.Applied, -9, 9), (first.Outcome, first.PointsChange, first.BalanceAfter));
        Assert.Equal(LoyaltyOutcome.AlreadyApplied, repeated.Outcome);
        Assert.Equal((LoyaltyOutcome.Applied, -9, 0), (second.Outcome, second.PointsChange, second.BalanceAfter));

        await using var verify = database.CreateContext();
        var customer = await verify.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        Assert.Equal(0, customer.LoyaltyPoints);
        Assert.Equal(CustomerTier.Standard, customer.Tier);
        var ledger = await verify.CustomerLoyaltyPoints.AsNoTracking().Where(p => p.CustomerId == customerId)
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.BalanceAfter == 18 ? 0 : p.BalanceAfter == 9 ? 1 : 2).ToListAsync();
        Assert.Equal([18, -9, -9], ledger.Select(p => p.PointsChange));
        Assert.Equal([18, 9, 0], ledger.Select(p => p.BalanceAfter));
    }

    [PostgresFact]
    public async Task Loyalty_TwoConcurrentEarnsForTheSameOrder_AddThePointsOnce()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var (customerId, orderIds) = await SeedAsync(database, orders: 1);
        var order = CompletedOrder(orderIds[0], customerId, subtotal: 2_000_000m, discount: 200_000m);

        // Act
        var results = await Task.WhenAll(
            Task.Run(() => InTransactionAsync(database, (db, _) => Loyalty(db).AwardForOrderAsync(order))),
            Task.Run(() => InTransactionAsync(database, (db, _) => Loyalty(db).AwardForOrderAsync(order))));

        // Assert
        Assert.Equal(1, results.Count(r => r.Outcome == LoyaltyOutcome.Applied));
        Assert.Equal(1, results.Count(r => r.Outcome == LoyaltyOutcome.AlreadyApplied));
        await using var verify = database.CreateContext();
        Assert.Equal(18, (await verify.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId)).LoyaltyPoints);
        Assert.Equal(1, await verify.CustomerLoyaltyPoints.CountAsync(p => p.CustomerId == customerId));
    }

    [PostgresFact]
    public async Task Loyalty_TwoDifferentOrdersEarnedAtTheSameTime_BothCountAndTheBalanceAddsUp()
    {
        // Arrange: the customer row lock must stop the second order overwriting the first order's balance.
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var (customerId, orderIds) = await SeedAsync(database, orders: 2);
        var first = CompletedOrder(orderIds[0], customerId, subtotal: 1_000_000m, discount: 0m);
        var second = CompletedOrder(orderIds[1], customerId, subtotal: 2_000_000m, discount: 0m);

        // Act
        await Task.WhenAll(
            Task.Run(() => InTransactionAsync(database, (db, _) => Loyalty(db).AwardForOrderAsync(first))),
            Task.Run(() => InTransactionAsync(database, (db, _) => Loyalty(db).AwardForOrderAsync(second))));

        // Assert
        await using var verify = database.CreateContext();
        Assert.Equal(30, (await verify.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId)).LoyaltyPoints);
        // Whichever order wins the lock goes first: its balance equals its own points, the other ends at 30.
        var rows = await verify.CustomerLoyaltyPoints.AsNoTracking().OrderBy(p => p.BalanceAfter).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(rows[0].PointsChange, rows[0].BalanceAfter);
        Assert.Equal(30, rows[1].BalanceAfter);
        Assert.Equal(30, rows.Sum(r => r.PointsChange));
    }

    [PostgresFact]
    public async Task Voucher_TwoOrdersHoldTheLastUseAtTheSameTime_OnlyOneSucceeds()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var (_, orderIds) = await SeedAsync(database, orders: 2);
        var voucherId = await AddVoucherAsync(database, usageLimit: 1);

        // Act
        var results = await Task.WhenAll(orderIds.Select(id =>
            Task.Run(() => InTransactionAsync(database, (db, uow) => Vouchers(db, uow).ReserveAsync(OrderFor(id, voucherId))))));

        // Assert
        Assert.Equal(1, results.Count(r => r.Outcome == VoucherUseOutcome.Applied));
        Assert.Equal(1, results.Count(r => r.Outcome == VoucherUseOutcome.Refused));
        await using var verify = database.CreateContext();
        var voucher = await verify.Vouchers.AsNoTracking().SingleAsync(v => v.Id == voucherId);
        Assert.Equal(1, voucher.ReservedCount);
        Assert.Equal(1, await verify.VoucherRedemptions.CountAsync());
    }

    [PostgresFact]
    public async Task Voucher_EightOrdersCompeteForThreeUses_ExactlyThreeAreHeld()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var (_, orderIds) = await SeedAsync(database, orders: 8);
        var voucherId = await AddVoucherAsync(database, usageLimit: 3);

        // Act
        var results = await Task.WhenAll(orderIds.Select(id =>
            Task.Run(() => InTransactionAsync(database, (db, uow) => Vouchers(db, uow).ReserveAsync(OrderFor(id, voucherId))))));

        // Assert
        Assert.Equal(3, results.Count(r => r.Outcome == VoucherUseOutcome.Applied));
        Assert.Equal(5, results.Count(r => r.Outcome == VoucherUseOutcome.Refused));
        await using var verify = database.CreateContext();
        var voucher = await verify.Vouchers.AsNoTracking().SingleAsync(v => v.Id == voucherId);
        Assert.Equal(3, voucher.ReservedCount);
        Assert.True(voucher.UsedCount + voucher.ReservedCount <= voucher.UsageLimit);
    }

    [PostgresFact]
    public async Task Voucher_HoldRolledBack_LeavesNeitherACountNorARow()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var (_, orderIds) = await SeedAsync(database, orders: 1);
        var voucherId = await AddVoucherAsync(database, usageLimit: 5);

        // Act: the payment flow fails after the hold and rolls back.
        var result = await InTransactionAsync(database,
            (db, uow) => Vouchers(db, uow).ReserveAsync(OrderFor(orderIds[0], voucherId)), commit: false);

        // Assert
        Assert.Equal(VoucherUseOutcome.Applied, result.Outcome);
        await using var verify = database.CreateContext();
        Assert.Equal(0, (await verify.Vouchers.AsNoTracking().SingleAsync(v => v.Id == voucherId)).ReservedCount);
        Assert.Equal(0, await verify.VoucherRedemptions.CountAsync());
    }

    [PostgresFact]
    public async Task Voucher_HoldRedeemAndLaterRelease_ConsumesOnceAndNeverGivesTheUseBack()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var (_, orderIds) = await SeedAsync(database, orders: 1);
        var voucherId = await AddVoucherAsync(database, usageLimit: 2);
        var order = OrderFor(orderIds[0], voucherId);

        // Act
        var held = await InTransactionAsync(database, (db, uow) => Vouchers(db, uow).ReserveAsync(order));
        var redeemed = await InTransactionAsync(database, (db, uow) => Vouchers(db, uow).RedeemAsync(order.Id));
        var redeemedAgain = await InTransactionAsync(database, (db, uow) => Vouchers(db, uow).RedeemAsync(order.Id));
        var released = await InTransactionAsync(database, (db, uow) => Vouchers(db, uow).ReleaseAsync(order.Id));

        // Assert
        Assert.Equal(VoucherUseOutcome.Applied, held.Outcome);
        Assert.Equal(VoucherUseOutcome.Applied, redeemed.Outcome);
        Assert.Equal(VoucherUseOutcome.Unchanged, redeemedAgain.Outcome);
        Assert.Equal(VoucherUseOutcome.Unchanged, released.Outcome);
        await using var verify = database.CreateContext();
        var voucher = await verify.Vouchers.AsNoTracking().SingleAsync(v => v.Id == voucherId);
        Assert.Equal((1, 0), (voucher.UsedCount, voucher.ReservedCount));
        Assert.Equal(VoucherRedemptionStatus.Redeemed, (await verify.VoucherRedemptions.AsNoTracking().SingleAsync()).Status);
    }

    [PostgresFact]
    public async Task Voucher_HoldThenReleaseBeforePayment_FreesTheUseForAnotherOrder()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        var (_, orderIds) = await SeedAsync(database, orders: 2);
        var voucherId = await AddVoucherAsync(database, usageLimit: 1);
        var first = OrderFor(orderIds[0], voucherId);
        var second = OrderFor(orderIds[1], voucherId);
        await InTransactionAsync(database, (db, uow) => Vouchers(db, uow).ReserveAsync(first));
        var blocked = await InTransactionAsync(database, (db, uow) => Vouchers(db, uow).ReserveAsync(second));

        // Act: the first payment expires and its hold is returned.
        await InTransactionAsync(database, (db, uow) => Vouchers(db, uow).ReleaseAsync(first.Id));
        var afterRelease = await InTransactionAsync(database, (db, uow) => Vouchers(db, uow).ReserveAsync(second));

        // Assert
        Assert.Equal(VoucherUseOutcome.Refused, blocked.Outcome);
        Assert.Equal(VoucherUseOutcome.Applied, afterRelease.Outcome);
    }

    // ---- helpers ----

    private static LoyaltyService Loyalty(AppDbContext db) => new(new LoyaltyRepository(db), new CustomerRepository(db));

    private static VoucherService Vouchers(AppDbContext db, IUnitOfWork uow) => new(new VoucherRepository(db), uow, TimeProvider.System);

    // One transaction and one context per caller, like one request: lock, change, save, commit (or roll back).
    private static async Task<T> InTransactionAsync<T>(PostgresDatabase database, Func<AppDbContext, IUnitOfWork, Task<T>> work, bool commit = true)
    {
        await using var db = database.CreateContext();
        var uow = new UnitOfWork(db);
        await uow.BeginTransactionAsync();
        try
        {
            var result = await work(db, uow);
            await uow.SaveChangesAsync();
            if (commit) await uow.CommitAsync();
            else await uow.RollbackAsync();
            return result;
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }

    private static async Task<(Guid CustomerId, List<Guid> OrderIds)> SeedAsync(PostgresDatabase database, int orders)
    {
        await using var db = database.CreateContext();
        var cashier = new User { Username = "cashier", Email = "cashier@test.vn", FullName = "Cashier", PasswordHash = "hash" };
        var customer = new Customer { FullName = "Khách A", Phone = "0900000001" };
        db.AddRange(cashier, customer);
        var ids = new List<Guid>();
        for (var i = 1; i <= orders; i++)
        {
            var order = new Order
            {
                OrderCode = $"ORD-202611-{i:0000}",
                CashierUserId = cashier.Id,
                CustomerId = customer.Id,
                Subtotal = 2_000_000m,
                TotalAmount = 2_000_000m
            };
            db.Orders.Add(order);
            ids.Add(order.Id);
        }

        await db.SaveChangesAsync();
        return (customer.Id, ids);
    }

    private static async Task<Guid> AddVoucherAsync(PostgresDatabase database, int usageLimit)
    {
        await using var db = database.CreateContext();
        var voucher = new Voucher
        {
            Code = "SALE10",
            DiscountType = VoucherType.Percentage,
            DiscountValue = 10m,
            UsageLimit = usageLimit,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();
        return voucher.Id;
    }

    private static Order CompletedOrder(Guid id, Guid customerId, decimal subtotal, decimal discount) => new()
    {
        Id = id,
        OrderCode = "ORD-202611-0001",
        CustomerId = customerId,
        Status = OrderStatus.Completed,
        Subtotal = subtotal,
        DiscountAmount = discount,
        TotalAmount = subtotal - discount
    };

    private static Order OrderFor(Guid id, Guid voucherId) => new()
    {
        Id = id,
        VoucherId = voucherId,
        Subtotal = 2_000_000m
    };
}
