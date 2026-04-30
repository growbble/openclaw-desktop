namespace OpenClawClient.Models;

/// <summary>
/// Сессия чата (аналог чата в Telegram).
/// </summary>
public class ChatSession
{
    public string SessionId { get; set; } = "main";
    public string DisplayName { get; set; } = "Главный чат";
    public List<ChatMessage> Messages { get; set; } = new();
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; }

    // ─── UI helpers ───

    public ChatMessage? LastMessage => Messages.LastOrDefault();
    public string Preview => LastMessage?.Text ?? "Нет сообщений";

    /// <summary>Время последней активности в локальном формате.</summary>
    public string TimeAgo
    {
        get
        {
            var local = LastActivity.Kind == DateTimeKind.Utc
                ? LastActivity.ToLocalTime()
                : LastActivity;

            var diff = DateTime.Now - local;

            if (diff.TotalMinutes < 1) return "только что";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}м";
            if (diff.TotalDays < 1) return local.ToString("HH:mm");
            if (diff.TotalDays < 7) return local.ToString("ddd");
            return local.ToString("dd.MM");
        }
    }
}
