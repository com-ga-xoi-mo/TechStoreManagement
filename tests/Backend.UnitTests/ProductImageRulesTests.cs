using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public class ProductImageRulesTests
{
    [Fact]
    public void Inspect_JpegSignature_ReturnsJpeg()
    {
        // Arrange
        var bytes = Convert.FromHexString("FFD8FF");
        // Act
        var info = ProductImageFileInspector.Inspect(bytes, out var error);
        // Assert
        info.Should().Be(new ProductImageFileInfo("image/jpeg", ".jpg"));
        error.Should().BeNull();
    }

    [Fact]
    public void Inspect_PngSignature_ReturnsPng()
    {
        // Arrange
        var bytes = Convert.FromHexString("89504E470D0A1A0A");
        // Act
        var info = ProductImageFileInspector.Inspect(bytes, out var error);
        // Assert
        info.Should().Be(new ProductImageFileInfo("image/png", ".png"));
        error.Should().BeNull();
    }

    [Fact]
    public void Inspect_WebpSignature_ReturnsWebp()
    {
        // Arrange
        var bytes = Convert.FromHexString("524946460000000057454250");
        // Act
        var info = ProductImageFileInspector.Inspect(bytes, out var error);
        // Assert
        info.Should().Be(new ProductImageFileInfo("image/webp", ".webp"));
        error.Should().BeNull();
    }

    [Fact]
    public void Inspect_TextNamedJpg_ReturnsTypeError()
    {
        // Arrange
        byte[] bytes = "hello"u8.ToArray();
        // Act
        var info = ProductImageFileInspector.Inspect(bytes, out var error);
        // Assert
        info.Should().BeNull();
        error.Should().Be("Only JPEG, PNG and WebP images are allowed.");
    }

    [Fact]
    public void Inspect_Empty_ReturnsEmptyError()
    {
        // Arrange
        byte[] bytes = [];
        // Act
        var info = ProductImageFileInspector.Inspect(bytes, out var error);
        // Assert
        info.Should().BeNull();
        error.Should().Be("The image file is empty.");
    }

    [Fact]
    public void Inspect_OverLimit_ReturnsSizeError()
    {
        // Arrange
        var bytes = new byte[5242881];
        Convert.FromHexString("89504E470D0A1A0A").CopyTo(bytes, 0);
        // Act
        var info = ProductImageFileInspector.Inspect(bytes, out var error);
        // Assert
        (info != null).Should().Be(false);
        error.Should().Be("The image file cannot exceed 5 MB.");
    }

    [Fact]
    public void Inspect_ExactlyLimit_Passes()
    {
        // Arrange
        var bytes = new byte[5242880];
        Convert.FromHexString("89504E470D0A1A0A").CopyTo(bytes, 0);
        // Act
        var info = ProductImageFileInspector.Inspect(bytes, out var error);
        // Assert
        (info != null).Should().Be(true);
        error.Should().Be(null);
    }

    [Fact]
    public void UrlOf_SharedAfterVariantImage_ReturnsShared()
    {
        // Arrange
        ProductImage[] images = [new() { VariantId = Guid.NewGuid(), Url = "variant", SortOrder = 0 }, new() { Url = "shared", SortOrder = 1 }];
        // Act
        var url = ProductCoverImage.UrlOf(images.Reverse());
        // Assert
        url.Should().Be("shared");
    }

    [Fact]
    public void UrlOf_OnlyVariantImages_ReturnsFirst()
    {
        // Arrange
        ProductImage[] images = [new() { VariantId = Guid.NewGuid(), Url = "second", SortOrder = 1 }, new() { VariantId = Guid.NewGuid(), Url = "first", SortOrder = 0 }];
        // Act
        var url = ProductCoverImage.UrlOf(images);
        // Assert
        url.Should().Be("first");
    }

    [Fact]
    public void UrlOf_NoImages_ReturnsNull()
    {
        // Arrange
        ProductImage[] images = [];
        // Act
        var url = ProductCoverImage.UrlOf(images);
        // Assert
        url.Should().BeNull();
    }

    [Fact]
    public void Renumber_Gaps_MakesZeroBased()
    {
        // Arrange
        ProductImage[] images = [new() { SortOrder = 4 }, new() { SortOrder = 9 }];
        // Act
        ProductCoverImage.Renumber(images);
        // Assert
        images.Select(i => i.SortOrder).Should().Equal(0, 1);
    }

    [Fact]
    public void Validate_NullItems_ReturnsItemsError()
    {
        // Arrange
        var request = new UpdateProductImagesLayoutRequest();
        // Act
        var errors = ProductImageLayoutValidator.Validate(request);
        // Assert
        errors["items"].Should().Equal("items is required.");
        ProductImageLayoutValidator.Validate(null).Should().ContainKey("items");
    }

    [Fact]
    public void Validate_DuplicateId_ReturnsErrorOnSecond()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateProductImagesLayoutRequest { Items = [new() { Id = id }, new() { Id = id }] };
        // Act
        var errors = ProductImageLayoutValidator.Validate(request);
        // Assert
        errors.Should().ContainSingle().Which.Key.Should().Be("items[1].id");
        errors["items[1].id"].Should().Equal($"Image '{id}' is listed more than once.");
    }

    [Fact]
    public void Validate_EmptyVariantId_ReturnsVariantError()
    {
        // Arrange
        var request = new UpdateProductImagesLayoutRequest { Items = [new() { Id = Guid.NewGuid(), VariantId = Guid.Empty }] };
        // Act
        var errors = ProductImageLayoutValidator.Validate(request);
        // Assert
        errors["items[0].variantId"].Should().Equal("Variant id cannot be empty.");
    }

    [Fact]
    public void Validate_ValidItems_ReturnsNoErrors()
    {
        // Arrange
        var request = new UpdateProductImagesLayoutRequest { Items = [new() { Id = Guid.NewGuid() }] };
        // Act
        var errors = ProductImageLayoutValidator.Validate(request);
        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Validate_MissingId_ReturnsIndexedError(string? id)
    {
        // Arrange
        var request = new UpdateProductImagesLayoutRequest { Items = [new() { Id = id == null ? null : Guid.Parse(id) }] };
        // Act
        var errors = ProductImageLayoutValidator.Validate(request);
        // Assert
        errors["items[0].id"].Should().Equal("Image id is required.");
    }
}
