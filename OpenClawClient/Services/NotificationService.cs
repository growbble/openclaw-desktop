namespace OpenClawClient.Services;

/// <summary>
/// Системные Toast-уведомления Windows.
/// Использует Microsoft.Toolkit.Uwp.Notifications.
/// </summary>
public class NotificationService
{
    private readonly ConfigService _configService;

    public NotificationService(ConfigService configService)
    {
        _configService = configService;
    }

    public void ShowNotification(string title, string message)
    {
        if (!_configService.Config.NotificationsEnabled) return;

        try
        {
            var builder = new Microsoft.Toolkit.Uwp.Notifications.ToastContentBuilder()
                .AddText(title)
                .AddText(message);

            builder.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification error: {ex.Message}");
        }
    }

    /// <summary>Проверить, доступны ли Toast-уведомления.</summary>
    public bool IsToastAvailable
    {
        get
        {
            try
            {
                // Простая проверка: если библиотека загружена — работает
                _ = typeof(Microsoft.Toolkit.Uwp.Notifications.ToastContentBuilder);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
