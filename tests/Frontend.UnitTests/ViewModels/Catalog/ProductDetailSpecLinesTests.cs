using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class ProductDetailSpecLinesTests
{
    private static Dictionary<string, JsonElement> Specs(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static async Task<ProductDetailViewModel> LoadAsync(StubSpecRepository specs)
    {
        var variant = CatalogSamples.Variant(CatalogSamples.ProductId, "P-1-V1") with
        {
            EffectiveSpecs = Specs("""{"ram_gb": 16, "wireless": false}""")
        };
        var reader = new StubProductReader
        {
            OnGetById = id => Task.FromResult<ProductDetail?>(CatalogSamples.Detail(id, variants: [variant],
                specs: Specs("""{"ram_gb": 16, "wireless": true, "fan_rpm": 2000}""")))
        };
        var vm = new ProductDetailViewModel(reader, new NavigationService(new AllowAllPreparer()), CatalogSamples.Session("Admin"),
            new RecordingProductCommands(), new ScriptedDialogService(), specs);
        vm.SetRoute(CatalogSamples.ProductId, null, null);
        await vm.ActivateAsync();
        return vm;
    }

    [Fact]
    public async Task SpecLines_UseDefinitionNamesUnitsAndYesNo()
    {
        // Act
        var vm = await LoadAsync(new StubSpecRepository());

        // Assert
        Assert.Equal([new SpecLine("RAM", "16 GB"), new SpecLine("Không dây", "Có"), new SpecLine("fan_rpm", "2000")], vm.SpecLines);
        Assert.Equal("RAM: 16 GB   ·   Không dây: Không", Assert.Single(vm.VariantLines).SpecsText);
    }

    [Fact]
    public async Task SpecLines_ShowKeysWhenDefinitionsCannotBeLoaded()
    {
        // Act
        var vm = await LoadAsync(new StubSpecRepository { FailDefinitions = true });

        // Assert
        Assert.Equal([new SpecLine("ram_gb", "16"), new SpecLine("wireless", "Có"), new SpecLine("fan_rpm", "2000")], vm.SpecLines);
    }
}
