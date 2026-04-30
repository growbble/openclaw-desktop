using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenClawClient.Models;
using OpenClawClient.ViewModels;

namespace OpenClawClient.Views;

public sealed partial class MainChatPage : Page
{
    internal MainViewModel ViewModel { get; }
    private Frame? _settingsFrame;

    public MainChatPage()
    {
        this.InitializeComponent();
        ViewModel = App.ViewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InputBox.Focus(FocusState.Programmatic);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsConnected))
        {
            // Обновляем цвет индикатора статуса
            App.DispatcherQueue?.TryEnqueue(() =>
            {
                StatusIndicator.Fill = ViewModel.IsConnected
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            });
        }
        else if (e.PropertyName == nameof(MainViewModel.Messages))
        {
            // Автоскролл вниз при новых сообщениях
            App.DispatcherQueue?.TryEnqueue(() =>
            {
                MessageScroller.ChangeView(null, MessageScroller.ScrollableHeight, null);
            });
        }
    }

    private void OnSendClick(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.SendMessageAsync();
    }

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            _ = ViewModel.SendMessageAsync();
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_settingsFrame != null)
        {
            // Уже открыто
            return;
        }

        _settingsFrame = new Frame();
        _settingsFrame.Navigate(typeof(SettingsPage));
        var root = GetRootGrid();
        if (root != null)
        {
            Grid.SetRowSpan(_settingsFrame, 3);
            root.Children.Add(_settingsFrame);
        }
    }

    public void CloseSettings()
    {
        if (_settingsFrame == null) return;

        var root = GetRootGrid();
        if (root != null && root.Children.Contains(_settingsFrame))
        {
            root.Children.Remove(_settingsFrame);
        }
        _settingsFrame = null;
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.Close();
    }

    private async void OnAttachFileClick(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await ViewModel.AttachAndSendFile(file.Path, $"📎 {file.Name}");
            InputBox.Focus(FocusState.Programmatic);
        }
    }

    private void OnNewSessionClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CreateNewSession($"Чат {DateTime.Now:HH:mm}");
    }

    private async void OnInlineButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is InlineButton inlineBtn)
        {
            await ViewModel.SendInlineButtonClick(inlineBtn.CallbackData);
        }
    }

    private async void OnDownloadAttachment(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Attachment attachment)
        {
            var downloadPath = App.ConfigService.Config.DownloadPath;
            if (string.IsNullOrEmpty(downloadPath))
                downloadPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", "OpenClaw");

            var progress = new Progress<double>(_ => { });
            try
            {
                var localPath = await App.GatewayService.DownloadFileAsync(
                    attachment.RemoteUrl, downloadPath, progress, CancellationToken.None);
                attachment.LocalPath = localPath;
                attachment.IsDownloaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Download error: {ex.Message}");
            }
        }
    }

    private Grid? GetRootGrid()
    {
        return App.MainWindowInstance?.LayoutRoot;
    }
}
