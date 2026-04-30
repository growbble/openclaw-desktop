using System.Text.Json.Serialization;

namespace OpenClawClient.Models;

/// <summary>
/// Вложение — файл, прикреплённый к сообщению (аналог media в Telegram).
/// </summary>
public class Attachment
{
    public enum FileType
    {
        Image,
        Document,
        Audio,
        Video,
        Other
    }

    public FileType Type { get; set; }
    public string FileName { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LocalPath { get; set; }

    public string RemoteUrl { get; set; } = "";
    public long FileSize { get; set; }
    public bool IsDownloaded { get; set; }

    // ─── UI helpers ───

    [JsonIgnore]
    public string SizeString
    {
        get
        {
            if (FileSize < 1024) return $"{FileSize} B";
            if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }

    [JsonIgnore]
    public string Icon => Type switch
    {
        FileType.Image => "🖼",
        FileType.Audio => "🎵",
        FileType.Video => "🎬",
        FileType.Document => "📄",
        _ => "📎"
    };

    [JsonIgnore]
    public bool CanPreview => Type == FileType.Image && IsDownloaded && !string.IsNullOrEmpty(LocalPath);
}
