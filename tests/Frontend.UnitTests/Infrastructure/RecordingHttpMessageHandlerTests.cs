using System.Net;
using System.Text;

namespace TechStore.Client.UnitTests.Infrastructure;

public sealed class RecordingHttpMessageHandlerTests
{
    [Fact]
    public async Task SendAsync_CapturesRequestAndUsesControlledResponse()
    {
        // Arrange
        var responseGate = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHttpMessageHandler((_, _) => responseGate.Task);
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.example.test/products?search=a%26b")
        {
            Content = new StringContent("{\"name\":\"Keyboard\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new("Bearer", "sentinel-token");

        // Act
        var pendingResponse = client.SendAsync(request);
        var captured = Assert.Single(handler.Requests);
        responseGate.SetResult(new HttpResponseMessage(HttpStatusCode.Created));
        using var response = await pendingResponse;

        // Assert
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal("https://api.example.test/products?search=a%26b", captured.RequestUri?.ToString());
        Assert.Equal("{\"name\":\"Keyboard\"}", captured.Body);
        Assert.Equal("Bearer", captured.Authorization?.Scheme);
        Assert.Equal("sentinel-token", captured.Authorization?.Parameter);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
