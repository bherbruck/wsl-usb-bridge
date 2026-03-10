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
            ExpandPriResources();
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

    /// <summary>
    /// Extracts embedded file resources (XBF, XAML) from SDK PRI files to loose files.
    /// This replaces the ExpandPriContent MSBuild task which requires VS build tools.
    /// </summary>
    private static void ExpandPriResources()
    {
        var baseDir = AppContext.BaseDirectory;
        var priFiles = new[] { "Microsoft.UI.pri", "Microsoft.UI.Xaml.Controls.pri" };

        foreach (var priFile in priFiles)
        {
            var priPath = Path.Combine(baseDir, priFile);
            if (!File.Exists(priPath)) continue;

            int hr = MrmCreateResourceManager(priPath, out var manager);
            if (hr != 0) continue;

            try
            {
                ExpandResourceMap(manager, IntPtr.Zero, baseDir);
            }
            finally
            {
                MrmDestroyResourceManager(manager);
            }
        }
    }

    private static void ExpandResourceMap(IntPtr manager, IntPtr map, string baseDir)
    {
        MrmGetResourceCount(manager, map, out uint count);

        for (uint i = 0; i < count; i++)
        {
            var data = new MrmResourceData();
            int hr = MrmLoadStringOrEmbeddedResourceByIndex(
                manager, IntPtr.Zero, map, i,
                out var type, out var name, out _, out data);

            if (hr != 0 || type != MrmType.Embedded || data.size == 0 || data.data == IntPtr.Zero)
                continue;

            // Resource names look like "Files/Microsoft.UI.Xaml/Themes/themeresources.xbf"
            if (name == null || !name.StartsWith("Files/", StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = name["Files/".Length..].Replace('/', Path.DirectorySeparatorChar);
            var outputPath = Path.Combine(baseDir, relativePath);

            if (File.Exists(outputPath)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var bytes = new byte[data.size];
            Marshal.Copy(data.data, bytes, 0, (int)data.size);
            File.WriteAllBytes(outputPath, bytes);
        }
    }

    private enum MrmType
    {
        Unknown = 0,
        String = 1,
        Path = 2,
        Embedded = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MrmResourceData
    {
        public uint size;
        public IntPtr data;
    }

    private delegate bool ConsoleCtrlHandler(int sig);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handler, bool add);

    [DllImport("MRM.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MrmCreateResourceManager(string priPath, out IntPtr manager);

    [DllImport("MRM.dll", ExactSpelling = true)]
    private static extern int MrmDestroyResourceManager(IntPtr manager);

    [DllImport("MRM.dll", ExactSpelling = true)]
    private static extern int MrmGetResourceCount(IntPtr manager, IntPtr map, out uint count);

    [DllImport("MRM.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MrmLoadStringOrEmbeddedResourceByIndex(
        IntPtr manager,
        IntPtr context,
        IntPtr map,
        uint index,
        out MrmType type,
        [MarshalAs(UnmanagedType.LPWStr)] out string? name,
        [MarshalAs(UnmanagedType.LPWStr)] out string? stringValue,
        out MrmResourceData data);
}
