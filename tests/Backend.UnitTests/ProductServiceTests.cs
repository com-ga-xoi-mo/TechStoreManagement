using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Data;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;
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

    // -------------------------------------------------------------
    // Task 5.2: GetByIdAsync Tests
    // -------------------------------------------------------------
    [Fact]
    public async Task GetByIdAsync_Should_Return_NotFound_When_Product_DoesNotExist()
    {
        // Arrange
        var productRepo = new FakeProductRepository();
        var categoryRepo = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(productRepo, categoryRepo, unitOfWork);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Found.Should().BeFalse();
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Product_With_Sorted_Variants_And_Merged_EffectiveSpecs()
    {
        // Arrange
        var productRepo = new FakeProductRepository();
        var categoryRepo = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(productRepo, categoryRepo, unitOfWork);

        var productId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            Name = "MacBook Pro",
            Sku = "MBP-01",
            Specs = "{\"cpu\": \"Apple M3\", \"color\": \"Silver\"}",
            Category = new Category { Name = "Laptops" }
        };

        var variantHighPrice = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            VariantName = "V2",
            Sku = "MBP-V2",
            Price = 40000000m,
            Specs = "{\"color\": \"Space Gray\"}"
        };

        var variantLowPrice = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            VariantName = "V1",
            Sku = "MBP-V1",
            Price = 35000000m,
            Specs = "{\"ram_gb\": 16}"
        };

        product.Variants.Add(variantHighPrice);
        product.Variants.Add(variantLowPrice);
        productRepo.Add(product);

        // Act
        var result = await service.GetByIdAsync(productId);

        // Assert
        result.Found.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Variants.Should().HaveCount(2);

        // Ordered by price ascending
        result.Data.Variants[0].Price.Should().Be(35000000m);
        result.Data.Variants[1].Price.Should().Be(40000000m);

        // Effective specs merged and overridden
        result.Data.Variants[1].EffectiveSpecs["cpu"].GetString().Should().Be("Apple M3");
        result.Data.Variants[1].EffectiveSpecs["color"].GetString().Should().Be("Space Gray");
    }

    // -------------------------------------------------------------
    // Task 5.3: CreateAsync Tests
    // -------------------------------------------------------------
    [Fact]
    public async Task CreateAsync_Should_Succeed_With_One_Save_And_IsActive_True()
    {
        // Arrange
        var productRepo = new FakeProductRepository();
        var categoryRepo = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(productRepo, categoryRepo, unitOfWork);

        var categoryId = Guid.NewGuid();
        categoryRepo.Categories[categoryId] = new Category { Id = categoryId, Name = "Phones" };

        var request = CreateValidRequest();
        request.CategoryId = categoryId;
        request.Variants = new List<CreateProductVariantRequest>
        {
            new() { VariantName = "V1", Sku = "V1-SKU", Price = 30000000m }
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        unitOfWork.SaveChangesCount.Should().Be(1);
        productRepo.AddedProducts.Should().HaveCount(1);
        var added = productRepo.AddedProducts[0];
        added.IsActive.Should().BeTrue();
        added.Variants.Should().HaveCount(1);
        added.Variants.First().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_Should_Fail_When_Category_NotFound_And_Zero_Saves()
    {
        // Arrange
        var productRepo = new FakeProductRepository();
        var categoryRepo = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(productRepo, categoryRepo, unitOfWork);

        var request = CreateValidRequest();
        request.CategoryId = Guid.NewGuid(); // not in categoryRepo

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("categoryId");
        unitOfWork.SaveChangesCount.Should().Be(0);
        productRepo.AddedProducts.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_Should_Fail_When_Sku_Exists_And_Zero_Saves()
    {
        // Arrange
        var productRepo = new FakeProductRepository();
        var categoryRepo = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(productRepo, categoryRepo, unitOfWork);

        var categoryId = Guid.NewGuid();
        categoryRepo.Categories[categoryId] = new Category { Id = categoryId, Name = "Phones" };

        var request = CreateValidRequest();
        request.CategoryId = categoryId;
        request.Sku = "EXISTING-SKU";
        productRepo.ExistingSkus.Add("EXISTING-SKU");

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_Should_Return_Conflict_When_UnitOfWork_Throws_DuplicateKeyException()
    {
        // Arrange
        var productRepo = new FakeProductRepository();
        var categoryRepo = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork
        {
            ExceptionToThrowOnSave = new DuplicateKeyException("uq_products_sku")
        };
        var service = new ProductService(productRepo, categoryRepo, unitOfWork);

        var categoryId = Guid.NewGuid();
        categoryRepo.Categories[categoryId] = new Category { Id = categoryId, Name = "Phones" };

        var request = CreateValidRequest();
        request.CategoryId = categoryId;

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_Should_Return_ValidationErrors_Without_Calling_Repository_When_Invalid()
    {
        // Arrange
        var productRepo = new FakeProductRepository();
        var categoryRepo = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(productRepo, categoryRepo, unitOfWork);

        var request = CreateValidRequest();
        request.Name = ""; // invalid

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("name");
        productRepo.AddedProducts.Should().BeEmpty();
        unitOfWork.SaveChangesCount.Should().Be(0);
    }

    // -------------------------------------------------------------
    // Task 5.4: SearchAsync Tests
    // -------------------------------------------------------------
    [Theory]
    [InlineData("RAM:16", null, null, "spec")]
    [InlineData(null, 30000000.0, 20000000.0, "minPrice")]
    [InlineData(null, null, null, "sort")]
    public async Task SearchAsync_Should_Fail_Validation_And_Not_Call_Repository(
        string? spec, double? minPrice, double? maxPrice, string expectedErrorKey)
    {
        // Arrange
        var productRepo = new FakeProductRepository();
        var categoryRepo = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(productRepo, categoryRepo, unitOfWork);

        var request = new ProductSearchRequest
        {
            MinPrice = (decimal?)minPrice,
            MaxPrice = (decimal?)maxPrice
        };
        if (spec != null) request.Spec = new List<string> { spec };
        if (expectedErrorKey == "sort") request.Sort = "invalid_field";

        // Act
        var result = await service.SearchAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey(expectedErrorKey);
        productRepo.SearchCallCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_Should_Pass_Mapped_Criteria_To_Repository()
    {
        // Arrange
        var productRepo = new FakeProductRepository();
        var categoryRepo = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(productRepo, categoryRepo, unitOfWork);

        var categoryId = Guid.NewGuid();
        var request = new ProductSearchRequest
        {
            Q = "  iPhone  ",
            Brand = "  Apple  ",
            CategoryId = categoryId,
            IsActive = true,
            MinPrice = 10000000m,
            MaxPrice = 30000000m,
            Spec = new List<string> { "ram_gb:16" },
            Sort = "price:asc",
            Page = 0,
            PageSize = 500
        };

        // Act
        var result = await service.SearchAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        productRepo.SearchCallCount.Should().Be(1);
        var criteria = productRepo.LastCriteria!;
        criteria.Keyword.Should().Be("iPhone");
        criteria.Brand.Should().Be("Apple");
        criteria.CategoryId.Should().Be(categoryId);
        criteria.IsActive.Should().BeTrue();
        criteria.MinPrice.Should().Be(10000000m);
        criteria.MaxPrice.Should().Be(30000000m);
        criteria.SortField.Should().Be(ProductSortField.Price);
        criteria.Descending.Should().BeFalse();
        criteria.Page.Should().Be(1);
        criteria.PageSize.Should().Be(100);
        criteria.SpecFilterJson.Should().Contain("\"ram_gb\":16");
    }

    [Fact]
    public async Task SearchAsync_Should_Map_Summary_PriceRange_Correctly()
    {
        // Arrange
        var productRepo = new FakeProductRepository();
        var categoryRepo = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(productRepo, categoryRepo, unitOfWork);

        var productWithVariants = new Product
        {
            Id = Guid.NewGuid(),
            Name = "P1",
            BasePrice = 10000000m,
            Category = new Category { Name = "C1" },
            Variants = new List<ProductVariant>
            {
                new() { Price = 8000000m },
                new() { Price = 12000000m }
            }
        };

        var productWithoutVariants = new Product
        {
            Id = Guid.NewGuid(),
            Name = "P2",
            BasePrice = 5000000m,
            Category = new Category { Name = "C2" },
            Variants = new List<ProductVariant>()
        };

        productRepo.CustomSearchResult = (new List<Product> { productWithVariants, productWithoutVariants }, 2);

        // Act
        var result = await service.SearchAsync(new ProductSearchRequest());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);

        // P1 with variants: min / max
        result.Data.Items[0].PriceFrom.Should().Be(8000000m);
        result.Data.Items[0].PriceTo.Should().Be(12000000m);
        result.Data.Items[0].VariantCount.Should().Be(2);

        // P2 without variants: base price for both
        result.Data.Items[1].PriceFrom.Should().Be(5000000m);
        result.Data.Items[1].PriceTo.Should().Be(5000000m);
        result.Data.Items[1].VariantCount.Should().Be(0);
    }
}
