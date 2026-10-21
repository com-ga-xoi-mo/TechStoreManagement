using System.Text.Json;
using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Data;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class SpecDefinitionTests
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    private static Dictionary<string, JsonElement> Specs(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static (FakeProductRepository Products, FakeCategoryRepository Categories, FakeUnitOfWork UnitOfWork,
        FakeSpecDefinitionRepository Definitions, ProductService Service, Category Category) CreateContext()
    {
        var products = new FakeProductRepository();
        var categories = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var definitions = new FakeSpecDefinitionRepository()
            .With("ram_gb", "RAM", "GB", SpecValueKinds.Number)
            .With("cpu", "Vi xử lý (CPU)", null, SpecValueKinds.String);
        var category = ProductCommandTestData.NewActiveCategory();
        categories.Categories[category.Id] = category;
        return (products, categories, unitOfWork, definitions, new ProductService(products, categories, unitOfWork, definitions), category);
    }

    private static CreateProductRequest NewCreateRequest(Guid categoryId, string specsJson) => new()
    {
        CategoryId = categoryId,
        Name = "Màn hình Gaming",
        Sku = "MON-144",
        Brand = "LG",
        BasePrice = 5000000m,
        CostPrice = 4000000m,
        Specs = Specs(specsJson)
    };

    private static SpecLabelRequest Label(string label, string? unit, string kind) =>
        new() { Label = label, Unit = unit, ValueKind = kind };

    [Fact]
    public void SpecLabelValidator_ValidEntry_HasNoErrors()
    {
        // Arrange
        var errors = new Dictionary<string, List<string>>();
        var labels = new Dictionary<string, SpecLabelRequest> { ["tan_so_quet_hz"] = Label("Tần số quét", "Hz", "number") };

        // Act
        SpecLabelValidator.Validate(labels, [Specs("""{"tan_so_quet_hz": 144}""")], errors);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{"other": 1}""", "tan_so_quet_hz", "Tần số quét", "number")]
    [InlineData("""{"tan_so_quet_hz": 144}""", "tan_so_quet_hz", "", "number")]
    [InlineData("""{"tan_so_quet_hz": 144}""", "tan_so_quet_hz", "Tần số quét", "string")]
    [InlineData("""{"tan_so_quet_hz": 144}""", "tan_so_quet_hz", "Tần số quét", "date")]
    [InlineData("""{"Bad Key": 144}""", "Bad Key", "Tần số quét", "number")]
    public void SpecLabelValidator_InvalidEntry_ReportsTheKey(string specsJson, string key, string label, string kind)
    {
        // Arrange
        var errors = new Dictionary<string, List<string>>();
        var labels = new Dictionary<string, SpecLabelRequest> { [key] = Label(label, null, kind) };

        // Act
        SpecLabelValidator.Validate(labels, [Specs(specsJson)], errors);

        // Assert
        errors.Should().ContainKey($"specLabels.{key}");
    }

    [Fact]
    public void SpecLabelValidator_SameNameTwice_ReportsTheSecondKey()
    {
        // Arrange
        var errors = new Dictionary<string, List<string>>();
        var labels = new Dictionary<string, SpecLabelRequest>
        {
            ["a_key"] = Label("Tên chung", null, "number"),
            ["b_key"] = Label("tên chung", null, "number")
        };

        // Act
        SpecLabelValidator.Validate(labels, [Specs("""{"a_key": 1, "b_key": 2}""")], errors);

        // Assert
        errors.Should().ContainKey("specLabels.b_key").And.NotContainKey("specLabels.a_key");
    }

    [Fact]
    public async Task CreateAsync_NewSpecName_IsSavedWithTheProduct()
    {
        // Arrange
        var (products, _, unitOfWork, definitions, service, category) = CreateContext();
        var request = NewCreateRequest(category.Id, """{"tan_so_quet_hz": 144, "ram_gb": 16}""");
        request.SpecLabels = new()
        {
            ["tan_so_quet_hz"] = Label(" Tần số quét ", " Hz ", "number"),
            ["ram_gb"] = Label("Bộ nhớ RAM", "GB", "number")
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        unitOfWork.SaveChangesCount.Should().Be(1);
        products.AddedProducts.Should().ContainSingle();
        var added = definitions.Added.Should().ContainSingle().Subject;
        added.Key.Should().Be("tan_so_quet_hz");
        added.Label.Should().Be("Tần số quét");
        added.Unit.Should().Be("Hz");
        added.ValueKind.Should().Be("number");
    }

    [Fact]
    public async Task CreateAsync_NameUsedByAnotherKey_IsRejectedAndNothingIsSaved()
    {
        // Arrange
        var (products, _, unitOfWork, definitions, service, category) = CreateContext();
        var request = NewCreateRequest(category.Id, """{"ram_size": 16}""");
        request.SpecLabels = new() { ["ram_size"] = Label("ram", "GB", "number") };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("specLabels.ram_size");
        products.AddedProducts.Should().BeEmpty();
        definitions.Added.Should().BeEmpty();
        unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_ValueOfAnotherKindThanTheDefinition_IsRejected()
    {
        // Arrange
        var (_, _, unitOfWork, _, service, category) = CreateContext();
        var request = NewCreateRequest(category.Id, """{"ram_gb": "16 GB"}""");
        request.Variants =
        [
            new CreateProductVariantRequest { VariantName = "Đen", Sku = "MON-144-BK", Price = 5000000m, Specs = Specs("""{"cpu": 3}""") }
        ];

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("specs").And.ContainKey("variants[0].specs");
        unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_SpecNameRace_ReturnsTheSpecNameConflict()
    {
        // Arrange
        var (_, _, unitOfWork, _, service, category) = CreateContext();
        unitOfWork.ExceptionToThrowOnSave = new DuplicateKeyException("PK_spec_definitions");
        var request = NewCreateRequest(category.Id, """{"tan_so_quet_hz": 144}""");
        request.SpecLabels = new() { ["tan_so_quet_hz"] = Label("Tần số quét", "Hz", "number") };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        result.IsConflict.Should().BeTrue();
        result.ConflictMessage.Should().Contain("spec name");
    }

    [Fact]
    public async Task UpdateAsync_NewSpecName_IsStagedInsideTheTransaction()
    {
        // Arrange
        var (products, _, unitOfWork, definitions, service, category) = CreateContext();
        var product = ProductCommandTestData.NewProduct(category.Id);
        products.Products[product.Id] = product;
        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Specs = Specs("""{"cpu": "Apple M3", "ram_gb": 8, "fan_rpm": 2000}""");
        request.SpecLabels = new() { ["fan_rpm"] = Label("Tốc độ quạt", "rpm", "number") };

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        definitions.Added.Should().ContainSingle(d => d.Key == "fan_rpm" && d.Label == "Tốc độ quạt");
        unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_ValueOfAnotherKind_RollsBackWithA400()
    {
        // Arrange
        var (products, _, unitOfWork, definitions, service, category) = CreateContext();
        var product = ProductCommandTestData.NewProduct(category.Id);
        products.Products[product.Id] = product;
        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Specs = Specs("""{"cpu": "Apple M3", "ram_gb": true}""");

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("specs");
        unitOfWork.RollbackCount.Should().Be(1);
        unitOfWork.SaveChangesCount.Should().Be(0);
        definitions.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSpecDefinitionsAsync_ReturnsAllDefinitionsOrderedByLabel()
    {
        // Arrange
        var (_, _, _, _, service, _) = CreateContext();

        // Act
        var definitions = await service.GetSpecDefinitionsAsync();

        // Assert
        definitions.Select(d => d.Key).Should().Equal("ram_gb", "cpu");
        definitions[0].Unit.Should().Be("GB");
        definitions[0].ValueKind.Should().Be("number");
    }
}
