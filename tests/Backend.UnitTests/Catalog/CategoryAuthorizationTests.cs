using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Catalog.Services;

namespace TechStore.UnitTests.Catalog;

public class CategoryAuthorizationTests
{
    [Theory]
    [InlineData("missing", "Admin", 401)]
    [InlineData("malformed", "Admin", 401)]
    [InlineData("expired", "Admin", 401)]
    [InlineData("signature", "Admin", 401)]
    [InlineData("issuer", "Admin", 401)]
    [InlineData("audience", "Admin", 401)]
    [InlineData("valid", "Manager", 403)]
    [InlineData("valid", "SalesStaff", 403)]
    [InlineData("valid", "WarehouseStaff", 403)]
    [InlineData("valid", "Admin", 200)]
    public async Task Update_Should_Enforce_Jwt_And_Admin_Before_Service(string kind, string role, int expected)
    {
        // Arrange
        var service = new FakeCategoryService();
        await using var host = new CategoryApiHost(services => services.AddSingleton<ICategoryService>(service));
        await host.StartAsync();
        if (kind != "missing") host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", kind == "malformed" ? "not-a-token" : host.Token(role, kind));

        // Act
        using var response = await host.Client.PutAsJsonAsync($"/api/categories/{Guid.NewGuid()}",
            new { Name = "Updated", IsActive = true });

        // Assert
        response.StatusCode.Should().Be((HttpStatusCode)expected);
        service.UpdateCount.Should().Be(expected == 200 ? 1 : 0);
    }

    [Theory]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:SigningKey", "")]
    [InlineData("Jwt:SigningKey", "short")]
    public async Task Startup_Should_Reject_Invalid_Jwt_Configuration(string setting, string value)
    {
        // Arrange / Act / Assert
        await FluentActions.Awaiting(async () =>
        {
            await using var host = new CategoryApiHost(services => services.AddSingleton<ICategoryService>(new FakeCategoryService()), setting, value);
            await host.StartAsync();
        }).Should().ThrowAsync<InvalidOperationException>().WithMessage("Configure Jwt:Issuer*");
    }
}
