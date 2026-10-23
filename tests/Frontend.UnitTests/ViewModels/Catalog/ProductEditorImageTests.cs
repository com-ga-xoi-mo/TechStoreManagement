using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public class ProductEditorImageTests
{
    private readonly RecordingProductCommands _commands = new();
    private readonly RecordingImageRepository _images = new();
    private readonly ScriptedDialogService _dialogs = new() { Result = DialogResult.Confirmed };
    private readonly NavigationService _navigation = new(new AllowAllPreparer());
    private async Task<ProductEditorViewModel> Ready()
    {
        var vm = new ProductEditorViewModel(new StubProductReader(), _commands, new StubCategoryReader(), _navigation, _dialogs,
            CatalogSamples.Session("Admin"), new StubSpecRepository(), new StubBrandRepository(), new ScriptedImageFilePicker(), _images);
        vm.SetRoute(null, null); await vm.ActivateAsync();
        vm.SelectedCategoryId = CatalogSamples.CategoryId; vm.Name = "Phone"; vm.Sku = "IP16"; vm.Brand = "Apple"; vm.BasePriceText = "100"; vm.CostPriceText = "80";
        return vm;
    }
    private static void Add(ProductEditorViewModel vm, string name = "a.png") => vm.Gallery.Images.Add(ProductImageItem.FromPicked(new(name, 1, [1])));
    [Fact]
    public async Task Save_LayoutRejectedForItems_ShowsChangedElsewhereMessage()
    {
        // Arrange
        var vm = await Ready(); Add(vm);
        _images.LayoutError = new(ApiErrorKind.Http, "Invalid layout", 400,
            fieldErrors: new Dictionary<string, IReadOnlyList<string>> { ["items"] = ["Layout changed"] });
        // Act
        await vm.SaveCommand.ExecuteAsync(null);
        // Assert
        Assert.Contains("ảnh của sản phẩm vừa được thay đổi ở nơi khác", vm.GeneralError);
        Assert.Contains("Hãy mở lại sản phẩm", vm.GeneralError);
        Assert.DoesNotContain("Bấm Lưu để thử lại", vm.GeneralError);
        Assert.True(vm.IsEditMode); Assert.True(vm.Gallery.HasChanges);
        Assert.NotEqual(AppRouteKind.ProductDetail, _navigation.CurrentRoute.Kind);
    }

    [Fact]
    public async Task Save_RemovedImageAlreadyGone_OpensDetail()
    {
        // Arrange
        var vm = await Ready(); vm.Gallery.RemovedImageIds.Add(Guid.NewGuid()); Add(vm);
        _images.DeleteError = new(ApiErrorKind.Http, "Gone", 404);
        // Act
        await vm.SaveCommand.ExecuteAsync(null);
        // Assert
        Assert.Equal(new[] { "delete", "upload", "layout" }, _images.Calls);
        Assert.Empty(vm.Gallery.RemovedImageIds); Assert.Empty(vm.GeneralError);
        Assert.Equal(AppRouteKind.ProductDetail, _navigation.CurrentRoute.Kind);
    }
    [Fact]
    public async Task Cancel_OnlyReorderedImages_AsksToDiscard()
    {
        // Arrange
        var vm = await Ready(); await vm.SaveCommand.ExecuteAsync(null);
        vm.Gallery.Load([new(Guid.NewGuid(), null, "/a.png", "image/png", 1, 0), new(Guid.NewGuid(), null, "/b.png", "image/png", 1, 1)], vm.Variants);
        vm.Gallery.MoveRightCommand.Execute(null);
        // Act
        await vm.CancelCommand.ExecuteAsync(null);
        // Assert
        Assert.Single(_dialogs.Requests); Assert.Empty(_images.Calls);
    }

    [Fact]
    public async Task Save_CreateWithImages_CreatesUploadsLayoutThenNavigates()
    {
        // Arrange
        var vm = await Ready(); Add(vm); Add(vm, "b.png");
        // Act
        await vm.SaveCommand.ExecuteAsync(null);
        // Assert
        Assert.Single(_commands.Created); Assert.Equal(new[] { "upload", "upload", "layout" }, _images.Calls); Assert.Equal(AppRouteKind.ProductDetail, _navigation.CurrentRoute.Kind);
    }
    [Fact]
    public async Task Cancel_AfterPicking_SendsNoImageRequests()
    {
        // Arrange
        var vm = await Ready(); Add(vm);
        // Act
        await vm.CancelCommand.ExecuteAsync(null);
        // Assert
        Assert.Empty(_commands.Created); Assert.Empty(_images.Calls); Assert.Single(_dialogs.Requests);
    }
    [Fact]
    public async Task Save_UnsavedVariantsDifferentOrderAndCase_MatchesBySku()
    {
        // Arrange
        var vm = await Ready();
        var black = new VariantRowItem(null) { VariantName = "Đen", Sku = "ip16-bk", PriceText = "100" };
        var white = new VariantRowItem(null) { VariantName = "Trắng", Sku = "IP16-WH", PriceText = "100" }; vm.Variants.Add(black); vm.Variants.Add(white);
        Add(vm); vm.Gallery.Images[0].Variant = black;
        var savedBlack = CatalogSamples.Variant(CatalogSamples.ProductId, "IP16-BK"); var savedWhite = CatalogSamples.Variant(CatalogSamples.ProductId, "IP16-WH");
        _commands.OnCreate = _ => Task.FromResult(CatalogSamples.Detail(CatalogSamples.ProductId, variants: [savedWhite, savedBlack]));
        // Act
        await vm.SaveCommand.ExecuteAsync(null);
        // Assert
        Assert.Equal(savedBlack.Id, Assert.Single(_images.Uploads).VariantId); Assert.Equal(savedBlack.Id, black.Id); Assert.True(black.CanChooseSelling); Assert.False(black.IsSkuEditable);
    }
    [Fact]
    public async Task Save_MissingVariantSku_SendsNoImagesAndKeepsEditMode()
    {
        // Arrange
        var vm = await Ready(); vm.Variants.Add(new(null) { VariantName = "Đen", Sku = "MISSING", PriceText = "100" }); Add(vm);
        // Act
        await vm.SaveCommand.ExecuteAsync(null);
        // Assert
        Assert.Empty(_images.Calls); Assert.Contains("Đã lưu sản phẩm nhưng chưa lưu được ảnh", vm.GeneralError); Assert.True(vm.IsEditMode); Assert.Equal(CatalogSamples.ProductId, vm.ProductId);
    }
    [Fact]
    public async Task Save_SecondUploadFails_RetryUpdatesAndSendsOnlyMissingImage()
    {
        // Arrange
        var vm = await Ready(); Add(vm); Add(vm, "b.png"); _images.FailUploadNumber = 2;
        // Act
        await vm.SaveCommand.ExecuteAsync(null);
        // Assert
        Assert.True(vm.IsEditMode); Assert.Equal("Chỉnh sửa sản phẩm", vm.PageTitle); Assert.False(vm.Gallery.Images[0].IsNew); Assert.True(vm.Gallery.Images[1].IsNew);
        Assert.Contains("Đã lưu sản phẩm nhưng chưa lưu được ảnh", vm.GeneralError); Assert.NotEqual(AppRouteKind.ProductDetail, _navigation.CurrentRoute.Kind);
        _images.FailUploadNumber = null; _images.Calls.Clear();
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Single(_commands.Created); Assert.Single(_commands.Updated); Assert.Equal(new[] { "upload", "layout" }, _images.Calls); Assert.Equal("b.png", _images.Uploads.Last().FileName);
        Assert.Equal(AppRouteKind.ProductDetail, _navigation.CurrentRoute.Kind);
    }
    [Fact]
    public async Task Save_WhileSaving_DoesNotStartSecondChain()
    {
        // Arrange
        var vm = await Ready(); var pending = new TaskCompletionSource<ProductDetail>(); _commands.OnCreate = _ => pending.Task;
        // Act
        var saving = vm.SaveCommand.ExecuteAsync(null); await vm.SaveCommand.ExecuteAsync(null);
        // Assert
        Assert.True(vm.IsSaving); Assert.Single(_commands.Created);
        pending.SetResult(CatalogSamples.Detail(CatalogSamples.ProductId)); await saving; Assert.False(vm.IsSaving);
    }
    [Fact]
    public async Task Cancel_WithOnlyImageChanges_AsksToDiscard()
    {
        // Arrange
        var vm = await Ready(); await vm.SaveCommand.ExecuteAsync(null); Add(vm);
        // Act
        await vm.CancelCommand.ExecuteAsync(null);
        // Assert
        Assert.Single(_dialogs.Requests); Assert.True(vm.HasUnsavedChanges);
    }
}
