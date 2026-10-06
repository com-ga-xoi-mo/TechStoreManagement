using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

public class CustomerEntityTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy_test;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void CustomerEntities_Should_Initialize_With_Defaults()
    {
        var customer = new Customer
        {
            FullName = "Nguyen Van A",
            Phone = "0987654321"
        };
        customer.Id.Should().NotBeEmpty();
        customer.LoyaltyPoints.Should().Be(0);
        customer.Tier.Should().Be(CustomerTier.Standard);
        customer.IsActive.Should().BeTrue();
        customer.LoyaltyPointsHistory.Should().NotBeNull();

        var point = new CustomerLoyaltyPoint
        {
            CustomerId = customer.Id,
            PointsChange = 50,
            BalanceAfter = 50,
            Reason = "First purchase"
        };
        point.Id.Should().NotBeEmpty();

        var voucher = new Voucher
        {
            Code = "TECHSTORE100K",
            DiscountType = VoucherType.FixedAmount,
            DiscountValue = 100000m,
            ExpiresAt = DateTime.UtcNow.AddMonths(1)
        };
        voucher.Id.Should().NotBeEmpty();
        voucher.UsageLimit.Should().Be(100);
        voucher.UsedCount.Should().Be(0);
        voucher.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CustomerModel_Should_Configure_Tables_And_PartialIndexes()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        var customerEntity = model.FindEntityType(typeof(Customer));
        customerEntity.Should().NotBeNull();
        customerEntity!.GetTableName().Should().Be("customers");

        var phoneIndex = customerEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "idx_customers_phone");
        phoneIndex.Should().NotBeNull();
        phoneIndex!.IsUnique.Should().BeTrue();

        var voucherEntity = model.FindEntityType(typeof(Voucher));
        voucherEntity.Should().NotBeNull();
        voucherEntity!.GetTableName().Should().Be("vouchers");

        var voucherIndex = voucherEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "idx_vouchers_active");
        voucherIndex.Should().NotBeNull();
        voucherIndex!.GetFilter().Should().Be("is_active = true");
    }
}
