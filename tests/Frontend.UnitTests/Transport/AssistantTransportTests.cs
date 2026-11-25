using System.Net;
using System.Text;
using System.Text.Json;
using TechStore.Client.Models.Assistant;
using TechStore.Client.Repositories.Assistant;
using TechStore.Client.Services.Api;
using TechStore.Client.UnitTests.Infrastructure;

namespace TechStore.Client.UnitTests.Transport;

public sealed class AssistantTransportTests
{
    [Fact]
    public async Task Repository_UsesNinetySecondTimeoutAndForwardsCancellation()
    {
        var api = new RecordingAssistantApi();
        using var cancellation = new CancellationTokenSource();
        var repository = new AssistantRepository(api);
        await repository.SendAsync([new AssistantTurn(true, "question")], cancellation.Token);
        Assert.Equal(TimeSpan.FromSeconds(90), api.Timeout);
        Assert.Equal(cancellation.Token, api.Token);
    }

    private sealed class RecordingAssistantApi : IApiClient
    {
        public TimeSpan Timeout { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Timeout = timeout;
            Token = cancellationToken;
            return Task.FromResult((TResponse)(object)new TechStore.Shared.DTOs.AssistantReplyDto { Reply = "ok" });
        }
        public Task<TResponse> GetAsync<TResponse>(string path, CancellationToken token = default) => throw new NotSupportedException();
        public Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ApiResponse<TResponse>> PostWithStatusAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken token = default) => throw new NotSupportedException();
        public Task PostAsync<TRequest>(string path, TRequest request, CancellationToken token = default) => throw new NotSupportedException();
        public Task<TResponse> PostFileAsync<TResponse>(string path, ApiFileContent file, IReadOnlyDictionary<string, string>? fields = null, CancellationToken token = default) => throw new NotSupportedException();
        public Task PatchAsync(string path, CancellationToken token = default) => throw new NotSupportedException();
        public Task<TResponse> PutAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken token = default) => throw new NotSupportedException();
        public Task<TResponse> PatchAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken token = default) => throw new NotSupportedException();
        public Task DeleteAsync(string path, CancellationToken token = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Repository_MapsMessagesAndReply()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"reply\":\"hello\",\"toolsUsed\":[\"get_stock\"],\"asOf\":\"2026-11-25T08:00:00Z\"}", Encoding.UTF8, "application/json")
        }));
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var repository = new AssistantRepository(new ApiClient(http, new ApiClientOptions()));
        var answer = await repository.SendAsync([new AssistantTurn(true, "question"), new AssistantTurn(false, "reply"), new AssistantTurn(true, "next")], CancellationToken.None);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/assistant/chat", request.RequestUri!.AbsolutePath);
        using var json = JsonDocument.Parse(request.Body!);
        var messages = json.RootElement.GetProperty("messages");
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Equal("next", messages[2].GetProperty("content").GetString());
        Assert.Equal("hello", answer.Reply);
        Assert.Equal("get_stock", Assert.Single(answer.ToolsUsed));
        Assert.NotNull(answer.AsOf);
    }

    [Fact]
    public async Task ExplicitTimeout_DoesNotChangeDefaultAndPreservesCancellation()
    {
        var handler = new RecordingHttpMessageHandler(async (_, token) =>
        {
            await Task.Delay(100, token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("\"ok\"") };
        });
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(http, new ApiClientOptions { TimeoutSeconds = 1 });
        var error = await Assert.ThrowsAsync<ApiException>(() => api.PostAsync<object, string>("test", new { }, TimeSpan.FromMilliseconds(10)));
        Assert.Equal(ApiErrorKind.Timeout, error.Kind);
        var shortDefaultApi = new ApiClient(http, new ApiClientOptions { TimeoutSeconds = 0.01 });
        Assert.Equal("ok", await shortDefaultApi.PostAsync<object, string>("test", new { }, TimeSpan.FromSeconds(1)));
        Assert.Equal("ok", await api.PostAsync<object, string>("test", new { }));
        using var cancellation = new CancellationTokenSource();
        var pending = api.PostAsync<object, string>("test", new { }, TimeSpan.FromSeconds(5), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }
}
