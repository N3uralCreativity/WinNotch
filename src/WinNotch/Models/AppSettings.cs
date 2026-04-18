using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinNotch.Models;

/// <summary>
/// Hover interaction mode for the notch.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HoverMode
{
    /// <summary>Hover slightly expands, click fully opens.</summary>
    HoverPeekClickOpen,
    /// <summary>Long hover fully opens, click also opens.</summary>
    LongHoverOpen
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppTheme
{
    Dark,
    Light,
    Auto
}

/// <summary>
/// User-configurable settings, persisted to %AppData%/WinNotch/settings.json.
/// </summary>
public class AppSettings
{
    // General
    public bool StartOnBoot { get; set; }
    public HoverMode HoverMode { get; set; } = HoverMode.HoverPeekClickOpen;
    public int HoverOpenDelayMs { get; set; } = 200;
    public int HoverCloseDelayMs { get; set; } = 150;
    public int LongHoverDelayMs { get; set; } = 600;

    // Appearance
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public double CornerRadiusScale { get; set; } = 1.0;
    public bool ShowShadow { get; set; } = true;

    // Music
    public bool ShowMusicControls { get; set; } = true;
    public bool ShowVisualizer { get; set; } = true;

    // HUD
    public bool ShowVolumeHud { get; set; } = true;
    public bool ShowBrightnessHud { get; set; } = true;

    // Calendar
    public bool ShowCalendar { get; set; } = true;

    // Battery
    public bool ShowBattery { get; set; } = true;

    // Webcam
    public bool ShowWebcam { get; set; } = false;
    public int WebcamFps { get; set; } = 15;

    // Beta features
    public bool UseLiquidGlassTheme { get; set; } = false;

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinNotch");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    /// <summary>Register or unregister the app from Windows startup.</summary>
    public void ApplyStartOnBoot()
    {
        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "WinNotch";

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (key == null) return;

            if (StartOnBoot)
            {
                var exePath = Environment.ProcessPath;
                if (exePath != null)
                    key.SetValue(valueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(valueName, false);
            }
        }
        catch { }
    }
}
