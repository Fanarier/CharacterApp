using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

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

            // Акцентный цвет — восстанавливаем из настроек
            var settings = AppSettings.Load();
            if (!string.IsNullOrWhiteSpace(settings.AccentColorHex))
            {
                CurrentAccentHex = settings.AccentColorHex;
                ApplyAccentOnStartup(settings.AccentColorHex);
            }

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

        private static Color Lighten(Color c, float amt) => Color.FromRgb(
            (byte)Math.Min(255, c.R + 255 * amt),
            (byte)Math.Min(255, c.G + 255 * amt),
            (byte)Math.Min(255, c.B + 255 * amt));

        private static Color Darken(Color c, float amt) => Color.FromRgb(
            (byte)Math.Max(0, c.R - 255 * amt),
            (byte)Math.Max(0, c.G - 255 * amt),
            (byte)Math.Max(0, c.B - 255 * amt));

        private void ApplyAccentOnStartup(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var res = Resources;
                var pairs = new (string Key, object Val)[]
                {
                    ("AccentBrush",                  new SolidColorBrush(color)),
                    ("AccentLightBrush",             new SolidColorBrush(Lighten(color, 0.2f))),
                    ("AccentDimBrush",               new SolidColorBrush(Color.FromArgb(45,  color.R, color.G, color.B))),
                    ("AccentGlowBrush",              new SolidColorBrush(Color.FromArgb(90,  color.R, color.G, color.B))),
                    ("AccentGradient",               new LinearGradientBrush(Darken(color, 0.2f),  Lighten(color, 0.2f), 0)),
                    ("AccentGradientV",              new LinearGradientBrush(Lighten(color, 0.1f), Darken(color, 0.1f), 90)),
                    ("BorderAccentBrush",            new SolidColorBrush(Color.FromArgb(100, color.R, color.G, color.B))),
                    ("AccentGlow",                   new DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Color = color, Opacity = 0.55 }),
                    ("SmallGlow",                    new DropShadowEffect { BlurRadius = 8,  ShadowDepth = 0, Color = color, Opacity = 0.5  }),
                    ("BurgerLineBrush",              new LinearGradientBrush(Lighten(color, 0.15f), color, 0)),
                    ("MenuTitleBrush",               new LinearGradientBrush(Lighten(color, 0.15f), color, 0)),
                    ("SidebarSeparatorBrush",        new SolidColorBrush(Color.FromArgb(26,  color.R, color.G, color.B))),
                    ("SidebarBottomSeparatorBrush",  new SolidColorBrush(Color.FromArgb(24,  color.R, color.G, color.B))),
                    ("NavActiveBgBrush",             new SolidColorBrush(Color.FromArgb(48,  color.R, color.G, color.B))),
                    ("NavActiveBarBrush",            new SolidColorBrush(color)),
                    ("NavHoverBgBrush",              new SolidColorBrush(Color.FromArgb(18,  color.R, color.G, color.B))),
                };
                foreach (var (k, v) in pairs) res[k] = v;
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
