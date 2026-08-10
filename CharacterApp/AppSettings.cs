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

        // ── Шрифт ────────────────────────────────────────────────────────────
        public string AppFontFamily { get; set; } = "Segoe UI";
        public double AppFontSize   { get; set; } = 13.0;

        // ── Видимость страниц ─────────────────────────────────────────────────
        public List<string> HiddenPages { get; set; } = new();

        /// <summary>
        /// Старые конфиги (theme.config, language.config, appsettings.json) уже
        /// разобраны и больше не читаются. Без этого флага получался цикл:
        /// файлы удалялись из %AppData%, но при следующем запуске копировались
        /// туда заново из папки с exe и снова перетирали тему и язык.
        /// </summary>
        public bool LegacyConfigsImported { get; set; } = false;

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
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, _opts) ?? new AppSettings();
                    // config.json есть — он и есть истина. Старые theme.config /
                    // language.config только убираем, значения из них не берём:
                    // они старее того, что пользователь уже сохранил.
                    AdoptLegacySideFiles(loaded, readValues: false);
                    return loaded;
                }
                catch (Exception ex)
                {
                    // Битый config.json — уводим в бэкап, иначе он будет молча
                    // сбрасывать настройки при каждом запуске и никто не поймёт почему
                    Helpers.Log.Warn("config.json не читается, откатываюсь к настройкам по умолчанию", ex);
                    try { File.Move(ConfigPath, ConfigPath + ".bad", true); } catch { }
                }
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

        /// <summary>
        /// Тема и язык раньше жили в отдельных theme.config и language.config
        /// рядом с config.json. Забираем их значения внутрь настроек и удаляем
        /// файлы — но только после успешной записи, иначе при сбое потеряли бы
        /// и то, и другое. Выполняется ровно один раз за всё время жизни
        /// настроек: дальше срабатывает флаг LegacyConfigsImported.
        /// </summary>
        /// <param name="readValues">
        /// true — забрать тему и язык из старых файлов (первый запуск, config.json ещё нет);
        /// false — только убрать файлы, значения оставить как есть в config.json.
        /// </param>
        private static void AdoptLegacySideFiles(AppSettings s, bool readValues = true)
        {
            if (s.LegacyConfigsImported) return;

            try
            {
                if (readValues && File.Exists(App.ThemeConfigFile))
                {
                    var t = File.ReadAllText(App.ThemeConfigFile).Trim();
                    if (t is "Dark" or "Light") s.SelectedTheme = t;
                }
                if (readValues && File.Exists(App.LanguageConfigFile))
                {
                    var l = File.ReadAllText(App.LanguageConfigFile).Trim();
                    if (!string.IsNullOrEmpty(l)) s.SelectedLanguage = l;
                }
            }
            catch (Exception ex)
            {
                Helpers.Log.Warn("не удалось прочитать theme.config / language.config", ex);
                return;   // флаг не ставим — попробуем в следующий раз
            }

            try
            {
                s.LegacyConfigsImported = true;
                s.Save();
                TryDelete(App.ThemeConfigFile);
                TryDelete(App.LanguageConfigFile);
                Helpers.Log.Info($"тема ('{s.SelectedTheme}') и язык ('{s.SelectedLanguage}') " +
                                 "перенесены в config.json, старые файлы больше не читаются");
            }
            catch (Exception ex)
            {
                s.LegacyConfigsImported = false;
                Helpers.Log.Warn("перенос темы и языка отложен: не удалось записать config.json", ex);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Helpers.Log.Warn($"не удалось удалить {path}", ex); }
        }

        private static void MigrateOldSettings(AppSettings s)
        {
            // config.json ещё нет — значит это первый запуск новой версии.
            // Только здесь имеет смысл тянуть конфиги из папки с exe.
            App.ImportConfigsFromExeFolder();

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
                catch (Exception ex) { Helpers.Log.Warn("миграция settings.json не удалась", ex); }
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

                    // Кастомные листы жили только в appsettings.json — без переноса
                    // пользователь при обновлении потерял бы свои страницы
                    if (r.TryGetProperty("CustomSheetNames", out v))
                        foreach (var item in v.EnumerateArray())
                            s.CustomSheetNames.Add(item.GetString() ?? "");

                    if (r.TryGetProperty("SavedCustomSheets", out v))
                    {
                        var sheets = v.Deserialize<List<CustomSheet>>();
                        if (sheets != null) s.SavedCustomSheets.AddRange(sheets);
                    }

                    Helpers.Log.Info($"настройки перенесены из appsettings.json " +
                                     $"(листов: {s.SavedCustomSheets.Count})");
                    File.Delete(oldAuto);
                }
                catch (Exception ex) { Helpers.Log.Warn("миграция appsettings.json не удалась", ex); }
            }

            // Миграция не должна ронять запуск, если папка настроек недоступна
            try { s.Save(); }
            catch (Exception ex) { Helpers.Log.Error("не удалось записать config.json после миграции", ex); }

            // Тема и язык из отдельных файлов — тем же путём, что и для
            // уже существующего config.json
            AdoptLegacySideFiles(s);
        }
    }
}
