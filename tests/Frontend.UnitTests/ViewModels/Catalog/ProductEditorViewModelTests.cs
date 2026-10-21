using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.UnitTests.Repositories.Orders;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.ViewModels.Pages;
using Xunit;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class ProductEditorViewModelTests
{
    private static ProductEditorViewModel CreateVm(
        IProductRepository? products = null,
        IProductCommandRepository? commands = null,
        ICategoryRepository? categories = null,
        INavigationService? nav = null,
        IDialogService? dialogs = null,
        ISessionStore? session = null,
        IProductSpecRepository? specs = null)
    {
        return new ProductEditorViewModel(
            products ?? new StubProductReader(),
            commands ?? new RecordingProductCommands(),
            categories ?? new StubCategoryReader(),
            nav ?? new NavigationService(new AllowAllPreparer()),
            dialogs ?? new ScriptedDialogService(),
            session ?? CatalogSamples.Session("Admin"),
            specs ?? new StubSpecRepository());
    }

    [Fact]
    public async Task CreateMode_LoadsCategoriesOnly_AndHasUnsavedChangesFalse()
    {
        // Arrange
        var vm = CreateVm();
        vm.SetRoute(null, null);

        // Act
        await vm.ActivateAsync();

        // Assert
        Assert.Equal(2, vm.Categories.Count);
        Assert.False(vm.IsEditMode);
        Assert.Empty(vm.Specs);
        Assert.Empty(vm.Variants);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal("Thêm sản phẩm mới", vm.PageTitle);
    }

    [Fact]
    public async Task EditMode_FillsNamePricesSpecsAndVariants_WithIsSkuEditableFalse()
    {
        // Arrange
        var sampleSpecs = new Dictionary<string, JsonElement>
        {
            ["dpi"] = JsonDocument.Parse("3200").RootElement.Clone()
        };
        var sampleVariants = new[] { CatalogSamples.Variant(CatalogSamples.ProductId, "SKU-V1") };
        var reader = new StubProductReader
        {
            OnGetById = id => Task.FromResult<ProductDetail?>(
                CatalogSamples.Detail(id, specs: sampleSpecs, variants: sampleVariants) with
                {
                    Name = "Pro Mouse",
                    BasePrice = 3200000m,
                    CostPrice = 2500000m
                })
        };
        var vm = CreateVm(products: reader);
        vm.SetRoute(CatalogSamples.ProductId, null);

        // Act
        await vm.ActivateAsync();

        // Assert
        Assert.True(vm.IsEditMode);
        Assert.Equal("Pro Mouse", vm.Name);
        Assert.Equal("3200000", vm.BasePriceText);
        Assert.Equal("2500000", vm.CostPriceText);

        var specRow = Assert.Single(vm.Specs);
        Assert.Equal("dpi", specRow.Key);
        Assert.Equal(1, specRow.KindIndex); // Number
        Assert.Equal("3200", specRow.Value);

        var variantRow = Assert.Single(vm.Variants);
        Assert.False(variantRow.IsSkuEditable);
        Assert.True(variantRow.IsSaved);
        Assert.Equal("SKU-V1", variantRow.Sku);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task EditMode_MissingProduct_SetsIsNotFoundTrue_AndContentStateEmpty()
    {
        // Arrange
        var reader = new StubProductReader { OnGetById = _ => Task.FromResult<ProductDetail?>(null) };
        var vm = CreateVm(products: reader);
        vm.SetRoute(Guid.NewGuid(), null);

        // Act
        await vm.ActivateAsync();

        // Assert
        Assert.True(vm.IsNotFound);
        Assert.Equal(PageContentState.Empty, vm.ContentState);
    }

    [Fact]
    public async Task CategoryLoadFailure_SetsContentStateError_AndRetryReloadsWithoutDuplicatingRows()
    {
        // Arrange
        var categories = new StubCategoryReader { Fail = true };
        var vm = CreateVm(categories: categories);
        vm.SetRoute(CatalogSamples.ProductId, null);

        // Act
        await vm.ActivateAsync();

        // Assert
        Assert.Equal(PageContentState.Error, vm.ContentState);

        // Act retry
        categories.Fail = false;
        await vm.RetryCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(PageContentState.Content, vm.ContentState);
        Assert.Single(vm.Variants); // Not duplicated!
    }

    [Fact]
    public async Task ValidCreate_CallsCreateOnce_AndNavigatesToProductDetailWithReturnQuery()
    {
        // Arrange
        var nav = new NavigationService(new AllowAllPreparer());
        var commands = new RecordingProductCommands
        {
            OnCreate = _ => Task.FromResult(CatalogSamples.Detail(CatalogSamples.ProductId))
        };
        var returnQuery = new ProductQuery { Query = "test" };
        var vm = CreateVm(commands: commands, nav: nav);
        vm.SetRoute(null, returnQuery);
        await vm.ActivateAsync();

        vm.SelectedCategoryId = CatalogSamples.CategoryId;
        vm.Name = "New Phone";
        vm.Sku = "NP-1";
        vm.Brand = "Brand";
        vm.BasePriceText = "10.000.000";
        vm.CostPriceText = "8.000.000";

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        var created = Assert.Single(commands.Created);
        Assert.Equal("New Phone", created.Name);
        Assert.Equal(AppRouteKind.ProductDetail, nav.CurrentRoute.Kind);
        Assert.Equal(CatalogSamples.ProductId, nav.CurrentRoute.ProductId);
        Assert.Equal("test", nav.CurrentRoute.ReturnQuery?.Query);
    }

    [Fact]
    public async Task ClientErrors_EmptyName_SetsNameError_AndMakesNoCommandCall()
    {
        // Arrange
        var commands = new RecordingProductCommands();
        var vm = CreateVm(commands: commands);
        vm.SetRoute(null, null);
        await vm.ActivateAsync();

        vm.Name = "";

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.True(vm.HasNameError);
        Assert.NotEmpty(vm.NameError!);
        Assert.Empty(commands.Created);
    }

    [Fact]
    public async Task DoubleSave_WhileFirstCallAwaits_MakesOnlyOneCall()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ProductDetail>();
        var commands = new RecordingProductCommands
        {
            OnCreate = _ => tcs.Task
        };
        var vm = CreateVm(commands: commands);
        vm.SetRoute(null, null);
        await vm.ActivateAsync();

        vm.SelectedCategoryId = CatalogSamples.CategoryId;
        vm.Name = "Double Phone";
        vm.Sku = "DP-1";
        vm.Brand = "Brand";
        vm.BasePriceText = "10.000.000";
        vm.CostPriceText = "8.000.000";

        // Act
        var task1 = vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsSaving);
        Assert.False(vm.SaveCommand.CanExecute(null));

        await vm.SaveCommand.ExecuteAsync(null); // Second execution while busy
        Assert.Single(commands.Created);

        tcs.SetResult(CatalogSamples.Detail(CatalogSamples.ProductId));
        await task1;

        // Assert
        Assert.False(vm.IsSaving);
    }

    [Fact]
    public async Task Server400_MapsFieldErrorsToSpecificProperties()
    {
        // Arrange
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>
        {
            ["name"] = ["x"],
            ["variants[1].sku"] = ["dup"],
            ["other"] = ["y"]
        };
        var badRequest = new ApiException(ApiErrorKind.Http, "Bad Request", statusCode: 400, fieldErrors: fieldErrors);
        var commands = new RecordingProductCommands { OnCreate = _ => throw badRequest };
        var vm = CreateVm(commands: commands);
        vm.SetRoute(null, null);
        await vm.ActivateAsync();

        vm.SelectedCategoryId = CatalogSamples.CategoryId;
        vm.Name = "Phone";
        vm.Sku = "P-1";
        vm.Brand = "Brand";
        vm.BasePriceText = "10.000.000";
        vm.CostPriceText = "8.000.000";
        vm.Variants.Add(new VariantRowItem(null) { VariantName = "V0", Sku = "SKU-0", PriceText = "1000" });
        vm.Variants.Add(new VariantRowItem(null) { VariantName = "V1", Sku = "SKU-1", PriceText = "2000" });

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("x", vm.NameError);
        Assert.Equal("dup", vm.Variants[1].SkuError);
        Assert.Contains("y", vm.GeneralError);
    }

    [Fact]
    public async Task Server409_WithDetail_SetsGeneralErrorAndKeepsFields()
    {
        // Arrange
        var conflict = RecordingApiClient.Http(409, "SKU already exists in system.");
        var commands = new RecordingProductCommands { OnCreate = _ => throw conflict };
        var vm = CreateVm(commands: commands);
        vm.SetRoute(null, null);
        await vm.ActivateAsync();

        vm.SelectedCategoryId = CatalogSamples.CategoryId;
        vm.Name = "Phone";
        vm.Sku = "P-1";
        vm.Brand = "Brand";
        vm.BasePriceText = "10.000.000";
        vm.CostPriceText = "8.000.000";

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("SKU already exists in system.", vm.GeneralError);
        Assert.Equal("Phone", vm.Name);
    }

    [Fact]
    public async Task Timeout_SetsGeneralErrorFromCatalogApiErrors()
    {
        // Arrange
        var timeout = new ApiException(ApiErrorKind.Timeout, "Timeout");
        var commands = new RecordingProductCommands { OnCreate = _ => throw timeout };
        var vm = CreateVm(commands: commands);
        vm.SetRoute(null, null);
        await vm.ActivateAsync();

        vm.SelectedCategoryId = CatalogSamples.CategoryId;
        vm.Name = "Phone";
        vm.Sku = "P-1";
        vm.Brand = "Brand";
        vm.BasePriceText = "10.000.000";
        vm.CostPriceText = "8.000.000";

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Yêu cầu quá thời gian chờ. Hãy tải lại sản phẩm để kiểm tra trước khi thử lại.", vm.GeneralError);
    }

    [Fact]
    public async Task AddingFirstVariant_ClearsBarcode_AndSetsIsBarcodeEnabledFalse()
    {
        // Arrange
        var vm = CreateVm();
        vm.SetRoute(null, null);
        await vm.ActivateAsync();

        vm.Barcode = "123456";
        Assert.True(vm.IsBarcodeEnabled);

        // Act
        vm.AddVariantCommand.Execute(null);

        // Assert
        Assert.Equal(string.Empty, vm.Barcode);
        Assert.False(vm.IsBarcodeEnabled);
        Assert.True(vm.HasVariants);
    }

    [Fact]
    public async Task RemoveVariant_SavedRowDoesNothing_NewRowRemovesIt()
    {
        // Arrange
        var vm = CreateVm();
        vm.SetRoute(CatalogSamples.ProductId, null);
        await vm.ActivateAsync();

        var savedRow = Assert.Single(vm.Variants);

        // Act 1: remove saved row
        vm.RemoveVariantCommand.Execute(savedRow);
        Assert.Single(vm.Variants);

        // Act 2: add and remove new row
        var newRow = new VariantRowItem(null) { VariantName = "New" };
        vm.Variants.Add(newRow);
        Assert.Equal(2, vm.Variants.Count);

        vm.RemoveVariantCommand.Execute(newRow);
        Assert.Single(vm.Variants);
    }

    [Fact]
    public async Task RetiringSavedVariant_ThenSaving_SendsIsActiveFalseWithId()
    {
        // Arrange
        var commands = new RecordingProductCommands();
        var vm = CreateVm(commands: commands);
        vm.SetRoute(CatalogSamples.ProductId, null);
        await vm.ActivateAsync();

        var variant = Assert.Single(vm.Variants);
        variant.IsActive = false;

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        var (id, req) = Assert.Single(commands.Updated);
        Assert.Equal(CatalogSamples.ProductId, id);
        var sentVariant = Assert.Single(req.Variants!);
        Assert.Equal(variant.Id, sentVariant.Id);
        Assert.False(sentVariant.IsActive);
    }

    [Fact]
    public void MarginText_For35990000And31200000_CalculatesProfit()
    {
        // Arrange
        var vm = CreateVm();

        // Act
        vm.BasePriceText = "35990000";
        vm.CostPriceText = "31200000";

        // Assert
        Assert.True(vm.HasMargin);
        Assert.False(vm.IsMarginLoss);
        Assert.Equal("Lãi 4.790.000 ₫ (13,3%)", vm.MarginText);
    }

    [Fact]
    public async Task Cancel_WithoutChanges_NavigatesWithoutDialog()
    {
        // Arrange
        var nav = new NavigationService(new AllowAllPreparer());
        nav.Navigate(AppRoute.Products());
        var dialogs = new ScriptedDialogService();
        var vm = CreateVm(nav: nav, dialogs: dialogs);
        vm.SetRoute(null, null);
        await vm.ActivateAsync();

        // Act
        await vm.CancelCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(dialogs.Requests);
        Assert.Equal(AppRouteKind.ProductList, nav.CurrentRoute.Kind);
    }

    [Fact]
    public async Task Cancel_WithChangesDeclined_KeepsPage()
    {
        // Arrange
        var nav = new NavigationService(new AllowAllPreparer());
        var dialogs = new ScriptedDialogService { Result = DialogResult.Cancelled };
        var vm = CreateVm(nav: nav, dialogs: dialogs);
        vm.SetRoute(null, null);
        await vm.ActivateAsync();

        vm.Name = "Changed";
        Assert.True(vm.HasUnsavedChanges);

        // Act
        await vm.CancelCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(dialogs.Requests);
        Assert.NotEqual(AppRouteKind.ProductList, nav.CurrentRoute.Kind);
    }

    [Fact]
    public async Task Cancel_WithChangesConfirmed_NavigatesBack()
    {
        // Arrange
        var nav = new NavigationService(new AllowAllPreparer());
        var dialogs = new ScriptedDialogService { Result = DialogResult.Confirmed };

        // Act 1: Edit cancel
        var vmEdit = CreateVm(nav: nav, dialogs: dialogs);
        vmEdit.SetRoute(CatalogSamples.ProductId, null);
        await vmEdit.ActivateAsync();
        vmEdit.Name = "Changed";

        await vmEdit.CancelCommand.ExecuteAsync(null);
        Assert.Equal(AppRouteKind.ProductDetail, nav.CurrentRoute.Kind);
        Assert.Equal(CatalogSamples.ProductId, nav.CurrentRoute.ProductId);

        // Act 2: Create cancel
        var vmCreate = CreateVm(nav: nav, dialogs: dialogs);
        vmCreate.SetRoute(null, null);
        await vmCreate.ActivateAsync();
        vmCreate.Name = "Changed";

        await vmCreate.CancelCommand.ExecuteAsync(null);
        Assert.Equal(AppRouteKind.ProductList, nav.CurrentRoute.Kind);
    }

    [Fact]
    public async Task SalesStaff_CanSaveIsFalse()
    {
        // Arrange
        var session = CatalogSamples.Session("SalesStaff");
        var vm = CreateVm(session: session);
        vm.SetRoute(null, null);
        await vm.ActivateAsync();

        // Assert
        Assert.False(vm.CanManage);
        Assert.False(vm.CanSave);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SessionClearedAfterLoad_CanManageTurnsFalse()
    {
        // Arrange
        var session = CatalogSamples.Session("Admin");
        var vm = CreateVm(session: session);
        vm.SetRoute(null, null);
        await vm.ActivateAsync();
        Assert.True(vm.CanManage);

        // Act
        session.Clear();

        // Assert
        Assert.False(vm.CanManage);
        Assert.False(vm.CanSave);
    }

    [Fact]
    public async Task SetRoute_AfterActivation_ThrowsInvalidOperationException()
    {
        // Arrange
        var vm = CreateVm();
        vm.SetRoute(null, null);
        await vm.ActivateAsync();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => vm.SetRoute(Guid.NewGuid(), null));
    }

    [Fact]
    public async Task Save_NumberSpecWithLeadingZero_SetsErrorWithoutExceptionAndDoesNotCreate()
    {
        // Arrange
        var commands = new RecordingProductCommands();
        var vm = CreateVm(commands: commands);
        vm.SetRoute(null, null);
        await vm.ActivateAsync();

        vm.SelectedCategoryId = CatalogSamples.CategoryId;
        vm.Name = "Test Mouse";
        vm.Sku = "TM-01";
        vm.Brand = "Brand";
        vm.BasePriceText = "1000000";
        vm.CostPriceText = "800000";

        vm.AddSpecCommand.Execute(null);
        vm.Specs[0].Key = "ram_gb";
        vm.Specs[0].KindIndex = 1;
        vm.Specs[0].Value = "08";

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Giá trị phải là số, ví dụ 16 hoặc 6.1.", vm.Specs[0].Error);
        Assert.True(vm.HasSpecsError);
        Assert.Empty(commands.Created);
    }
}
