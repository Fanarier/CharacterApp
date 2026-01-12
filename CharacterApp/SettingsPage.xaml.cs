using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;


namespace CharacterApp
{
    public class AutoSaveConfig
    {
        public bool Enabled { get; set; } = false;
        public int IntervalMinutes { get; set; } = 5;
        public string Folder { get; set; } = string.Empty;
        public string FilePattern { get; set; } = "autosave_{0:yyyyMMdd_HHmmss}.json";
    }

    public partial class SettingsPage : Page
    {
        private const string ThemeConfigFile = "theme.config";
        private const string LanguageConfigFile = "language.config";
        private const string SettingsFile = "appsettings.json";
        private AutoSaveConfig _config = new AutoSaveConfig();

        public SettingsPage()
        {
            InitializeComponent();

            InitThemeSelection();

            InitLanguageSelection();
            LoadSettings();
            ApplyToUI();
        }

        private void InitThemeSelection()
        {
            string theme = "Light";
            if (File.Exists(ThemeConfigFile))
            {
                var t = File.ReadAllText(ThemeConfigFile).Trim();
                if (t == "Dark" || t == "Light")
                    theme = t;
            }
            RbLight.IsChecked = theme == "Light";
            RbDark.IsChecked = theme == "Dark";
        }

        private void InitLanguageSelection()
        {
            string code = "ru";
            if (File.Exists(LanguageConfigFile))
            {
                var txt = File.ReadAllText(LanguageConfigFile).Trim();
                if (new[] { "ru", "en", "jp" }.Contains(txt))
                    code = txt;
            }
            LanguageComboBox.SelectedValue = code;
        }

        private async void ConfirmTheme_Click(object sender, RoutedEventArgs e)
        {
            string selectedTheme = RbDark.IsChecked == true ? "Dark" : "Light";
            try
            {
                File.WriteAllText(ThemeConfigFile, selectedTheme);
            }
            catch (Exception ex)
            {
                var main = Application.Current.MainWindow as MainWindow;
                main?.ShowNotification("Ошибка сохранения темы: " + ex.Message, NotificationType.Error);
                return;
            }

            if (Application.Current.MainWindow is Window mainWindow)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
                mainWindow.BeginAnimation(Window.OpacityProperty, fadeOut);
                await Task.Delay(300);

                var appDicts = Application.Current.Resources.MergedDictionaries;
                var langDicts = appDicts.Where(d => d.Source != null && d.Source.OriginalString.Contains("Strings/Strings.")).ToList();

                var themeDicts = appDicts.Where(d => d.Source != null && d.Source.OriginalString.StartsWith("Themes/")).ToList();
                foreach (var td in themeDicts) appDicts.Remove(td);

                var themeUri = new Uri($"Themes/{selectedTheme}Theme.xaml", UriKind.Relative);
                appDicts.Insert(0, new ResourceDictionary { Source = themeUri });

                foreach (var ld in langDicts)
                    if (!appDicts.Contains(ld)) appDicts.Add(ld);

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
                mainWindow.BeginAnimation(Window.OpacityProperty, fadeIn);
            }
        }

        private void ApplyLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (LanguageComboBox.SelectedValue is string code)
            {
                try
                {
                    File.WriteAllText(LanguageConfigFile, code);
                }
                catch (Exception ex)
                {
                    var main = Application.Current.MainWindow as MainWindow;
                    main?.ShowNotification("Не удалось сохранить язык: " + ex.Message, NotificationType.Error);
                }

                App.LoadLanguage(code);

                LanguageComboBox.SelectedValue = code;
            }
        }

        private void LoadSettings()
        {
            if (File.Exists(SettingsFile))
            {
                try
                {
                    var json = File.ReadAllText(SettingsFile);
                    var fromFile = JsonSerializer.Deserialize<AutoSaveConfig>(json);
                    if (fromFile != null) _config = fromFile;
                }
                catch
                {
                    _config = new AutoSaveConfig();
                }
            }
            else
            {
                _config = new AutoSaveConfig();
            }
        }

        private void ApplyToUI()
        {
            CbEnableAutoSave.IsChecked = _config.Enabled;
            TbAutoSaveInterval.Text = _config.IntervalMinutes.ToString();
            TbAutoSaveFolder.Text = _config.Folder;
            TbAutoSavePattern.Text = _config.FilePattern;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Выберите папку"
            };
            if (dlg.ShowDialog() == true)
            {
                var folder = Path.GetDirectoryName(dlg.FileName);
                if (!string.IsNullOrEmpty(folder))
                    TbAutoSaveFolder.Text = folder;
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            _config.Enabled = CbEnableAutoSave.IsChecked == true;
            if (int.TryParse(TbAutoSaveInterval.Text, out var mins))
                _config.IntervalMinutes = mins;
            _config.Folder = TbAutoSaveFolder.Text;
            _config.FilePattern = TbAutoSavePattern.Text;

            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_config, opts);
                File.WriteAllText(SettingsFile, json);
                var main = Application.Current.MainWindow as MainWindow;
                main?.ShowNotification("Настройки сохранены", NotificationType.Success);
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.LoadAutoSaveConfig();
                    mw.ApplyAutoSaveSettings();
                }
            }
            catch (Exception ex)
            {
                var main = Application.Current.MainWindow as MainWindow;
                main?.ShowNotification("Ошибка сохранения настроек: " + ex.Message, NotificationType.Error);
            }
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}
