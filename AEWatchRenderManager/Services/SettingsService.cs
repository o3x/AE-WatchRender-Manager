using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace AEWatchRenderManager.Services
{
    public class AppSettings
    {
        public string MonitorPath { get; set; } = string.Empty;
        public string MoveTargetPath { get; set; } = string.Empty;
        public int ScanIntervalSeconds { get; set; } = 60;
    }

    public static class SettingsService
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AEWatchRenderManager");
        
        private static readonly string ConfigFile = Path.Combine(AppDataPath, "config.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var json = File.ReadAllText(ConfigFile);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (JsonException ex) { Debug.WriteLine($"[SettingsService.Load] JSON解析エラー: {ex.Message}"); }
            catch (IOException ex) { Debug.WriteLine($"[SettingsService.Load] IO例外: {ex.Message}"); }
            catch (UnauthorizedAccessException ex) { Debug.WriteLine($"[SettingsService.Load] アクセス拒否: {ex.Message}"); }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(AppDataPath))
                {
                    Directory.CreateDirectory(AppDataPath);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFile, json);
            }
            catch (IOException ex) { Debug.WriteLine($"[SettingsService.Save] IO例外: {ex.Message}"); }
            catch (UnauthorizedAccessException ex) { Debug.WriteLine($"[SettingsService.Save] アクセス拒否: {ex.Message}"); }
        }
    }
}
