using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UsbBridge;

public record DeviceEvent(UsbDeviceState Device, string Message);

public class UsbIpdService
{
    private CancellationTokenSource? _cts;
    private readonly HashSet<string> _attachedBusIds = [];

    public event Action<DeviceEvent>? OnAttached;
    public event Action<DeviceEvent>? OnDetached;
    public event Action<DeviceEvent>? OnFailed;
    public event Action<List<UsbDeviceState>>? OnDevicesUpdated;

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public void Start(AppConfig config)
    {
        Stop();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _ = Task.Run(() => PollLoop(config, ct), ct);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _attachedBusIds.Clear();
    }

    private async Task PollLoop(AppConfig config, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var state = await GetState(ct);
                if (state?.Devices is null) { await Delay(config.PollMs, ct); continue; }

                OnDevicesUpdated?.Invoke(state.Devices);

                var rules = config.Rules.Where(r => r.Enabled).ToList();

                foreach (var dev in state.Devices)
                {
                    if (ct.IsCancellationRequested) break;
                    if (dev.VidPid is null || !dev.IsConnected) continue;
                    if (!MatchesAny(dev.VidPid, rules)) continue;

                    var busId = dev.BusId!;
                    var force = rules.Any(r => GlobMatch(dev.VidPid, r.Pattern) && r.ForceBind);

                    if (_attachedBusIds.Contains(busId) && dev.IsAttached) continue;
                    if (dev.IsAttached) { _attachedBusIds.Add(busId); continue; }

                    // Settle
                    await Delay(500, ct);

                    await Run($"detach --busid {busId}", ct);
                    await Run($"bind --busid {busId}{(force ? " --force" : "")}", ct);
                    await Delay(500, ct);

                    var args = $"attach --wsl --busid {busId}";
                    if (!string.IsNullOrEmpty(config.WslDistribution))
                        args += $" --distribution {config.WslDistribution}";

                    await Run(args, ct);
                    await Delay(500, ct);

                    // Verify
                    var check = await GetState(ct);
                    var result = check?.Devices?.FirstOrDefault(d => d.BusId == busId);

                    if (result?.IsAttached == true)
                    {
                        _attachedBusIds.Add(busId);
                        OnAttached?.Invoke(new(dev, $"{dev.Description} ({busId}) → WSL"));
                    }
                    else
                    {
                        OnFailed?.Invoke(new(dev, $"{dev.Description} ({busId}) failed"));
                    }
                }

                // Stale cleanup
                var current = state.Devices.Where(d => d.IsConnected).Select(d => d.BusId!).ToHashSet();
                foreach (var id in _attachedBusIds.Where(id => !current.Contains(id)).ToList())
                {
                    _attachedBusIds.Remove(id);
                    OnDetached?.Invoke(new(new UsbDeviceState(id, null, null, null, false, null, null), $"{id} disconnected"));
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Debug.WriteLine($"Poll error: {ex.Message}"); }

            await Delay(config.PollMs, ct);
        }
    }

    public static async Task BridgeOnce(string busId, bool force, string? distribution)
    {
        await Run($"bind --busid {busId}{(force ? " --force" : "")}", CancellationToken.None);
        await Task.Delay(500);
        var args = $"attach --wsl --busid {busId}";
        if (!string.IsNullOrEmpty(distribution))
            args += $" --distribution {distribution}";
        await Run(args, CancellationToken.None);
    }

    public static async Task Unbind(string busId)
    {
        await Run($"unbind --busid {busId}", CancellationToken.None);
    }

    // --- helpers ---

    static bool MatchesAny(string vidpid, List<ForwardRule> rules) =>
        rules.Any(r => GlobMatch(vidpid, r.Pattern));

    public static bool GlobMatch(string input, string pattern)
    {
        var re = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, re, RegexOptions.IgnoreCase);
    }

    static async Task<UsbIpdState?> GetState(CancellationToken ct)
    {
        var output = await Run("state", ct);
        if (string.IsNullOrWhiteSpace(output)) return null;
        try { return JsonSerializer.Deserialize<UsbIpdState>(output); }
        catch { return null; }
    }

    static async Task<string> Run(string args, CancellationToken ct)
    {
        using var proc = new Process
        {
            StartInfo = new()
            {
                FileName = "usbipd",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        proc.Start();
        var output = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        return output;
    }

    static async Task Delay(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); } catch (OperationCanceledException) { }
    }
}
