using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests;

public class PurchaseOrderEntityTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy_test;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void PurchaseOrderEntities_Should_Initialize_With_Defaults()
    {
        var supplier = new Supplier { Name = "FPT Synnex", Phone = "0901234567" };
        supplier.Id.Should().NotBeEmpty();
        supplier.IsActive.Should().BeTrue();
        supplier.PurchaseOrders.Should().NotBeNull();

        var po = new PurchaseOrder
        {
            SupplierId = supplier.Id,
            CreatedByUserId = Guid.NewGuid(),
            PoNumber = "PO-202610-001"
        };
        po.Id.Should().NotBeEmpty();
        po.Status.Should().Be(PurchaseOrderStatus.Draft);
        po.TotalCost.Should().Be(0);
        po.Items.Should().NotBeNull();

        var item = new PurchaseOrderItem
        {
            PurchaseOrderId = po.Id,
            ProductId = Guid.NewGuid(),
            OrderedQuantity = 10,
            UnitCost = 15000000m
        };
        item.Id.Should().NotBeEmpty();
        item.ReceivedQuantity.Should().Be(0);
    }

    [Fact]
    public void PurchaseOrderModel_Should_Configure_Composite_And_AreNullsDistinct()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        var supplierEntity = model.FindEntityType(typeof(Supplier));
        supplierEntity.Should().NotBeNull();
        supplierEntity!.GetTableName().Should().Be("suppliers");

        var poEntity = model.FindEntityType(typeof(PurchaseOrder));
        poEntity.Should().NotBeNull();
        poEntity!.GetTableName().Should().Be("purchase_orders");

        var poItemEntity = model.FindEntityType(typeof(PurchaseOrderItem));
        poItemEntity.Should().NotBeNull();
        poItemEntity!.GetTableName().Should().Be("purchase_order_items");

        // Verify AreNullsDistinct(false) unique index
        var uniqueIndex = poItemEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "uq_po_items_order_product_variant");
        uniqueIndex.Should().NotBeNull();
        uniqueIndex!.IsUnique.Should().BeTrue();
        // Check annotations
        uniqueIndex!.FindAnnotation("Npgsql:NullsDistinct")?.Value.Should().Be(false);
    }
}
