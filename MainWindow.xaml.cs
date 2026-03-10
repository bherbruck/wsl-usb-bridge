using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace UsbBridge;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.Resize(new SizeInt32(500, 650));
        CenterOnScreen();
    }

    private void CenterOnScreen()
    {
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        var x = (workArea.Width - 500) / 2 + workArea.X;
        var y = (workArea.Height - 650) / 2 + workArea.Y;
        AppWindow.Move(new PointInt32(x, y));
    }
}
