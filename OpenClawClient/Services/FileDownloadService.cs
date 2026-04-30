using System.Text.RegularExpressions;

namespace OpenClawClient.Services;

/// <summary>
/// Находит ссылки на файлы в тексте и скачивает их.
/// </summary>
public class FileDownloadService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ConfigService _configService;

    private static readonly Regex FileUrlPattern = new(
        @"(https?://[^\s<>""'()]+\.([a-zA-Z0-9]{2,6}))(?:\?[^\s<>""']*)?(?=[\s<>""'()]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".gz", ".tar",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".bmp",
        ".mp3", ".wav", ".ogg", ".flac", ".aac", ".wma",
        ".mp4", ".mov", ".avi", ".mkv", ".webm", ".flv",
        ".iso", ".dmg", ".pkg", ".deb", ".rpm", ".apk",
        ".bin", ".dat",
        ".json", ".csv", ".xml", ".txt", ".log",
        ".py", ".js", ".ts", ".cs", ".go", ".rs", ".cpp", ".c", ".h", ".hpp",
        ".sql", ".yaml", ".yml", ".md",
        // Код и скрипты — не автоскачиваем (опасность XSS/exec)
        // ".bat", ".ps1", ".sh" — убраны из авто-Download
        // ".exe", ".msi", ".dll" — изначально были, но убраны как опасные
    };

    public FileDownloadService(ConfigService configService)
    {
        _configService = configService;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OpenClawDesktop/1.0");
    }

    public async Task<List<Models.ReceivedFile>> DownloadFilesFromTextAsync(string text)
    {
        var result = new List<Models.ReceivedFile>();
        var downloadPath = _configService.Config.DownloadPath;
        if (string.IsNullOrWhiteSpace(downloadPath))
            return result;

        Directory.CreateDirectory(downloadPath);

        var urls = new List<string>();
        var matches = FileUrlPattern.Matches(text);
        foreach (Match match in matches)
        {
            var ext = Path.GetExtension(match.Groups[1].Value);
            if (!string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext))
                urls.Add(match.Groups[1].Value);
        }

        urls = urls.Distinct().ToList();

        foreach (var url in urls)
        {
            try
            {
                var file = await DownloadFileAsync(url, downloadPath);
                if (file != null) result.Add(file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to download {url}: {ex.Message}");
            }
        }

        return result;
    }

    private async Task<Models.ReceivedFile?> DownloadFileAsync(string url, string downloadPath)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        // Только HTTP/HTTPS
        if (uri.Scheme != "http" && uri.Scheme != "https")
            return null;

        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"download_{Guid.NewGuid():N}";

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mimeType = GetMimeType(ext);

        var savePath = Path.Combine(downloadPath, SanitizeFileName(fileName));
        var counter = 1;
        while (File.Exists(savePath))
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            savePath = Path.Combine(downloadPath, $"{SanitizeFileName(name)}_{counter}{ext}");
            counter++;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            // Не следуем редиректам
            if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                return null;

            // Проверка Content-Type — игнорируем HTML
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                return null;

            var contentLength = response.Content.Headers.ContentLength ?? -1;

            // Проверка максимального размера
            var maxSize = _configService.Config.MaxAutoDownloadSize;
            if (maxSize > 0 && contentLength > maxSize)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(savePath);
            await stream.CopyToAsync(fileStream);

            return new Models.ReceivedFile
            {
                FileName = Path.GetFileName(savePath),
                LocalPath = savePath,
                Size = contentLength > 0 ? contentLength : new FileInfo(savePath).Length,
                SavedAt = DateTime.Now,
                MimeType = mimeType
            };
        }
        catch
        {
            if (File.Exists(savePath))
                File.Delete(savePath);
            throw;
        }
    }

    private static string GetMimeType(string ext) => ext.ToLowerInvariant() switch
    {
        ".zip" => "application/zip",
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".mp3" => "audio/mpeg",
        ".mp4" => "video/mp4",
        ".json" => "application/json",
        ".csv" => "text/csv",
        ".txt" => "text/plain",
        ".xml" => "application/xml",
        _ => "application/octet-stream"
    };

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "download";
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient?.Dispose();
    }
}
