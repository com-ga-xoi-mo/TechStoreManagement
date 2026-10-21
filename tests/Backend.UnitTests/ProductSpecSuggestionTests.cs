using System.Text.Json;
using FluentAssertions;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Catalog.Services;
using Xunit;

namespace TechStore.UnitTests;

public class ProductSpecSuggestionTests
{
    private static SpecValueCount Row(string key, string? valueJson, int count) =>
        new() { Key = key, ValueJson = valueJson, ProductCount = count };

    [Fact]
    public void SpecFilterParser_QuotedValue_IsAString()
    {
        // Act
        var result = ProductSpecFilterHelper.Parse(["bluetooth:\"5.2\"", "flag:\"true\""]);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ParsedValues!["bluetooth"].Should().Be("5.2");
        result.ParsedValues!["flag"].Should().Be("true");
        result.JsonString.Should().Contain("\"bluetooth\":\"5.2\"");
    }

    [Fact]
    public void SpecFilterParser_EmptyQuotedValue_IsRejected()
    {
        // Act
        var result = ProductSpecFilterHelper.Parse(["cpu:\"\""]);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Build_OrdersKeysByProductCountThenKey_AndUsesTheKeyTotal()
    {
        // Arrange: ram_gb has 3 products in total although its values add up to 4 (one product has two variants).
        var rows = new[]
        {
            Row("cpu", null, 1), Row("cpu", "\"Apple M3\"", 1),
            Row("ram_gb", null, 3), Row("ram_gb", "16", 2), Row("ram_gb", "8", 2),
            Row("color", null, 1), Row("color", "\"Black\"", 1)
        };

        // Act
        var keys = ProductSpecSuggestionBuilder.Build(rows);

        // Assert
        keys.Select(k => k.Key).Should().Equal("ram_gb", "color", "cpu");
        keys[0].ProductCount.Should().Be(3);
        keys[0].Values.Select(v => v.Label).Should().Equal("8", "16");
    }

    [Fact]
    public void Build_OrdersValuesNumbersThenStringsThenFalseThenTrue()
    {
        // Arrange
        var rows = new[]
        {
            Row("mixed", null, 6), Row("mixed", "true", 1), Row("mixed", "\"b\"", 1), Row("mixed", "10", 1),
            Row("mixed", "false", 1), Row("mixed", "\"a\"", 1), Row("mixed", "2.5", 1)
        };

        // Act
        var values = ProductSpecSuggestionBuilder.Build(rows).Single().Values;

        // Assert
        values.Select(v => v.Label).Should().Equal("2.5", "10", "a", "b", "false", "true");
    }

    [Theory]
    [InlineData("\"Apple M3\"", "Apple M3", "Apple M3")]
    [InlineData("\"5.2\"", "5.2", "\"5.2\"")]
    [InlineData("\"true\"", "true", "\"true\"")]
    [InlineData("\"128\"", "128", "\"128\"")]
    [InlineData("16", "16", "16")]
    [InlineData("6.1", "6.1", "6.1")]
    [InlineData("true", "true", "true")]
    public void Build_FilterValueMatchesTheStoredValue(string valueJson, string expectedLabel, string expectedFilterValue)
    {
        // Arrange
        var rows = new[] { Row("spec_key", null, 1), Row("spec_key", valueJson, 1) };

        // Act
        var value = ProductSpecSuggestionBuilder.Build(rows).Single().Values.Single();
        var parsed = ProductSpecFilterHelper.Parse([$"spec_key:{value.FilterValue}"]);
        using var filter = JsonDocument.Parse(parsed.JsonString!);

        // Assert
        value.Label.Should().Be(expectedLabel);
        value.FilterValue.Should().Be(expectedFilterValue);
        JsonElement.DeepEquals(filter.RootElement.GetProperty("spec_key"), value.Value).Should().BeTrue();
    }

    [Fact]
    public void Build_SkipsEmptyStrings_AndKeysWithoutValues()
    {
        // Arrange
        var rows = new[] { Row("note", null, 1), Row("note", "\"\"", 1) };

        // Act
        var keys = ProductSpecSuggestionBuilder.Build(rows);

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact]
    public void Build_CapsValuesPerKey()
    {
        // Arrange
        var rows = Enumerable.Range(1, 150).Select(i => Row("size", i.ToString(), 1)).Append(Row("size", null, 150));

        // Act
        var values = ProductSpecSuggestionBuilder.Build(rows).Single().Values;

        // Assert
        values.Should().HaveCount(ProductSpecSuggestionBuilder.MaxValuesPerKey);
        values[0].Label.Should().Be("1");
    }

    [Fact]
    public async Task GetSpecKeysAsync_PassesTheCategory_AndAddsDefinitionLabels()
    {
        // Arrange
        var productRepo = new FakeProductRepository();
        productRepo.SpecValueCounts.AddRange([Row("ram_gb", null, 1), Row("ram_gb", "16", 1)]);
        var definitions = new FakeSpecDefinitionRepository().With("ram_gb", "RAM", "GB", "number");
        var service = new ProductService(productRepo, new FakeCategoryRepository(), new FakeUnitOfWork(), definitions);
        var categoryId = Guid.NewGuid();

        // Act
        var keys = await service.GetSpecKeysAsync(categoryId);

        // Assert
        productRepo.LastSpecCategoryId.Should().Be(categoryId);
        keys.Single().Key.Should().Be("ram_gb");
        keys.Single().Label.Should().Be("RAM");
        keys.Single().Unit.Should().Be("GB");
        keys.Single().ValueKind.Should().Be("number");
    }
}
