using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechStore.Api.Catalog;
using Xunit;

namespace TechStore.UnitTests;

public class ProductsControllerAuthorizationTests
{
    [Theory]
    [InlineData(nameof(ProductsController.Create))]
    [InlineData(nameof(ProductsController.Update))]
    [InlineData(nameof(ProductsController.SetStatus))]
    [InlineData(nameof(ProductsController.Delete))]
    public void WriteActions_RequireAdminOrManager(string actionName)
    {
        // Arrange & Act
        var method = typeof(ProductsController).GetMethod(actionName);
        method.Should().NotBeNull();

        var authAttr = method!.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        authAttr.Should().NotBeNull();
        authAttr!.Roles.Should().Be("Admin,Manager");
    }

    [Theory]
    [InlineData(nameof(ProductsController.Search))]
    [InlineData(nameof(ProductsController.GetById))]
    [InlineData(nameof(ProductsController.GetSpecKeys))]
    [InlineData(nameof(ProductsController.GetSpecDefinitions))]
    [InlineData(nameof(ProductsController.GetBrands))]
    public void ReadActions_AreNotProtected(string actionName)
    {
        // Arrange & Act
        var method = typeof(ProductsController).GetMethod(actionName);
        method.Should().NotBeNull();

        var authAttr = method!.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        authAttr.Should().BeNull();
    }

    [Fact]
    public void Controller_HasNoClassLevelAuthorize()
    {
        // Act
        var authAttr = typeof(ProductsController).GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        authAttr.Should().BeNull();
    }

    [Fact]
    public void Routes_AreAsSpecified()
    {
        // Arrange & Act
        var updateMethod = typeof(ProductsController).GetMethod(nameof(ProductsController.Update));
        var setStatusMethod = typeof(ProductsController).GetMethod(nameof(ProductsController.SetStatus));
        var deleteMethod = typeof(ProductsController).GetMethod(nameof(ProductsController.Delete));

        var putAttr = updateMethod!.GetCustomAttribute<HttpPutAttribute>();
        var patchAttr = setStatusMethod!.GetCustomAttribute<HttpPatchAttribute>();
        var deleteAttr = deleteMethod!.GetCustomAttribute<HttpDeleteAttribute>();

        // Assert
        putAttr.Should().NotBeNull();
        putAttr!.Template.Should().Be("{id:guid}");

        patchAttr.Should().NotBeNull();
        patchAttr!.Template.Should().Be("{id:guid}/status");

        deleteAttr.Should().NotBeNull();
        deleteAttr!.Template.Should().Be("{id:guid}");
    }
}
