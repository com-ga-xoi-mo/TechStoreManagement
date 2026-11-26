using System.Text.Json;

namespace TechStore.Api.Common.Ai;

public interface IChatModelClient
{
    Task<ChatModelResponse> CompleteAsync(ChatModelRequest request, CancellationToken cancellationToken);
}

// ExtraContent: provider data (Gemini thought_signature) that must be sent back unchanged.
public sealed record ChatToolCall(string Id, string Name, string ArgumentsJson, JsonElement? ExtraContent = null);
public sealed record ChatMessage(string Role, string? Content, IReadOnlyList<ChatToolCall>? ToolCalls = null, string? ToolCallId = null);
public sealed record ChatTool(string Name, string Description, JsonElement Parameters);
public sealed record ChatModelRequest(IReadOnlyList<ChatMessage> Messages, IReadOnlyList<ChatTool> Tools);
public sealed record ChatModelResponse(string? Content, IReadOnlyList<ChatToolCall> ToolCalls);
public sealed class ChatModelUnavailableException : Exception
{
    public ChatModelUnavailableException() : base("Không kết nối được dịch vụ AI.") { }
}
