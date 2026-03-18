using System.IO;
using Microsoft.UI.Xaml;

namespace UsbBridge;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }
    public static bool HandleClosedEvents { get; set; } = true;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UsbBridge", "crash.log");

    public App()
    {
        UnhandledException += (_, e) =>
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now}] UNHANDLED: {e.Exception}\n");
            if (HandleClosedEvents)
                e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var config = ConfigService.Load();

            MainWindow = new MainWindow();
            MainWindow.Activate();

            if (config.StartMinimized)
                MainWindow.AppWindow.Hide();
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now}] CRASH: {ex}\n");
        }
    }
}
