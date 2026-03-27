using System.Text.Json;

namespace VaderLink.Config;

/// <summary>
/// Loads and saves <see cref="AppConfig"/> as a JSON file at
/// %APPDATA%\VaderLink\config.json.
/// </summary>
public static class ConfigManager
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private static string ConfigPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VaderLink",
            "config.json");

    /// <summary>
    /// Loads config from disk. Returns defaults if the file does not exist.
    /// Silently falls back to defaults on any parse error.
    /// </summary>
    public static AppConfig Load()
    {
        var path = ConfigPath;
        if (!File.Exists(path))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions)
                   ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    /// <summary>Persists config to disk, creating the directory if needed.</summary>
    public static void Save(AppConfig config)
    {
        var path = ConfigPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(path, json);
    }

    // ── Windows autostart ─────────────────────────────────────────────────────

    private const string RunKeyPath    = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName  = "VaderLink";

    public static void SetStartWithWindows(bool enable)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null) return;

        if (enable)
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath is not null)
                key.SetValue(RunValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }
}
