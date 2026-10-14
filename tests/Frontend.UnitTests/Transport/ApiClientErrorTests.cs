using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.UnitTests.Infrastructure;

namespace TechStore.Client.UnitTests.Transport;

public sealed class ApiClientErrorTests
{
    [Fact]
    public async Task ApiClient_LoggingDoesNotExposeTokenOrPayload()
    {
        // Arrange
        const string token = "sentinel-token-secret";
        const string payload = "sentinel-customer-payload";
        const string trace = "trace-safe-42";
        var messages = new List<string>();
        var logger = new RecordingLogger<ApiClient>(messages);
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                $"{{\"title\":\"Bad request\",\"detail\":\"{payload}\",\"traceId\":\"{trace}\"}}",
                Encoding.UTF8,
                "application/problem+json")
        }));
        var store = new InMemorySessionStore();
        store.SetAccessToken(token);
        using var client = new HttpClient(new BearerTokenHandler(store) { InnerHandler = handler })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        var api = new ApiClient(client, new ApiClientOptions(), logger);

        // Act
        _ = await Assert.ThrowsAsync<ApiException>(() => api.GetAsync<string>("products?secret=sentinel-query"));
        var output = string.Join('\n', messages);

        // Assert
        Assert.Contains("400", output);
        Assert.Contains("Http", output);
        Assert.Contains(trace, output);
        Assert.DoesNotContain(token, output);
        Assert.DoesNotContain(payload, output);
        Assert.DoesNotContain("sentinel-query", output);
    }

    [Fact]
    public async Task GetAsync_NormalizesProblemDetailsAndFieldValidation()
    {
        // Arrange
        const string json = """{"type":"about:blank","title":"Validation failed","detail":"Fix input","status":599,"traceId":"trace-42","code":"invalid_product","errors":{"Name":["Name is required."]}}""";
        var handler = CreateHandler(HttpStatusCode.BadRequest, json);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(client, new ApiClientOptions());

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetAsync<string>("products"));

        // Assert
        Assert.Equal(ApiErrorKind.Http, error.Kind);
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("Validation failed", error.Title);
        Assert.Equal("Fix input", error.Detail);
        Assert.Equal("invalid_product", error.Code);
        Assert.Equal("trace-42", error.TraceId);
        Assert.Equal("Name is required.", Assert.Single(error.FieldErrors["Name"]));
        Assert.DoesNotContain("Fix input", error.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetAsync_PreservesActualStatusForUnrecognizedError(HttpStatusCode status)
    {
        // Arrange
        var handler = CreateHandler(status, "<html>private server detail</html>", "text/html");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(client, new ApiClientOptions());

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetAsync<string>("products"));

        // Assert
        Assert.Equal(ApiErrorKind.Http, error.Kind);
        Assert.Equal((int)status, error.StatusCode);
        Assert.DoesNotContain("private server detail", error.Message);
        Assert.Null(error.Detail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{\"message\":\"unknown shape\"}")]
    public async Task GetAsync_UsesSafeFallbackForEmptyMalformedOrUnknownProblem(string body)
    {
        // Arrange
        var handler = CreateHandler(HttpStatusCode.Conflict, body);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(client, new ApiClientOptions());

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetAsync<string>("products"));

        // Assert
        Assert.Equal((int)HttpStatusCode.Conflict, error.StatusCode);
        Assert.Contains("HTTP status 409", error.Message);
        Assert.Null(error.Title);
        Assert.Null(error.Detail);
    }

    private static RecordingHttpMessageHandler CreateHandler(
        HttpStatusCode status,
        string body,
        string mediaType = "application/problem+json") =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        }));

    private sealed class RecordingLogger<T>(ICollection<string> messages) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullLogger<T>.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Add(formatter(state, exception));
    }
}
