using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.ViewModels.Catalog;
using Xunit;

namespace TechStore.Client.Tests.Catalog;

public sealed class ProductCreateViewModelTests
{
    [Fact]
    public async Task Submit_InvalidRequiredFieldShowsFirstErrorAndDoesNotCallRepository()
    {
        var repository = new RecordingProductRepository();
        var viewModel = await CreateReadyViewModel(repository);
        viewModel.Name = " ";

        await viewModel.SubmitAsync();

        Assert.Equal("Name", viewModel.FirstInvalidFieldName);
        Assert.NotEmpty(viewModel.NameError);
        Assert.Equal(0, repository.CreateCalls);
        Assert.False(viewModel.IsSubmitting);
    }

    [Fact]
    public async Task Submit_InvalidPriceAndIncompleteDynamicRowsAreReported()
    {
        var repository = new RecordingProductRepository();
        var viewModel = await CreateReadyViewModel(repository);
        viewModel.BasePriceText = "abc";
        viewModel.Variants[0].PriceText = "-1";
        viewModel.AddSpecification();
        viewModel.Specifications[0].Key = "CPU";

        await viewModel.SubmitAsync();

        Assert.NotEmpty(viewModel.BasePriceError);
        Assert.NotEmpty(viewModel.Variants[0].PriceError);
        Assert.NotEmpty(viewModel.Specifications[0].Error);
        Assert.Equal(0, repository.CreateCalls);
    }

    [Fact]
    public async Task Submit_InvalidImageUrlIsReportedAndHttpsCanBePreviewed()
    {
        var repository = new RecordingProductRepository();
        var viewModel = await CreateReadyViewModel(repository);
        viewModel.ImageUrl = "file:///local/image.png";

        await viewModel.SubmitAsync();
        Assert.Equal(nameof(ProductCreateViewModel.ImageUrl), viewModel.FirstInvalidFieldName);
        Assert.NotEmpty(viewModel.ImageUrlError);
        Assert.Equal(0, repository.CreateCalls);

        viewModel.ImageUrl = "https://cdn.example.test/device.png";
        await viewModel.SubmitAsync();
        Assert.True(viewModel.IsCreated);
        Assert.Equal("https://cdn.example.test/device.png", repository.LastDraft!.ImageUrl);
    }

    [Fact]
    public async Task Submit_CorrectionAfterValidationCanSucceed()
    {
        var repository = new RecordingProductRepository();
        var viewModel = await CreateReadyViewModel(repository);
        viewModel.ProductSku = string.Empty;

        await viewModel.SubmitAsync();
        Assert.NotEmpty(viewModel.ProductSkuError);

        viewModel.ProductSku = "PRODUCT-1";
        await viewModel.SubmitAsync();

        Assert.True(viewModel.IsCreated);
        Assert.Equal(1, repository.CreateCalls);
        Assert.Equal("PRODUCT-1", repository.LastDraft!.ProductSku);
    }

    [Fact]
    public async Task Submit_RepositoryErrorCanBeRetriedWithoutLosingFields()
    {
        var repository = new RecordingProductRepository
        {
            Result = new ProductCreateResult(ProductCreateResultStatus.Failed, Message: "Preview error")
        };
        var viewModel = await CreateReadyViewModel(repository);
        viewModel.Name = "Keep this on retry";

        await viewModel.SubmitAsync();
        Assert.True(viewModel.IsSubmissionError);

        repository.Result = RecordingProductRepository.SuccessResult();
        await viewModel.SubmitAsync();

        Assert.True(viewModel.IsCreated);
        Assert.Equal("Keep this on retry", repository.LastDraft!.Name);
        Assert.False(viewModel.IsSubmissionError);
    }

    [Fact]
    public async Task Submit_VariantSkuConflictMapsToTheVariantField()
    {
        var repository = new RecordingProductRepository
        {
            Result = new ProductCreateResult(
                ProductCreateResultStatus.Conflict,
                FieldName: "Variants[0].Sku",
                Message: "Preview conflict; server owns final uniqueness.")
        };
        var viewModel = await CreateReadyViewModel(repository);

        await viewModel.SubmitAsync();

        Assert.Equal("Variants[0].Sku", viewModel.FirstInvalidFieldName);
        Assert.Equal(viewModel.SubmissionMessage, viewModel.Variants[0].SkuError);
    }

    [Fact]
    public async Task Submit_WithFakeRepositoriesAddsPreviewProductWithoutInitialStock()
    {
        var products = new FakeProductRepository(products: []);
        var viewModel = await CreateReadyViewModel(products, new FakeCategoryRepository());

        await viewModel.SubmitAsync();

        var result = await products.SearchProductsAsync(new ProductListCriteria(SearchQuery: "PRODUCT-1"));
        var created = Assert.Single(result.Items);
        Assert.True(viewModel.IsCreated);
        Assert.Equal(0, created.AvailableQuantity);
        Assert.Equal(ProductStatus.OutOfStock, created.Status);
    }

    [Fact]
    public async Task Submit_CancelledRequestDoesNotInsertAndKeepsEnteredFields()
    {
        var products = new FakeProductRepository(products: []);
        var viewModel = await CreateReadyViewModel(products, new FakeCategoryRepository());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await viewModel.SubmitAsync(cancellation.Token);

        var result = await products.SearchProductsAsync(new ProductListCriteria());
        Assert.Empty(result.Items);
        Assert.False(viewModel.IsCreated);
        Assert.Equal("Test product", viewModel.Name);
        Assert.False(viewModel.IsSubmitting);
    }

    [Fact]
    public async Task SpecificationAndVariantRows_CanBeAddedAndRemoved()
    {
        var viewModel = await CreateReadyViewModel(new RecordingProductRepository());

        viewModel.AddSpecificationCommand.Execute(null);
        viewModel.AddVariantCommand.Execute(null);
        Assert.Single(viewModel.Specifications);
        Assert.Equal(2, viewModel.Variants.Count);
        Assert.All(viewModel.Variants, variant => Assert.True(variant.CanRemove));

        viewModel.RemoveSpecificationCommand.Execute(viewModel.Specifications[0]);
        viewModel.RemoveVariantCommand.Execute(viewModel.Variants[1]);
        viewModel.RemoveVariantCommand.Execute(viewModel.Variants[0]);

        Assert.Empty(viewModel.Specifications);
        Assert.Single(viewModel.Variants);
        Assert.False(viewModel.Variants[0].CanRemove);
    }

    [Theory]
    [InlineData(ProductCreateResultStatus.Conflict, nameof(ProductCreateDraft.ProductSku))]
    [InlineData(ProductCreateResultStatus.Failed, null)]
    public async Task Submit_RepositoryFailurePreservesFormAndReportsPreviewIssue(
        ProductCreateResultStatus status,
        string? fieldName)
    {
        var repository = new RecordingProductRepository
        {
            Result = new ProductCreateResult(status, FieldName: fieldName, Message: "Preview error; server owns final uniqueness.")
        };
        var viewModel = await CreateReadyViewModel(repository);
        viewModel.Name = "Product kept after failure";

        await viewModel.SubmitAsync();

        Assert.False(viewModel.IsCreated);
        Assert.True(viewModel.IsSubmissionError);
        Assert.Equal("Product kept after failure", viewModel.Name);
        Assert.NotEmpty(viewModel.SubmissionMessage);
        Assert.False(viewModel.IsSubmitting);
        if (fieldName == nameof(ProductCreateDraft.ProductSku))
        {
            Assert.Equal(viewModel.ProductSkuError, viewModel.SubmissionMessage);
            Assert.Equal(nameof(ProductCreateDraft.ProductSku), viewModel.FirstInvalidFieldName);
        }
    }

    [Fact]
    public async Task Submit_DoesNotStartSecondCreateWhileOneIsPending()
    {
        var completion = new TaskCompletionSource<ProductCreateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingProductRepository { CreateCompletion = completion };
        var viewModel = await CreateReadyViewModel(repository);

        var first = viewModel.SubmitAsync();
        var second = viewModel.SubmitAsync();

        Assert.True(viewModel.IsSubmitting);
        Assert.Equal(1, repository.CreateCalls);
        completion.SetResult(RecordingProductRepository.SuccessResult());
        await Task.WhenAll(first, second);

        Assert.True(viewModel.IsCreated);
        Assert.False(viewModel.IsSubmitting);
    }

    [Fact]
    public async Task LoadCategories_ReportsReadableFailureAndCanRetry()
    {
        var categories = new RecordingCategoryRepository
        {
            Failure = true
        };
        var viewModel = new ProductCreateViewModel(new RecordingProductRepository(), categories);

        await viewModel.LoadCategoriesAsync();
        Assert.True(viewModel.IsCategoriesError);
        Assert.Contains("danh mục", viewModel.CategoriesErrorMessage, StringComparison.OrdinalIgnoreCase);

        categories.Failure = false;
        await viewModel.LoadCategoriesAsync();
        Assert.False(viewModel.IsCategoriesError);
        Assert.Contains(viewModel.Categories, category => category.Id == "phones");
    }

    private static async Task<ProductCreateViewModel> CreateReadyViewModel(RecordingProductRepository repository)
    {
        return await CreateReadyViewModel((IProductRepository)repository, new FakeCategoryRepository());
    }

    private static async Task<ProductCreateViewModel> CreateReadyViewModel(IProductRepository repository, ICategoryRepository categories)
    {
        var viewModel = new ProductCreateViewModel(repository, categories);
        await viewModel.LoadCategoriesAsync();
        viewModel.Name = "Test product";
        viewModel.Brand = "Test brand";
        viewModel.CategoryId = "phones";
        viewModel.ProductSku = "PRODUCT-1";
        viewModel.BasePriceText = "35000000";
        viewModel.CostPriceText = "30000000";
        viewModel.Variants[0].Name = "Blue / 256GB";
        viewModel.Variants[0].Sku = "VARIANT-1";
        viewModel.Variants[0].PriceText = "35000000";
        return viewModel;
    }

    private sealed class RecordingCategoryRepository : ICategoryRepository
    {
        public bool Failure { get; set; }

        public Task<IReadOnlyList<CategoryOption>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure)
            {
                throw new InvalidOperationException("Preview category failure");
            }

            return Task.FromResult<IReadOnlyList<CategoryOption>>([new("phones", "Điện thoại")]);
        }

        public Task<CategoryTreeSnapshot> GetCategoryTreeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CategoryTreeSnapshot([], IsPreviewData: false));
        }
    }

    private sealed class RecordingProductRepository : IProductRepository
    {
        public int CreateCalls { get; private set; }
        public ProductCreateDraft? LastDraft { get; private set; }
        public ProductCreateResult? Result { get; set; }
        public TaskCompletionSource<ProductCreateResult>? CreateCompletion { get; set; }

        public Task<PagedResult<ProductSummary>> SearchProductsAsync(ProductListCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<ProductSummary>([], 0, 0, 8));

        public Task<ProductDetail?> GetProductDetailAsync(string productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDetail?>(null);

        public Task<ProductCreateResult> CreateProductAsync(ProductCreateDraft draft, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateCalls++;
            LastDraft = draft;
            return CreateCompletion?.Task ?? Task.FromResult(Result ?? SuccessResult());
        }

        public Task<ProductEditData?> GetProductForEditAsync(ProductEditQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductEditData?>(null);

        public Task<ProductEditResult> UpdateProductAsync(ProductEditDraft draft, CancellationToken cancellationToken = default) =>
            Task.FromException<ProductEditResult>(new NotSupportedException());

        public static ProductCreateResult SuccessResult() => new(
            ProductCreateResultStatus.Created,
            new ProductSummary("created", "Test product", "Test", "phones", "Điện thoại", null,
                ProductStatus.OutOfStock, [new ProductVariantSummary("v1", "Blue", "VARIANT-1", 1m, 0)], DateTimeOffset.UtcNow,
                ProductSku: "PRODUCT-1"));
    }
}
