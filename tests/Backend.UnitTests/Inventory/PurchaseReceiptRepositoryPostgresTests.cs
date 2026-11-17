using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Shared.Enums;
using Xunit;

namespace TechStore.UnitTests.Inventory;

public class PurchaseReceiptRepositoryPostgresTests
{
    [PostgresFact]
    public async Task SearchReceiptsAsync_Should_Project_Summary_Fields_And_Support_Literal_Search()
    {
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var context = database.CreateContext();

        // Arrange
        var receiver = new User
        {
            Username = "receiver_user",
            Email = "receiver@example.com",
            PasswordHash = "hash",
            FullName = "Nguyen Van Receiver",
            IsActive = true
        };
        var creator = new User
        {
            Username = "creator_user",
            Email = "creator@example.com",
            PasswordHash = "hash",
            FullName = "Tran Van Creator",
            IsActive = true
        };
        var supplier1 = new Supplier
        {
            Name = "100% Original Tech Ltd",
            Phone = "0123456789",
            IsActive = true
        };
        var supplier2 = new Supplier
        {
            Name = "FPT Trading",
            Phone = "0987654321",
            IsActive = true
        };
        var category = new Category { Name = "Accessories", Slug = "accessories", IsActive = true };
        var product1 = new Product
        {
            CategoryId = category.Id,
            Category = category,
            Name = "USB Cable",
            Sku = "CBL-001",
            Brand = "Generic",
            BasePrice = 50000m,
            CostPrice = 30000m
        };
        var product2 = new Product
        {
            CategoryId = category.Id,
            Category = category,
            Name = "Power Adapter",
            Sku = "ADP-001",
            Brand = "Generic",
            BasePrice = 120000m,
            CostPrice = 70000m
        };

        context.Users.AddRange(receiver, creator);
        context.Suppliers.AddRange(supplier1, supplier2);
        context.Categories.Add(category);
        context.Products.AddRange(product1, product2);
        await context.SaveChangesAsync();

        var po1 = new PurchaseOrder
        {
            SupplierId = supplier1.Id,
            CreatedByUserId = creator.Id,
            ReceivedByUserId = receiver.Id,
            PoNumber = "PN-20261116-001",
            SupplierDocumentNumber = "DOC_123",
            Status = PurchaseOrderStatus.Received,
            TotalCost = 150000m,
            ReceivedAt = DateTime.UtcNow.AddMinutes(-10),
            Items =
            [
                new PurchaseOrderItem
                {
                    ProductId = product1.Id,
                    OrderedQuantity = 10,
                    ReceivedQuantity = 10,
                    UnitCost = 10000m
                },
                new PurchaseOrderItem
                {
                    ProductId = product2.Id,
                    OrderedQuantity = 5,
                    ReceivedQuantity = 5,
                    UnitCost = 10000m
                }
            ]
        };

        var po2 = new PurchaseOrder
        {
            SupplierId = supplier2.Id,
            CreatedByUserId = creator.Id,
            ReceivedByUserId = null,
            PoNumber = "PN-20261116-002",
            SupplierDocumentNumber = "DOCX456",
            Status = PurchaseOrderStatus.Received,
            TotalCost = 200000m,
            ReceivedAt = DateTime.UtcNow.AddMinutes(-5),
            Items =
            [
                new PurchaseOrderItem
                {
                    ProductId = product1.Id,
                    OrderedQuantity = 20,
                    ReceivedQuantity = 20,
                    UnitCost = 10000m
                }
            ]
        };

        context.PurchaseOrders.AddRange(po1, po2);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new PurchaseOrderRepository(context);

        // Act 1: Projection check (all receipts)
        var (items, totalCount) = await repository.SearchReceiptsAsync(
            new PurchaseReceiptSearchCriteria(null, null, 1, 20));

        // Assert 1: Summary projection fields
        totalCount.Should().Be(2);
        var item1 = items.First(x => x.PoNumber == "PN-20261116-001");
        item1.SupplierName.Should().Be("100% Original Tech Ltd");
        item1.SupplierDocumentNumber.Should().Be("DOC_123");
        item1.ReceivedByName.Should().Be("Nguyen Van Receiver");
        item1.LineCount.Should().Be(2);
        item1.TotalQuantity.Should().Be(15);
        item1.TotalCost.Should().Be(150000m);

        var item2 = items.First(x => x.PoNumber == "PN-20261116-002");
        item2.SupplierName.Should().Be("FPT Trading");
        item2.ReceivedByName.Should().BeNull();
        item2.LineCount.Should().Be(1);
        item2.TotalQuantity.Should().Be(20);
        item2.TotalCost.Should().Be(200000m);

        // Act 2: Literal '%' search
        var (percentResults, percentCount) = await repository.SearchReceiptsAsync(
            new PurchaseReceiptSearchCriteria("%", null, 1, 20));

        // Assert 2: Matches only "100% Original Tech Ltd"
        percentCount.Should().Be(1);
        percentResults.Should().ContainSingle(x => x.PoNumber == "PN-20261116-001");

        // Act 3: Literal '_' search
        var (underscoreResults, underscoreCount) = await repository.SearchReceiptsAsync(
            new PurchaseReceiptSearchCriteria("DOC_", null, 1, 20));

        // Assert 3: Matches only "DOC_123", does not match "DOCX456"
        underscoreCount.Should().Be(1);
        underscoreResults.Should().ContainSingle(x => x.PoNumber == "PN-20261116-001");
    }
}
