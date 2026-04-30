using System.Text.Json.Serialization;

namespace OpenClawClient.Models;

/// <summary>
/// Одно сообщение в чате (аналог Message в Telegram).
/// </summary>
public class ChatMessage
{
    private static long _idCounter;

    public ChatMessage()
    {
        Id = GenerateShortId();
    }

    /// <summary>Локальный ID сообщения (короткий, 6 символов).</summary>
    public string Id { get; set; }

    public enum SenderType
    {
        User,
        Agent,
        System
    }

    public SenderType Sender { get; set; }

    /// <summary>ID сообщения на сервере (ответ Gateway).</summary>
    public string? ServerMessageId { get; set; }

    /// <summary>Статус доставки.</summary>
    public enum DeliveryStatus
    {
        Sending,     // ⏳
        Sent,        // ✓
        Delivered,   // ✓✓
        Read         // ✓✓ (синие)
    }

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Sent;

    /// <summary>Текст сообщения. Не null.</summary>
    private string _text = "";
    public string Text
    {
        get => _text;
        set => _text = value ?? "";
    }

    /// <summary>Время отправки (UTC).</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Вложения (медиа).</summary>
    public List<Attachment> Attachments { get; set; } = new();

    /// <summary>Inline-кнопки.</summary>
    public List<InlineButton> InlineButtons { get; set; } = new();

    // ─── UI helpers (не сериализуются) ───

    [JsonIgnore]
    public bool IsUser => Sender == SenderType.User;

    [JsonIgnore]
    public bool IsAgent => Sender == SenderType.Agent;

    [JsonIgnore]
    public bool IsSystem => Sender == SenderType.System;

    [JsonIgnore]
    public string StatusIcon => Status switch
    {
        DeliveryStatus.Sending => "⏳",
        DeliveryStatus.Sent => "✓",
        DeliveryStatus.Delivered => "✓✓",
        DeliveryStatus.Read => "✓✓",
        _ => ""
    };

    [JsonIgnore]
    public string TimeString
    {
        get
        {
            var local = Timestamp.Kind == DateTimeKind.Utc
                ? Timestamp.ToLocalTime()
                : Timestamp;
            return local.ToString("HH:mm");
        }
    }

    private static string GenerateShortId()
    {
        var count = Interlocked.Increment(ref _idCounter);
        // 6 hex chars = ~16M уникальных
        return (count & 0xFFFFFF).ToString("X6");
    }
}
