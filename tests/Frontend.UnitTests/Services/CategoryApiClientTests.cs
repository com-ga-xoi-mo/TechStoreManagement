using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Authentication;
using TechStore.Shared.DTOs;

namespace TechStore.Frontend.UnitTests.Services;

public class CategoryApiClientTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Authorization { get; private set; }
        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? RequestBody { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK) { Content = new StringContent("[]") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Authorization = request.Headers.Authorization?.ToString();
            RequestUri = request.RequestUri;
            Method = request.Method;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            LastToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Response;
        }
    }

    private static (CategoryApiClient Client, SessionAccessTokenProvider Tokens, Handler Handler) Create()
    {
        var handler = new Handler();
        var tokens = new SessionAccessTokenProvider();
        tokens.SetAccessToken("test-session");
        return (new(new HttpClient(handler) { BaseAddress = new("https://example.test/") }, tokens), tokens, handler);
    }

    [Fact]
    public async Task GetTree_NestedData_MapsRealFieldsAndLeavesStatisticsUnavailable()
    {
        // Arrange
        var (client, _, handler) = Create();
        var id = Guid.NewGuid();
        handler.Response.Content = JsonContent.Create(new[] { new CategoryTreeNodeDto
        {
            Id = id, Name = "Root", Description = "Root description", ParentId = null,
            SubCategories = [new() { Name = "Hidden", IsActive = false, Description = "Child description", ParentId = id }]
        } });
        // Act
        var nodes = await client.GetTreeAsync();
        // Assert
        nodes.Should().ContainSingle().Which.Id.Should().Be(id);
        nodes[0].Children[0].IsActive.Should().BeFalse();
        nodes[0].ProductCount.Should().BeNull();
        nodes[0].Children[0].SkuCount.Should().BeNull();
        nodes[0].Description.Should().Be("Root description");
        nodes[0].Children[0].Description.Should().Be("Child description");
        nodes[0].Children[0].ParentId.Should().Be(id);
        handler.Authorization.Should().Be("Bearer test-session");
        handler.RequestUri.Should().Be(new Uri("https://example.test/api/categories/tree"));
        handler.LastToken.CanBeCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task GetTree_MissingToken_DoesNotSendRequest()
    {
        // Arrange
        var (client, tokens, handler) = Create();
        tokens.SetAccessToken(null);
        // Act / Assert
        var failure = await FluentActions.Awaiting(() => client.GetTreeAsync()).Should().ThrowAsync<CategoryApiException>();
        failure.Which.Failure.Should().Be(CategoryApiFailure.SessionRequired);
        handler.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, CategoryApiFailure.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, CategoryApiFailure.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, CategoryApiFailure.Unavailable)]
    public async Task GetTree_ApiFailure_MapsSafeOutcome(HttpStatusCode status, CategoryApiFailure expected)
    {
        // Arrange
        var (client, _, handler) = Create();
        handler.Response.StatusCode = status;
        // Act / Assert
        var failure = await FluentActions.Awaiting(() => client.GetTreeAsync()).Should().ThrowAsync<CategoryApiException>();
        failure.Which.Failure.Should().Be(expected);
        failure.Which.Message.Should().NotContain("test-session");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{ invalid }")]
    public async Task GetTree_MalformedBody_ReportsInvalidResponse(string body)
    {
        // Arrange
        var (client, _, handler) = Create();
        handler.Response.Content = new StringContent(body);
        // Act / Assert
        var failure = await FluentActions.Awaiting(() => client.GetTreeAsync()).Should().ThrowAsync<CategoryApiException>();
        failure.Which.Failure.Should().Be(CategoryApiFailure.InvalidResponse);
    }

    [Fact]
    public async Task GetTree_EmptyDataAndChangedSession_UsesCurrentTokenPerRequest()
    {
        // Arrange
        var (client, tokens, handler) = Create();
        // Act
        (await client.GetTreeAsync()).Should().BeEmpty();
        tokens.SetAccessToken("new-session");
        handler.Response = new(HttpStatusCode.OK) { Content = new StringContent("[]") };
        await client.GetTreeAsync();
        // Assert
        handler.Authorization.Should().Be("Bearer new-session");
    }

    [Fact]
    public async Task GetTree_CancelledToken_PropagatesCancellation()
    {
        // Arrange
        var (client, _, handler) = Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        // Act / Assert
        await FluentActions.Awaiting(() => client.GetTreeAsync(cancellation.Token)).Should().ThrowAsync<OperationCanceledException>();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Create_ValidRequest_SendsPostAndReturnsDto()
    {
        // Arrange
        var (client, _, handler) = Create();
        var id = Guid.NewGuid();
        handler.Response = new(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new CategoryDto
            {
                Id = id,
                Name = "Tai nghe",
                Slug = "tai-nghe",
                IsActive = true
            })
        };
        var req = new TechStore.Shared.Requests.CreateCategoryRequest
        {
            Name = "Tai nghe",
            Slug = "tai-nghe",
            IsActive = true
        };

        // Act
        var result = await client.CreateAsync(req);

        // Assert
        result.Id.Should().Be(id);
        result.Name.Should().Be("Tai nghe");
        handler.RequestUri.Should().Be(new Uri("https://example.test/api/categories"));
        handler.Authorization.Should().Be("Bearer test-session");
    }

    [Fact]
    public async Task Create_Conflict_ThrowsCategoryApiExceptionWithConflictMessage()
    {
        // Arrange
        var (client, _, handler) = Create();
        handler.Response = new(HttpStatusCode.Conflict)
        {
            Content = new StringContent("{\"detail\":\"Category with slug already exists.\"}")
        };

        // Act / Assert
        var failure = await FluentActions.Awaiting(() => client.CreateAsync(new() { Name = "Test" }))
            .Should().ThrowAsync<CategoryApiException>();
        failure.Which.Failure.Should().Be(CategoryApiFailure.Conflict);
        failure.Which.Message.Should().Be("Category with slug already exists.");
    }

    [Fact]
    public async Task Create_BadRequestWithValidationErrors_ThrowsCategoryApiExceptionWithCombinedMessage()
    {
        // Arrange
        var (client, _, handler) = Create();
        handler.Response = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"title\":\"One or more validation errors occurred.\",\"errors\":{\"Name\":[\"Name is required.\"]}}")
        };

        // Act / Assert
        var failure = await FluentActions.Awaiting(() => client.CreateAsync(new()))
            .Should().ThrowAsync<CategoryApiException>();
        failure.Which.Failure.Should().Be(CategoryApiFailure.BadRequest);
        failure.Which.Message.Should().Be("Name is required.");
    }

    [Fact]
    public async Task Create_MissingToken_ThrowsSessionRequired()
    {
        // Arrange
        var (client, tokens, handler) = Create();
        tokens.SetAccessToken(null);

        // Act / Assert
        var failure = await FluentActions.Awaiting(() => client.CreateAsync(new() { Name = "Test" }))
            .Should().ThrowAsync<CategoryApiException>();
        failure.Which.Failure.Should().Be(CategoryApiFailure.SessionRequired);
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Update_ValidRequest_SendsPutWithSupportedFieldsAndReturnsDto()
    {
        var (client, _, handler) = Create();
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        handler.Response = new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CategoryDto
            {
                Id = id, Name = "Updated", Slug = "updated", ParentId = parentId,
                Description = "Description", IsActive = true
            })
        };

        var result = await client.UpdateAsync(id, new TechStore.Shared.Requests.UpdateCategoryRequest
        {
            Name = "Updated", Slug = "updated", ParentId = parentId, Description = "Description", IsActive = true
        });

        result.Id.Should().Be(id);
        handler.Method.Should().Be(HttpMethod.Put);
        handler.RequestUri.Should().Be(new Uri($"https://example.test/api/categories/{id}"));
        handler.Authorization.Should().Be("Bearer test-session");
        handler.RequestBody.Should().Contain("\"name\":\"Updated\"");
        handler.RequestBody.Should().Contain("\"parentId\"");
        handler.RequestBody.Should().Contain("\"isActive\":true");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, CategoryApiFailure.BadRequest)]
    [InlineData(HttpStatusCode.NotFound, CategoryApiFailure.NotFound)]
    [InlineData(HttpStatusCode.Conflict, CategoryApiFailure.Conflict)]
    public async Task Update_ErrorStatus_MapsTypedFailure(HttpStatusCode status, CategoryApiFailure expected)
    {
        var (client, _, handler) = Create();
        handler.Response = new(status) { Content = new StringContent("{\"detail\":\"Update failed.\"}") };

        var failure = await FluentActions.Awaiting(() => client.UpdateAsync(Guid.NewGuid(), new()))
            .Should().ThrowAsync<CategoryApiException>();

        failure.Which.Failure.Should().Be(expected);
        failure.Which.Message.Should().Be("Update failed.");
    }

    [Fact]
    public async Task Update_CancelledToken_PropagatesCancellationWithoutSending()
    {
        var (client, _, handler) = Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await FluentActions.Awaiting(() => client.UpdateAsync(Guid.NewGuid(), new(), cancellation.Token))
            .Should().ThrowAsync<OperationCanceledException>();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Delete_ValidRequest_SendsDeleteWithBearerAndAcceptsNoContent()
    {
        var (client, _, handler) = Create();
        var id = Guid.NewGuid();
        handler.Response = new(HttpStatusCode.NoContent);

        await client.DeleteAsync(id);

        handler.Method.Should().Be(HttpMethod.Delete);
        handler.RequestUri.Should().Be(new Uri($"https://example.test/api/categories/{id}"));
        handler.Authorization.Should().Be("Bearer test-session");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, CategoryApiFailure.BadRequest)]
    [InlineData(HttpStatusCode.Conflict, CategoryApiFailure.Conflict)]
    [InlineData(HttpStatusCode.Unauthorized, CategoryApiFailure.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, CategoryApiFailure.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, CategoryApiFailure.NotFound)]
    public async Task Delete_ErrorStatus_MapsTypedFailure(HttpStatusCode status, CategoryApiFailure expected)
    {
        var (client, _, handler) = Create();
        handler.Response = new(status) { Content = new StringContent("{\"detail\":\"Delete failed.\"}") };

        var failure = await FluentActions.Awaiting(() => client.DeleteAsync(Guid.NewGuid()))
            .Should().ThrowAsync<CategoryApiException>();

        failure.Which.Failure.Should().Be(expected);
        failure.Which.Message.Should().Be(expected is CategoryApiFailure.Unauthorized or CategoryApiFailure.Forbidden
            ? "Category data operation failed."
            : "Delete failed.");
    }

    [Fact]
    public async Task Delete_MissingToken_DoesNotSendRequest()
    {
        var (client, tokens, handler) = Create();
        tokens.SetAccessToken(null);

        var failure = await FluentActions.Awaiting(() => client.DeleteAsync(Guid.NewGuid()))
            .Should().ThrowAsync<CategoryApiException>();
        failure.Which.Failure.Should().Be(CategoryApiFailure.SessionRequired);
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Delete_CancelledToken_PropagatesCancellationWithoutSending()
    {
        var (client, _, handler) = Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await FluentActions.Awaiting(() => client.DeleteAsync(Guid.NewGuid(), cancellation.Token))
            .Should().ThrowAsync<OperationCanceledException>();
        handler.Calls.Should().Be(0);
    }
}
