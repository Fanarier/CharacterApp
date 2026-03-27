using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace CharacterApp
{
    public partial class App : Application
    {
        private const string ThemeConfigFile = "theme.config";
        private const string LanguageConfigFile = "language.config";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Попытка загрузить тему (заменяем ТОЛЬКО словарь из Themes/)
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

            // Если по какой-то причине CoreResources не загрузился - добавим его (защита)
            EnsureCoreResources();

            var main = new MainWindow();
            main.Show();
        }

        private void ReplaceMergedDictionaryByFolder(string folderMarker, Uri newDictUri)
        {
            var dicts = Resources.MergedDictionaries;
            var old = dicts.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains(folderMarker, StringComparison.OrdinalIgnoreCase));
            if (old != null) dicts.Remove(old);
            dicts.Add(new ResourceDictionary { Source = newDictUri });
        }

        public static void LoadLanguage(string langCode)
        {
            try
            {
                var dicts = Current.Resources.MergedDictionaries;
                var old = dicts.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings/Strings.", StringComparison.OrdinalIgnoreCase));
                if (old != null) dicts.Remove(old);
                var uri = new Uri($"Strings/Strings.{langCode}.xaml", UriKind.Relative);
                dicts.Add(new ResourceDictionary { Source = uri });
            }
            catch { }
        }

        private void EnsureCoreResources()
        {
            try
            {
                var dicts = Resources.MergedDictionaries;
                bool hasCore = dicts.Any(d => d.Source != null && d.Source.OriginalString.EndsWith("CoreResources.xaml", StringComparison.OrdinalIgnoreCase));
                if (!hasCore)
                {
                    try { dicts.Insert(0, new ResourceDictionary { Source = new Uri("Resources/CoreResources.xaml", UriKind.Relative) }); }
                    catch { /* ignore */ }
                }
            }
            catch { }
        }
    }
}
