namespace OpenClawClient.Models;

/// <summary>
/// Ответ от агента (парсится из OpenAI Chat Completion response).
/// </summary>
public class AgentResponse
{
    public string Text { get; set; } = "";
    public List<Attachment> Attachments { get; set; } = new();
    public List<InlineButton> InlineButtons { get; set; } = new();
    public string? AgentId { get; set; }
}
