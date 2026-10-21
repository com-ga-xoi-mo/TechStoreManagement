using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class ProductEditorBrandTests
{
    private static async Task<ProductEditorViewModel> ActivateAsync(StubBrandRepository? brands = null)
    {
        var vm = new ProductEditorViewModel(new StubProductReader(), new RecordingProductCommands(), new StubCategoryReader(),
            new NavigationService(new AllowAllPreparer()), new ScriptedDialogService(), CatalogSamples.Session("Admin"),
            new StubSpecRepository(), brands ?? new StubBrandRepository());
        vm.SetRoute(null, null);
        await vm.ActivateAsync();
        return vm;
    }

    [Fact]
    public async Task TypingPartOfABrand_SuggestsIt_AndPickingFillsTheBox()
    {
        // Arrange
        var vm = await ActivateAsync();

        // Act
        vm.Brand = "app";
        var suggestion = Assert.Single(vm.BrandSuggestions);
        vm.PickBrandCommand.Execute(suggestion);
        vm.PickBrandCommand.Execute(vm);

        // Assert
        Assert.Equal("Apple (3)", suggestion.DisplayName);
        Assert.Equal("Apple", vm.Brand);
        Assert.False(vm.HasBrandSuggestions);
        Assert.False(vm.HasBrandHint);
    }

    [Fact]
    public async Task SameBrandInAnotherLetterCase_ShowsTheHint_WithoutSuggestions()
    {
        // Arrange
        var vm = await ActivateAsync();

        // Act
        vm.Brand = " apple ";

        // Assert
        Assert.True(vm.HasBrandHint);
        Assert.Contains("“Apple”", vm.BrandHint);
        Assert.False(vm.HasBrandSuggestions);
    }

    [Fact]
    public async Task NewBrand_HasNoSuggestionOrHint()
    {
        // Arrange
        var vm = await ActivateAsync();

        // Act
        vm.Brand = "Razer";

        // Assert
        Assert.False(vm.HasBrandSuggestions);
        Assert.False(vm.HasBrandHint);
    }

    [Fact]
    public async Task BrandsUnavailable_TheBoxStaysFreeText()
    {
        // Arrange
        var vm = await ActivateAsync(new StubBrandRepository { Fail = true });

        // Act
        vm.Brand = "app";

        // Assert
        Assert.False(vm.HasBrandSuggestions);
        Assert.Equal("app", vm.Brand);
    }
}
