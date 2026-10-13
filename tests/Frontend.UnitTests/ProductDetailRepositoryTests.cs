using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class ProductDetailRepositoryTests
{
    [Fact]
    public async Task GetProductDetail_ReusesListProductAndVariantIdsAndValues()
    {
        var repository = new FakeProductRepository();
        var summary = (await repository.SearchProductsAsync(new ProductListCriteria(SearchQuery: "IP15PM-256-NAT")))
            .Items.Single();

        var detail = await repository.GetProductDetailAsync(summary.Id);

        Assert.NotNull(detail);
        Assert.Equal(summary.Id, detail.Id);
        Assert.Equal(summary.Name, detail.Name);
        Assert.Equal(summary.ImageUrl, detail.ImageUrl);
        Assert.Equal(summary.Variants.Select(item => item.Id), detail.Variants.Select(item => item.Id));
        Assert.Equal(summary.Variants.Select(item => item.Sku), detail.Variants.Select(item => item.Sku));
        Assert.Equal(summary.Variants.Select(item => item.SellingPrice), detail.Variants.Select(item => item.SellingPrice));
        Assert.Equal(summary.Variants.Select(item => item.AvailableQuantity), detail.Variants.Select(item => item.AvailableQuantity));
        Assert.True(detail.IsPreviewData);
    }

    [Fact]
    public async Task GetProductDetail_ReturnsNullForUnknownOrEmptyScenarioProduct()
    {
        var populated = new FakeProductRepository();
        var empty = new FakeProductRepository(scenario: CatalogPreviewScenario.Empty);

        Assert.Null(await populated.GetProductDetailAsync("does-not-exist"));
        Assert.Null(await empty.GetProductDetailAsync("iphone-15-pro-max"));
    }

    [Fact]
    public async Task GetProductDetail_ContainsOnlyActualVariantTuples()
    {
        var repository = new FakeProductRepository();
        var detail = await repository.GetProductDetailAsync("iphone-15-pro-max");

        Assert.NotNull(detail);
        Assert.Equal(2, detail.Variants.Count);
        Assert.Contains(detail.Variants, item => item.StorageLabel == "256GB" && item.ColorLabel == "Titan Tự nhiên");
        Assert.Contains(detail.Variants, item => item.StorageLabel == "256GB" && item.ColorLabel == "Titan Xanh");
        Assert.DoesNotContain(detail.Variants, item => item.StorageLabel == "512GB");
    }

    [Fact]
    public async Task GetProductDetail_UsesExplicitErrorDelayAndCancellationFixtures()
    {
        var error = new FakeProductRepository(scenario: CatalogPreviewScenario.Error);
        var delayed = new FakeProductRepository(scenario: CatalogPreviewScenario.Delayed);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAsync<InvalidOperationException>(() => error.GetProductDetailAsync("iphone-15-pro-max"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delayed.GetProductDetailAsync("iphone-15-pro-max", canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FakeProductRepository().GetProductDetailAsync("iphone-15-pro-max", canceled.Token));
    }

    [Fact]
    public async Task GetProductDetail_CanConfigureUnavailableAndErrorWithoutHidingTheList()
    {
        var unavailable = new FakeProductRepository(detailScenario: ProductDetailPreviewScenario.Unavailable);
        var error = new FakeProductRepository(detailScenario: ProductDetailPreviewScenario.Error);

        Assert.NotEmpty((await unavailable.SearchProductsAsync(new ProductListCriteria())).Items);
        Assert.Null(await unavailable.GetProductDetailAsync("iphone-15-pro-max"));
        Assert.NotEmpty((await error.SearchProductsAsync(new ProductListCriteria())).Items);
        await Assert.ThrowsAsync<InvalidOperationException>(() => error.GetProductDetailAsync("iphone-15-pro-max"));
    }

    [Fact]
    public async Task UnavailableRepository_DoesNotFallbackAndHonorsCancellation()
    {
        var repository = new UnavailableProductRepository();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetProductDetailAsync("iphone-15-pro-max"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetProductDetailAsync("iphone-15-pro-max", canceled.Token));
    }
}
