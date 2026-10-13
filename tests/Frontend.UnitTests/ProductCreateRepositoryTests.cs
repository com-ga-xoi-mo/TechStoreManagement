using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class ProductCreateRepositoryTests
{
    [Fact]
    public async Task FakeCategoryRepository_ReturnsPreviewCategories()
    {
        var repository = new FakeCategoryRepository();

        var categories = await repository.GetCategoriesAsync();

        Assert.NotEmpty(categories);
        Assert.All(categories, category => Assert.True(category.IsPreviewData));
        Assert.Contains(categories, category => category.Id == "phones" && category.Name == "Điện thoại");
    }

    [Fact]
    public async Task FakeCategoryRepository_ReturnsCompletePreviewTreeWithIndependentCounts()
    {
        var repository = new FakeCategoryRepository();

        var snapshot = await repository.GetCategoryTreeAsync();

        Assert.True(snapshot.IsPreviewData);
        Assert.Equal(24, snapshot.CategoryCount);
        Assert.Equal(642, snapshot.Roots.Single(category => category.Id == "phones").ProductCount);
        Assert.Equal(248, snapshot.Roots.Single(category => category.Id == "phones").Children[0].ProductCount);
        Assert.Contains(snapshot.Roots.SelectMany(category => category.Children), category => !category.IsActive);
    }

    [Fact]
    public async Task FakeCategoryRepository_SupportsEmptyCategoryTreeScenario()
    {
        var repository = new FakeCategoryRepository(CategoryTreePreviewScenario.Empty);

        var snapshot = await repository.GetCategoryTreeAsync();

        Assert.Empty(snapshot.Roots);
        Assert.True(snapshot.IsPreviewData);
        Assert.Equal(4, (await repository.GetCategoriesAsync()).Count);
    }

    [Fact]
    public async Task FakeCategoryRepository_ErrorScenarioFailsInsteadOfReturningEmpty()
    {
        var repository = new FakeCategoryRepository(CategoryTreePreviewScenario.Error);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetCategoryTreeAsync());
    }

    [Fact]
    public async Task FakeCategoryRepository_DelayedScenarioHonorsCancellation()
    {
        var repository = new FakeCategoryRepository(CategoryTreePreviewScenario.Delayed);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetCategoryTreeAsync(cancellation.Token));
    }

    [Theory]
    [InlineData("Empty", CategoryTreePreviewScenario.Empty)]
    [InlineData("delayed", CategoryTreePreviewScenario.Delayed)]
    [InlineData("unknown", CategoryTreePreviewScenario.Populated)]
    public void CategoryTreePreviewSettings_ParseIsCaseInsensitiveAndDefaultsSafely(
        string value,
        CategoryTreePreviewScenario expected)
    {
        Assert.Equal(expected, CategoryTreePreviewSettings.Parse(value));
    }

    [Fact]
    public async Task FakeProductRepository_CreateAddsPreviewProductVisibleToListSearch()
    {
        var repository = new FakeProductRepository(products: []);

        var created = await repository.CreateProductAsync(Draft());
        var result = await repository.SearchProductsAsync(new ProductListCriteria(SearchQuery: "NEW-PRODUCT-SKU"));

        Assert.True(created.IsSuccess);
        Assert.Equal("NEW-PRODUCT-SKU", created.Product!.Sku);
        Assert.Equal("NEW-PRODUCT-SKU", Assert.Single(result.Items).ProductSku);
        Assert.Equal(0, result.Items[0].AvailableQuantity);
        Assert.Equal(ProductStatus.OutOfStock, result.Items[0].Status);
        Assert.Contains("preview-", result.Items[0].Id, StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(ProductCreateDraft).GetProperties(), property =>
            property.Name.Contains("Stock", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Quantity", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("SerialNumber", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Imei", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FakeProductRepository_RejectsCancelledCreateBeforeMutation()
    {
        var repository = new FakeProductRepository(products: []);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.CreateProductAsync(Draft(), cancellation.Token));

        var result = await repository.SearchProductsAsync(new ProductListCriteria());
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData(ProductCreatePreviewScenario.DuplicateSku, ProductCreateResultStatus.Conflict)]
    [InlineData(ProductCreatePreviewScenario.DuplicateBarcode, ProductCreateResultStatus.Conflict)]
    [InlineData(ProductCreatePreviewScenario.Error, ProductCreateResultStatus.Failed)]
    public async Task FakeProductRepository_ConfiguredCreateIssuesArePreviewOutcomes(
        ProductCreatePreviewScenario scenario,
        ProductCreateResultStatus expectedStatus)
    {
        var repository = new FakeProductRepository(products: [], createScenario: scenario);

        var created = await repository.CreateProductAsync(Draft());
        var list = await repository.SearchProductsAsync(new ProductListCriteria());

        Assert.Equal(expectedStatus, created.Status);
        Assert.False(created.IsSuccess);
        Assert.NotNull(created.Message);
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task FakeProductRepository_ReportsExistingSkuConflictAsProvisional()
    {
        var repository = new FakeProductRepository(products: [ProductWithSku("NEW-PRODUCT-SKU")]);

        var result = await repository.CreateProductAsync(Draft());

        Assert.Equal(ProductCreateResultStatus.Conflict, result.Status);
        Assert.Equal(nameof(ProductCreateDraft.ProductSku), result.FieldName);
        Assert.Contains("preview", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("server", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    private static ProductCreateDraft Draft() => new(
        Name: "New Preview Product",
        Brand: "Example",
        CategoryId: "phones",
        ProductSku: "NEW-PRODUCT-SKU",
        Barcode: "1234567890123",
        IsSerialTracked: true,
        BasePrice: 100m,
        CostPrice: 75m,
        ImageUrl: null,
        Specifications: [new ProductSpecificationEntry("CPU", "Example CPU")],
        Variants:
        [
            new ProductVariantDraft(
                "Blue / 256GB",
                "NEW-VARIANT-SKU",
                "1234567890124",
                100m,
                [new ProductSpecificationEntry("Storage", "256GB")])
        ]);

    private static ProductSummary ProductWithSku(string sku) => new(
        Id: "existing-product",
        Name: "Existing product",
        Brand: "Example",
        CategoryId: "phones",
        CategoryName: "Điện thoại",
        ImageUrl: null,
        Status: ProductStatus.InStock,
        Variants: [new ProductVariantSummary("variant", "Existing", "VARIANT-SKU", 100m, 1)],
        CreatedAt: DateTimeOffset.UnixEpoch,
        ProductSku: sku);
}
