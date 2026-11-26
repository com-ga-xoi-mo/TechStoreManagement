namespace TechStore.Shared.Requests;

public class AssistantChatRequest
{
    public List<AssistantMessageRequest> Messages { get; set; } = [];
}

public class AssistantMessageRequest
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
