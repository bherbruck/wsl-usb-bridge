using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

#pragma warning disable MVVMTK0045, MVVMTK0050

namespace UsbBridge;

[ObservableObject]
public sealed partial class TrayIconView : UserControl
{
    public TrayIconView()
    {
        InitializeComponent();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        TrayIcon.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath));
        TrayIcon.ForceCreate();
    }

    [RelayCommand]
    private void ShowWindow()
    {
        App.MainWindow?.Activate();
    }

    [RelayCommand]
    private void ExitApplication()
    {
        App.HandleClosedEvents = false;

        try
        {
            if (App.MainWindow?.Content is FrameworkElement { DataContext: MainViewModel vm })
                vm.Cleanup();
        }
        catch { }

        TrayIcon.Dispose();
        App.MainWindow?.Close();
    }
}
