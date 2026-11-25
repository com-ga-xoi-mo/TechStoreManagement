using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TechStore.Api.Assistant;

namespace TechStore.Api.Common.Ai;

public sealed class OpenAiCompatibleChatClient(HttpClient http, IOptions<AssistantOptions> options) : IChatModelClient
{
    public async Task<ChatModelResponse> CompleteAsync(ChatModelRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var messages = request.Messages.Select(m => new Dictionary<string, object?>
        {
            ["role"] = m.Role, ["content"] = m.Content,
            ["tool_calls"] = m.ToolCalls?.Select(ToWire).ToArray(),
            ["tool_call_id"] = m.ToolCallId
        }.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value)).ToArray();
        var body = new Dictionary<string, object> { ["model"] = settings.Model, ["messages"] = messages, ["stream"] = false };
        if (request.Tools.Count > 0)
            body["tools"] = request.Tools.Select(t => new { type = "function", function = new { name = t.Name, description = t.Description, parameters = t.Parameters } }).ToArray();
        try
        {
            using var response = await SendAsync(settings, body, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new ChatModelUnavailableException();
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var choice = json.RootElement.GetProperty("choices")[0].GetProperty("message");
            string? content = choice.TryGetProperty("content", out var text) && text.ValueKind != JsonValueKind.Null ? text.GetString() : null;
            var calls = new List<ChatToolCall>();
            if (choice.TryGetProperty("tool_calls", out var tools) && tools.ValueKind != JsonValueKind.Null)
                foreach (var tool in tools.EnumerateArray())
                {
                    var function = tool.GetProperty("function");
                    JsonElement? extra = tool.TryGetProperty("extra_content", out var e) && e.ValueKind == JsonValueKind.Object ? e.Clone() : null;
                    calls.Add(new(tool.GetProperty("id").GetString() ?? throw new JsonException(), function.GetProperty("name").GetString() ?? throw new JsonException(), function.GetProperty("arguments").GetString() ?? throw new JsonException(), extra));
                }
            return new(content, calls);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or TaskCanceledException or FormatException)
        { throw new ChatModelUnavailableException(); }
    }

    // Gemini free tier often answers 429 (per-minute quota) or 503 (overloaded); one retry clears most of them.
    private async Task<HttpResponseMessage> SendAsync(AssistantOptions settings, object body, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl.TrimEnd('/') + "/chat/completions");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            message.Content = JsonContent.Create(body);
            var response = await http.SendAsync(message, cancellationToken);
            if (attempt > 0 || response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)) return response;
            response.Dispose();
            await Task.Delay(RetryDelay, cancellationToken);
        }
    }

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private static Dictionary<string, object> ToWire(ChatToolCall c)
    {
        var wire = new Dictionary<string, object> { ["id"] = c.Id, ["type"] = "function", ["function"] = new { name = c.Name, arguments = c.ArgumentsJson } };
        if (c.ExtraContent is { } extra) wire["extra_content"] = extra;
        return wire;
    }
}
