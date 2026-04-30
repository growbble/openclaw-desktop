using Microsoft.UI.Xaml;

namespace OpenClawClient.Views;

public sealed partial class MainWindow : Window
{
    internal Grid LayoutRoot => MainGrid;

    public MainWindow()
    {
        this.InitializeComponent();

        // Настройка окна
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Title = "OpenClaw Desktop";
        appWindow.Resize(new Windows.Graphics.SizeInt32(960, 680));

        // Центрирование на экране
        try
        {
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                var wa = displayArea.WorkArea;
                appWindow.Move(new Windows.Graphics.PointInt32(
                    (wa.Width - 960) / 2,
                    (wa.Height - 680) / 2));
            }
        }
        catch
        {
            // Не критично если центрирование не удалось
        }

        // Асинхронная проверка соединения при старте
        _ = App.ViewModel.CheckConnectionAsync();
    }
}
