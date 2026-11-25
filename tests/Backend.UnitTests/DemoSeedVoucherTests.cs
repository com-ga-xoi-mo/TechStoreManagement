using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

/// <summary>Demo vouchers cover every state the voucher screen and checkout rules need, and seeding never fights an admin edit.</summary>
public class DemoSeedVoucherTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Manifest_Vouchers_HaveUniqueCodesAndSatisfyTheSchemaChecks()
    {
        // Arrange / Act
        var vouchers = DemoSeedManifest.Vouchers;

        // Assert
        vouchers.Select(x => x.Code).Should().OnlyHaveUniqueItems().And.OnlyContain(x => x == x.ToUpperInvariant());
        vouchers.Should().OnlyContain(x => x.ExpiresAt > x.CreatedAt && x.CreatedAt.Kind == DateTimeKind.Utc && x.ExpiresAt.Kind == DateTimeKind.Utc);
        vouchers.Should().OnlyContain(x => x.DiscountValue > 0 && x.UsageLimit > 0 && x.UsedCount >= 0 && x.UsedCount <= x.UsageLimit);
        vouchers.Where(x => x.DiscountType == VoucherType.Percentage).Should().OnlyContain(x => x.DiscountValue <= 100);
        vouchers.Should().Contain(x => x.Code == "DEMO-OCT26" && x.DiscountType == VoucherType.FixedAmount && x.DiscountValue == 100000m && x.UsedCount == 0 && x.IsActive);
    }

    [Fact]
    public void Manifest_Vouchers_CoverActiveExpiredExhaustedAndInactive()
    {
        // Arrange / Act
        var vouchers = DemoSeedManifest.Vouchers;

        // Assert
        vouchers.Should().Contain(x => x.IsActive && x.ExpiresAt > Now && x.UsedCount < x.UsageLimit && x.DiscountType == VoucherType.Percentage);
        vouchers.Should().Contain(x => x.IsActive && x.ExpiresAt > Now && x.UsedCount < x.UsageLimit && x.DiscountType == VoucherType.FixedAmount);
        vouchers.Should().Contain(x => x.IsActive && x.ExpiresAt <= Now);
        vouchers.Should().Contain(x => x.IsActive && x.ExpiresAt > Now && x.UsedCount == x.UsageLimit);
        vouchers.Should().Contain(x => !x.IsActive);
    }

    [Fact]
    public async Task SeedAsync_CreatesEveryDemoVoucherOnceAndRerunKeepsThem()
    {
        // Arrange
        await using var db = NewDb();
        var seeder = NewSeeder(db);

        // Act
        await seeder.SeedAsync();
        var first = await db.Vouchers.AsNoTracking().OrderBy(x => x.Code).ToListAsync();
        await seeder.SeedAsync();
        var second = await db.Vouchers.AsNoTracking().OrderBy(x => x.Code).ToListAsync();

        // Assert
        first.Select(x => x.Code).Should().BeEquivalentTo(DemoSeedManifest.Vouchers.Select(x => x.Code));
        second.Should().BeEquivalentTo(first);
        foreach (var seed in DemoSeedManifest.Vouchers)
        {
            var row = first.Single(x => x.Code == seed.Code);
            row.Id.Should().Be(DemoSeedManifest.Id($"voucher:{seed.Code}"));
            row.DiscountType.Should().Be(seed.DiscountType);
            row.DiscountValue.Should().Be(seed.DiscountValue);
            row.MinOrderAmount.Should().Be(seed.MinOrderAmount);
            row.MaxDiscountAmount.Should().Be(seed.MaxDiscountAmount);
            row.UsageLimit.Should().Be(seed.UsageLimit);
            row.UsedCount.Should().Be(seed.UsedCount);
            row.ReservedCount.Should().Be(0);
            row.IsActive.Should().Be(seed.IsActive);
            row.CreatedAt.Should().Be(seed.CreatedAt);
            row.ExpiresAt.Should().Be(seed.ExpiresAt);
        }
    }

    [Fact]
    public async Task SeedAsync_AfterAnAdminEditsASeededVoucher_KeepsTheEditAndStillSucceeds()
    {
        // Arrange: the voucher screen lets Admin/Manager switch off, edit and use up a seeded voucher.
        await using var db = NewDb();
        var seeder = NewSeeder(db);
        await seeder.SeedAsync();
        var voucher = await db.Vouchers.SingleAsync(x => x.Code == "DEMO-OCT26");
        voucher.IsActive = false;
        voucher.DiscountValue = 250000m;
        voucher.UsageLimit = 10;
        voucher.UsedCount = 3;
        voucher.ReservedCount = 1;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act
        var act = () => seeder.SeedAsync();

        // Assert
        await act.Should().NotThrowAsync();
        var after = await db.Vouchers.AsNoTracking().SingleAsync(x => x.Code == "DEMO-OCT26");
        after.IsActive.Should().BeFalse();
        after.DiscountValue.Should().Be(250000m);
        after.UsageLimit.Should().Be(10);
        after.UsedCount.Should().Be(3);
        after.ReservedCount.Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_WhenOneDemoVoucherWasDeleted_AddsOnlyTheMissingOne()
    {
        // Arrange
        await using var db = NewDb();
        var seeder = NewSeeder(db);
        await seeder.SeedAsync();
        db.Vouchers.Remove(await db.Vouchers.SingleAsync(x => x.Code == "DEMO-SOLDOUT"));
        var untouched = await db.Vouchers.SingleAsync(x => x.Code == "DEMO-VIP10");
        untouched.Description = "Edited by an admin";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act
        await seeder.SeedAsync();

        // Assert
        (await db.Vouchers.CountAsync()).Should().Be(DemoSeedManifest.Vouchers.Count);
        (await db.Vouchers.SingleAsync(x => x.Code == "DEMO-SOLDOUT")).UsedCount.Should().Be(20);
        (await db.Vouchers.SingleAsync(x => x.Code == "DEMO-VIP10")).Description.Should().Be("Edited by an admin");
    }

    [Fact]
    public async Task SeedAsync_WhenAVoucherCodeIsDuplicatedByAnotherRow_FailsClearly()
    {
        // Arrange: two rows with one code cannot be matched to a seed row, which is the one conflict that remains.
        await using var db = NewDb();
        var seeder = NewSeeder(db);
        await seeder.SeedAsync();
        var seeded = await db.Vouchers.AsNoTracking().SingleAsync(x => x.Code == "DEMO-OFF");
        db.Vouchers.Add(new TechStore.Api.Customers.Entities.Voucher
        {
            Id = Guid.NewGuid(), Code = "demo-off", DiscountType = VoucherType.FixedAmount, DiscountValue = 1m,
            UsageLimit = 1, ExpiresAt = seeded.ExpiresAt, CreatedAt = seeded.CreatedAt
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act
        var act = () => seeder.SeedAsync();

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*voucher*DEMO-OFF*");
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static DataSeeder NewSeeder(AppDbContext db) =>
        new(new DemoSeedRepository(db), new FakeUnitOfWork(db), new FakeObjectStorage());
}
