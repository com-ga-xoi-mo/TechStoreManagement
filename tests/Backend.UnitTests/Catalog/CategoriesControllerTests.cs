using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Api.Catalog;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests.Catalog;

public class CategoriesControllerTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task UpdateAsync_Should_Map_Result_And_Forward_Request(int status)
    {
        // Arrange
        var id = Guid.NewGuid();
        var dto = new CategoryDto { Id = id, Name = "Updated", Slug = "updated" };
        var service = new FakeCategoryService
        {
            Result = status switch
            {
                200 => UpdateCategoryResult.Success(dto),
                400 => UpdateCategoryResult.ValidationFailed(new() { ["parentId"] = ["Invalid parent."] }),
                404 => UpdateCategoryResult.NotFound(),
                _ => UpdateCategoryResult.Conflict("The category slug is already in use.")
            }
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var controller = new CategoriesController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = provider } }
        };
        using var cancellation = new CancellationTokenSource();
        var request = new UpdateCategoryRequest { Name = "Updated", IsActive = true };

        // Act
        var response = (ObjectResult)await controller.UpdateAsync(id, request, cancellation.Token);

        // Assert
        response.StatusCode.Should().Be(status);
        service.LastId.Should().Be(id);
        service.LastRequest.Should().BeSameAs(request);
        service.LastToken.Should().Be(cancellation.Token);
        if (status == 200) response.Value.Should().BeSameAs(dto);
        else
        {
            var problem = response.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
            problem.Status.Should().Be(status);
            if (status == 400)
                ((ValidationProblemDetails)problem).Errors.Should().ContainKey("parentId");
            if (status == 409) problem.Detail.Should().Be(service.Result.ConflictReason);
        }
    }

    [Theory]
    [InlineData(204)]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task DeleteAsync_Should_Map_Result_And_Forward_Request(int status)
    {
        // Arrange
        var id = Guid.NewGuid();
        var service = new FakeCategoryService
        {
            DeleteResult = status switch
            {
                204 => DeleteCategoryResult.Success(),
                400 => DeleteCategoryResult.ValidationFailed(new()
                {
                    ["category"] = ["Cannot delete category that contains subcategories. Move or delete subcategories first."]
                }),
                404 => DeleteCategoryResult.NotFound(),
                _ => DeleteCategoryResult.Conflict("Cannot delete category that contains associated products. Reassign or delete associated products first.")
            }
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var controller = new CategoriesController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = provider } }
        };
        using var cancellation = new CancellationTokenSource();

        // Act
        var response = await controller.DeleteAsync(id, cancellation.Token);

        // Assert
        response.Should().NotBeNull();
        service.DeleteCount.Should().Be(1);
        service.LastDeleteId.Should().Be(id);
        service.LastDeleteToken.Should().Be(cancellation.Token);
        if (status == 204)
        {
            response.Should().BeOfType<NoContentResult>();
        }
        else
        {
            var objectResult = response.Should().BeAssignableTo<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(status);
            objectResult.Value.Should().BeAssignableTo<ProblemDetails>();
            if (status == 409)
                ((ProblemDetails)objectResult.Value!).Detail.Should().Be(service.DeleteResult.ConflictReason);
        }
    }

    [Fact]
    public async Task DeleteAsync_Should_Require_Admin_Authorization()
    {
        // Arrange
        var service = new FakeCategoryService { DeleteResult = DeleteCategoryResult.Success() };
        await using var host = new CategoryApiHost(services => services.AddSingleton<ICategoryService>(service));
        await host.StartAsync();
        var id = Guid.NewGuid();

        // Act / Assert: no token
        var unauthenticated = await host.Client.DeleteAsync($"/api/categories/{id}");
        unauthenticated.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        service.DeleteCount.Should().Be(0);

        // Act / Assert: invalid token
        host.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid");
        var invalid = await host.Client.DeleteAsync($"/api/categories/{id}");
        invalid.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        service.DeleteCount.Should().Be(0);

        // Act / Assert: non-admin token
        host.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", host.Token("Manager"));
        var forbidden = await host.Client.DeleteAsync($"/api/categories/{id}");
        forbidden.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        service.DeleteCount.Should().Be(0);

        // Act / Assert: Admin token
        host.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", host.Token("Admin"));
        var accepted = await host.Client.DeleteAsync($"/api/categories/{id}");
        accepted.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
        service.DeleteCount.Should().Be(1);
    }
}

public sealed class FakeCategoryService : ICategoryService
{
    public UpdateCategoryResult Result { get; set; } = UpdateCategoryResult.Success(new CategoryDto());
    public DeleteCategoryResult DeleteResult { get; set; } = DeleteCategoryResult.Success();
    public int UpdateCount { get; private set; }
    public int DeleteCount { get; private set; }
    public int TreeCount { get; private set; }
    public Guid LastId { get; private set; }
    public Guid LastDeleteId { get; private set; }
    public UpdateCategoryRequest? LastRequest { get; private set; }
    public CancellationToken LastToken { get; private set; }
    public CancellationToken LastDeleteToken { get; private set; }

    public Task<UpdateCategoryResult> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCount++;
        LastId = id;
        LastRequest = request;
        LastToken = cancellationToken;
        return Task.FromResult(Result);
    }

    public Task<DeleteCategoryResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCount++;
        LastDeleteId = id;
        LastDeleteToken = cancellationToken;
        return Task.FromResult(DeleteResult);
    }

    public Task<CategoryTreeResult> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        TreeCount++;
        return Task.FromResult(new CategoryTreeResult(true, []));
    }

    public Task<CreateCategoryResult> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
