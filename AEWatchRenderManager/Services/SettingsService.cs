using System;
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
            catch { }
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
            catch { }
        }
    }
}
