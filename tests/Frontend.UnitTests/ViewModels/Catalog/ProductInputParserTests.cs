using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.ViewModels.Catalog;
using Xunit;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class ProductInputParserTests
{
    [Theory]
    [InlineData("35990000", 35990000)]
    [InlineData("35.990.000", 35990000)]
    [InlineData("35.990.000,5", 35990000.5)]
    [InlineData("12.5", 12.5)]
    [InlineData("6.100", 6100)]
    [InlineData(" 1000 ", 1000)]
    public void TryParseMoney_ValidInputs_ParsesExpectedAmount(string input, decimal expected)
    {
        // Act
        var success = ProductInputParser.TryParseMoney(input, out var value);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("10000000000000000")]
    public void TryParseMoney_InvalidInputs_ReturnsFalse(string? input)
    {
        // Act
        var success = ProductInputParser.TryParseMoney(input, out var value);

        // Assert
        Assert.False(success);
        Assert.Equal(0m, value);
    }

    [Fact]
    public void ParseSpecs_TypedValues_ParsesNumberBooleanAndTextCorrectly()
    {
        // Arrange
        var rows = new[]
        {
            new SpecDraft("ram_gb", SpecValueKind.Number, "16"),
            new SpecDraft("has_5g", SpecValueKind.Boolean, "TRUE"),
            new SpecDraft("cpu", SpecValueKind.Text, "Apple M3"),
            new SpecDraft("screen_size", SpecValueKind.Number, "6,1"),
            new SpecDraft("large_id", SpecValueKind.Number, "12345678901234567890")
        };

        // Act
        var result = ProductInputParser.ParseSpecs(rows);

        // Assert
        Assert.True(result.IsValid);
        Assert.All(result.RowErrors, Assert.Null);

        Assert.Equal(JsonValueKind.Number, result.Values["ram_gb"].ValueKind);
        Assert.Equal("16", result.Values["ram_gb"].GetRawText());

        Assert.Equal(JsonValueKind.True, result.Values["has_5g"].ValueKind);

        Assert.Equal(JsonValueKind.String, result.Values["cpu"].ValueKind);
        Assert.Equal("Apple M3", result.Values["cpu"].GetString());

        Assert.Equal(JsonValueKind.Number, result.Values["screen_size"].ValueKind);
        Assert.Equal("6.1", result.Values["screen_size"].GetRawText());

        Assert.Equal(JsonValueKind.Number, result.Values["large_id"].ValueKind);
        Assert.Equal("12345678901234567890", result.Values["large_id"].GetRawText());
    }

    [Theory]
    [InlineData("RAM GB")]
    [InlineData("1abc")]
    public void ParseSpecs_InvalidKey_ReturnsSnakeCaseError(string key)
    {
        // Arrange
        var rows = new[] { new SpecDraft(key, SpecValueKind.Text, "value") };

        // Act
        var result = ProductInputParser.ParseSpecs(rows);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Tên thông số phải dùng snake_case, ví dụ ram_gb.", result.RowErrors[0]);
    }

    [Fact]
    public void ParseSpecs_DuplicateKey_ErrorsOnSecondRowOnly()
    {
        // Arrange
        var rows = new[]
        {
            new SpecDraft("ram_gb", SpecValueKind.Number, "8"),
            new SpecDraft("ram_gb", SpecValueKind.Number, "16")
        };

        // Act
        var result = ProductInputParser.ParseSpecs(rows);

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(result.RowErrors[0]);
        Assert.Equal("Tên thông số bị trùng.", result.RowErrors[1]);
    }

    [Fact]
    public void ParseSpecs_EmptyValue_ReturnsEmptyValueError()
    {
        // Arrange
        var rows = new[] { new SpecDraft("ram_gb", SpecValueKind.Number, "   ") };

        // Act
        var result = ProductInputParser.ParseSpecs(rows);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Giá trị không được để trống.", result.RowErrors[0]);
    }

    [Fact]
    public void ParseSpecs_InvalidNumber_ReturnsNumberError()
    {
        // Arrange
        var rows = new[] { new SpecDraft("ram_gb", SpecValueKind.Number, "abc") };

        // Act
        var result = ProductInputParser.ParseSpecs(rows);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Giá trị phải là số, ví dụ 16 hoặc 6.1.", result.RowErrors[0]);
    }

    [Fact]
    public void ParseSpecs_InvalidBoolean_ReturnsBooleanError()
    {
        // Arrange
        var rows = new[] { new SpecDraft("has_5g", SpecValueKind.Boolean, "yes") };

        // Act
        var result = ProductInputParser.ParseSpecs(rows);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Giá trị phải là true hoặc false.", result.RowErrors[0]);
    }

    [Fact]
    public void ParseSpecs_EmptyInput_ReturnsValidWithEmptyDictionary()
    {
        // Arrange & Act
        var result = ProductInputParser.ParseSpecs([]);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.RowErrors);
        Assert.Empty(result.Values);
    }

    [Fact]
    public void SpecsFromJson_ParsesFromJsonElementMap()
    {
        // Arrange
        const string json = """{"ram_gb": 8, "cpu": "M3", "has_5g": false}""";
        using var doc = JsonDocument.Parse(json);
        var map = new Dictionary<string, JsonElement>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            map[prop.Name] = prop.Value;
        }

        // Act
        var drafts = ProductInputParser.SpecsFromJson(map);

        // Assert
        Assert.Equal(3, drafts.Count);

        Assert.Equal("ram_gb", drafts[0].Key);
        Assert.Equal(SpecValueKind.Number, drafts[0].Kind);
        Assert.Equal("8", drafts[0].Value);

        Assert.Equal("cpu", drafts[1].Key);
        Assert.Equal(SpecValueKind.Text, drafts[1].Kind);
        Assert.Equal("M3", drafts[1].Value);

        Assert.Equal("has_5g", drafts[2].Key);
        Assert.Equal(SpecValueKind.Boolean, drafts[2].Kind);
        Assert.Equal("false", drafts[2].Value);
    }

    [Fact]
    public void SpecsRoundTrip_EqualsOriginalByRawText()
    {
        // Arrange
        const string json = """{"ram_gb": 16, "cpu": "M3", "has_5g": true}""";
        using var doc = JsonDocument.Parse(json);
        var map = new Dictionary<string, JsonElement>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            map[prop.Name] = prop.Value;
        }

        // Act
        var drafts = ProductInputParser.SpecsFromJson(map);
        var parsed = ProductInputParser.ParseSpecs(drafts);

        // Assert
        Assert.True(parsed.IsValid);
        Assert.Equal(map.Count, parsed.Values.Count);
        foreach (var (key, expectedElement) in map)
        {
            Assert.True(parsed.Values.ContainsKey(key));
            Assert.Equal(expectedElement.GetRawText(), parsed.Values[key].GetRawText());
        }
    }
}
