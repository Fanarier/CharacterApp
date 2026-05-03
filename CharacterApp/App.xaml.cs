using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace CharacterApp
{
    public partial class App : Application
    {
        // Единое расположение всех пользовательских файлов
        public static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "CharacterApp");

        public static string ThemeConfigFile    => Path.Combine(DataDir, "theme.config");
        public static string LanguageConfigFile => Path.Combine(DataDir, "language.config");

        /// <summary>Текущий кастомный акцентный цвет (сохраняется в памяти на сессию).</summary>
        public static string CurrentAccentHex { get; set; } = "";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Создаём папку если нет
            Directory.CreateDirectory(DataDir);

            // Миграция старых файлов из рабочей папки
            MigrateIfNeeded("theme.config",    ThemeConfigFile);
            MigrateIfNeeded("language.config", LanguageConfigFile);
            MigrateIfNeeded("appsettings.json",
                Path.Combine(DataDir, "appsettings.json"));

            // Тема
            string theme = "Light";
            if (File.Exists(ThemeConfigFile))
            {
                var t = File.ReadAllText(ThemeConfigFile).Trim();
                if (!string.IsNullOrEmpty(t)) theme = t;
            }
            try
            {
                var themeUri = new Uri($"Themes/{theme}Theme.xaml", UriKind.Relative);
                ReplaceMergedDictionaryByFolder("Themes/", themeUri);
            }
            catch { }

            // Язык
            string lang = "ru";
            if (File.Exists(LanguageConfigFile))
            {
                var l = File.ReadAllText(LanguageConfigFile).Trim();
                if (!string.IsNullOrEmpty(l)) lang = l;
            }
            LoadLanguage(lang);

            EnsureCoreResources();
            var main = new MainWindow();
            main.Show();
        }

        private static void MigrateIfNeeded(string oldRelative, string newPath)
        {
            if (File.Exists(oldRelative) && !File.Exists(newPath))
            {
                try { File.Copy(oldRelative, newPath); } catch { }
            }
        }

        private void ReplaceMergedDictionaryByFolder(string folderMarker, Uri newDictUri)
        {
            var dicts = Resources.MergedDictionaries;
            var old = dicts.FirstOrDefault(d => d.Source != null &&
                d.Source.OriginalString.Contains(folderMarker, StringComparison.OrdinalIgnoreCase));
            if (old != null) dicts.Remove(old);
            dicts.Add(new ResourceDictionary { Source = newDictUri });
        }

        public static void LoadLanguage(string langCode)
        {
            try
            {
                var dicts = Current.Resources.MergedDictionaries;
                var old = dicts.FirstOrDefault(d => d.Source != null &&
                    d.Source.OriginalString.Contains("Strings/Strings.", StringComparison.OrdinalIgnoreCase));
                if (old != null) dicts.Remove(old);
                var uri = new Uri($"Strings/Strings.{langCode}.xaml", UriKind.Relative);
                dicts.Add(new ResourceDictionary { Source = uri });
            }
            catch { }
        }

        private void EnsureCoreResources()
        {
            var dicts = Resources.MergedDictionaries;
            bool hasCoreResources = dicts.Any(d => d.Source != null &&
                d.Source.OriginalString.Contains("CoreResources", StringComparison.OrdinalIgnoreCase));
            if (!hasCoreResources)
            {
                try { dicts.Insert(0, new ResourceDictionary
                    { Source = new Uri("Resources/CoreResources.xaml", UriKind.Relative) }); }
                catch { }
            }
        }
    }
}
