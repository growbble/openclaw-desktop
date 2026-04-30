using System.Text;
using System.Text.Json;
using OpenClawClient.Models;

namespace OpenClawClient.Services;

/// <summary>
/// HTTP-клиент для OpenClaw Gateway через OpenAI-совместимый API.
/// </summary>
public interface IOpenClawGatewayService
{
    Task<AgentResponse> SendMessageAsync(string sessionId, string text, CancellationToken ct);
    string LastMessageId { get; }
    Task<string> DownloadFileAsync(string fileUrl, string destinationFolder,
        IProgress<double> progress, CancellationToken ct);
    Task SendFileAsync(string sessionId, string localFilePath, string? caption, CancellationToken ct);
    Task<bool> CheckConnectionAsync();
}

public class OpenClawGatewayService : IOpenClawGatewayService
{
    private readonly HttpClient _httpClient;
    private readonly ConfigService _configService;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Последний ID сообщения от сервера (для дедупликации).</summary>
    public string LastMessageId { get; private set; } = "";

    public OpenClawGatewayService(ConfigService configService)
    {
        _configService = configService;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        ApplyAuth();
    }

    public void ApplyAuth()
    {
        _authLock.Wait();
        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var token = _configService.Config.AuthToken?.Trim();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }
        finally
        {
            _authLock.Release();
        }
    }

    private string BaseUrl => _configService.Config.ServerUrl.TrimEnd('/');
    private string AgentId => _configService.Config.AgentId;
    private string SafeAgentId => string.IsNullOrWhiteSpace(AgentId) ? "openclaw" : AgentId.Trim().ToLowerInvariant();

    /// <inheritdoc/>
    public async Task<AgentResponse> SendMessageAsync(string sessionId, string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new AgentResponse { Text = "" };

        // Не отправляем служебные ping-команды как настоящие сообщения
        if (text == "/ping")
            return new AgentResponse { Text = "(pong)" };

        var request = new ChatRequest
        {
            Model = SafeAgentId,
            Messages = new List<ChatRequestMessage>
            {
                new() { Role = "user", Content = text }
            },
            Stream = false,
            User = sessionId
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(300)); // 5 min max

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/chat/completions")
        {
            Content = content
        };
        httpRequest.Headers.TryAddWithoutValidation("x-openclaw-agent-id", SafeAgentId);

        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseJson, JsonOptions);

            var agentText = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
            if (string.IsNullOrWhiteSpace(agentText))
                agentText = "(пустой ответ)";

            // Сохраняем ID ответа для дедупликации
            if (!string.IsNullOrEmpty(chatResponse?.Id))
                LastMessageId = chatResponse.Id;

            // Парсим markdown-вложения и inline-кнопки из текста
            var attachments = ParseAttachments(agentText);
            var buttons = ParseInlineButtons(agentText);

            return new AgentResponse
            {
                Text = agentText,
                Attachments = attachments,
                InlineButtons = buttons,
                AgentId = SafeAgentId
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw new TaskCanceledException("Request cancelled by user");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Неверный токен доступа. Проверьте Gateway Token в настройках.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new HttpRequestException("Сервер не найден. Проверьте URL в настройках.");
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"Ошибка соединения: {ex.Message}");
        }
        finally
        {
            response?.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task<string> DownloadFileAsync(string fileUrl, string destinationFolder,
        IProgress<double> progress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            throw new ArgumentException("URL файла не указан");

        Directory.CreateDirectory(destinationFolder);

        // Безопасно создаём URI
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
            throw new UriFormatException($"Некорректный URL файла: {fileUrl}");

        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrEmpty(fileName))
            fileName = $"download_{Guid.NewGuid():N}";

        // Уникальное имя файла
        var savePath = Path.Combine(destinationFolder, SanitizeFileName(fileName));
        var counter = 1;
        while (File.Exists(savePath))
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            savePath = Path.Combine(destinationFolder, $"{SanitizeFileName(name)}_{counter}{ext}");
            counter++;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(10));

        try
        {
            using var response = await _httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var fileStream = File.Create(savePath);

            if (totalBytes > 0)
            {
                var buffer = new byte[81920]; // 80 KB buffer для скорости
                long readSoFar = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
                    readSoFar += bytesRead;
                    progress?.Report((double)readSoFar / totalBytes);
                }
            }
            else
            {
                await stream.CopyToAsync(fileStream, cts.Token);
            }

            return savePath;
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            // Удаляем недокачанный файл
            if (File.Exists(savePath))
                File.Delete(savePath);
            throw new TaskCanceledException("Загрузка файла отменена");
        }
        catch (HttpRequestException ex)
        {
            if (File.Exists(savePath))
                File.Delete(savePath);
            throw new HttpRequestException($"Ошибка загрузки файла: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task SendFileAsync(string sessionId, string localFilePath, string? caption, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(localFilePath))
            throw new ArgumentException("Путь к файлу не указан");

        if (!File.Exists(localFilePath))
            throw new FileNotFoundException("Файл не найден", localFilePath);

        var fileInfo = new FileInfo(localFilePath);
        if (fileInfo.Length == 0)
            throw new ArgumentException("Файл пустой");

        // Лимит размера файла (100 MB)
        if (fileInfo.Length > 100L * 1024 * 1024)
            throw new ArgumentException("Файл слишком большой (макс. 100 MB)");

        var fileName = Path.GetFileName(localFilePath);
        var mimeType = GetMimeType(fileName);

        var fileBytes = await File.ReadAllBytesAsync(localFilePath, ct);
        var base64 = Convert.ToBase64String(fileBytes);

        var payload = new
        {
            model = SafeAgentId,
            input = new object[]
            {
                new
                {
                    type = "input_file",
                    source = new
                    {
                        type = "base64",
                        media_type = mimeType,
                        data = base64,
                        filename = fileName
                    }
                },
                new
                {
                    type = "message",
                    role = "user",
                    content = new[] { new { type = "input_text", text = caption ?? "" } }
                }
            },
            stream = false,
            user = sessionId
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/responses")
        {
            Content = content
        };
        httpRequest.Headers.TryAddWithoutValidation("x-openclaw-agent-id", SafeAgentId);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, cts.Token);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge)
        {
            throw new HttpRequestException("Файл слишком большой для отправки на сервер");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.UnsupportedMediaType)
        {
            throw new HttpRequestException("Тип файла не поддерживается сервером");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync($"{BaseUrl}/v1/models", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    // ---- Attachment/Button parsing ----

    private static List<Attachment> ParseAttachments(string text)
    {
        var result = new List<Attachment>();

        // Markdown images: ![alt](url)
        var imgMatches = System.Text.RegularExpressions.Regex.Matches(text,
            @"!\[(.*?)\]\((https?://[^\s)]+)\)");
        foreach (System.Text.RegularExpressions.Match match in imgMatches)
        {
            var url = match.Groups[2].Value;
            if (!Uri.TryCreate(url, UriKind.Absolute, out _)) continue;

            result.Add(new Attachment
            {
                Type = Attachment.FileType.Image,
                RemoteUrl = url,
                FileName = ExtractFileName(match, url),
                IsDownloaded = false
            });
        }

        // File links: [name](url.ext)
        var fileMatches = System.Text.RegularExpressions.Regex.Matches(text,
            @"\[([^\]]+)\]\((https?://[^\s)]+\.([a-zA-Z0-9]+))\)");
        foreach (System.Text.RegularExpressions.Match match in fileMatches)
        {
            var url = match.Groups[2].Value;
            if (!Uri.TryCreate(url, UriKind.Absolute, out _)) continue;

            var ext = match.Groups[3].Value.ToLowerInvariant();
            var type = ext switch
            {
                "png" or "jpg" or "jpeg" or "gif" or "webp" or "bmp" or "svg" => Attachment.FileType.Image,
                "mp3" or "wav" or "ogg" or "flac" or "aac" or "wma" => Attachment.FileType.Audio,
                "mp4" or "mov" or "avi" or "mkv" or "webm" or "flv" => Attachment.FileType.Video,
                _ => Attachment.FileType.Document
            };

            result.Add(new Attachment
            {
                Type = type,
                FileName = match.Groups[1].Value,
                RemoteUrl = url,
                IsDownloaded = false
            });
        }

        return result;
    }

    private static List<InlineButton> ParseInlineButtons(string text)
    {
        var result = new List<InlineButton>();

        // Ищем JSON-блок с inline_keyboard
        var jsonMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"```(?:json)?\s*(\{.*?""inline_keyboard"".*?\})\s*```",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (jsonMatch.Success)
        {
            try
            {
                var keyboard = JsonSerializer.Deserialize<InlineKeyboard>(jsonMatch.Groups[1].Value, JsonOptions);
                if (keyboard?.InlineKeyboardList != null)
                {
                    foreach (var row in keyboard.InlineKeyboardList)
                    {
                        foreach (var btn in row)
                        {
                            result.Add(btn);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // ignore malformed JSON
            }
        }

        return result;
    }

    private static string ExtractFileName(System.Text.RegularExpressions.Match match, string url)
    {
        var alt = match.Groups[1].Value;
        if (!string.IsNullOrWhiteSpace(alt))
            return alt;

        try
        {
            return Path.GetFileName(new Uri(url).LocalPath);
        }
        catch
        {
            return "file";
        }
    }

    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".flac" => "audio/flac",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            ".rar" => "application/vnd.rar",
            ".7z" => "application/x-7z-compressed",
            ".doc" or ".docx" => "application/msword",
            ".xls" or ".xlsx" => "application/vnd.ms-excel",
            ".ppt" or ".pptx" => "application/vnd.ms-powerpoint",
            _ => "application/octet-stream"
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private class InlineKeyboard
    {
        [System.Text.Json.Serialization.JsonPropertyName("inline_keyboard")]
        public List<List<InlineButton>>? InlineKeyboardList { get; set; }
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient?.Dispose();
        _authLock?.Dispose();
    }
}
