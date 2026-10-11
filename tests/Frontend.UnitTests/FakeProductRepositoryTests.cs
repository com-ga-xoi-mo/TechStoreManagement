using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class FakeProductRepositoryTests
{
    [Fact]
    public async Task Search_AppliesCaseInsensitiveNameAndVariantSkuMatching()
    {
        var repository = new FakeProductRepository(
        [
            Product("one", "MacBook Air", "Apple", "laptops", 25_000_000m, ProductStatus.InStock, 5),
            Product("two", "Galaxy Phone", "Samsung", "phones", 15_000_000m, ProductStatus.LowStock, 2)
        ]);

        var byName = await repository.SearchProductsAsync(new ProductListCriteria(SearchQuery: "MACBOOK"));
        var bySku = await repository.SearchProductsAsync(new ProductListCriteria(SearchQuery: "SKU-TWO"));

        Assert.Equal("one", Assert.Single(byName.Items).Id);
        Assert.Equal("two", Assert.Single(bySku.Items).Id);
    }

    [Fact]
    public async Task Search_AppliesCategoryBrandPriceAndStockTogetherAndCountsAllMatches()
    {
        var repository = new FakeProductRepository(
        [
            Product("match-1", "Phone One", "Apple", "phones", 10_000_000m, ProductStatus.LowStock, 2),
            Product("match-2", "Phone Two", "Apple", "phones", 25_000_000m, ProductStatus.LowStock, 1),
            Product("wrong-brand", "Phone Three", "Samsung", "phones", 15_000_000m, ProductStatus.LowStock, 2),
            Product("wrong-stock", "Phone Four", "Apple", "phones", 15_000_000m, ProductStatus.InStock, 5),
            Product("wrong-category", "Laptop One", "Apple", "laptops", 15_000_000m, ProductStatus.LowStock, 2)
        ]);

        var result = await repository.SearchProductsAsync(new ProductListCriteria(
            CategoryId: "PHONES",
            Brand: "apple",
            PriceBand: ProductPriceBand.From10To25Million,
            StockStatus: ProductStatus.LowStock,
            PageSize: 1));

        Assert.Equal(2, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal("match-1", Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Search_UsesStartingPriceForInclusivePriceBands()
    {
        var repository = new FakeProductRepository(
        [
            Product("below", "Below", "Brand", "phones", 9_999_999m, ProductStatus.InStock, 1),
            Product("ten", "Ten", "Brand", "phones", 10_000_000m, ProductStatus.InStock, 1),
            Product("twenty-five", "Twenty five", "Brand", "phones", 25_000_000m, ProductStatus.InStock, 1),
            Product("above", "Above", "Brand", "phones", 25_000_001m, ProductStatus.InStock, 1)
        ]);

        var under = await repository.SearchProductsAsync(new ProductListCriteria(PriceBand: ProductPriceBand.Under10Million));
        var middle = await repository.SearchProductsAsync(new ProductListCriteria(PriceBand: ProductPriceBand.From10To25Million));
        var above = await repository.SearchProductsAsync(new ProductListCriteria(PriceBand: ProductPriceBand.Above25Million));

        Assert.Equal(["below"], under.Items.Select(product => product.Id));
        Assert.Equal(["ten", "twenty-five"], middle.Items.Select(product => product.Id));
        Assert.Equal(["above"], above.Items.Select(product => product.Id));
    }

    [Fact]
    public async Task Search_SortsNewestPriceAndStockWithStableIdTieBreaks()
    {
        var sameDate = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var repository = new FakeProductRepository(
        [
            Product("c", "C", "Brand", "phones", 20m, ProductStatus.InStock, 5, sameDate),
            Product("a", "A", "Brand", "phones", 10m, ProductStatus.InStock, 5, sameDate),
            Product("b", "B", "Brand", "phones", 10m, ProductStatus.InStock, 9, sameDate),
            Product("newest", "Newest", "Brand", "phones", 30m, ProductStatus.InStock, 1, sameDate.AddDays(1))
        ]);

        var newest = await repository.SearchProductsAsync(new ProductListCriteria());
        var ascending = await repository.SearchProductsAsync(new ProductListCriteria(SortMode: ProductSortMode.PriceAscending));
        var descending = await repository.SearchProductsAsync(new ProductListCriteria(SortMode: ProductSortMode.PriceDescending));
        var stock = await repository.SearchProductsAsync(new ProductListCriteria(SortMode: ProductSortMode.StockDescending));

        Assert.Equal(["newest", "a", "b", "c"], newest.Items.Select(product => product.Id));
        Assert.Equal(["a", "b", "c", "newest"], ascending.Items.Select(product => product.Id));
        Assert.Equal(["newest", "c", "a", "b"], descending.Items.Select(product => product.Id));
        Assert.Equal(["b", "a", "c", "newest"], stock.Items.Select(product => product.Id));
    }

    [Fact]
    public async Task Search_ClampsOutOfRangePageAndKeepsFilteredTotal()
    {
        var repository = new FakeProductRepository(
        [
            Product("one", "One", "Brand", "phones", 10m, ProductStatus.InStock, 1),
            Product("two", "Two", "Brand", "phones", 20m, ProductStatus.InStock, 1),
            Product("three", "Three", "Brand", "phones", 30m, ProductStatus.InStock, 1)
        ]);

        var result = await repository.SearchProductsAsync(new ProductListCriteria(PageIndex: 99, PageSize: 2));

        Assert.Equal(3, result.TotalItems);
        Assert.Equal(1, result.PageIndex);
        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task Search_UsesEightItemDefaultPagesAndKeepsTotalAcrossPages()
    {
        var products = Enumerable.Range(1, 10)
            .Select(index => Product($"product-{index:D2}", $"Product {index}", "Brand", "phones", index, ProductStatus.InStock, 1));
        var repository = new FakeProductRepository(products);

        var firstPage = await repository.SearchProductsAsync(new ProductListCriteria());
        var secondPage = await repository.SearchProductsAsync(new ProductListCriteria(PageIndex: 1));

        Assert.Equal(10, firstPage.TotalItems);
        Assert.Equal(8, firstPage.PageSize);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(8, firstPage.Items.Count);
        Assert.Equal(2, secondPage.Items.Count);
        Assert.Equal(10, secondPage.TotalItems);
        Assert.Equal("product-09", secondPage.Items[0].Id);
    }

    private static ProductSummary Product(
        string id,
        string name,
        string brand,
        string categoryId,
        decimal startingPrice,
        ProductStatus status,
        int availableQuantity,
        DateTimeOffset? createdAt = null) =>
        new(
            Id: id,
            Name: name,
            Brand: brand,
            CategoryId: categoryId,
            CategoryName: categoryId,
            ImageUrl: null,
            Status: status,
            Variants:
            [
                new ProductVariantSummary($"{id}-1", "Base", $"SKU-{id}", startingPrice, availableQuantity),
                new ProductVariantSummary($"{id}-2", "Premium", $"SKU-{id}-PREMIUM", startingPrice + 5m, availableQuantity)
            ],
            CreatedAt: createdAt ?? DateTimeOffset.UnixEpoch);
}
