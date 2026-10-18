using FluentAssertions;
using TechStore.Api.Common.Data;
using Xunit;

namespace TechStore.UnitTests;

public class DemoSeedManifestTests
{
    [Fact]
    public void Build_Should_UseStableIdentifiersAndTimestamps()
    {
        // Arrange
        var categoryIdsBefore = DemoSeedManifest.Categories.Select(x => DemoSeedManifest.Id($"category:{x.Slug}")).ToArray();
        var productIdsBefore = DemoSeedManifest.Products.Select(x => DemoSeedManifest.Id($"product:{x.Sku}")).ToArray();
        var customerIdsBefore = DemoSeedManifest.Customers.Select(x => DemoSeedManifest.Id($"customer:{x.Phone}")).ToArray();
        var orderIdsBefore = DemoSeedManifest.Orders.Select(x => DemoSeedManifest.Id($"order:{x.OrderCode}")).ToArray();

        // Act
        var categoryIdsAfter = DemoSeedManifest.Categories.Select(x => DemoSeedManifest.Id($"category:{x.Slug}")).ToArray();
        var productIdsAfter = DemoSeedManifest.Products.Select(x => DemoSeedManifest.Id($"product:{x.Sku}")).ToArray();
        var customerIdsAfter = DemoSeedManifest.Customers.Select(x => DemoSeedManifest.Id($"customer:{x.Phone}")).ToArray();
        var orderIdsAfter = DemoSeedManifest.Orders.Select(x => DemoSeedManifest.Id($"order:{x.OrderCode}")).ToArray();

        // Assert
        categoryIdsAfter.Should().Equal(categoryIdsBefore);
        productIdsAfter.Should().Equal(productIdsBefore);
        customerIdsAfter.Should().Equal(customerIdsBefore);
        orderIdsAfter.Should().Equal(orderIdsBefore);
        DemoSeedManifest.AnchorUtc.Should().Be(new DateTime(2026, 10, 18, 5, 0, 0, DateTimeKind.Utc));
        DemoSeedManifest.Products.Select(x => x.CreatedAt).Should().OnlyContain(x => x.Kind == DateTimeKind.Utc);
        DemoSeedManifest.Orders.SelectMany(x => new[] { x.CreatedAt, x.PaidAt, x.CompletedAt }.Where(t => t.HasValue).Select(t => t!.Value))
            .Should().OnlyContain(x => x.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public void Manifest_Should_HaveExpandedCountsAndValidUniqueImeis()
    {
        // Arrange / Act
        var imeis = DemoSeedManifest.Serials.Select(x => x.Imei).ToArray();

        // Assert
        DemoSeedManifest.Categories.Should().HaveCount(5);
        DemoSeedManifest.Products.Should().HaveCount(20);
        DemoSeedManifest.Customers.Should().HaveCount(10);
        DemoSeedManifest.Orders.Should().HaveCount(20);
        imeis.Should().HaveCount(50).And.OnlyHaveUniqueItems();
        imeis.Should().OnlyContain(x => x.Length == 15 && x.All(char.IsAsciiDigit));
        DemoSeedManifest.Orders.Select(x => x.Status).Distinct().Should().HaveCount(4);
    }
}
