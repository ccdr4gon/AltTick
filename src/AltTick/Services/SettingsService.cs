using System.IO;
using System.Text.Json;
using AltTick.Models;
using Microsoft.Win32;

namespace AltTick.Services;

internal sealed class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AltTick");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    public bool IsRunAtStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue("AltTick") != null;
        }
        catch
        {
            return false;
        }
    }

    public void SetRunAtStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (exePath != null)
                    key.SetValue("AltTick", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("AltTick", false);
            }

            Settings.RunAtStartup = enable;
            Save();
        }
        catch { }
    }
}
