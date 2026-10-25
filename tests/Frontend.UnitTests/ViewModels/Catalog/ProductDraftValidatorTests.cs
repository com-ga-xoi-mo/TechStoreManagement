using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.ViewModels.Catalog;
using Xunit;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class ProductDraftValidatorTests
{
    private static ProductDraft ValidDraft() => new(
        CategoryId: Guid.NewGuid(),
        Name: "Chuột Logitech G Pro X",
        Sku: "LOGI-GPX",
        Brand: "Logitech",
        BasePrice: "3.200.000",
        CostPrice: "2.500.000",
        Barcode: "BARCODE-1",
        Description: "Chuột gaming không dây",
        IsSerialTracked: true,
        Specs: [new SpecDraft("dpi", SpecValueKind.Number, "25600")],
        Variants: []);

    [Fact]
    public void Validate_ValidCreate_BuildsCreateProductRequestWithBasePriceAndSpec()
    {
        // Arrange
        var draft = ValidDraft();

        // Act
        var result = ProductDraftValidator.Validate(draft, isCreate: true);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.Create);
        Assert.Null(result.Update);
        Assert.Equal(3200000m, result.Create.BasePrice);
        Assert.Equal(2500000m, result.Create.CostPrice);
        Assert.NotNull(result.Create.Specs);
        Assert.Equal(JsonValueKind.Number, result.Create.Specs["dpi"].ValueKind);
        Assert.Equal("25600", result.Create.Specs["dpi"].GetRawText());
    }

    [Fact]
    public void Validate_EmptyNameNegativeCostAndNewVariantWithoutPrice_ReportsErrorsTogetherAndNoRequest()
    {
        // Arrange
        var draft = ValidDraft() with
        {
            Name = "",
            CostPrice = "-1",
            Variants = [new VariantDraft(null, "White", "SKU-W", "", "", true, [])]
        };

        // Act
        var result = ProductDraftValidator.Validate(draft, isCreate: true);

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(result.Create);
        Assert.Null(result.Update);
        Assert.Contains("name", result.FieldErrors.Keys);
        Assert.Contains("costPrice", result.FieldErrors.Keys);
        Assert.Contains("variants[0].price", result.FieldErrors.Keys);
    }

    [Fact]
    public void Validate_CreateWithoutSku_ReportsSkuError()
    {
        // Arrange
        var draft = ValidDraft() with { Sku = "" };

        // Act
        var result = ProductDraftValidator.Validate(draft, isCreate: true);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("sku", result.FieldErrors.Keys);
    }

    [Fact]
    public void Validate_UpdateWithoutSku_HasNoSkuError()
    {
        // Arrange
        var draft = ValidDraft() with { Sku = "" };

        // Act
        var result = ProductDraftValidator.Validate(draft, isCreate: false);

        // Assert
        Assert.True(result.IsValid);
        Assert.DoesNotContain("sku", result.FieldErrors.Keys);
        Assert.NotNull(result.Update);
    }

    [Fact]
    public void Validate_NameOver200Chars_ReportsNameError()
    {
        // Arrange
        var draft = ValidDraft() with { Name = new string('A', 201) };

        // Act
        var result = ProductDraftValidator.Validate(draft, isCreate: true);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("name", result.FieldErrors.Keys);
    }

    [Fact]
    public void Validate_Barcode_SentAsNullWhenVariantExists_AndKeptWhenNone()
    {
        // Arrange & Act
        var resultNoVariant = ProductDraftValidator.Validate(
            ValidDraft() with { Barcode = "123456" },
            isCreate: true);

        var resultWithVariant = ProductDraftValidator.Validate(
            ValidDraft() with
            {
                Barcode = "123456",
                Variants = [new VariantDraft(null, "White", "VAR-1", "3.200.000", "V-BAR", true, [])]
            },
            isCreate: true);

        // Assert
        Assert.True(resultNoVariant.IsValid);
        Assert.Equal("123456", resultNoVariant.Create!.Barcode);

        Assert.True(resultWithVariant.IsValid);
        Assert.Null(resultWithVariant.Create!.Barcode);
    }

    [Fact]
    public void Validate_TwoNewVariantsWithSameSkuCaseInsensitive_ReportsErrorOnlyOnSecondVariant()
    {
        // Arrange
        var draft = ValidDraft() with
        {
            Variants =
            [
                new VariantDraft(null, "Black", "ip16-256", "3.200.000", "", true, []),
                new VariantDraft(null, "White", "IP16-256", "3.200.000", "", true, [])
            ]
        };

        // Act
        var result = ProductDraftValidator.Validate(draft, isCreate: true);

        // Assert
        Assert.False(result.IsValid);
        Assert.DoesNotContain("variants[0].sku", result.FieldErrors.Keys);
        Assert.Equal("SKU trùng với một biến thể khác.", result.FieldErrors["variants[1].sku"]);
    }

    [Fact]
    public void Validate_NewVariantSkuEqualToProductSkuOnCreate_ReportsErrorOnVariant0()
    {
        // Arrange
        var draft = ValidDraft() with
        {
            Sku = "IP16-256",
            Variants =
            [
                new VariantDraft(null, "Black", "ip16-256", "3.200.000", "", true, [])
            ]
        };

        // Act
        var result = ProductDraftValidator.Validate(draft, isCreate: true);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("variants[0].sku", result.FieldErrors.Keys);
    }

    [Fact]
    public void Validate_SavedVariantWithId_SendsSkuNullIdAndIsActiveFalseWhenDraftSaysSo()
    {
        // Arrange
        var savedId = Guid.NewGuid();
        var draft = ValidDraft() with
        {
            Variants =
            [
                new VariantDraft(savedId, "Black", "OLD-SKU", "3.200.000", "BAR-1", false, [])
            ]
        };

        // Act
        var result = ProductDraftValidator.Validate(draft, isCreate: false);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.Update);
        var variant = Assert.Single(result.Update.Variants!);
        Assert.Equal(savedId, variant.Id);
        Assert.Null(variant.Sku);
        Assert.False(variant.IsActive);
    }

    [Fact]
    public void Validate_BadVariantSpecKey_ReportsVariantSpecsAndRowError()
    {
        // Arrange
        var draft = ValidDraft() with
        {
            Variants =
            [
                new VariantDraft(null, "Black", "VAR-SKU", "3.200.000", "", true,
                    [new SpecDraft("BAD KEY", SpecValueKind.Text, "val")])
            ]
        };

        // Act
        var result = ProductDraftValidator.Validate(draft, isCreate: true);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("variants[0].specs", result.FieldErrors.Keys);
        Assert.Equal("Tên thông số phải dùng snake_case, ví dụ ram_gb.", result.VariantSpecRowErrors[0][0]);
    }
}
