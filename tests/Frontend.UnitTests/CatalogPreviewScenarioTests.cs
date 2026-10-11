using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class CatalogPreviewScenarioTests
{
    [Theory]
    [InlineData("populated", CatalogPreviewScenario.Populated)]
    [InlineData("EMPTY", CatalogPreviewScenario.Empty)]
    [InlineData("Delayed", CatalogPreviewScenario.Delayed)]
    [InlineData("error", CatalogPreviewScenario.Error)]
    [InlineData("missingimage", CatalogPreviewScenario.MissingImage)]
    [InlineData("unknown-value", CatalogPreviewScenario.Populated)]
    [InlineData(null, CatalogPreviewScenario.Populated)]
    public void Parse_MapsConfigurationToKnownScenarioOrPopulatedDefault(
        string? value,
        CatalogPreviewScenario expected)
    {
        Assert.Equal(expected, CatalogPreviewSettings.Parse(value));
    }

    [Fact]
    public async Task EmptyScenario_ReturnsNoProducts()
    {
        var repository = new FakeProductRepository(scenario: CatalogPreviewScenario.Empty);

        var result = await repository.SearchProductsAsync(new ProductListCriteria());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
    }

    [Fact]
    public async Task MissingImageScenario_ReturnsProductWithoutImageUrl()
    {
        var repository = new FakeProductRepository(scenario: CatalogPreviewScenario.MissingImage);

        var result = await repository.SearchProductsAsync(new ProductListCriteria());

        Assert.Null(Assert.Single(result.Items).ImageUrl);
    }

    [Fact]
    public async Task ErrorScenario_ThrowsConfiguredPreviewError()
    {
        var repository = new FakeProductRepository(scenario: CatalogPreviewScenario.Error);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SearchProductsAsync(new ProductListCriteria()));

        Assert.Contains("Configured Catalog preview error", error.Message);
    }

    [Fact]
    public async Task DelayedScenario_WaitsBeforeReturningResults()
    {
        var repository = new FakeProductRepository(scenario: CatalogPreviewScenario.Delayed);
        var startedAt = DateTime.UtcNow;

        var result = await repository.SearchProductsAsync(new ProductListCriteria());

        Assert.True(DateTime.UtcNow - startedAt >= TimeSpan.FromSeconds(1.8));
        Assert.NotEmpty(result.Items);
    }
}
