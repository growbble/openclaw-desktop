using Microsoft.UI.Xaml;
using OpenClawClient.Services;
using OpenClawClient.ViewModels;
using OpenClawClient.Views;

namespace OpenClawClient;

/// <summary>
/// Точка входа WinUI 3 приложения.
/// Все сервисы инициализируются как статические поля (simplified DI).
/// </summary>
public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }
    public static Microsoft.UI.Dispatching.DispatcherQueue? DispatcherQueue { get; private set; }

    // Глобальные сервисы
    public static ConfigService ConfigService { get; } = new();
    public static SessionService SessionService { get; } = new(ConfigService);
    public static OpenClawGatewayService GatewayService { get; } = new(ConfigService);
    public static FileDownloadService FileService { get; } = new(ConfigService);
    public static NotificationService NotificationService { get; } = new(ConfigService);
    public static PollingService PollingService { get; } = new(GatewayService, SessionService);
    public static MainViewModel ViewModel { get; } = new(
        GatewayService, SessionService, FileService, NotificationService, PollingService);

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            MainWindowInstance = new MainWindow();
            MainWindowInstance.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App.OnLaunched] {ex}");
            // WinUI 3 выбросит исключение при старте если нет Windows App SDK
            throw;
        }
    }
}
