using System.Text.Json;
using FluentAssertions;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class UpdateProductValidatorTests
{
    private static UpdateProductRequest ValidRequest() => new()
    {
        CategoryId = Guid.NewGuid(),
        Name = "MacBook Air M3",
        Brand = "Apple",
        BasePrice = 27990000m,
        CostPrice = 24000000m,
        IsSerialTracked = true
    };

    [Fact]
    public void Validate_ValidRequest_ReturnsNoErrors()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NullRequest_ReturnsRequestError()
    {
        // Act
        var errors = UpdateProductValidator.Validate(null);

        // Assert
        errors.Should().ContainKey("request");
    }

    [Fact]
    public void Validate_MissingIsSerialTracked_ReturnsError()
    {
        // Arrange
        var request = ValidRequest();
        request.IsSerialTracked = null;

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("isSerialTracked");
    }

    [Fact]
    public void Validate_EmptyNameAndNegativeCostPrice_ReportsBoth()
    {
        // Arrange
        var request = ValidRequest();
        request.Name = "";
        request.CostPrice = -1m;

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("name");
        errors.Should().ContainKey("costPrice");
    }

    [Fact]
    public void Validate_MissingCategoryAndBasePrice_ReportsBoth()
    {
        // Arrange
        var request = ValidRequest();
        request.CategoryId = null;
        request.BasePrice = null;

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("categoryId");
        errors.Should().ContainKey("basePrice");
    }

    [Fact]
    public void Validate_InvalidSpecKey_ReturnsSpecsError()
    {
        // Arrange
        var request = ValidRequest();
        request.Specs = new Dictionary<string, JsonElement>
        {
            ["RAM GB"] = JsonDocument.Parse("16").RootElement.Clone()
        };

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("specs");
    }

    [Fact]
    public void Validate_NewVariantWithoutSku_ReturnsSkuError()
    {
        // Arrange
        var request = ValidRequest();
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = null,
                VariantName = "Midnight",
                Sku = null,
                Price = 27990000m
            }
        ];

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("variants[0].sku");
    }

    [Fact]
    public void Validate_ExistingVariantWithoutSku_Passes()
    {
        // Arrange
        var request = ValidRequest();
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = Guid.NewGuid(),
                VariantName = "Midnight",
                Sku = null,
                Price = 27990000m
            }
        ];

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyVariantId_ReturnsIdError()
    {
        // Arrange
        var request = ValidRequest();
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = Guid.Empty,
                VariantName = "Midnight",
                Price = 27990000m
            }
        ];

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("variants[0].id");
    }

    [Fact]
    public void Validate_DuplicateVariantId_ReturnsErrorOnSecondItem()
    {
        // Arrange
        var sharedId = Guid.NewGuid();
        var request = ValidRequest();
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = sharedId,
                VariantName = "Midnight",
                Price = 27990000m
            },
            new UpdateProductVariantRequest
            {
                Id = sharedId,
                VariantName = "Starlight",
                Price = 27990000m
            }
        ];

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().NotContainKey("variants[0].id");
        errors.Should().ContainKey("variants[1].id");
    }

    [Fact]
    public void Validate_DuplicateNewVariantSkuIgnoringCase_ReturnsErrorOnSecondItem()
    {
        // Arrange
        var request = ValidRequest();
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = null,
                VariantName = "Midnight",
                Sku = "ABC-1",
                Price = 27990000m
            },
            new UpdateProductVariantRequest
            {
                Id = null,
                VariantName = "Starlight",
                Sku = "abc-1",
                Price = 27990000m
            }
        ];

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().NotContainKey("variants[0].sku");
        errors.Should().ContainKey("variants[1].sku");
    }

    [Fact]
    public void Validate_NegativeVariantPrice_ReturnsPriceError()
    {
        // Arrange
        var request = ValidRequest();
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                VariantName = "Midnight",
                Sku = "MBA-M3-MD",
                Price = -100m
            }
        ];

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("variants[0].price");
    }

    [Fact]
    public void Validate_NestedVariantSpecs_ReturnsSpecsError()
    {
        // Arrange
        var request = ValidRequest();
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                VariantName = "Midnight",
                Sku = "MBA-M3-MD",
                Price = 27990000m,
                Specs = new Dictionary<string, JsonElement>
                {
                    ["a"] = JsonDocument.Parse("{\"a\":1}").RootElement.Clone()
                }
            }
        ];

        // Act
        var errors = UpdateProductValidator.Validate(request);

        // Assert
        errors.Should().ContainKey("variants[0].specs");
    }
}
