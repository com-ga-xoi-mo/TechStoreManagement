using TechStore.Client.Models.Assistant;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Assistant;

public interface IAssistantRepository
{
    Task<AssistantAnswer> SendAsync(IReadOnlyList<AssistantTurn> history, CancellationToken cancellationToken);
}

public sealed class AssistantRepository(IApiClient api) : IAssistantRepository
{
    public async Task<AssistantAnswer> SendAsync(IReadOnlyList<AssistantTurn> history, CancellationToken cancellationToken)
    {
        var request = new AssistantChatRequest
        {
            Messages = history.Select(turn => new AssistantMessageRequest
            {
                Role = turn.IsUser ? "user" : "assistant",
                Content = turn.Text
            }).ToList()
        };
        var result = await api.PostAsync<AssistantChatRequest, AssistantReplyDto>(
            "api/assistant/chat", request, TimeSpan.FromSeconds(90), cancellationToken);
        return new AssistantAnswer(result.Reply, result.ToolsUsed, result.AsOf);
    }
}
