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

            // 1) Загружаем тему
            string theme = "Light";
            if (File.Exists(ThemeConfigFile))
            {
                var t = File.ReadAllText(ThemeConfigFile).Trim();
                if (!string.IsNullOrEmpty(t)) theme = t;
            }
            var themeUri = new Uri($"Themes/{theme}Theme.xaml", UriKind.Relative);
            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });

            // 2) Загружаем язык
            string lang = "ru";  
            if (File.Exists(LanguageConfigFile))
            {
                var l = File.ReadAllText(LanguageConfigFile).Trim();
                if (!string.IsNullOrEmpty(l)) lang = l;
            }
            LoadLanguage(lang);

            var main = new MainWindow();
            main.Show();
        }

        public static void LoadLanguage(string langCode)
        {
         
            var dicts = Current.Resources.MergedDictionaries;
            var old = dicts.FirstOrDefault(d =>
                d.Source != null && d.Source.OriginalString.Contains("Strings/Strings."));
            if (old != null) dicts.Remove(old);

            var uri = new Uri($"Strings/Strings.{langCode}.xaml", UriKind.Relative);
            dicts.Add(new ResourceDictionary { Source = uri });
        }
    }
}
