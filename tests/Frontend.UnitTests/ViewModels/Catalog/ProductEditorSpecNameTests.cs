using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Catalog;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class ProductEditorSpecNameTests
{
    private static ProductEditorViewModel CreateVm(
        RecordingProductCommands? commands = null,
        StubProductReader? reader = null,
        StubSpecRepository? specs = null) =>
        new(reader ?? new StubProductReader(), commands ?? new RecordingProductCommands(), new StubCategoryReader(),
            new NavigationService(new AllowAllPreparer()), new ScriptedDialogService(), CatalogSamples.Session("Admin"),
            specs ?? new StubSpecRepository());

    private static async Task<ProductEditorViewModel> CreateReadyToSave(RecordingProductCommands commands)
    {
        var vm = CreateVm(commands);
        vm.SetRoute(null, null);
        await vm.ActivateAsync();
        vm.SelectedCategoryId = CatalogSamples.CategoryId;
        vm.Name = "Màn hình";
        vm.Sku = "MON-1";
        vm.Brand = "LG";
        vm.BasePriceText = "5000000";
        vm.CostPriceText = "4000000";
        return vm;
    }

    private static Dictionary<string, JsonElement> Specs(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public async Task TypingAnExistingName_UsesItsKeyKindAndUnit_WithoutRewritingTheText()
    {
        // Arrange
        var commands = new RecordingProductCommands();
        var vm = await CreateReadyToSave(commands);
        vm.AddSpecCommand.Execute(null);
        var row = vm.Specs[0];

        // Act
        row.Name = "ram";
        row.Value = "16";
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("ram", row.Name);
        Assert.Equal("ram_gb", row.Key);
        Assert.True(row.IsKnown);
        Assert.False(row.IsKindEditable);
        Assert.False(row.CanEditKey);
        Assert.Equal("key: ram_gb", row.KeyCaption);
        Assert.Equal(1, row.KindIndex);
        Assert.Equal("GB", row.Unit);
        var request = Assert.Single(commands.Created);
        Assert.Equal(16, request.Specs!["ram_gb"].GetInt32());
        Assert.Null(request.SpecLabels);
    }

    [Fact]
    public async Task TypingANewName_GeneratesTheKey_AndSendsTheNameWithTheProduct()
    {
        // Arrange
        var commands = new RecordingProductCommands();
        var vm = await CreateReadyToSave(commands);
        vm.AddSpecCommand.Execute(null);
        var row = vm.Specs[0];

        // Act
        row.Name = "Tần số quét (Hz)";
        row.KindIndex = 1;
        row.Value = "144";
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("tan_so_quet_hz", row.Key);
        Assert.False(row.IsKnown);
        Assert.Equal("Hz", row.Unit);
        Assert.Equal("Thông số mới · key: tan_so_quet_hz", row.KeyCaption);
        Assert.True(row.CanEditKey);
        var request = Assert.Single(commands.Created);
        Assert.Equal(144, request.Specs!["tan_so_quet_hz"].GetInt32());
        var label = Assert.Single(request.SpecLabels!);
        Assert.Equal("tan_so_quet_hz", label.Key);
        Assert.Equal("Tần số quét", label.Value.Label);
        Assert.Equal("Hz", label.Value.Unit);
        Assert.Equal("number", label.Value.ValueKind);
    }

    [Fact]
    public async Task PartialName_ShowsSuggestions_AndPickingOneFillsTheRow()
    {
        // Arrange
        var vm = await CreateReadyToSave(new RecordingProductCommands());
        vm.AddSpecCommand.Execute(null);
        var row = vm.Specs[0];

        // Act
        row.Name = "bo nho";
        var suggestion = Assert.Single(row.Suggestions);
        vm.PickSpecSuggestionCommand.Execute(suggestion);
        vm.PickSpecSuggestionCommand.Execute(vm);

        // Assert
        Assert.Equal("Bộ nhớ trong (GB) · storage_gb", suggestion.DisplayName);
        Assert.Equal("Bộ nhớ trong", row.Name);
        Assert.Equal("storage_gb", row.Key);
        Assert.True(row.IsKnown);
        Assert.False(row.HasSuggestions);
    }

    [Fact]
    public async Task EditMode_ShowsDefinitionNames_AndKeepsAnUndefinedKeyWhileItGetsAName()
    {
        // Arrange
        var commands = new RecordingProductCommands();
        var reader = new StubProductReader
        {
            OnGetById = id => Task.FromResult<ProductDetail?>(CatalogSamples.Detail(id, variants: [],
                specs: Specs("""{"ram_gb": 8, "cpu": "M3", "fan_rpm": 2000}""")))
        };
        var vm = CreateVm(commands, reader);
        vm.SetRoute(CatalogSamples.ProductId, null);
        await vm.ActivateAsync();
        var fan = vm.Specs[2];

        // Act
        var namesAfterLoad = vm.Specs.Select(s => s.Name).ToList();
        var unchangedAfterLoad = vm.HasUnsavedChanges;
        fan.Name = "Tốc độ quạt (rpm)";
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(["RAM", "Vi xử lý (CPU)", ""], namesAfterLoad);
        Assert.False(unchangedAfterLoad);
        Assert.Equal("fan_rpm", fan.Key);
        var update = Assert.Single(commands.Updated).Request;
        var label = Assert.Single(update.SpecLabels!);
        Assert.Equal("fan_rpm", label.Key);
        Assert.Equal("Tốc độ quạt", label.Value.Label);
        Assert.Equal("rpm", label.Value.Unit);
    }

    [Fact]
    public async Task NameWithoutLettersOrDigits_BlocksTheSave()
    {
        // Arrange
        var commands = new RecordingProductCommands();
        var vm = await CreateReadyToSave(commands);
        vm.AddSpecCommand.Execute(null);

        // Act
        vm.Specs[0].Name = "!!!";
        vm.Specs[0].Value = "1";
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Hãy nhập tên thông số có chữ hoặc số.", vm.Specs[0].Error);
        Assert.Empty(commands.Created);
    }

    [Fact]
    public async Task DefinitionsUnavailable_EditorStillWorksWithGeneratedKeys()
    {
        // Arrange
        var vm = CreateVm(specs: new StubSpecRepository { FailDefinitions = true });
        vm.SetRoute(null, null);

        // Act
        await vm.ActivateAsync();
        vm.AddSpecCommand.Execute(null);
        vm.Specs[0].Name = "RAM";

        // Assert
        Assert.Equal(PageContentState.Content, vm.ContentState);
        Assert.True(vm.HasSpecCatalogMessage);
        Assert.Equal("ram", vm.Specs[0].Key);
        Assert.False(vm.Specs[0].IsKnown);
    }

    [Fact]
    public async Task ServerSpecLabelError_LandsOnTheRowWithThatKey()
    {
        // Arrange
        var commands = new RecordingProductCommands
        {
            OnCreate = _ => Task.FromException<ProductDetail>(new ApiException(ApiErrorKind.Http, "bad", 400,
                fieldErrors: new Dictionary<string, IReadOnlyList<string>> { ["specLabels.tan_so_quet_hz"] = ["Tên đã dùng."] }))
        };
        var vm = await CreateReadyToSave(commands);
        vm.AddSpecCommand.Execute(null);
        vm.Specs[0].Name = "Tần số quét (Hz)";
        vm.Specs[0].KindIndex = 1;
        vm.Specs[0].Value = "144";

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Tên đã dùng.", vm.Specs[0].Error);
    }
}
