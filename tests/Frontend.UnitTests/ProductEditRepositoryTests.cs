using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class ProductEditRepositoryTests
{
    private const string ProductId = "iphone-15-pro-max";

    [Fact]
    public async Task GetProductForEdit_OmitsCostWhenQueryDoesNotGrantCostAccess()
    {
        var repository = new FakeProductRepository();

        var permitted = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));
        var restricted = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: false));

        Assert.NotNull(permitted?.CostPrice);
        Assert.All(permitted!.Variants, variant => Assert.NotNull(variant.CostPrice));
        Assert.Null(restricted?.CostPrice);
        Assert.All(restricted!.Variants, variant => Assert.Null(variant.CostPrice));
    }

    [Fact]
    public async Task UpdateProduct_ChangesCatalogFieldsButPreservesVariantAvailability()
    {
        var repository = new FakeProductRepository();
        var original = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));
        var draft = DraftFrom(original!) with { Name = "Updated preview phone" };

        var result = await repository.UpdateProductAsync(draft);
        var saved = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));
        var list = await repository.SearchProductsAsync(new ProductListCriteria(SearchQuery: "Updated preview phone"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated preview phone", saved?.Name);
        Assert.Equal(original!.AvailableQuantity, saved?.AvailableQuantity);
        Assert.Equal(original.Variants.Select(variant => variant.AvailableQuantity), saved!.Variants.Select(variant => variant.AvailableQuantity));
        Assert.Equal("Updated preview phone", Assert.Single(list.Items).Name);
    }

    [Theory]
    [InlineData(ProductEditPreviewScenario.SaveError)]
    [InlineData(ProductEditPreviewScenario.DuplicateSku)]
    [InlineData(ProductEditPreviewScenario.DuplicateBarcode)]
    [InlineData(ProductEditPreviewScenario.ConcurrentUpdate)]
    public async Task UpdateProduct_FailedOrConflictingScenarioDoesNotMutateCatalog(ProductEditPreviewScenario scenario)
    {
        var repository = new FakeProductRepository(editScenario: scenario);
        var original = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));

        var result = await repository.UpdateProductAsync(DraftFrom(original!) with { Name = "Must not be saved" });
        var after = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));

        Assert.False(result.IsSuccess);
        Assert.Equal(original!.Name, after?.Name);
    }

    [Fact]
    public async Task UpdateProduct_RejectsChangingVariantIdentifierWhenPreviewHasHistory()
    {
        var repository = new FakeProductRepository(editScenario: ProductEditPreviewScenario.VariantHasTransactions);
        var original = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));
        var firstVariant = original!.Variants[0];
        var modifiedVariant = DraftFrom(original) with
        {
            Variants = DraftFrom(original).Variants
                .Select((variant, index) => index == 0 ? variant with { Sku = "CHANGED-SKU" } : variant)
                .ToArray()
        };

        var result = await repository.UpdateProductAsync(modifiedVariant);
        var after = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));

        Assert.Equal(ProductEditConflictKind.VariantHasTransactions, result.ConflictKind);
        Assert.Equal(firstVariant.Sku, after?.Variants[0].Sku);
    }

    [Fact]
    public async Task UpdateProduct_RejectsDuplicateIdentifiersWithinDraftAndKeepsStoredCatalog()
    {
        var repository = new FakeProductRepository();
        var original = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));
        var draft = DraftFrom(original!) with
        {
            Variants = DraftFrom(original!).Variants
                .Select((variant, index) => index == 1 ? variant with { Sku = original!.Variants[0].Sku } : variant)
                .ToArray()
        };

        var result = await repository.UpdateProductAsync(draft);
        var after = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));

        Assert.Equal(ProductEditResultStatus.Conflict, result.Status);
        Assert.Equal(ProductEditConflictKind.DuplicateSku, result.ConflictKind);
        Assert.Equal(original!.Variants.Select(variant => variant.Sku), after!.Variants.Select(variant => variant.Sku));
    }

    [Fact]
    public async Task GetProductForEdit_ReturnsNullForUnknownProduct()
    {
        var repository = new FakeProductRepository();

        var result = await repository.GetProductForEditAsync(new ProductEditQuery("missing", IncludeCostFields: false));

        Assert.Null(result);
    }

    [Fact]
    public async Task DelayedEditLoad_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        var repository = new FakeProductRepository(editScenario: ProductEditPreviewScenario.DelayedLoad);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: false), cancellation.Token));
    }

    [Fact]
    public async Task DelayedEditSave_ObservesCancellationWithoutChangingData()
    {
        var repository = new FakeProductRepository(editScenario: ProductEditPreviewScenario.DelayedSave);
        var original = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.UpdateProductAsync(DraftFrom(original!) with { Name = "Cancelled update" }, cancellation.Token));
        var after = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));

        Assert.Equal(original!.Name, after?.Name);
    }

    [Fact]
    public async Task UnavailableRepository_ReportsNoIntegrationAndHonorsPreCancelledToken()
    {
        var repository = new UnavailableProductRepository();

        var loadError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: false)));
        Assert.Contains("not configured", loadError.Message, StringComparison.OrdinalIgnoreCase);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var data = await new FakeProductRepository()
            .GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.UpdateProductAsync(DraftFrom(data!), cancellation.Token));
    }

    private static ProductEditDraft DraftFrom(ProductEditData data) => new(
        data.Id,
        data.Name,
        data.Brand,
        data.CategoryId,
        data.ProductSku,
        data.Barcode,
        data.ModelId,
        data.ImageUrl,
        data.BasePrice,
        data.CostPrice,
        data.IsSerialTracked,
        data.Specifications,
        data.Variants.Select(variant => new ProductEditVariantDraft(
            variant.Id,
            variant.Name,
            variant.Sku,
            variant.Barcode,
            variant.StorageLabel,
            variant.ColorLabel,
            variant.SwatchColor,
            variant.SellingPrice,
            variant.CostPrice,
            variant.Specifications)).ToArray());
}
