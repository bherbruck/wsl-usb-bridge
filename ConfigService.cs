using System.IO;
using System.Text.Json;

namespace UsbBridge;

public static class ConfigService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UsbBridge");

    private static readonly string FilePath = Path.Combine(Dir, "config.json");

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static AppConfig Load()
    {
        if (!File.Exists(FilePath))
        {
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }

        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<AppConfig>(json, Opts) ?? new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(config, Opts));
    }
}
