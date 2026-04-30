using System.Text.Json;
using OpenClawClient.Models;

namespace OpenClawClient.Services;

/// <summary>
/// Загрузка/сохранение конфигурации из %LOCALAPPDATA%/OpenClawClient/.
/// ⚠️ Пароль хранится локально — защищён только файловой системой.
/// </summary>
public class ConfigService
{
    private readonly string _configDir;
    private readonly string _configPath;
    private readonly string _configPathLegacy;
    private AppConfig _config = new();

    public ConfigService()
    {
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClawClient");
        _configPath = Path.Combine(_configDir, "settings.json");
        _configPathLegacy = Path.Combine(_configDir, "appsettings.json");
        Directory.CreateDirectory(_configDir);
        Load();
    }

    public AppConfig Config => _config;

    public void Load()
    {
        try
        {
            var path = _configPath;
            if (!File.Exists(path) && File.Exists(_configPathLegacy))
                path = _configPathLegacy;

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    _config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }

            // Валидация полей при загрузке
            ValidateConfig(_config);
        }
        catch (JsonException)
        {
            _config = new AppConfig();
            Save(); // перезаписать битый файл
        }
        catch
        {
            _config = new AppConfig();
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (dir != null) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(_configPath, json);

            // Удалить старый файл, если существует
            if (File.Exists(_configPathLegacy))
                File.Delete(_configPathLegacy);
        }
        catch (UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine("Config save: access denied");
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Config save IO error: {ex.Message}");
        }
    }

    /// <summary>Путь к папке с сессиями.</summary>
    public string SessionsDir => Path.Combine(_configDir, "sessions");

    private static void ValidateConfig(AppConfig cfg)
    {
        // Санитизация URL — удалить лишние пробелы, добавить схему если нет
        if (!string.IsNullOrWhiteSpace(cfg.ServerUrl))
        {
            cfg.ServerUrl = cfg.ServerUrl.Trim();
            if (!cfg.ServerUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !cfg.ServerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                cfg.ServerUrl = "https://" + cfg.ServerUrl;
            }
        }

        // Ограничение MaxAutoDownloadSize разумным пределом
        if (cfg.MaxAutoDownloadSize <= 0 || cfg.MaxAutoDownloadSize > 500L * 1024 * 1024)
            cfg.MaxAutoDownloadSize = 20L * 1024 * 1024;
    }
}
