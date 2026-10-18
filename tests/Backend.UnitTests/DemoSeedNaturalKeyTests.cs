using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Sales.Entities;
using Xunit;

namespace TechStore.UnitTests;

public class DemoSeedNaturalKeyTests
{
    [Fact]
    public void FindSingle_Should_ReconcileLegacyRowsByDomainNaturalKeys()
    {
        // Arrange: these rows deliberately have arbitrary legacy IDs.
        var category = new Category { Id = Guid.NewGuid(), Slug = "smartphones" };
        var product = new Product { Id = Guid.NewGuid(), Sku = "IP15PM-256" };
        var variant = new ProductVariant { Id = Guid.NewGuid(), Sku = "IP15PM-256-NT" };
        var customer = new Customer { Id = Guid.NewGuid(), Phone = "0901234567" };
        var order = new Order { Id = Guid.NewGuid(), OrderCode = "ORD-202610-0001" };
        var serial = new SerialImei { Id = Guid.NewGuid(), Imei = "358912345678901" };
        var role = new Role { Id = Guid.NewGuid(), Name = TechStore.Shared.Enums.RoleType.Admin };
        var user = new User { Id = Guid.NewGuid(), Username = "admin", Email = "admin@techstore.vn" };
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "FPT Synnex Distribution" };
        var po = new PurchaseOrder { Id = Guid.NewGuid(), PoNumber = "PO-DEMO-202610-001" };
        var purchaseItem = new PurchaseOrderItem { Id = Guid.NewGuid(), PurchaseOrderId = po.Id, ProductId = product.Id };
        var stock = new InventoryStock { Id = Guid.NewGuid(), ProductId = product.Id, VariantId = variant.Id };
        var voucher = new Voucher { Id = Guid.NewGuid(), Code = "DEMO-OCT26" };
        var session = new PosSession { Id = Guid.NewGuid(), CashierUserId = user.Id, OpenedAt = DateTime.UnixEpoch };
        var orderItem = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id };
        var payment = new VietQrTransaction { Id = Guid.NewGuid(), TransactionCode = "DQR-ORD-1" };

        // Act / Assert
        DemoSeedNaturalKey.FindSingle([category], x => x.Slug, " Smartphones ", "category").Should().BeSameAs(category);
        DemoSeedNaturalKey.FindSingle([product], x => x.Sku, "ip15pm-256", "product").Should().BeSameAs(product);
        DemoSeedNaturalKey.FindSingle([variant], x => x.Sku, "IP15PM-256-NT", "variant").Should().BeSameAs(variant);
        DemoSeedNaturalKey.FindSingle([customer], x => x.Phone, "0901234567", "customer").Should().BeSameAs(customer);
        DemoSeedNaturalKey.FindSingle([order], x => x.OrderCode, "ORD-202610-0001", "order").Should().BeSameAs(order);
        DemoSeedNaturalKey.FindSingle([serial], x => x.Imei!, "358912345678901", "IMEI").Should().BeSameAs(serial);
        DemoSeedNaturalKey.FindSingle([role], x => x.Name.ToString(), "Admin", "role").Should().BeSameAs(role);
        DemoSeedNaturalKey.FindSingle([user], x => x.Username, "ADMIN", "user").Should().BeSameAs(user);
        DemoSeedNaturalKey.FindSingle([supplier], x => x.Name, "fpt synnex distribution", "supplier").Should().BeSameAs(supplier);
        DemoSeedNaturalKey.FindSingle([po], x => x.PoNumber, "PO-DEMO-202610-001", "purchase order").Should().BeSameAs(po);
        DemoSeedNaturalKey.FindSingle([purchaseItem], x => $"{x.PurchaseOrderId}:{x.ProductId}:{x.VariantId}", $"{po.Id}:{product.Id}:", "purchase item").Should().BeSameAs(purchaseItem);
        DemoSeedNaturalKey.FindSingle([stock], x => $"{x.ProductId}:{x.VariantId}", $"{product.Id}:{variant.Id}", "stock balance").Should().BeSameAs(stock);
        DemoSeedNaturalKey.FindSingle([voucher], x => x.Code, "demo-oct26", "voucher").Should().BeSameAs(voucher);
        DemoSeedNaturalKey.FindSingle([session], x => $"{x.CashierUserId}:{x.OpenedAt:O}", $"{user.Id}:{DateTime.UnixEpoch:O}", "POS session").Should().BeSameAs(session);
        DemoSeedNaturalKey.FindSingle([orderItem], x => $"{x.OrderId}:line-1", $"{order.Id}:line-1", "order item").Should().BeSameAs(orderItem);
        DemoSeedNaturalKey.FindSingle([payment], x => x.TransactionCode, "DQR-ORD-1", "VietQR transaction").Should().BeSameAs(payment);
    }

    [Fact]
    public void FindSingle_WhenNaturalKeyIsDuplicated_ShouldFailClearly()
    {
        // Arrange
        var rows = new[]
        {
            new Category { Slug = "smartphones" },
            new Category { Slug = "smartphones" }
        };

        // Act
        var action = () => DemoSeedNaturalKey.FindSingle(rows, x => x.Slug, "smartphones", "category");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Demo seed conflict for category 'smartphones': duplicate natural key.");
    }
}
