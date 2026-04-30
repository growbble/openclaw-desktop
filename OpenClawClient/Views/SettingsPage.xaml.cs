using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OpenClawClient.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        this.InitializeComponent();
        LoadConfig();
    }

    private void LoadConfig()
    {
        var cfg = App.ConfigService.Config;
        ServerUrlBox.Text = cfg.ServerUrl;
        AuthTokenBox.Password = cfg.AuthToken;
        AgentIdBox.Text = cfg.AgentId;
        DownloadPathBox.Text = cfg.DownloadPath;
        AutoImagesCheck.IsChecked = cfg.AutoDownloadImages;
        AutoDocsCheck.IsChecked = cfg.AutoDownloadDocuments;
        MaxSizeBox.Text = (cfg.MaxAutoDownloadSize / (1024 * 1024)).ToString();
        NotificationsSwitch.IsOn = cfg.NotificationsEnabled;
    }

    private async void OnBrowseFolder(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
        picker.FileTypeFilter.Add("*");

        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
            DownloadPathBox.Text = folder.Path;
    }

    private async void OnCheckConnection(object sender, RoutedEventArgs e)
    {
        // Временно применяем настройки
        var cfg = App.ConfigService.Config;
        cfg.ServerUrl = ServerUrlBox.Text;
        cfg.AuthToken = AuthTokenBox.Password;
        cfg.AgentId = AgentIdBox.Text;
        App.GatewayService.ApplyAuth();

        StatusText.Text = "⏳ Проверка соединения...";
        var ok = await App.GatewayService.CheckConnectionAsync();
        StatusText.Text = ok ? "✅ Подключено к серверу" : "❌ Ошибка соединения";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var cfg = App.ConfigService.Config;
        cfg.ServerUrl = ServerUrlBox.Text;
        cfg.AuthToken = AuthTokenBox.Password;
        cfg.AgentId = AgentIdBox.Text;
        cfg.DownloadPath = DownloadPathBox.Text;
        cfg.AutoDownloadImages = AutoImagesCheck.IsChecked ?? true;
        cfg.AutoDownloadDocuments = AutoDocsCheck.IsChecked ?? true;

        // Валидация MaxSizeBox
        if (long.TryParse(MaxSizeBox.Text, out var mb) && mb > 0)
            cfg.MaxAutoDownloadSize = mb * 1024 * 1024;
        else
            cfg.MaxAutoDownloadSize = 20L * 1024 * 1024; // default 20 MB

        cfg.NotificationsEnabled = NotificationsSwitch.IsOn;

        App.ConfigService.Save();
        App.GatewayService.ApplyAuth();
        NavigateBack();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        NavigateBack();
    }

    private static void NavigateBack()
    {
        // Ищем MainChatPage в визуальном дереве и вызываем CloseSettings
        var window = App.MainWindowInstance;
        if (window?.Content is MainChatPage chatPage)
        {
            chatPage.CloseSettings();
        }
        else
        {
            // Fallback: ищем Frame в MainGrid
            var root = window?.LayoutRoot;
            if (root == null) return;

            for (int i = root.Children.Count - 1; i >= 0; i--)
            {
                if (root.Children[i] is Frame)
                {
                    root.Children.RemoveAt(i);
                    break;
                }
            }
        }
    }
}
