using System.Text.Json;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Catalog;
using TechStore.Client.ViewModels.Catalog;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class SpecNamingTests
{
    private static readonly SpecCatalog Catalog = new(new StubSpecRepository().Definitions);

    [Theory]
    [InlineData("Tần số quét (Hz)", "Tần số quét", "Hz")]
    [InlineData("  RAM (GB) ", "RAM", "GB")]
    [InlineData("Vi xử lý", "Vi xử lý", null)]
    [InlineData("(Hz)", "(Hz)", null)]
    public void SplitUnit_TakesAShortTrailingParenthesis(string text, string label, string? unit)
    {
        // Act
        var result = SpecKeyGenerator.SplitUnit(text);

        // Assert
        Assert.Equal(label, result.Label);
        Assert.Equal(unit, result.Unit);
    }

    [Theory]
    [InlineData("Tần số quét", "Hz", "tan_so_quet_hz")]
    [InlineData("Đường kính quạt", "mm", "duong_kinh_quat_mm")]
    [InlineData("Pin mAh", "mAh", "pin_mah")]
    [InlineData("Kết nối 4G / 5G", null, "ket_noi_4g_5g")]
    [InlineData("4G", null, "spec_4g")]
    [InlineData("!!!", null, null)]
    [InlineData("", null, null)]
    public void Generate_BuildsASnakeCaseKey(string label, string? unit, string? expected)
    {
        // Act
        var key = SpecKeyGenerator.Generate(label, unit);

        // Assert
        Assert.Equal(expected, key);
    }

    [Fact]
    public void Generate_CapsTheLength()
    {
        // Act
        var key = SpecKeyGenerator.Generate(new string('a', 100));

        // Assert
        Assert.Equal(SpecKeyGenerator.MaxKeyLength, key!.Length);
    }

    [Fact]
    public void SpecCatalog_FindsByNameIgnoringCaseAndAccents_AndSuggestsByNameOrKey()
    {
        // Act & Assert
        Assert.Equal("storage_gb", Catalog.FindByName("bo nho TRONG")!.Key);
        Assert.Null(Catalog.FindByName("bo nho"));
        Assert.Equal("ram_gb", Catalog.FindByKey("ram_gb")!.Key);
        Assert.Equal(["storage_gb"], Catalog.Suggest("bo nho").Select(d => d.Key));
        Assert.Equal(["storage_gb"], Catalog.Suggest("stor").Select(d => d.Key));
        Assert.Empty(Catalog.Suggest("  "));
    }

    [Theory]
    [InlineData("16", "GB", "16 GB")]
    [InlineData("16", null, "16")]
    [InlineData("true", null, "Có")]
    [InlineData("false", null, "Không")]
    [InlineData("\"Apple M3\"", null, "Apple M3")]
    public void SpecPresentation_ValueText_AddsUnitAndYesNo(string json, string? unit, string expected)
    {
        // Arrange
        using var document = JsonDocument.Parse(json);

        // Act
        var text = SpecPresentation.ValueText(document.RootElement, unit);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void SpecPresentation_NamesAndCounts()
    {
        // Act & Assert
        Assert.Equal("RAM", SpecPresentation.KeyName("ram_gb", "RAM"));
        Assert.Equal("fan_rpm", SpecPresentation.KeyName("fan_rpm", null));
        Assert.Equal("RAM (3)", SpecPresentation.WithCount("RAM", 3));
        Assert.Equal("Có", SpecPresentation.ValueText("true", SpecValueKind.Boolean, null));
        Assert.Equal("512 GB", SpecPresentation.ValueText("512", SpecValueKind.Number, "GB"));
    }
}
