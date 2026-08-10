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

        // Старые отдельные файлы темы и языка. Больше не пишутся — читаются
        // один раз при миграции в config.json и удаляются.
        public static string ThemeConfigFile    => Path.Combine(DataDir, "theme.config");
        public static string LanguageConfigFile => Path.Combine(DataDir, "language.config");

        private static AppSettings? _settings;

        /// <summary>
        /// Настройки приложения — один экземпляр на весь процесс.
        /// Раньше AppSettings.Load() вызывался из App трижды и ещё раз из
        /// MainWindow, то есть по памяти гуляли четыре независимые копии.
        /// </summary>
        public static AppSettings Settings => _settings ??= AppSettings.Load();

        /// <summary>Текущий кастомный акцентный цвет (сохраняется в памяти на сессию).</summary>
        public static string CurrentAccentHex { get; set; } = "";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Создаём папку если нет
            Directory.CreateDirectory(DataDir);

            // Ловим всё, что не поймали локально: без этого падение выглядит
            // как «приложение просто закрылось» и разбираться не с чем
            DispatcherUnhandledException += (_, args) =>
            {
                Helpers.Log.Error("необработанное исключение в UI-потоке", args.Exception);
                System.Windows.MessageBox.Show(
                    "Произошла ошибка:\n\n" + args.Exception.Message +
                    "\n\nПодробности записаны в:\n" + Helpers.Log.FilePath,
                    "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                args.Handled = true;   // не роняем приложение — данные ещё в памяти
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Helpers.Log.Error("необработанное исключение вне UI-потока",
                                  args.ExceptionObject as Exception);

            Helpers.Log.Info("=== запуск " +
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + " ===");

            // Единственное чтение настроек за запуск (внутри — миграция старых файлов)
            var settings = Settings;

            // Тема
            ApplyTheme(settings.SelectedTheme);

            // Язык
            LoadLanguage(settings.SelectedLanguage);

            EnsureCoreResources();

            // Шрифт
            Resources["AppFontFamily"] = new System.Windows.Media.FontFamily(settings.AppFontFamily);
            Resources["AppFontSize"]   = settings.AppFontSize;

            // Акцентный цвет
            if (!string.IsNullOrWhiteSpace(settings.AccentColorHex))
            {
                CurrentAccentHex = settings.AccentColorHex;
                ApplyAccentOnStartup(settings.AccentColorHex);
            }

            // Кэш распакованных картинок: убираем то, к чему давно не обращались
            Helpers.CharacterAssets.CleanCache();

            var main = new MainWindow();
            main.Show();
        }

        /// <summary>
        /// Подтягивает конфиг совсем старых версий из папки рядом с exe.
        /// Вызывается только при первом запуске (пока нет config.json):
        /// раньше это делалось на каждом старте, и файлы, оставшиеся в bin,
        /// возвращались в %AppData% сразу после того, как их оттуда удаляли —
        /// из-за чего тема и язык откатывались к старым значениям.
        /// </summary>
        internal static void ImportConfigsFromExeFolder()
        {
            CopyIfMissing("theme.config",     ThemeConfigFile);
            CopyIfMissing("language.config",  LanguageConfigFile);
            CopyIfMissing("appsettings.json", Path.Combine(DataDir, "appsettings.json"));
        }

        private static void CopyIfMissing(string oldRelative, string newPath)
        {
            if (File.Exists(oldRelative) && !File.Exists(newPath))
            {
                try { File.Copy(oldRelative, newPath); Helpers.Log.Info($"мигрирован {oldRelative} → {newPath}"); }
                catch (Exception ex) { Helpers.Log.Warn($"миграция {oldRelative} не удалась", ex); }
            }
        }

        /// <summary>Подключает словарь темы ("Light" / "Dark").</summary>
        public static void ApplyTheme(string themeName)
        {
            if (themeName != "Light" && themeName != "Dark") themeName = "Light";
            try
            {
                ReplaceMergedDictionary("Themes/",
                    new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative));
            }
            catch (Exception ex) { Helpers.Log.Warn($"не удалось применить тему '{themeName}'", ex); }
        }

        /// <summary>
        /// Меняет словарь ресурсов на месте, сохраняя его позицию в списке.
        /// Раньше старт удалял словарь и дописывал новый в конец, а страница
        /// настроек — вставляла в начало: порядок словарей зависел от того,
        /// каким путём пришли. Теперь он одинаковый всегда.
        /// </summary>
        private static void ReplaceMergedDictionary(string folderMarker, Uri newDictUri)
        {
            var dicts = Current.Resources.MergedDictionaries;
            var fresh = new ResourceDictionary { Source = newDictUri };

            for (int i = 0; i < dicts.Count; i++)
            {
                if (dicts[i].Source != null &&
                    dicts[i].Source.OriginalString.Contains(folderMarker, StringComparison.OrdinalIgnoreCase))
                {
                    // Remove + Insert, а не присваивание по индексу: так WPF
                    // гарантированно пересчитывает ресурсы у живых элементов
                    dicts.RemoveAt(i);
                    dicts.Insert(i, fresh);
                    return;
                }
            }
            dicts.Add(fresh);
        }

        public static void LoadLanguage(string langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode)) langCode = "ru";
            try
            {
                ReplaceMergedDictionary("Strings/Strings.",
                    new Uri($"Strings/Strings.{langCode}.xaml", UriKind.Relative));
            }
            catch (Exception ex) { Helpers.Log.Warn($"не удалось загрузить язык '{langCode}'", ex); }
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
                    ("AccentDimBrush",               new SolidColorBrush(Color.FromArgb(28,  color.R, color.G, color.B))),
                    ("AccentGlowBrush",              new SolidColorBrush(Color.FromArgb(56,  color.R, color.G, color.B))),
                    ("AccentGradient",               new LinearGradientBrush(Darken(color, 0.2f),  Lighten(color, 0.2f), 0)),
                    ("AccentGradientV",              new LinearGradientBrush(Lighten(color, 0.1f), Darken(color, 0.1f), 90)),
                    ("BorderAccentBrush",            new SolidColorBrush(Color.FromArgb(64,  color.R, color.G, color.B))),
                    ("AccentGlow",                   new DropShadowEffect { BlurRadius = 14, ShadowDepth = 0, Color = color, Opacity = 0.30 }),
                    ("SmallGlow",                    new DropShadowEffect { BlurRadius = 7,  ShadowDepth = 0, Color = color, Opacity = 0.28  }),
                    ("BurgerLineBrush",              new LinearGradientBrush(Lighten(color, 0.15f), color, 0)),
                    ("MenuTitleBrush",               new LinearGradientBrush(Lighten(color, 0.15f), color, 0)),
                    ("SidebarSeparatorBrush",        new SolidColorBrush(Color.FromArgb(18,  color.R, color.G, color.B))),
                    ("SidebarBottomSeparatorBrush",  new SolidColorBrush(Color.FromArgb(17,  color.R, color.G, color.B))),
                    ("NavActiveBgBrush",             new SolidColorBrush(Color.FromArgb(42,  color.R, color.G, color.B))),
                    ("NavActiveBarBrush",            new SolidColorBrush(color)),
                    ("NavHoverBgBrush",              new SolidColorBrush(Color.FromArgb(18,  color.R, color.G, color.B))),
                };
                foreach (var (k, v) in pairs) res[k] = v;
            }
            catch (Exception ex) { Helpers.Log.Warn($"не удалось применить акцентный цвет '{hex}'", ex); }
        }

        public static void ApplyFontSettings(string family, double size)
        {
            try
            {
                var res = Current.Resources;
                res["AppFontFamily"] = new System.Windows.Media.FontFamily(family);
                res["AppFontSize"]   = size;

                // Apply to default TextBox/TextBlock styles if they use DynamicResource
                // (themes should reference {DynamicResource AppFontFamily} and {DynamicResource AppFontSize})
            }
            catch (Exception ex) { Helpers.Log.Warn($"не удалось применить шрифт '{family}' {size}", ex); }
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
                catch (Exception ex) { Helpers.Log.Error("не удалось загрузить CoreResources.xaml", ex); }
            }
        }
    }
}
