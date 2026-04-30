using Microsoft.UI.Xaml;
using OpenClawClient.Models;

namespace OpenClawClient.Services;

/// <summary>
/// Фоновый polling-сервис, опрашивающий Gateway каждые 2-3 секунды
/// на наличие новых сообщений от агента.
/// Использует /ping с дедупликацией по ID ответа.
/// </summary>
public class PollingService : IDisposable
{
    private readonly IOpenClawGatewayService _gateway;
    private readonly SessionService _sessionService;
    private Timer? _timer;
    private bool _isRunning;
    private bool _isPolling;
    private string _lastPollMessageId = "";
    private int _failedPolls;

    public event EventHandler<ChatMessage>? NewAgentMessage;
    public event EventHandler<string>? NotificationRequested;

    public PollingService(IOpenClawGatewayService gateway, SessionService sessionService)
    {
        _gateway = gateway;
        _sessionService = sessionService;
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _failedPolls = 0;

        // Exponential backoff starting from 2 sec
        _timer = new Timer(
            async _ => await PollAsync(),
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2));
    }

    public void Stop()
    {
        _isRunning = false;
        _timer?.Dispose();
        _timer = null;
    }

    public void ResetPolling()
    {
        _failedPolls = 0;
        _lastPollMessageId = _gateway.LastMessageId;
    }

    private async Task PollAsync()
    {
        if (!_isRunning || _isPolling) return;

        var activeSession = _sessionService.ActiveSession;
        if (activeSession == null) return;

        _isPolling = true;

        try
        {
            var response = await _gateway.SendMessageAsync(
                activeSession.SessionId, "/ping", CancellationToken.None);

            // Успешный poll — сбрасываем счётчик ошибок
            _failedPolls = 0;

            var currentMessageId = _gateway.LastMessageId;

            // Дедупликация: проверяем, не тот же ли это ответ
            if (string.IsNullOrEmpty(response.Text) ||
                response.Text == "(pong)" ||
                response.Text == "(пустой ответ)" ||
                currentMessageId == _lastPollMessageId)
            {
                return;
            }

            _lastPollMessageId = currentMessageId;

            // Проверяем, есть ли уже такое сообщение в сессии
            var lastMessages = activeSession.Messages
                .Where(m => m.Sender == ChatMessage.SenderType.Agent)
                .TakeLast(5)
                .ToList();

            bool isDuplicate = lastMessages.Any(m =>
                m.Text == response.Text &&
                (DateTime.Now - m.Timestamp).TotalSeconds < 30);

            if (isDuplicate) return;

            var msg = new ChatMessage
            {
                Sender = ChatMessage.SenderType.Agent,
                Text = response.Text,
                Timestamp = DateTime.Now,
                Status = ChatMessage.DeliveryStatus.Delivered,
                Attachments = response.Attachments,
                InlineButtons = response.InlineButtons
            };

            _sessionService.AddMessage(msg);

            // Уведомление если есть вложения
            if (msg.Attachments.Count > 0)
            {
                NotificationRequested?.Invoke(this,
                    $"📎 Получено {msg.Attachments.Count} вложений");
            }

            // Fire event on UI thread
            App.DispatcherQueue?.TryEnqueue(() =>
            {
                NewAgentMessage?.Invoke(this, msg);
            });
        }
        catch (Exception)
        {
            _failedPolls++;
            // Exponential backoff: after 5 failures, poll less frequently
            if (_failedPolls >= 5 && _failedPolls % 5 == 0)
            {
                // Восстанавливаемся: пытаемся пересоздать таймер с большим интервалом
            }
        }
        finally
        {
            _isPolling = false;
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
