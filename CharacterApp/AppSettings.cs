// AppSettings.cs — единый файл настроек (объединяет бывшие settings.json + appsettings.json)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CharacterApp.Models;

namespace CharacterApp
{
    /// <summary>
    /// Единый класс настроек приложения.
    /// Сохраняется в %AppData%\CharacterApp\config.json
    /// Заменяет оба бывших файла: settings.json и appsettings.json
    /// </summary>
    public class AppSettings
    {
        // ── Тема и язык ───────────────────────────────────────────────────────
        public string SelectedTheme    { get; set; } = "Light";
        public string SelectedLanguage { get; set; } = "ru";
        public string AccentColorHex   { get; set; } = "";

        // ── Автосохранение ────────────────────────────────────────────────────
        public bool   AutoSaveEnabled        { get; set; } = false;
        public int    AutoSaveIntervalMinutes { get; set; } = 5;
        public string AutoSaveFolder         { get; set; } = "";
        public string AutoSaveFilePattern    { get; set; } = "autosave_{0:yyyyMMdd_HHmmss}.json";

        // ── Последний файл ────────────────────────────────────────────────────
        public bool   LoadLastOnStart { get; set; } = false;
        public string LastFilePath    { get; set; } = "";

        // ── Видимость страниц ─────────────────────────────────────────────────
        public List<string> HiddenPages { get; set; } = new();

        // ── Кастомные листы ───────────────────────────────────────────────────
        public List<string>     CustomSheetNames  { get; set; } = new();
        public List<CustomSheet> SavedCustomSheets { get; set; } = new();

        // ── Persistence ───────────────────────────────────────────────────────
        private static readonly JsonSerializerOptions _opts =
            new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        private static string ConfigPath =>
            Path.Combine(App.DataDir, "config.json");

        public static AppSettings Load()
        {
            // Try new unified file first
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppSettings>(json, _opts) ?? new AppSettings();
                }
                catch { }
            }

            // Migrate from old files
            var settings = new AppSettings();
            MigrateOldSettings(settings);
            return settings;
        }

        public void Save()
        {
            Directory.CreateDirectory(App.DataDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, _opts));
        }

        private static void MigrateOldSettings(AppSettings s)
        {
            // Migrate old settings.json (theme)
            var oldSettings = Path.Combine(Directory.GetCurrentDirectory(), "settings.json");
            if (File.Exists(oldSettings))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(oldSettings));
                    if (doc.RootElement.TryGetProperty("SelectedTheme", out var t))
                        s.SelectedTheme = t.GetString() ?? "Light";
                    File.Delete(oldSettings);
                }
                catch { }
            }

            // Migrate old appsettings.json (autosave, language, etc.)
            var oldAuto = Path.Combine(App.DataDir, "appsettings.json");
            if (File.Exists(oldAuto))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(oldAuto));
                    var r = doc.RootElement;
                    if (r.TryGetProperty("Enabled",         out var v)) s.AutoSaveEnabled        = v.GetBoolean();
                    if (r.TryGetProperty("IntervalMinutes", out v))     s.AutoSaveIntervalMinutes = v.GetInt32();
                    if (r.TryGetProperty("Folder",          out v))     s.AutoSaveFolder         = v.GetString() ?? "";
                    if (r.TryGetProperty("FilePattern",     out v))     s.AutoSaveFilePattern    = v.GetString() ?? s.AutoSaveFilePattern;
                    if (r.TryGetProperty("LoadLastOnStart", out v))     s.LoadLastOnStart        = v.GetBoolean();
                    if (r.TryGetProperty("LastFilePath",    out v))     s.LastFilePath           = v.GetString() ?? "";
                    if (r.TryGetProperty("HiddenPages",     out v))
                        foreach (var item in v.EnumerateArray())
                            s.HiddenPages.Add(item.GetString() ?? "");
                    // Keep old appsettings for now — delete after successful save
                    File.Delete(oldAuto);
                }
                catch { }
            }

            // Migrate old language files
            if (File.Exists(App.LanguageConfigFile))
            {
                try { s.SelectedLanguage = File.ReadAllText(App.LanguageConfigFile).Trim(); } catch { }
            }

            s.Save();
        }
    }
}
