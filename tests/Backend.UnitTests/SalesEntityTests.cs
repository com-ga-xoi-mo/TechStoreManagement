using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Sales.Entities;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

public class SalesEntityTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy_test;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void SalesEntities_Should_Initialize_With_Defaults()
    {
        var session = new PosSession
        {
            CashierUserId = Guid.NewGuid(),
            OpeningBalance = 1000000m
        };
        session.Id.Should().NotBeEmpty();
        session.Status.Should().Be(PosSessionStatus.Open);
        session.CashSalesTotal.Should().Be(0);
        session.VietQrSalesTotal.Should().Be(0);
        session.CardSalesTotal.Should().Be(0);
        session.VietQrTransactions.Should().NotBeNull();

        var tx = new VietQrTransaction
        {
            OrderId = Guid.NewGuid(),
            TransactionCode = "TS20261001",
            BankBin = "970422",
            BankAccountNumber = "123456789",
            Amount = 500000m,
            QrContent = "000201...",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
        tx.Id.Should().NotBeEmpty();
        tx.Status.Should().Be(VietQrStatus.Pending);
    }

    [Fact]
    public void SalesModel_Should_Configure_Tables_And_PartialUniqueIndexes()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        var sessionEntity = model.FindEntityType(typeof(PosSession));
        sessionEntity.Should().NotBeNull();
        sessionEntity!.GetTableName().Should().Be("pos_sessions");

        var sessionOpenIndex = sessionEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "uq_pos_sessions_cashier_open");
        sessionOpenIndex.Should().NotBeNull();
        sessionOpenIndex!.IsUnique.Should().BeTrue();
        sessionOpenIndex.GetFilter().Should().Be("status = 'Open'");

        var txEntity = model.FindEntityType(typeof(VietQrTransaction));
        txEntity.Should().NotBeNull();
        txEntity!.GetTableName().Should().Be("vietqr_transactions");

        var confirmedIndex = txEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "uq_vietqr_confirmed_order");
        confirmedIndex.Should().NotBeNull();
        confirmedIndex!.IsUnique.Should().BeTrue();
        confirmedIndex.GetFilter().Should().Be("status = 'Confirmed'");
    }
}
