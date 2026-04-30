using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace OpenClawClient.Models;

/// <summary>
/// Конфигурация приложения. Сохраняется как JSON.
/// </summary>
public class AppConfig
{
    private string _serverUrl = "http://localhost:18789";
    private string _authToken = "";
    private string _agentId = "openclaw";
    private string _downloadPath = "";
    private bool _autoDownloadImages = true;
    private bool _autoDownloadDocuments = true;
    private long _maxAutoDownloadSize = 20 * 1024 * 1024; // 20 MB
    private bool _notificationsEnabled = true;

    /// <summary>URL сервера OpenClaw Gateway.</summary>
    public string ServerUrl
    {
        get => _serverUrl;
        set
        {
            var val = (value ?? "").Trim();
            if (!val.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !val.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                val = "https://" + val;
            }
            _serverUrl = val;
        }
    }

    /// <summary>Токен аутентификации (Bearer).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string AuthToken
    {
        get => _authToken;
        set => _authToken = (value ?? "").Trim();
    }

    /// <summary>ID агента (модели) для Chat Completions.</summary>
    public string AgentId
    {
        get => _agentId;
        set => _agentId = string.IsNullOrWhiteSpace(value) ? "openclaw" : value.Trim();
    }

    /// <summary>Папка для сохранения скачанных файлов.</summary>
    public string DownloadPath
    {
        get => _downloadPath;
        set => _downloadPath = (value ?? "").Trim();
    }

    /// <summary>Автоматически скачивать изображения.</summary>
    public bool AutoDownloadImages
    {
        get => _autoDownloadImages;
        set => _autoDownloadImages = value;
    }

    /// <summary>Автоматически скачивать документы.</summary>
    public bool AutoDownloadDocuments
    {
        get => _autoDownloadDocuments;
        set => _autoDownloadDocuments = value;
    }

    /// <summary>Максимальный размер файла для автозагрузки (байт).</summary>
    public long MaxAutoDownloadSize
    {
        get => _maxAutoDownloadSize;
        set => _maxAutoDownloadSize = Math.Clamp(value, 1024, 500L * 1024 * 1024);
    }

    /// <summary>Включить системные Toast-уведомления.</summary>
    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set => _notificationsEnabled = value;
    }
}
