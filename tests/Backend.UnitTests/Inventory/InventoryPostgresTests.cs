using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;

namespace TechStore.UnitTests.Inventory;

public class InventoryPostgresTests
{
    [PostgresFact]
    public async Task SearchStocks_PercentInSearch_MatchesLiterally()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync();
        await using var context = database.CreateContext();

        var category = new Category { Name = "Keyboards", Slug = "keyboards", IsActive = true };
        var p1 = new Product
        {
            CategoryId = category.Id,
            Category = category,
            Name = "60% Mechanical Keyboard",
            Sku = "KB-60PCT",
            Brand = "Keychron",
            BasePrice = 2000000m,
            CostPrice = 1500000m
        };
        var p2 = new Product
        {
            CategoryId = category.Id,
            Category = category,
            Name = "Full Size Keyboard",
            Sku = "KB-100",
            Brand = "Keychron",
            BasePrice = 2500000m,
            CostPrice = 1800000m
        };
        context.Categories.Add(category);
        context.Products.AddRange(p1, p2);
        context.InventoryStocks.AddRange(
            new InventoryStock { ProductId = p1.Id, Quantity = 5 },
            new InventoryStock { ProductId = p2.Id, Quantity = 5 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new InventoryStockRepository(context);

        // Act
        var (items, totalCount) = await repository.SearchStocksAsync(
            new InventoryStockSearchCriteria("%", null, null, null, null, null, null, 1, 20));

        // Assert
        totalCount.Should().Be(1);
        items.Should().ContainSingle().Which.Stock.ProductId.Should().Be(p1.Id);
    }
}
