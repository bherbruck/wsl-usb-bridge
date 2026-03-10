using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UsbBridge;

public partial class App : Application
{
    private TaskbarIcon? _tray;
    private MainWindow? _window;
    private AppConfig _config = null!;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UsbBridge", "crash.log");

    public App()
    {
        UnhandledException += (_, e) =>
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now}] UNHANDLED: {e.Exception}\n");
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _config = ConfigService.Load();

            _tray = new TaskbarIcon
            {
                ToolTipText = "WSL USB Bridge",
                IconSource = new GeneratedIconSource
                {
                    Text = "\uE88E",
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Windows.UI.Color.FromArgb(255, 0, 120, 215)),
                    FontSize = 100,
                },
                ContextFlyout = BuildFlyout(),
                DoubleClickCommand = new RelayAction(ShowWindow)
            };
            _tray.ForceCreate();

            if (!_config.StartMinimized)
                ShowWindow();
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now}] CRASH: {ex}\n");
        }
    }

    private sealed class RelayAction(Action action) : ICommand
    {
#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }

    private MenuFlyout BuildFlyout()
    {
        var show = new MenuFlyoutItem { Text = "Show" };
        show.Click += (_, _) => ShowWindow();

        var exit = new MenuFlyoutItem { Text = "Exit" };
        exit.Click += (_, _) => ExitApp();

        var flyout = new MenuFlyout();
        flyout.Items.Add(show);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(exit);
        return flyout;
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow();
            _window.AppWindow.Closing += (_, e) =>
            {
                e.Cancel = true;
                _window.AppWindow.Hide();
            };
        }

        _window.Activate();
    }

    private void ExitApp()
    {
        try
        {
            if (_window?.Content is FrameworkElement { DataContext: MainViewModel vm })
                vm.Cleanup();
            _tray?.Dispose();
        }
        catch { }

        Process.GetCurrentProcess().Kill();
    }
}
