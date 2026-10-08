using FluentAssertions;
using TechStore.Api.Catalog.Services;
using Xunit;

namespace TechStore.UnitTests;

public class ProductServiceTests
{
    [Theory]
    [InlineData(null, null, 1, 20)]
    [InlineData(0, null, 1, 20)]
    [InlineData(-5, null, 1, 20)]
    [InlineData(2, null, 2, 20)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, -10, 1, 1)]
    [InlineData(1, 500, 1, 100)]
    [InlineData(0, 500, 1, 100)]
    [InlineData(3, 50, 3, 50)]
    public void PagingNormalizer_Should_Normalize_Page_And_PageSize(
        int? inputPage, int? inputPageSize, int expectedPage, int expectedPageSize)
    {
        // Act
        var (page, pageSize) = ProductPagingHelper.Normalize(inputPage, inputPageSize);

        // Assert
        page.Should().Be(expectedPage);
        pageSize.Should().Be(expectedPageSize);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(-1, 20, 0)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(100, 20, 5)]
    [InlineData(101, 20, 6)]
    public void CalculateTotalPages_Should_Return_Ceiling_Or_Zero_When_Empty(
        int totalCount, int pageSize, int expectedTotalPages)
    {
        // Act
        var totalPages = ProductPagingHelper.CalculateTotalPages(totalCount, pageSize);

        // Assert
        totalPages.Should().Be(expectedTotalPages);
    }

    [Theory]
    [InlineData("ram_gb")] // no colon
    [InlineData(":16")] // empty key
    [InlineData("RAM:16")] // not snake_case
    [InlineData("ram_gb:")] // empty value
    public void SpecFilterParser_Should_Reject_Malformed_Spec(string malformedSpec)
    {
        // Act
        var result = ProductSpecFilterHelper.Parse(new[] { malformedSpec });

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SpecFilterParser_Should_Reject_Duplicate_Key_With_Different_Values()
    {
        // Arrange
        var specs = new[] { "ram_gb:16", "ram_gb:32" };

        // Act
        var result = ProductSpecFilterHelper.Parse(specs);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Duplicate spec key");
    }

    [Fact]
    public void SpecFilterParser_Should_Parse_Typed_Values_Correctly()
    {
        // Arrange
        var specs = new[]
        {
            "ram_gb:16",
            "screen_size:16.5",
            "in_stock:true",
            "cpu:Apple M3"
        };

        // Act
        var result = ProductSpecFilterHelper.Parse(specs);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ParsedValues.Should().NotBeNull();
        result.ParsedValues!["ram_gb"].Should().Be(16L);
        result.ParsedValues!["screen_size"].Should().Be(16.5m);
        result.ParsedValues!["in_stock"].Should().Be(true);
        result.ParsedValues!["cpu"].Should().Be("Apple M3");
        result.JsonString.Should().Contain("\"ram_gb\":16");
        result.JsonString.Should().Contain("\"screen_size\":16.5");
        result.JsonString.Should().Contain("\"in_stock\":true");
        result.JsonString.Should().Contain("\"cpu\":\"Apple M3\"");
    }

    [Fact]
    public void SpecsValidator_Should_Pass_For_Valid_Flat_Object()
    {
        // Arrange
        using var doc = System.Text.Json.JsonDocument.Parse("{\"cpu\": \"Apple M3\", \"ram_gb\": 16, \"in_stock\": true}");
        var dict = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var errors = new Dictionary<string, List<string>>();

        // Act
        var isValid = ProductSpecValidatorHelper.ValidateSpecs(dict, "specs", errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void SpecsValidator_Should_Reject_Invalid_Key()
    {
        // Arrange
        using var doc = System.Text.Json.JsonDocument.Parse("{\"RAM GB\": 16}");
        var dict = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var errors = new Dictionary<string, List<string>>();

        // Act
        var isValid = ProductSpecValidatorHelper.ValidateSpecs(dict, "specs", errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().ContainKey("specs");
        errors["specs"].Should().Contain(msg => msg.Contains("RAM GB"));
    }

    [Fact]
    public void SpecsValidator_Should_Reject_Nested_Object()
    {
        // Arrange
        using var doc = System.Text.Json.JsonDocument.Parse("{\"display\": {\"size_inch\": 14}}");
        var dict = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var errors = new Dictionary<string, List<string>>();

        // Act
        var isValid = ProductSpecValidatorHelper.ValidateSpecs(dict, "variants[0].specs", errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().ContainKey("variants[0].specs");
        errors["variants[0].specs"].Should().Contain(msg => msg.Contains("nested object"));
    }

    [Fact]
    public void SpecsValidator_Should_Reject_Array()
    {
        // Arrange
        using var doc = System.Text.Json.JsonDocument.Parse("{\"tags\": [1, 2, 3]}");
        var dict = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var errors = new Dictionary<string, List<string>>();

        // Act
        var isValid = ProductSpecValidatorHelper.ValidateSpecs(dict, "specs", errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().ContainKey("specs");
        errors["specs"].Should().Contain(msg => msg.Contains("array"));
    }

    [Fact]
    public void SpecsValidator_Should_Reject_Null_Value()
    {
        // Arrange
        using var doc = System.Text.Json.JsonDocument.Parse("{\"color\": null}");
        var dict = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var errors = new Dictionary<string, List<string>>();

        // Act
        var isValid = ProductSpecValidatorHelper.ValidateSpecs(dict, "variants[1].specs", errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().ContainKey("variants[1].specs");
        errors["variants[1].specs"].Should().Contain(msg => msg.Contains("null"));
    }

    [Fact]
    public void EffectiveSpecs_Should_Override_Product_Values_And_Inherit_Remaining()
    {
        // Arrange
        using var prodDoc = System.Text.Json.JsonDocument.Parse("{\"cpu\": \"Apple M3\", \"color\": \"Silver\"}");
        using var varDoc = System.Text.Json.JsonDocument.Parse("{\"color\": \"Space Gray\"}");
        var prodSpecs = prodDoc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        var varSpecs = varDoc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        // Act
        var effective = ProductSpecHelper.MergeEffectiveSpecs(prodSpecs, varSpecs);

        // Assert
        effective["cpu"].GetString().Should().Be("Apple M3");
        effective["color"].GetString().Should().Be("Space Gray");
    }

    [Fact]
    public void EffectiveSpecs_Should_Inherit_All_When_Variant_Specs_Are_Empty()
    {
        // Arrange
        using var prodDoc = System.Text.Json.JsonDocument.Parse("{\"cpu\": \"Apple M3\", \"ram_gb\": 16}");
        var prodSpecs = prodDoc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        // Act
        var effective = ProductSpecHelper.MergeEffectiveSpecs(prodSpecs, new Dictionary<string, System.Text.Json.JsonElement>());

        // Assert
        effective["cpu"].GetString().Should().Be("Apple M3");
        effective["ram_gb"].GetInt64().Should().Be(16L);
    }

    [Fact]
    public void CalculateDisplayPrice_Should_Return_Min_Max_When_Variants_Present()
    {
        // Arrange
        var basePrice = 30000000m;
        var variantPrices = new[] { 20000000m, 25000000m, 22000000m };

        // Act
        var (priceFrom, priceTo) = ProductSpecHelper.CalculateDisplayPrice(basePrice, variantPrices);

        // Assert
        priceFrom.Should().Be(20000000m);
        priceTo.Should().Be(25000000m);
    }

    [Fact]
    public void CalculateDisplayPrice_Should_Return_BasePrice_When_No_Variants()
    {
        // Arrange
        var basePrice = 15000000m;

        // Act
        var (priceFrom, priceTo) = ProductSpecHelper.CalculateDisplayPrice(basePrice, Array.Empty<decimal>());

        // Assert
        priceFrom.Should().Be(15000000m);
        priceTo.Should().Be(15000000m);
    }

    private static TechStore.Shared.Requests.CreateProductRequest CreateValidRequest()
    {
        return new TechStore.Shared.Requests.CreateProductRequest
        {
            CategoryId = Guid.NewGuid(),
            Name = "iPhone 15 Pro Max",
            Sku = "IP15PM-256",
            Brand = "Apple",
            BasePrice = 30000000m,
            CostPrice = 25000000m
        };
    }

    [Fact]
    public void CreateProductValidator_Should_Pass_For_Valid_Request()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var errors = CreateProductValidator.Validate(request);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void CreateProductValidator_Should_Reject_Empty_Name()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Name = "   ";

        // Act
        var errors = CreateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("name");
    }

    [Fact]
    public void CreateProductValidator_Should_Report_Missing_BasePrice_And_CategoryId_Together()
    {
        // Arrange
        var request = CreateValidRequest();
        request.BasePrice = null;
        request.CategoryId = null;

        // Act
        var errors = CreateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("basePrice");
        errors.Should().ContainKey("categoryId");
    }

    [Fact]
    public void CreateProductValidator_Should_Reject_65_Char_Sku()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Sku = new string('A', 65);

        // Act
        var errors = CreateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("sku");
    }

    [Fact]
    public void CreateProductValidator_Should_Reject_Negative_BasePrice()
    {
        // Arrange
        var request = CreateValidRequest();
        request.BasePrice = -50000m;

        // Act
        var errors = CreateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("basePrice");
    }

    [Fact]
    public void CreateProductValidator_Should_Reject_Negative_Variant_Price()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Variants = new List<TechStore.Shared.Requests.CreateProductVariantRequest>
        {
            new() { VariantName = "V1", Sku = "V1-SKU", Price = 1000m },
            new() { VariantName = "V2", Sku = "V2-SKU", Price = -1m }
        };

        // Act
        var errors = CreateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("variants[1].price");
    }

    [Fact]
    public void CreateProductValidator_Should_Reject_Missing_Variant_Price()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Variants = new List<TechStore.Shared.Requests.CreateProductVariantRequest>
        {
            new() { VariantName = "V1", Sku = "V1-SKU", Price = null }
        };

        // Act
        var errors = CreateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("variants[0].price");
    }

    [Fact]
    public void CreateProductValidator_Should_Reject_Variant_Sku_Equal_To_Product_Sku()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Sku = "IP15PM-256";
        request.Variants = new List<TechStore.Shared.Requests.CreateProductVariantRequest>
        {
            new() { VariantName = "V1", Sku = "ip15pm-256", Price = 30000000m }
        };

        // Act
        var errors = CreateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("variants[0].sku");
    }

    [Fact]
    public void CreateProductValidator_Should_Report_Multiple_Errors_Together()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Name = "";
        request.CostPrice = -500m;

        // Act
        var errors = CreateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("name");
        errors.Should().ContainKey("costPrice");
    }
}
