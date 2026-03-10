using System.Collections.ObjectModel;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

#pragma warning disable MVVMTK0045 // WinRT AOT compat — not needed for this desktop app

namespace UsbBridge;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b ? !b : value;
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is bool b ? !b : value;
}

public class NonEmptyToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility v ? v == Visibility.Visible : false;
}

public class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && hex.StartsWith('#') && hex.Length == 7)
        {
            var r = System.Convert.ToByte(hex.Substring(1, 2), 16);
            var g = System.Convert.ToByte(hex.Substring(3, 2), 16);
            var b = System.Convert.ToByte(hex.Substring(5, 2), 16);
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
        }
        return new SolidColorBrush(Colors.Gray);
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public partial class DeviceViewModel : ObservableObject
{
    [ObservableProperty] private string busId = "";
    [ObservableProperty] private string vidPid = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string status = "Disconnected";
    [ObservableProperty] private string statusColor = "#95a5a6";
    [ObservableProperty] private bool hasRule;
    [ObservableProperty] private bool isExpanded;
}

public partial class RuleViewModel : ObservableObject
{
    [ObservableProperty] private string pattern = "";
    [ObservableProperty] private string label = "";
    [ObservableProperty] private bool enabled = true;
    [ObservableProperty] private bool forceBind = true;

    public ForwardRule ToModel() => new()
    {
        Pattern = Pattern, Label = Label, Enabled = Enabled, ForceBind = ForceBind
    };

    public static RuleViewModel From(ForwardRule r) => new()
    {
        Pattern = r.Pattern, Label = r.Label, Enabled = r.Enabled, ForceBind = r.ForceBind
    };
}

public partial class MainViewModel : ObservableObject
{
    private readonly UsbIpdService _svc = new();
    private readonly DispatcherQueue _dispatcherQueue;
    private AppConfig _config;

    private string? _expandedBusId;

    [ObservableProperty] private bool isRunning = true;
    [ObservableProperty] private bool startWithWindows;
    [ObservableProperty] private bool startMinimized;
    [ObservableProperty] private string newPattern = "";
    [ObservableProperty] private string newLabel = "";

    public ObservableCollection<DeviceViewModel> ForwardedDevices { get; } = [];
    public ObservableCollection<DeviceViewModel> OtherDevices { get; } = [];
    public ObservableCollection<RuleViewModel> Rules { get; } = [];
    public ObservableCollection<string> Log { get; } = [];

    public bool IsForwardedEmpty => ForwardedDevices.Count == 0;
    public bool IsOtherEmpty => OtherDevices.Count == 0;
    public bool IsRulesEmpty => Rules.Count == 0;

    public MainViewModel()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ForwardedDevices.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsForwardedEmpty));
        OtherDevices.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsOtherEmpty));
        Rules.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsRulesEmpty));

        _config = ConfigService.Load();
        startWithWindows = _config.StartWithWindows;
        startMinimized = _config.StartMinimized;
        foreach (var r in _config.Rules) Rules.Add(RuleViewModel.From(r));

        _svc.OnAttached += e => Dispatch(() => AddLog($"✅ {e.Message}"));
        _svc.OnDetached += e => Dispatch(() => AddLog($"⬅ {e.Message}"));
        _svc.OnFailed += e => Dispatch(() => AddLog($"❌ {e.Message}"));
        _svc.OnDevicesUpdated += devices => Dispatch(() => UpdateDevices(devices));

        SaveConfig();
        _svc.Start(_config);
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        _config = _config with { StartWithWindows = value };
        ConfigService.Save(_config);
        SetAutoStart(value);
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        _config = _config with { StartMinimized = value };
        ConfigService.Save(_config);
    }

    private static void SetAutoStart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (key is null) return;

        if (enable)
        {
            var exePath = Environment.ProcessPath;
            if (exePath is not null)
                key.SetValue("UsbBridge", $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue("UsbBridge", throwOnMissingValue: false);
        }
    }

    partial void OnIsRunningChanged(bool value)
    {
        if (value)
        {
            SaveConfig();
            _svc.Start(_config);
        }
        else
        {
            _svc.Stop();
        }
    }

    [RelayCommand]
    void AddRule()
    {
        if (string.IsNullOrWhiteSpace(NewPattern)) return;
        Rules.Add(new RuleViewModel
        {
            Pattern = NewPattern.Trim().ToLower(),
            Label = string.IsNullOrWhiteSpace(NewLabel) ? NewPattern : NewLabel.Trim(),
            Enabled = true,
            ForceBind = true
        });
        NewPattern = "";
        NewLabel = "";
        RestartIfRunning();
    }

    [RelayCommand]
    void ToggleExpand(DeviceViewModel? device)
    {
        if (device is null) return;
        bool wasExpanded = device.IsExpanded;
        foreach (var d in ForwardedDevices) d.IsExpanded = false;
        foreach (var d in OtherDevices) d.IsExpanded = false;
        device.IsExpanded = !wasExpanded;
        _expandedBusId = device.IsExpanded ? device.BusId : null;
    }

    [RelayCommand]
    async Task BridgeDevice(DeviceViewModel? device)
    {
        if (device is null || string.IsNullOrEmpty(device.BusId)) return;
        await UsbIpdService.BridgeOnce(device.BusId, true, _config.WslDistribution);
    }

    [RelayCommand]
    void ToggleDeviceRule(DeviceViewModel? device)
    {
        if (device is null || string.IsNullOrEmpty(device.VidPid)) return;

        // Only manage exact vid:pid rules; wildcard rules are managed via the Rules form
        var exactRule = Rules.FirstOrDefault(r => r.Pattern == device.VidPid);
        if (exactRule is not null)
        {
            Rules.Remove(exactRule);
            device.HasRule = Rules.Any(r => UsbIpdService.GlobMatch(device.VidPid, r.Pattern));
        }
        else
        {
            Rules.Add(new RuleViewModel
            {
                Pattern = device.VidPid,
                Label = device.Description,
                Enabled = true,
                ForceBind = true
            });
            device.HasRule = true;
        }
        RestartIfRunning();
    }

    [RelayCommand]
    void RemoveRule(RuleViewModel? rule)
    {
        if (rule is null) return;
        Rules.Remove(rule);
        RestartIfRunning();
    }

    void RestartIfRunning()
    {
        SaveConfig();
        if (!IsRunning) return;
        _svc.Stop();
        _svc.Start(_config);
    }

    void SaveConfig()
    {
        _config = _config with { Rules = Rules.Select(r => r.ToModel()).ToList() };
        ConfigService.Save(_config);
    }

    void UpdateDevices(List<UsbDeviceState> devices)
    {
        var rulePatterns = Rules.Select(r => r.Pattern).ToList();
        var incoming = devices
            .Where(d => d.IsConnected && d.VidPid is not null && d.BusId is not null)
            .ToDictionary(d => d.BusId!);
        var incomingIds = incoming.Keys.ToHashSet();

        // Remove stale devices
        for (int i = ForwardedDevices.Count - 1; i >= 0; i--)
            if (!incomingIds.Contains(ForwardedDevices[i].BusId))
                ForwardedDevices.RemoveAt(i);
        for (int i = OtherDevices.Count - 1; i >= 0; i--)
            if (!incomingIds.Contains(OtherDevices[i].BusId))
                OtherDevices.RemoveAt(i);

        foreach (var (busId, d) in incoming)
        {
            var hasRule = rulePatterns.Any(p => UsbIpdService.GlobMatch(d.VidPid!, p));
            var status = d.IsAttached ? "Attached" : d.IsBound ? "Shared" : "";
            var color = d.IsAttached ? "#2ecc71" : d.IsBound ? "#f39c12" : "#95a5a6";
            var target = hasRule ? ForwardedDevices : OtherDevices;
            var other = hasRule ? OtherDevices : ForwardedDevices;

            // Find existing VM in either collection
            var vm = target.FirstOrDefault(x => x.BusId == busId)
                  ?? other.FirstOrDefault(x => x.BusId == busId);

            if (vm is not null)
            {
                // Update properties in place
                vm.VidPid = d.VidPid ?? "";
                vm.Description = d.Description ?? "Unknown";
                vm.Status = status;
                vm.StatusColor = color;
                vm.HasRule = hasRule;

                // Move between collections if group changed
                if (other.Remove(vm))
                    target.Add(vm);
            }
            else
            {
                // New device
                target.Add(new DeviceViewModel
                {
                    BusId = busId,
                    VidPid = d.VidPid ?? "",
                    Description = d.Description ?? "Unknown",
                    Status = status,
                    StatusColor = color,
                    HasRule = hasRule,
                    IsExpanded = busId == _expandedBusId
                });
            }
        }
    }

    void AddLog(string msg)
    {
        Log.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
        while (Log.Count > 100) Log.RemoveAt(Log.Count - 1);
    }

    void Dispatch(Action action) => _dispatcherQueue.TryEnqueue(() => action());

    public void Cleanup() => _svc.Stop();
}
