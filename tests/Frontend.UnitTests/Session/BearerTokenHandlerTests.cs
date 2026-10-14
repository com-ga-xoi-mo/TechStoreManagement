using System.Net;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.UnitTests.Infrastructure;

namespace TechStore.Client.UnitTests.Session;

public sealed class BearerTokenHandlerTests
{
    [Fact]
    public async Task HandlerReadsCurrentTokenForEachRequestAndRemovesItAfterClear()
    {
        // Arrange
        var store = new InMemorySessionStore();
        store.SetAccessToken("token-a");
        var terminal = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = CreateClient(new BearerTokenHandler(store) { InnerHandler = terminal });

        // Act
        await client.GetAsync("https://localhost/a");
        store.SetAccessToken("token-b");
        await client.GetAsync("https://localhost/b");
        store.Clear();
        await client.GetAsync("https://localhost/c");

        // Assert
        Assert.Equal("token-a", terminal.Requests[0].Authorization?.Parameter);
        Assert.Equal("token-b", terminal.Requests[1].Authorization?.Parameter);
        Assert.Null(terminal.Requests[2].Authorization);
        Assert.All(terminal.Requests.Take(2), item => Assert.Equal("Bearer", item.Authorization?.Scheme));
    }

    [Fact]
    public async Task HandlerDoesNotLetCallerOverrideAuthorization()
    {
        // Arrange
        var store = new InMemorySessionStore();
        store.SetAccessToken("token-a");
        var terminal = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = CreateClient(new BearerTokenHandler(store) { InnerHandler = terminal });
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/a");
        request.Headers.Authorization = new("Bearer", "token-override");

        // Act
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));

        // Assert
        Assert.Contains("managed", error.Message);
        Assert.Empty(terminal.Requests);
    }

    [Fact]
    public async Task UnauthorizedResponseExpiresOnlyTheSessionThatSentTheRequest()
    {
        // Arrange
        var store = new InMemorySessionStore();
        store.SetAccessToken("token-a");
        var requestAStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRequestA = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminal = new RecordingHttpMessageHandler(async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/a", StringComparison.Ordinal))
            {
                requestAStarted.SetResult();
                return await finishRequestA.Task;
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = CreateClient(new BearerTokenHandler(store) { InnerHandler = terminal });

        // Act
        var oldRequest = client.GetAsync("https://localhost/a");
        await requestAStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        store.SetAccessToken("token-b");
        using var currentResponse = await client.GetAsync("https://localhost/b");
        finishRequestA.SetResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var oldResponse = await oldRequest;

        // Assert
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldResponse.StatusCode);
        Assert.Equal(SessionStatus.Active, store.Current.Status);
        Assert.Equal("token-b", store.Current.AccessToken);
        Assert.Equal("token-b", terminal.Requests[1].Authorization?.Parameter);
    }

    [Theory]
    [InlineData("token-b")]
    [InlineData("token-a")]
    public async Task LateUnauthorizedAfterReplacementDoesNotExpireTheNewGeneration(string replacementToken)
    {
        // Arrange
        var store = new InMemorySessionStore();
        store.SetAccessToken("token-a");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminal = new RecordingHttpMessageHandler(async (_, _) =>
        {
            started.SetResult();
            return await finish.Task;
        });
        using var client = CreateClient(new BearerTokenHandler(store) { InnerHandler = terminal });

        // Act
        var oldRequest = client.GetAsync("https://localhost/a");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        store.SetAccessToken(replacementToken);
        finish.SetResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var oldResponse = await oldRequest;

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, oldResponse.StatusCode);
        Assert.Equal(SessionStatus.Active, store.Current.Status);
        Assert.Equal(replacementToken, store.Current.AccessToken);
    }

    [Fact]
    public async Task UnauthorizedResponseExpiresCurrentSessionAndNextRequestIsAnonymous()
    {
        // Arrange
        var store = new InMemorySessionStore();
        store.SetAccessToken("token-a");
        var terminal = new RecordingHttpMessageHandler((request, _) => Task.FromResult(
            new HttpResponseMessage(request.Headers.Authorization is null
                ? HttpStatusCode.OK
                : HttpStatusCode.Unauthorized)));
        using var client = CreateClient(new BearerTokenHandler(store) { InnerHandler = terminal });

        // Act
        using var unauthorized = await client.GetAsync("https://localhost/first");
        using var anonymous = await client.GetAsync("https://localhost/second");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        Assert.Equal(SessionStatus.Expired, store.Current.Status);
        Assert.Null(store.Current.AccessToken);
        Assert.Equal("token-a", terminal.Requests[0].Authorization?.Parameter);
        Assert.Null(terminal.Requests[1].Authorization);
    }

    [Fact]
    public async Task ForbiddenAndAnonymousUnauthorizedDoNotExpireSession()
    {
        // Arrange
        var store = new InMemorySessionStore();
        store.SetAccessToken("token-a");
        var terminal = new RecordingHttpMessageHandler((request, _) => Task.FromResult(
            new HttpResponseMessage(request.RequestUri!.AbsolutePath.EndsWith("/forbidden", StringComparison.Ordinal)
                ? HttpStatusCode.Forbidden
                : HttpStatusCode.Unauthorized)));
        using var client = CreateClient(new BearerTokenHandler(store) { InnerHandler = terminal });

        // Act
        using var forbidden = await client.GetAsync("https://localhost/forbidden");
        store.Clear();
        using var anonymousUnauthorized = await client.GetAsync("https://localhost/anonymous");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousUnauthorized.StatusCode);
        Assert.Equal(SessionStatus.Anonymous, store.Current.Status);
        Assert.Null(terminal.Requests[1].Authorization);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler)
    {
        Timeout = Timeout.InfiniteTimeSpan
    };
}
