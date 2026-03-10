using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace UsbBridge;

// --- usbipd state JSON mapping ---

public record UsbIpdState(
    [property: JsonPropertyName("Devices")] List<UsbDeviceState> Devices
);

public record UsbDeviceState(
    [property: JsonPropertyName("BusId")] string? BusId,
    [property: JsonPropertyName("ClientIPAddress")] string? ClientIPAddress,
    [property: JsonPropertyName("Description")] string? Description,
    [property: JsonPropertyName("InstanceId")] string? InstanceId,
    [property: JsonPropertyName("IsForced")] bool IsForced,
    [property: JsonPropertyName("PersistedGuid")] string? PersistedGuid,
    [property: JsonPropertyName("StubInstanceId")] string? StubInstanceId
)
{
    public bool IsConnected => BusId is not null;
    public bool IsBound => PersistedGuid is not null;
    public bool IsAttached => ClientIPAddress is not null;

    public string? VidPid
    {
        get
        {
            if (InstanceId is null) return null;
            var m = Regex.Match(InstanceId, @"VID_([0-9A-Fa-f]{4})&PID_([0-9A-Fa-f]{4})");
            return m.Success ? $"{m.Groups[1].Value}:{m.Groups[2].Value}".ToLower() : null;
        }
    }
}

// --- App config ---

public record ForwardRule
{
    public string Pattern { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool ForceBind { get; set; } = true;
}

public record AppConfig
{
    public List<ForwardRule> Rules { get; set; } = [];
    public string? WslDistribution { get; set; }
    public int PollMs { get; set; } = 500;
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = true;
}
