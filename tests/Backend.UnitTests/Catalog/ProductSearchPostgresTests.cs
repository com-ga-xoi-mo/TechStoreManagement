using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Inventory.Entities;

namespace TechStore.UnitTests.Catalog;

public class ProductSearchPostgresTests
{
    [PostgresFact]
    public async Task SearchAsync_CombinesKeywordsSerialFiltersAndStablePaging()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var context = database.CreateContext();
        var category = new Category { Name = "Computers", Slug = "computers" };
        var first = new Product { Category = category, Name = "MacBook Air M3", Sku = "MAC-1", Brand = "Apple", BasePrice = 100, Specs = "{\"ram_gb\":16}", Description = "Portable workstation" };
        var second = new Product { Category = category, Name = "MacBook Air M3", Sku = "MAC-2", Brand = "Apple", BasePrice = 100, Specs = "{\"ram_gb\":16}" };
        var wrongBrand = new Product { Category = category, Name = "MacBook Air M3", Sku = "MAC-3", Brand = "Other", BasePrice = 100, Specs = "{\"ram_gb\":16}" };
        var wrongPrice = new Product { Category = category, Name = "MacBook Air M3", Sku = "MAC-4", Brand = "Apple", BasePrice = 300, Specs = "{\"ram_gb\":16}" };
        var wrongSpec = new Product { Category = category, Name = "MacBook Air M3", Sku = "MAC-5", Brand = "Apple", BasePrice = 100, Specs = "{\"ram_gb\":8}" };
        var literal = new Product { Category = category, Name = @"Literal 50%_\ marker", Sku = "LITERAL", Brand = "Other", BasePrice = 100 };
        first.Variants.Add(new ProductVariant { VariantName = "Midnight", Sku = "VARIANT-1", Barcode = "VAR-BAR", Price = 100, Specs = "{}" });
        context.Products.AddRange(first, second, wrongBrand, wrongPrice, wrongSpec, literal);
        context.SerialImeis.Add(new SerialImei { Product = first, SerialNumber = "Serial%_\\AbC123" });
        context.SerialImeis.Add(new SerialImei { Product = second, Imei = "123456789012345" });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new ProductRepository(context);
        var criteria = new ProductSearchCriteria(null, null, null, null, null, null, null, ProductSortField.Name, false, 1, 100);
        async Task<IReadOnlyList<Guid>> Ids(string keyword) =>
            (await repository.SearchAsync(criteria with { Keyword = keyword })).Items.Select(p => p.Id).ToArray();

        // Act and Assert: execute each search and check its results.
        (await Ids("  macbook\tM3  ")).Should().BeEquivalentTo(new[] { first.Id, second.Id, wrongBrand.Id, wrongPrice.Id, wrongSpec.Id });
        (await Ids("macbook missing")).Should().BeEmpty();
        (await Ids("portable workstation")).Should().Equal(first.Id);
        (await Ids("apple midnight")).Should().Equal(first.Id);
        (await Ids("var-bar")).Should().Equal(first.Id);
        (await Ids("  serial%_\\abc123  ")).Should().Equal(first.Id);
        (await Ids("serial%_\\")).Should().BeEmpty();
        (await Ids("  123456789012345  ")).Should().Equal(second.Id);
        (await Ids("12345678")).Should().BeEmpty();
        (await Ids(@"50%_\")).Should().Equal(literal.Id);
        (await Ids("50%X")).Should().BeEmpty();
        (await Ids("macbook air m3 apple mac book air m3 ignored")).Should().BeEquivalentTo(new[] { first.Id, second.Id, wrongPrice.Id, wrongSpec.Id });

        var filtered = criteria with { Keyword = "macbook m3", Brand = "apple", MinPrice = 90, MaxPrice = 110, SpecFilterJson = "{\"ram_gb\":16}", PageSize = 1 };
        var page1 = await repository.SearchAsync(filtered);
        var page2 = await repository.SearchAsync(filtered with { Page = 2 });
        var all = await repository.SearchAsync(filtered with { PageSize = 100 });
        page1.TotalCount.Should().Be(2);
        page2.TotalCount.Should().Be(2);
        all.TotalCount.Should().Be(2);
        page1.Items.Concat(page2.Items).Select(p => p.Id).Should().Equal(all.Items.Select(p => p.Id));
        all.Items.Select(p => p.Id).Should().BeEquivalentTo(new[] { first.Id, second.Id });

        var serialFiltered = filtered with { Keyword = "serial%_\\abc123", PageSize = 100 };
        (await repository.SearchAsync(serialFiltered)).TotalCount.Should().Be(1);
        (await repository.SearchAsync(serialFiltered with { Brand = "Other" })).TotalCount.Should().Be(0);
        (await repository.SearchAsync(serialFiltered with { MinPrice = 200 })).TotalCount.Should().Be(0);
        (await repository.SearchAsync(serialFiltered with { SpecFilterJson = "{\"ram_gb\":8}" })).TotalCount.Should().Be(0);

        var imeiFiltered = filtered with { Keyword = "123456789012345", PageSize = 100 };
        (await repository.SearchAsync(imeiFiltered)).TotalCount.Should().Be(1);
        (await repository.SearchAsync(imeiFiltered with { Brand = "Other" })).TotalCount.Should().Be(0);
        (await repository.SearchAsync(imeiFiltered with { MinPrice = 200 })).TotalCount.Should().Be(0);
        (await repository.SearchAsync(imeiFiltered with { MaxPrice = 90 })).TotalCount.Should().Be(0);
        (await repository.SearchAsync(imeiFiltered with { SpecFilterJson = "{\"ram_gb\":8}" })).TotalCount.Should().Be(0);
    }
}
