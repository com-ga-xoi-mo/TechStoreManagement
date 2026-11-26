namespace TechStore.Shared.DTOs;

public class AssistantReplyDto
{
    public string Reply { get; set; } = string.Empty;
    public List<string> ToolsUsed { get; set; } = [];
    public DateTimeOffset? AsOf { get; set; }
}
