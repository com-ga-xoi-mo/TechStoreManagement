using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

public class InventoryEntityTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy_test;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void InventoryEntities_Should_Initialize_With_Defaults()
    {
        var stock = new InventoryStock
        {
            ProductId = Guid.NewGuid(),
            Quantity = 10,
            ReservedQuantity = 2
        };
        stock.Id.Should().NotBeEmpty();
        stock.MinStockAlert.Should().Be(5);
        stock.Movements.Should().NotBeNull();

        var movement = new InventoryMovement
        {
            InventoryStockId = stock.Id,
            MovementType = InventoryMovementType.OpeningBalance,
            QuantityChange = 10,
            QuantityAfter = 10,
            PerformedByUserId = Guid.NewGuid()
        };
        movement.Id.Should().NotBeEmpty();

        var serial = new SerialImei
        {
            ProductId = stock.ProductId,
            Imei = "123456789012345",
            Status = SerialImeiStatus.InStock
        };
        serial.Id.Should().NotBeEmpty();
        serial.WarrantyMonths.Should().Be(12);
        serial.Status.Should().Be(SerialImeiStatus.InStock);
    }

    [Fact]
    public void InventoryModel_Should_Configure_Constraints_And_Indexes()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        var stockEntity = model.FindEntityType(typeof(InventoryStock));
        stockEntity.Should().NotBeNull();
        stockEntity!.GetTableName().Should().Be("inventory_stocks");

        var stockUniqueIndex = stockEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "idx_inventory_stocks_product_variant");
        stockUniqueIndex.Should().NotBeNull();
        stockUniqueIndex!.IsUnique.Should().BeTrue();
        stockUniqueIndex.FindAnnotation("Npgsql:NullsDistinct")?.Value.Should().Be(false);

        var movementEntity = model.FindEntityType(typeof(InventoryMovement));
        movementEntity.Should().NotBeNull();
        movementEntity!.GetTableName().Should().Be("inventory_movements");

        var serialEntity = model.FindEntityType(typeof(SerialImei));
        serialEntity.Should().NotBeNull();
        serialEntity!.GetTableName().Should().Be("serial_imeis");

        var inStockIndex = serialEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "idx_serial_imeis_instock");
        inStockIndex.Should().NotBeNull();
        inStockIndex!.GetFilter().Should().Be("status = 'InStock'");

        var serialComposite = serialEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "uq_serial_imeis_id_product");
        serialComposite.Should().NotBeNull();
        serialComposite!.IsUnique.Should().BeTrue();
    }
}
