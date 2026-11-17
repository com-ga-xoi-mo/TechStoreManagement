using Microsoft.EntityFrameworkCore;
using Npgsql;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public sealed class PosShiftSchemaPostgresTests
{
    [PostgresFact]
    public async Task Migrate_SecondOpenShiftForSameCashier_IsRejectedByPartialUniqueIndex()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var cashier = AddUser(db, "cashier-a");
        db.PosSessions.Add(new PosSession { CashierUserId = cashier.Id });
        await db.SaveChangesAsync();

        // Act
        db.PosSessions.Add(new PosSession { CashierUserId = cashier.Id });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Assert
        Assert.Equal("uq_pos_sessions_cashier_open", ((PostgresException)error.InnerException!).ConstraintName);
    }

    [PostgresFact]
    public async Task Migrate_ClosedShiftDoesNotBlockNewOpenShift()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var cashier = AddUser(db, "cashier-a");
        db.PosSessions.Add(new PosSession { CashierUserId = cashier.Id, Status = PosSessionStatus.Closed });

        // Act
        db.PosSessions.Add(new PosSession { CashierUserId = cashier.Id });

        // Assert
        await db.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task Migrate_CheckoutKey_IsUniquePerCashierAndIgnoresNull()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var a = AddUser(db, "cashier-a");
        var b = AddUser(db, "cashier-b");
        var key = Guid.NewGuid();
        db.Orders.AddRange(NewOrder("ORD-1", a.Id, key), NewOrder("ORD-2", b.Id, key),
            NewOrder("ORD-3", a.Id, null), NewOrder("ORD-4", a.Id, null));
        await db.SaveChangesAsync();

        // Act
        db.Orders.Add(NewOrder("ORD-5", a.Id, key));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Assert
        Assert.Equal("uq_orders_cashier_checkout_key", ((PostgresException)error.InnerException!).ConstraintName);
    }

    [PostgresFact]
    public async Task Migrate_OrderWithUnknownShift_IsRejectedByForeignKey()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var cashier = AddUser(db, "cashier-a");
        var order = NewOrder("ORD-1", cashier.Id, null);
        order.PosSessionId = Guid.NewGuid();
        db.Orders.Add(order);

        // Act
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Assert
        Assert.Equal("23503", ((PostgresException)error.InnerException!).SqlState);
    }

    private static User AddUser(AppDbContext db, string name)
    {
        var user = new User { Username = name, Email = name + "@test.vn", FullName = name, PasswordHash = "hash" };
        db.Users.Add(user);
        return user;
    }

    private static Order NewOrder(string code, Guid cashierId, Guid? key) => new()
    {
        OrderCode = code,
        CashierUserId = cashierId,
        CheckoutKey = key,
        Subtotal = 100m,
        TotalAmount = 100m
    };
}
