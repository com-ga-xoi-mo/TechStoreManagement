using FluentAssertions;
using TechStore.Client.Models.Inventory;
using Xunit;

namespace TechStore.UnitTests.Inventory;

public sealed class StockDetailModelTests
{
    [Fact]
    public void StockBalanceKey_Should_Require_Product_And_Allow_Optional_Variant()
    {
        var key = new StockBalanceKey("product-1");

        key.ProductId.Should().Be("product-1");
        key.VariantId.Should().BeNull();
        FluentActions.Invoking(() => new StockBalanceKey(" "))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new StockBalanceKey("product-1", " "))
            .Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, -1)]
    public void StockMovement_Should_Reject_Zero_Change_Or_Negative_Balance(int change, int after)
    {
        FluentActions.Invoking(() => new StockMovement(
                "movement-1", DateTimeOffset.UtcNow, StockMovementType.Receipt, change, after))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Snapshot_Should_Preserve_Repository_Movement_Order_And_Values()
    {
        var newest = new StockMovement("new", DateTimeOffset.UtcNow, StockMovementType.Sale, -1, 9, "Order A");
        var older = new StockMovement("old", DateTimeOffset.UtcNow.AddDays(-1), StockMovementType.Receipt, 10, 10);
        var snapshot = new StockDetailSnapshot(CreateBalance(), [newest, older], DateTimeOffset.UtcNow, true);

        snapshot.RecentMovements.Should().ContainInOrder(newest, older);
        snapshot.RecentMovements[0].PhysicalQuantityAfter.Should().Be(9);
        snapshot.Balance.PhysicalQuantity.Should().Be(9);
        snapshot.IsPreviewData.Should().BeTrue();
    }

    [Fact]
    public void Snapshot_Should_Reject_More_Than_Five_Movements()
    {
        var movements = Enumerable.Range(1, 6)
            .Select(index => new StockMovement($"movement-{index}", DateTimeOffset.UtcNow,
                StockMovementType.Adjustment, 1, index));

        FluentActions.Invoking(() => new StockDetailSnapshot(CreateBalance(), movements, DateTimeOffset.UtcNow, true))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Snapshot_Should_Not_Infer_Movements_From_Balance()
    {
        var snapshot = new StockDetailSnapshot(CreateBalance(), [], DateTimeOffset.UtcNow, true);

        snapshot.Balance.PhysicalQuantity.Should().Be(9);
        snapshot.RecentMovements.Should().BeEmpty();
    }

    private static StockBalance CreateBalance() => new(
        "product-1", "Phone", "PHONE-1", "variant-1", "256GB", "PHONE-1-256",
        "phones", "Phones", 20_000_000m, 9, 2, 3);
}
