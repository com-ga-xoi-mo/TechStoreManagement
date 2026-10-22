using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class ProductSellingStateTests
{
    private static async Task<ProductDetailViewModel> DetailAsync(ProductDetail product)
    {
        var reader = new StubProductReader { OnGetById = _ => Task.FromResult<ProductDetail?>(product) };
        var vm = new ProductDetailViewModel(reader, new NavigationService(new AllowAllPreparer()), CatalogSamples.Session("Admin"),
            new RecordingProductCommands(), new ScriptedDialogService(), new StubSpecRepository());
        vm.SetRoute(CatalogSamples.ProductId, null, null);
        await vm.ActivateAsync();
        return vm;
    }

    [Fact]
    public async Task SellingHint_InactiveProductWithVariants_AsksToOpenProductAndVariants()
    {
        // Act
        var vm = await DetailAsync(CatalogSamples.Detail(CatalogSamples.ProductId, isActive: false,
            variants: [CatalogSamples.Variant(CatalogSamples.ProductId, "P-1-V1", isActive: false)]));

        // Assert
        Assert.True(vm.HasSellingHint);
        Assert.Contains("bật Đang bán cho các biến thể", vm.SellingHint);
        Assert.Equal("Mở bán", vm.ToggleStatusText);
    }

    [Fact]
    public async Task SellingHint_InactiveProductWithoutVariants_OnlyAsksToOpenIt()
    {
        // Act
        var vm = await DetailAsync(CatalogSamples.Detail(CatalogSamples.ProductId, isActive: false, variants: []));

        // Assert
        Assert.Equal("Sản phẩm đang không bán nên chưa chọn được khi lập đơn. Bấm Mở bán khi sẵn sàng.", vm.SellingHint);
    }

    [Fact]
    public async Task SellingHint_ActiveProductWithoutSellingVariant_WarnsAboutVariants()
    {
        // Act
        var vm = await DetailAsync(CatalogSamples.Detail(CatalogSamples.ProductId, isActive: true,
            variants: [CatalogSamples.Variant(CatalogSamples.ProductId, "P-1-V1", isActive: false)]));

        // Assert
        Assert.Contains("chưa có biến thể nào đang bán", vm.SellingHint);
    }

    [Fact]
    public async Task SellingHint_ActiveProductWithASellingVariant_IsEmpty()
    {
        // Act
        var vm = await DetailAsync(CatalogSamples.Detail(CatalogSamples.ProductId, isActive: true,
            variants: [CatalogSamples.Variant(CatalogSamples.ProductId, "P-1-V1", isActive: true)]));

        // Assert
        Assert.False(vm.HasSellingHint);
    }

    [Fact]
    public async Task Editor_NewVariantRow_StartsNotSelling_AndCreateSendsEachFlag()
    {
        // Arrange
        var commands = new RecordingProductCommands();
        var vm = new ProductEditorViewModel(new StubProductReader(), commands, new StubCategoryReader(),
            new NavigationService(new AllowAllPreparer()), new ScriptedDialogService(), CatalogSamples.Session("Admin"),
            new StubSpecRepository(), new StubBrandRepository());
        vm.SetRoute(null, null);
        await vm.ActivateAsync();
        vm.SelectedCategoryId = CatalogSamples.CategoryId;
        vm.Name = "iPhone 16";
        vm.Sku = "IP16";
        vm.Brand = "Apple";
        vm.BasePriceText = "20000000";
        vm.CostPriceText = "17000000";
        vm.AddVariantCommand.Execute(null);
        vm.AddVariantCommand.Execute(null);
        vm.Variants[0].VariantName = "Đen";
        vm.Variants[0].Sku = "IP16-BK";
        vm.Variants[0].PriceText = "20000000";
        vm.Variants[1].VariantName = "Trắng";
        vm.Variants[1].Sku = "IP16-WH";
        vm.Variants[1].PriceText = "23000000";
        var defaultFlag = vm.Variants[0].IsActive;
        vm.Variants[1].IsActive = true;

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.False(defaultFlag);
        var request = Assert.Single(commands.Created);
        Assert.Equal([false, true], request.Variants!.Select(v => v.IsActive ?? false));
    }
}
