using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using TechStore.Api.Catalog;

namespace TechStore.UnitTests;

public class ProductImagesControllerAuthorizationTests
{
    [Fact]
    public void Controller_RequiresAdminOrManager()
    {
        // Arrange
        var type = typeof(ProductImagesController);
        // Act
        var authorization = type.GetCustomAttribute<AuthorizeAttribute>();
        // Assert
        authorization.Should().NotBeNull();
        authorization!.Roles.Should().Be("Admin,Manager");
        type.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
    }

    [Fact]
    public void Upload_ConsumesMultipartAndLimitsRequestSize()
    {
        // Arrange
        var method = typeof(ProductImagesController).GetMethod(nameof(ProductImagesController.Upload))!;
        // Act
        var consumes = method.GetCustomAttribute<ConsumesAttribute>();
        var limit = (IRequestSizeLimitMetadata)method.GetCustomAttribute<RequestSizeLimitAttribute>()!;
        // Assert
        consumes!.ContentTypes.Should().Contain("multipart/form-data");
        limit.MaxRequestBodySize.Should().Be(6_291_456);
        method.GetCustomAttribute<RequestFormLimitsAttribute>()!.MultipartBodyLengthLimit.Should().Be(6_291_456);
    }

    [Theory]
    [InlineData(nameof(ProductImagesController.Upload))]
    [InlineData(nameof(ProductImagesController.Delete))]
    [InlineData(nameof(ProductImagesController.SaveLayout))]
    public void Action_HasNoAnonymousOverride(string name)
    {
        // Arrange
        var method = typeof(ProductImagesController).GetMethod(name)!;
        // Act
        var anonymous = method.GetCustomAttribute<AllowAnonymousAttribute>();
        // Assert
        anonymous.Should().BeNull();
    }
}
