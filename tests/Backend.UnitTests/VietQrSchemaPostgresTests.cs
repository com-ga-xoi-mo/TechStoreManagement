using Microsoft.EntityFrameworkCore;
using Npgsql;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public sealed class VietQrSchemaPostgresTests
{
    [PostgresFact]
    public async Task Migrate_SecondPendingQrForTheSameOrder_IsRejected()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var order = AddOrder(db);
        db.VietQrTransactions.Add(NewQr(order, "TSAAAAAAAAAA", VietQrStatus.Pending));
        await db.SaveChangesAsync();

        // Act
        db.VietQrTransactions.Add(NewQr(order, "TSBBBBBBBBBB", VietQrStatus.Pending));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Assert
        Assert.Equal("uq_vietqr_pending_order", ((PostgresException)error.InnerException!).ConstraintName);
    }

    [PostgresFact]
    public async Task Migrate_NewPendingQrAfterTheFirstEnded_IsAllowed()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var order = AddOrder(db);
        db.VietQrTransactions.Add(NewQr(order, "TSAAAAAAAAAA", VietQrStatus.Expired));

        // Act
        db.VietQrTransactions.Add(NewQr(order, "TSBBBBBBBBBB", VietQrStatus.Pending));

        // Assert
        await db.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task Migrate_BankEventReplay_IsStoredOnce()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        db.VietQrBankEvents.Add(NewEvent("sandbox", "FT1"));
        await db.SaveChangesAsync();

        // Act
        db.VietQrBankEvents.Add(NewEvent("sandbox", "FT1"));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Assert
        Assert.Equal("uq_vietqr_events_provider_transaction", ((PostgresException)error.InnerException!).ConstraintName);
    }

    [PostgresFact]
    public async Task Migrate_SameReferenceFromAnotherProvider_IsAllowed()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        db.VietQrBankEvents.Add(NewEvent("sandbox", "FT1"));

        // Act
        db.VietQrBankEvents.Add(NewEvent("other", "FT1"));

        // Assert
        await db.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task Migrate_ResolvedEventWithoutNote_IsRejected()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var db = database.CreateContext();
        var user = new User { Username = "m", Email = "m@test.vn", FullName = "M", PasswordHash = "hash" };
        db.Users.Add(user);
        var bankEvent = NewEvent("sandbox", "FT1");
        bankEvent.Status = VietQrEventStatus.Resolved;
        bankEvent.ResolvedAt = DateTime.UtcNow;
        bankEvent.ResolvedByUserId = user.Id;
        db.VietQrBankEvents.Add(bankEvent);

        // Act
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Assert
        Assert.Equal("ck_vietqr_events_resolution", ((PostgresException)error.InnerException!).ConstraintName);
    }

    private static Order AddOrder(AppDbContext db)
    {
        var user = new User { Username = "c" + Guid.NewGuid().ToString("N")[..6], Email = Guid.NewGuid().ToString("N") + "@test.vn", FullName = "C", PasswordHash = "hash" };
        db.Users.Add(user);
        var order = new Order
        {
            OrderCode = "ORD-" + Guid.NewGuid().ToString("N")[..8],
            CashierUserId = user.Id,
            PaymentMethod = PaymentMethod.VietQr,
            Subtotal = 100m,
            TotalAmount = 100m
        };
        db.Orders.Add(order);
        return order;
    }

    private static VietQrTransaction NewQr(Order order, string code, VietQrStatus status) => new()
    {
        OrderId = order.Id,
        TransactionCode = code,
        BankBin = "970422",
        BankAccountNumber = "0123456789",
        Amount = 100m,
        QrContent = "000201",
        Status = status,
        ExpiresAt = DateTime.UtcNow.AddMinutes(15)
    };

    private static VietQrBankEvent NewEvent(string provider, string transactionId) => new()
    {
        Provider = provider,
        ProviderTransactionId = transactionId,
        Amount = 100m,
        TransferContent = "TSAAAAAAAAAA",
        AccountNumber = "0123456789",
        TransferredAt = DateTime.UtcNow,
        ReceivedAt = DateTime.UtcNow,
        Status = VietQrEventStatus.NeedsReconciliation,
        Reason = VietQrEventReason.Unmatched,
        Payload = "{}"
    };
}
