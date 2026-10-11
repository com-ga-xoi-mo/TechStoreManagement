using System;
using TechStore.Client.Models.Inventory;
using Xunit;

namespace TechStore.Client.Tests.Inventory;

public sealed class StockBalanceTests
{
    [Fact]
    public void StockBalance_AvailableQuantityIsPhysicalMinusReserved()
    {
        var balance = Create(physical: 10, reserved: 3, alert: 2);

        Assert.Equal(7, balance.AvailableQuantity);
        Assert.Equal(InventoryStockStatus.Available, balance.Status);
        Assert.Equal("VAR-1", balance.Sku);
    }

    [Fact]
    public async Task StockBalance_ExplicitlyIdentifiesSerialAndRegularPreviewBalances()
    {
        var page = await new TechStore.Client.Repositories.Inventory.FakeInventoryRepository()
            .GetStockPageAsync(new StockListQuery(PageSize: 20));

        Assert.True(page.Items[0].IsSerialTracked);
        Assert.False(page.Items[6].IsSerialTracked);
        Assert.All(page.Items, row => Assert.NotNull(row.IsSerialTracked));
    }

    [Theory]
    [InlineData(2, 1, 2, InventoryStockStatus.LowStock)]
    [InlineData(2, 2, 2, InventoryStockStatus.OutOfStock)]
    public void StockBalance_DerivesStatusFromAvailableAndPreviewThreshold(
        int physical, int reserved, int alert, InventoryStockStatus expected)
    {
        Assert.Equal(expected, Create(physical, reserved, alert).Status);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 2)]
    public void StockBalance_RejectsInvalidQuantityInvariants(int physical, int reserved)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(physical, reserved, 0));
    }

    private static StockBalance Create(int physical, int reserved, int alert) => new(
        "p-1", "Laptop", "LAP-1", "v-1", "16GB", "VAR-1", "laptops", "Laptop", 25_000_000m,
        physical, reserved, alert);
}
