using System.Text.Json;
using OpenClawClient.Models;

namespace OpenClawClient.Services;

/// <summary>
/// Управление сессиями чата: загрузка, сохранение, переключение.
/// </summary>
public class SessionService
{
    private readonly ConfigService _configService;
    private readonly List<ChatSession> _sessions = new();
    private bool _loaded;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public SessionService(ConfigService configService)
    {
        _configService = configService;
    }

    public IReadOnlyList<ChatSession> Sessions => _sessions.AsReadOnly();
    public ChatSession? ActiveSession { get; private set; }

    /// <summary>Загрузить все сессии с диска.</summary>
    public void LoadAll()
    {
        if (_loaded) return;
        _loaded = true;

        _sessions.Clear();
        var dir = _configService.SessionsDir;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            CreateDefaultSession();
            return;
        }

        var files = Directory.GetFiles(dir, "*.json");
        if (files.Length == 0)
        {
            CreateDefaultSession();
            return;
        }

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(json)) continue;

                var session = JsonSerializer.Deserialize<ChatSession>(json, JsonOptions);
                if (session != null)
                {
                    _sessions.Add(session);
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Session load JSON error {file}: {ex.Message}");
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Session load IO error {file}: {ex.Message}");
            }
        }

        // Сортируем по активности (последняя активная сверху)
        _sessions.Sort((a, b) => b.LastActivity.CompareTo(a.LastActivity));

        // Если нет сессий — создаём дефолтную
        if (_sessions.Count == 0)
        {
            CreateDefaultSession();
            return;
        }

        ActiveSession = _sessions.FirstOrDefault(s => s.IsActive) ?? _sessions[0];
    }

    /// <summary>Сохранить все сессии на диск.</summary>
    public void SaveAll()
    {
        var dir = _configService.SessionsDir;
        Directory.CreateDirectory(dir);

        foreach (var session in _sessions)
        {
            var safeName = SanitizeFileName(session.SessionId);
            var path = Path.Combine(dir, $"{safeName}.json");

            try
            {
                var json = JsonSerializer.Serialize(session, JsonOptions);
                var tempPath = path + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, path, overwrite: true);
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"Session save access denied: {session.SessionId}");
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Session save IO error {session.SessionId}: {ex.Message}");
            }
        }

        // Удаляем файлы сессий, которых больше нет
        var currentFileIds = _sessions.Select(s => SanitizeFileName(s.SessionId) + ".json").ToHashSet();
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var fileName = Path.GetFileName(file);
                if (!currentFileIds.Contains(fileName))
                {
                    try { File.Delete(file); }
                    catch { /* ignore */ }
                }
            }
        }
    }

    /// <summary>Переключиться на сессию.</summary>
    public void ActivateSession(string sessionId)
    {
        foreach (var s in _sessions)
            s.IsActive = s.SessionId == sessionId;

        ActiveSession = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
    }

    /// <summary>Добавить сообщение в активную сессию.</summary>
    public void AddMessage(ChatMessage message)
    {
        if (ActiveSession == null) return;

        ActiveSession.Messages.Add(message);
        ActiveSession.LastActivity = DateTime.Now;

        // Перемещаем вверх списка
        _sessions.Remove(ActiveSession);
        _sessions.Insert(0, ActiveSession);
    }

    /// <summary>Создать новую сессию.</summary>
    public ChatSession CreateSession(string sessionId, string displayName)
    {
        var safeId = string.IsNullOrWhiteSpace(sessionId)
            ? $"session_{Guid.NewGuid():N}"
            : sessionId;

        var session = new ChatSession
        {
            SessionId = safeId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Новый чат" : displayName,
            IsActive = true,
            LastActivity = DateTime.Now
        };

        // Деактивируем все остальные
        foreach (var s in _sessions)
            s.IsActive = false;

        _sessions.Insert(0, session);
        ActiveSession = session;
        SaveAll();

        return session;
    }

    /// <summary>Удалить сессию.</summary>
    public bool DeleteSession(string sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
        if (session == null) return false;

        _sessions.Remove(session);

        // Удалить файл
        var safeName = SanitizeFileName(sessionId);
        var path = Path.Combine(_configService.SessionsDir, $"{safeName}.json");
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }

        // Если удалили активную — переключаемся на первую
        if (ActiveSession == session)
        {
            ActiveSession = _sessions.FirstOrDefault();
            if (ActiveSession != null)
            {
                ActiveSession.IsActive = true;
            }
        }

        return true;
    }

    private void CreateDefaultSession()
    {
        var session = new ChatSession
        {
            SessionId = "main",
            DisplayName = "Главный чат",
            IsActive = true,
            LastActivity = DateTime.Now
        };
        _sessions.Add(session);
        ActiveSession = session;
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "unnamed";
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}
