using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenClawClient.Models;
using OpenClawClient.Services;

namespace OpenClawClient.ViewModels;

/// <summary>
/// Готовая к показу UI-модель сообщения чата.
/// </summary>
public class ChatMessageDisplay
{
    public string Text { get; set; } = "";
    public string StatusIcon { get; set; } = "";
    public string Time { get; set; } = "";
    public HorizontalAlignment Alignment { get; set; }
    public Thickness BubbleMargin { get; set; }
    public List<Attachment> Attachments { get; set; } = new();
    public List<InlineButton> InlineButtons { get; set; } = new();
    public bool IsUser { get; set; }

    public static ChatMessageDisplay FromMessage(ChatMessage msg)
    {
        ArgumentNullException.ThrowIfNull(msg);

        var localTime = msg.Timestamp.Kind == DateTimeKind.Utc
            ? msg.Timestamp.ToLocalTime()
            : msg.Timestamp;

        return new ChatMessageDisplay
        {
            Text = msg.Text,
            StatusIcon = msg.StatusIcon,
            Time = localTime.ToString("HH:mm"),
            Alignment = msg.IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            BubbleMargin = msg.IsUser
                ? new Thickness(48, 0, 0, 0)
                : new Thickness(0, 0, 48, 0),
            Attachments = msg.Attachments ?? new(),
            InlineButtons = msg.InlineButtons ?? new(),
            IsUser = msg.IsUser
        };
    }
}

/// <summary>
/// Главная ViewModel — управляет состоянием чата, сессиями, сообщениями.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly IOpenClawGatewayService _gatewayService;
    private readonly SessionService _sessionService;
    private readonly FileDownloadService _fileService;
    private readonly NotificationService _notificationService;
    private readonly PollingService _pollingService;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _globalCts = new();

    public MainViewModel(
        IOpenClawGatewayService gatewayService,
        SessionService sessionService,
        FileDownloadService fileService,
        NotificationService notificationService,
        PollingService pollingService)
    {
        _gatewayService = gatewayService ?? throw new ArgumentNullException(nameof(gatewayService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _pollingService = pollingService ?? throw new ArgumentNullException(nameof(pollingService));

        _sessionService.LoadAll();
        RefreshSessions();

        _pollingService.NewAgentMessage += OnNewAgentMessage;
        _pollingService.NotificationRequested += OnNotificationRequested;
    }

    // ─── Observable Properties ───

    private ObservableCollection<ChatSession> _sessions = new();
    public ObservableCollection<ChatSession> Sessions
    {
        get => _sessions;
        private set { _sessions = value; OnPropertyChanged(); }
    }

    private ObservableCollection<ChatMessageDisplay> _messages = new();
    public ObservableCollection<ChatMessageDisplay> Messages
    {
        get => _messages;
        private set { _messages = value; OnPropertyChanged(); }
    }

    private string _inputText = "";
    public string InputText
    {
        get => _inputText;
        set { _inputText = value; OnPropertyChanged(); }
    }

    private string _chatTitle = "OpenClaw";
    public string ChatTitle
    {
        get => _chatTitle;
        set { _chatTitle = value; OnPropertyChanged(); }
    }

    private bool _isWaiting;
    public bool IsWaiting
    {
        get => _isWaiting;
        set { _isWaiting = value; OnPropertyChanged(); }
    }

    private string _statusText = "Подключение...";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); }
    }

    private ChatSession? _selectedSession;
    public ChatSession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (_selectedSession != value)
            {
                _selectedSession = value;
                OnPropertyChanged();
                OnSessionChanged();
            }
        }
    }

    // ─── Публичные методы ───

    public async Task CheckConnectionAsync()
    {
        StatusText = "Проверка соединения...";
        try
        {
            var ok = await _gatewayService.CheckConnectionAsync();
            IsConnected = ok;
            StatusText = ok ? "🟢 В сети" : "🔴 Нет соединения";
            if (ok)
            {
                _pollingService.Start();
            }
        }
        catch
        {
            IsConnected = false;
            StatusText = "🔴 Ошибка соединения";
        }
    }

    public async Task SendMessageAsync()
    {
        var text = InputText?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        if (!EnsureSession()) return;

        // Не даём отправить дважды одновременно
        if (!await _sendLock.WaitAsync(0))
            return;

        try
        {
            var userMsg = new ChatMessage
            {
                Sender = ChatMessage.SenderType.User,
                Text = text,
                Timestamp = DateTime.UtcNow,
                Status = ChatMessage.DeliveryStatus.Sending
            };

            _sessionService.AddMessage(userMsg);
            InputText = "";
            RefreshMessages();

            // Статус → Sent
            userMsg.Status = ChatMessage.DeliveryStatus.Sent;
            RefreshMessages();

            // Отправляем на сервер
            IsWaiting = true;
            StatusText = "⏳ Отправка...";

            var response = await _gatewayService.SendMessageAsync(
                _selectedSession!.SessionId, text, _globalCts.Token);

            userMsg.Status = ChatMessage.DeliveryStatus.Delivered;

            var agentMsg = new ChatMessage
            {
                Sender = ChatMessage.SenderType.Agent,
                Text = response.Text,
                Timestamp = DateTime.UtcNow,
                Status = ChatMessage.DeliveryStatus.Delivered,
                Attachments = response.Attachments,
                InlineButtons = response.InlineButtons
            };

            _sessionService.AddMessage(agentMsg);

            // Автоскачивание
            _ = AutoDownloadFilesAsync(response.Text);

            StatusText = IsConnected ? "🟢 В сети" : "🔴 Нет соединения";
            RefreshMessages();
            _sessionService.SaveAll();

            _pollingService.ResetPolling();
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusText = $"🔴 {ex.Message}";
            IsConnected = false;
        }
        catch (OperationCanceledException)
        {
            StatusText = "🔴 Отменено";
        }
        catch (HttpRequestException ex)
        {
            StatusText = $"🔴 {ex.Message}";
            IsConnected = false;
        }
        catch (Exception ex)
        {
            StatusText = $"🔴 Ошибка: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[SendMessage] {ex}");
        }
        finally
        {
            IsWaiting = false;
            _sendLock.Release();
        }
    }

    public async Task SendInlineButtonClick(string callbackData)
    {
        if (string.IsNullOrWhiteSpace(callbackData)) return;
        InputText = callbackData;
        await SendMessageAsync();
    }

    public async Task AttachAndSendFile(string filePath, string? caption)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        if (!EnsureSession()) return;

        if (!await _sendLock.WaitAsync(0)) return;

        try
        {
            IsWaiting = true;
            StatusText = "⏳ Загрузка файла...";

            await _gatewayService.SendFileAsync(
                _selectedSession!.SessionId, filePath, caption, _globalCts.Token);

            var fileName = Path.GetFileName(filePath);
            var msg = new ChatMessage
            {
                Sender = ChatMessage.SenderType.User,
                Text = $"📎 {fileName}",
                Timestamp = DateTime.UtcNow,
                Status = ChatMessage.DeliveryStatus.Delivered
            };

            _sessionService.AddMessage(msg);
            RefreshMessages();
            _sessionService.SaveAll();
            _pollingService.ResetPolling();

            StatusText = IsConnected ? "🟢 В сети" : "🔴 Нет соединения";
        }
        catch (FileNotFoundException ex)
        {
            StatusText = $"🔴 {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusText = $"🔴 Ошибка загрузки: {ex.Message}";
        }
        finally
        {
            IsWaiting = false;
            _sendLock.Release();
        }
    }

    public void CreateNewSession(string displayName)
    {
        var session = _sessionService.CreateSession(
            $"session_{Guid.NewGuid():N}",
            string.IsNullOrWhiteSpace(displayName) ? "Новый чат" : displayName);
        RefreshSessions();
        SelectedSession = session;
    }

    public void DeleteCurrentSession()
    {
        if (_selectedSession == null) return;
        if (_sessions.Count <= 1) return;

        var sessionId = _selectedSession.SessionId;
        _sessionService.DeleteSession(sessionId);
        RefreshSessions();
        SelectedSession = _sessions.FirstOrDefault();
        RefreshMessages();
        ChatTitle = _selectedSession?.DisplayName ?? "OpenClaw";
        _sessionService.SaveAll();
    }

    // ─── Приватные методы ───

    private bool EnsureSession()
    {
        if (_selectedSession != null) return true;

        _sessionService.CreateSession(
            $"session_{Guid.NewGuid():N}",
            $"Чат {DateTime.Now:HH:mm}");
        RefreshSessions();
        SelectedSession = _sessions.FirstOrDefault();
        return _selectedSession != null;
    }

    private async Task AutoDownloadFilesAsync(string text)
    {
        var config = App.ConfigService.Config;
        if (!config.AutoDownloadImages && !config.AutoDownloadDocuments) return;
        if (string.IsNullOrWhiteSpace(config.DownloadPath)) return;

        try
        {
            await _fileService.DownloadFilesFromTextAsync(text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoDownload] {ex.Message}");
        }
    }

    private void OnNewAgentMessage(object? sender, ChatMessage msg)
    {
        if (msg.Sender == ChatMessage.SenderType.Agent)
        {
            RefreshMessages();
            _sessionService.SaveAll();
        }
    }

    private void OnNotificationRequested(object? sender, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _notificationService.ShowNotification("OpenClaw", message);
        }
    }

    private void OnSessionChanged()
    {
        ChatTitle = _selectedSession?.DisplayName ?? "OpenClaw";

        if (_selectedSession != null)
        {
            _sessionService.ActivateSession(_selectedSession.SessionId);
        }

        RefreshMessages();
    }

    private void RefreshSessions()
    {
        Sessions = new ObservableCollection<ChatSession>(_sessionService.Sessions);
    }

    private void RefreshMessages()
    {
        var session = _selectedSession;
        if (session == null)
        {
            Messages = new ObservableCollection<ChatMessageDisplay>();
            return;
        }

        var displayMessages = session.Messages
            .Select(ChatMessageDisplay.FromMessage)
            .ToList();

        Messages = new ObservableCollection<ChatMessageDisplay>(displayMessages);
    }

    // ─── INotifyPropertyChanged ───

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
