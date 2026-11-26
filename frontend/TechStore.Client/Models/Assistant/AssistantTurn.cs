namespace TechStore.Client.Models.Assistant;

public sealed class AssistantTurn(bool isUser, string text)
{
    public bool IsUser { get; } = isUser;
    public string Text { get; } = text;
}

public sealed record AssistantAnswer(string Reply, IReadOnlyList<string> ToolsUsed, DateTimeOffset? AsOf);
