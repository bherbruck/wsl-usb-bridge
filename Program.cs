using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace UsbBridge;

class Program
{
    private static ConsoleCtrlHandler? _ctrlHandler;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UsbBridge", "crash.log");

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            _ctrlHandler = _ => { Environment.Exit(0); return true; };
            SetConsoleCtrlHandler(_ctrlHandler, true);

            Application.Start(_ =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now}] FATAL: {ex}\n");
        }
    }

    private delegate bool ConsoleCtrlHandler(int sig);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handler, bool add);
}
