namespace OpenClawClient.Models;

/// <summary>
/// Inline-кнопка, как в Telegram Bot API (inline_keyboard).
/// </summary>
public class InlineButton
{
    public string Text { get; set; } = "";
    public string CallbackData { get; set; } = "";
    public string? Url { get; set; }
}
