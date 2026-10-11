using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Models.Identity;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class ProductEditViewModelTests
{
    private const string ProductId = "iphone-15-pro-max";

    [Fact]
    public async Task Load_CopiesExistingProductAndVariantDraftsIntoIndependentRows()
    {
        var session = CreateSession(withEdit: true, withCost: true);
        using var viewModel = new ProductEditViewModel(new FakeProductRepository(), new FakeCategoryRepository(), session);

        await viewModel.LoadAsync(ProductId);

        Assert.True(viewModel.IsPopulated);
        Assert.Equal("iPhone 15 Pro Max 256GB", viewModel.Name);
        Assert.Equal(2, viewModel.Variants.Count);
        Assert.NotNull(viewModel.SelectedVariant);
        Assert.True(viewModel.IsCostFieldsVisible);
        Assert.True(viewModel.IsSerialTrackingReadOnly);
    }

    [Fact]
    public async Task SwitchingVariantsKeepsEachUnsavedRowValue()
    {
        using var viewModel = CreateAuthorizedViewModel();
        await viewModel.LoadAsync(ProductId);
        var first = viewModel.Variants[0];
        var second = viewModel.Variants[1];
        first.Sku = "MANUAL-FIRST-SKU";

        Assert.True(viewModel.SelectVariant(second.Id));
        second.Sku = "MANUAL-SECOND-SKU";
        Assert.True(viewModel.SelectVariant(first.Id));

        Assert.Equal("MANUAL-FIRST-SKU", first.Sku);
        Assert.Equal("MANUAL-SECOND-SKU", second.Sku);
        Assert.Same(first, viewModel.SelectedVariant);
    }

    [Fact]
    public async Task CancelAndDraftVariantChangesDoNotMutateSharedRepository()
    {
        var repository = new FakeProductRepository();
        using var viewModel = new ProductEditViewModel(repository, new FakeCategoryRepository(), CreateSession(true, true));
        await viewModel.LoadAsync(ProductId);
        var original = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));

        viewModel.Name = "Unsaved name";
        viewModel.AddVariant();
        Assert.Equal(3, viewModel.Variants.Count);
        viewModel.RemoveVariant(viewModel.Variants[2]);
        viewModel.Cancel();
        var after = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));

        Assert.True(viewModel.IsCancelled);
        Assert.Equal(original?.Name, after?.Name);
        Assert.Equal(original?.Variants.Count, after?.Variants.Count);
    }

    [Fact]
    public async Task LoadFailureAndUnavailableProductHaveSeparateStates()
    {
        using var failed = new ProductEditViewModel(
            new FakeProductRepository(editScenario: ProductEditPreviewScenario.LoadError),
            new FakeCategoryRepository(), CreateSession(true, true));
        using var missing = new ProductEditViewModel(
            new FakeProductRepository(editScenario: ProductEditPreviewScenario.NotFound),
            new FakeCategoryRepository(), CreateSession(true, true));

        await failed.LoadAsync(ProductId);
        await missing.LoadAsync(ProductId);

        Assert.True(failed.IsLoadError);
        Assert.False(failed.IsUnavailable);
        Assert.True(missing.IsUnavailable);
        Assert.False(missing.IsLoadError);
    }

    [Fact]
    public async Task DelayedLoadExposesLoadingStateUntilProductArrives()
    {
        using var viewModel = new ProductEditViewModel(
            new FakeProductRepository(editScenario: ProductEditPreviewScenario.DelayedLoad),
            new FakeCategoryRepository(), CreateSession(true, true));

        var load = viewModel.LoadAsync(ProductId);
        await Task.Delay(50);

        Assert.True(viewModel.IsLoading);
        Assert.False(viewModel.IsPopulated);
        await load;
        Assert.False(viewModel.IsLoading);
        Assert.True(viewModel.IsPopulated);
    }

    [Fact]
    public async Task CancelLoadStopsLoadingWithoutPublishingLateProduct()
    {
        using var viewModel = new ProductEditViewModel(
            new FakeProductRepository(editScenario: ProductEditPreviewScenario.DelayedLoad),
            new FakeCategoryRepository(), CreateSession(true, true));

        var load = viewModel.LoadAsync(ProductId);
        viewModel.CancelLoad();
        await load;

        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.IsPopulated);
    }

    [Fact]
    public async Task SessionWithoutEditCapabilityCannotLoadOrSubmit()
    {
        using var viewModel = new ProductEditViewModel(new FakeProductRepository(), new FakeCategoryRepository(), CreateSession(false, false));

        await viewModel.LoadAsync(ProductId);
        await viewModel.SubmitAsync();

        Assert.False(viewModel.HasEditCapability);
        Assert.True(viewModel.IsUnavailable);
        Assert.False(viewModel.IsSaved);
    }

    [Fact]
    public async Task CostFieldsAreNotExposedWithoutCostWriteCapability()
    {
        using var viewModel = new ProductEditViewModel(
            new FakeProductRepository(), new FakeCategoryRepository(), CreateSession(withEdit: true, withCost: false));

        await viewModel.LoadAsync(ProductId);

        Assert.False(viewModel.IsCostFieldsVisible);
        Assert.Equal(string.Empty, viewModel.CostPriceText);
        Assert.All(viewModel.Variants, variant => Assert.Equal(string.Empty, variant.CostPriceText));
        Assert.Contains("preview", viewModel.ProvenanceMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImagePreviewFailureIsReportedAndClearedWhenUrlChanges()
    {
        using var viewModel = CreateAuthorizedViewModel();
        await viewModel.LoadAsync(ProductId);

        viewModel.ReportImagePreviewFailure();
        Assert.Contains("Không tải được ảnh", viewModel.ImagePreviewMessage);

        viewModel.ImageUrl = "https://example.test/replacement.png";
        Assert.False(viewModel.IsImagePreviewError);
        Assert.Contains("xem trước", viewModel.ImagePreviewMessage);
    }

    [Fact]
    public async Task SerialTrackingValueRemainsReadOnlyWhenBuildingSaveDraft()
    {
        var repository = new FakeProductRepository();
        using var viewModel = new ProductEditViewModel(repository, new FakeCategoryRepository(), CreateSession(true, true));
        await viewModel.LoadAsync(ProductId);
        MakeSavable(viewModel);
        viewModel.IsSerialTracked = !viewModel.IsSerialTracked;

        await viewModel.SubmitAsync();
        var after = await repository.GetProductForEditAsync(new ProductEditQuery(ProductId, IncludeCostFields: true));

        Assert.True(viewModel.IsSerialTrackingReadOnly);
        Assert.True(after!.IsSerialTracked);
    }

    [Fact]
    public void PreviewEditCapabilitiesAreOptInAndExplicitlyNamed()
    {
        var denied = ProductEditCapabilityPreviewSettings.BuildPreviewCapabilities(
            PreviewCapabilityMode.Denied, PreviewCapabilityMode.Denied);
        var authorized = ProductEditCapabilityPreviewSettings.BuildPreviewCapabilities(
            PreviewCapabilityMode.Authorized, PreviewCapabilityMode.Authorized);

        Assert.Empty(denied);
        Assert.Contains(ProductEditViewModel.EditCapability, authorized);
        Assert.Contains(ProductEditViewModel.CostWriteCapability, authorized);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("not-a-price")]
    public async Task SubmitBlocksMalformedOrNegativePriceAndFocusesFirstInvalidField(string invalidPrice)
    {
        using var viewModel = CreateAuthorizedViewModel();
        await viewModel.LoadAsync(ProductId);
        MakeSavable(viewModel);
        viewModel.BasePriceText = invalidPrice;

        await viewModel.SubmitAsync();

        Assert.False(viewModel.IsSaved);
        Assert.Equal(nameof(viewModel.BasePriceText), viewModel.FirstInvalidFieldName);
        Assert.Contains("không âm", viewModel.BasePriceError);
    }

    [Fact]
    public async Task SubmitBlocksIncompleteAndDuplicateSpecifications()
    {
        using var viewModel = CreateAuthorizedViewModel();
        await viewModel.LoadAsync(ProductId);
        MakeSavable(viewModel);
        viewModel.AddSpecification();

        await viewModel.SubmitAsync();

        Assert.False(viewModel.IsSaved);
        Assert.Equal("Specifications[3].Key", viewModel.FirstInvalidFieldName);
        Assert.NotEmpty(viewModel.Specifications[3].Error);

        viewModel.Specifications.RemoveAt(3);
        viewModel.Specifications.Add(new ProductEditSpecificationViewModel(
            viewModel.Specifications[0].Key, "duplicate value"));
        await viewModel.SubmitAsync();

        Assert.False(viewModel.IsSaved);
        Assert.NotEmpty(viewModel.Specifications[^1].Error);
    }

    [Fact]
    public async Task SubmitBlocksInvalidVariantPriceAndVariantSpecifications()
    {
        using var viewModel = CreateAuthorizedViewModel();
        await viewModel.LoadAsync(ProductId);
        MakeSavable(viewModel);
        var variant = viewModel.Variants[0];
        variant.SellingPriceText = "-5";

        await viewModel.SubmitAsync();

        Assert.Equal("Variants[0].SellingPriceText", viewModel.FirstInvalidFieldName);
        Assert.NotEmpty(variant.PriceError);

        variant.SellingPriceText = "34990000";
        variant.AddSpecification();
        await viewModel.SubmitAsync();

        Assert.Equal("Variants[0].Specifications[1].Key", viewModel.FirstInvalidFieldName);
        Assert.NotEmpty(variant.Specifications[1].Error);
    }

    [Fact]
    public async Task SkuSuggestionIsAdvisoryAndManualEditIsNotReplaced()
    {
        using var viewModel = CreateAuthorizedViewModel();
        await viewModel.LoadAsync(ProductId);
        var variant = viewModel.Variants[0];

        viewModel.SuggestSku(variant);
        var suggestion = variant.Sku;
        viewModel.ModelId = "CHANGED-MODEL";

        Assert.Equal("A3106-256GB-TITAN-TU-NHIEN", suggestion);
        Assert.Equal(suggestion, variant.Sku);
        Assert.True(variant.IsSkuSuggestion);
        Assert.Contains("tham khảo", variant.SkuOriginMessage);

        variant.Sku = "MANUAL-SKU";
        viewModel.ModelId = "ANOTHER-MODEL";

        Assert.Equal("MANUAL-SKU", variant.Sku);
        Assert.False(variant.IsSkuSuggestion);
    }

    [Theory]
    [InlineData(ProductEditPreviewScenario.DuplicateSku)]
    [InlineData(ProductEditPreviewScenario.DuplicateBarcode)]
    [InlineData(ProductEditPreviewScenario.SaveError)]
    public async Task SaveConflictOrFailureKeepsDraftInput(ProductEditPreviewScenario scenario)
    {
        using var viewModel = new ProductEditViewModel(
            new FakeProductRepository(editScenario: scenario), new FakeCategoryRepository(), CreateSession(true, true));
        await viewModel.LoadAsync(ProductId);
        MakeSavable(viewModel);
        viewModel.Name = "Draft must remain after failure";
        var firstVariant = viewModel.Variants[0];
        firstVariant.Sku = "MANUAL-EDIT-SKU";

        await viewModel.SubmitAsync();

        Assert.False(viewModel.IsSaved);
        Assert.True(viewModel.IsSaveError);
        Assert.Equal("Draft must remain after failure", viewModel.Name);
        Assert.Equal("MANUAL-EDIT-SKU", firstVariant.Sku);
        if (scenario is ProductEditPreviewScenario.DuplicateSku or ProductEditPreviewScenario.DuplicateBarcode)
        {
            Assert.NotEmpty(viewModel.RepositoryFieldName);
            Assert.NotEmpty(viewModel.FirstInvalidFieldName);
        }
    }

    private static void MakeSavable(ProductEditViewModel viewModel)
    {
        viewModel.Name = "Updated phone";
        viewModel.Brand = "Apple";
        viewModel.CategoryId = "phones";
        viewModel.ProductSku = "EDITED-PRODUCT-SKU";
        viewModel.BasePriceText = "34990000";
        viewModel.CostPriceText = "30000000";
        foreach (var variant in viewModel.Variants)
        {
            variant.Name = string.IsNullOrWhiteSpace(variant.Name) ? "Edited variant" : variant.Name;
            variant.Sku = $"EDIT-{variant.Id}";
            variant.SellingPriceText = "34990000";
        }
    }

    private static ProductEditViewModel CreateAuthorizedViewModel() =>
        new(new FakeProductRepository(), new FakeCategoryRepository(), CreateSession(withEdit: true, withCost: true));

    private static CurrentSessionProvider CreateSession(bool withEdit, bool withCost)
    {
        var capabilities = new HashSet<string>();
        if (withEdit) capabilities.Add(ProductEditViewModel.EditCapability);
        if (withCost) capabilities.Add(ProductEditViewModel.CostWriteCapability);
        var session = new CurrentSessionProvider();
        session.SetAuthenticated(new SessionSnapshot("preview tester", capabilities));
        return session;
    }
}
