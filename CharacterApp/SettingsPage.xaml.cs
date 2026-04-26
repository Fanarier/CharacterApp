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
        public bool   Enabled          { get; set; } = false;
        public int    IntervalMinutes  { get; set; } = 5;
        public string Folder           { get; set; } = string.Empty;
        public string FilePattern      { get; set; } = "autosave_{0:yyyyMMdd_HHmmss}.json";
        public bool   LoadLastOnStart  { get; set; } = false;
        public string LastFilePath     { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> HiddenPages   { get; set; } = new();
        public System.Collections.Generic.List<string> CustomSheetNames { get; set; } = new();
        // Полные описания кастомных листов (колонки) для восстановления при старте
        public System.Collections.Generic.List<Models.CustomSheet> SavedCustomSheets { get; set; } = new();
    }

    public partial class SettingsPage : Page
    {
        private static string ThemeConfigFile    => App.ThemeConfigFile;
        private static string LanguageConfigFile => App.LanguageConfigFile;
        private static string SettingsFile       => Path.Combine(App.DataDir, "appsettings.json");
        private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };
        private AutoSaveConfig _config = new();

        private static readonly string[] _langCodes = { "ru", "en", "jp" };

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
                if (t == "Dark" || t == "Light") theme = t;
            }
            RbLight.IsChecked = theme == "Light";
            RbDark.IsChecked  = theme == "Dark";
        }

        private async void ConfirmTheme_Click(object sender, RoutedEventArgs e)
        {
            string selectedTheme = RbDark.IsChecked == true ? "Dark" : "Light";
            try { File.WriteAllText(ThemeConfigFile, selectedTheme); }
            catch (Exception ex)
            {
                (Application.Current.MainWindow as MainWindow)
                    ?.ShowNotification("Ошибка темы: " + ex.Message, NotificationType.Error);
                return;
            }
            if (Application.Current.MainWindow is Window win)
            {
                win.BeginAnimation(Window.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3)));
                await Task.Delay(300);
                var dicts = Application.Current.Resources.MergedDictionaries;
                var langDicts  = dicts.Where(d => d.Source != null && d.Source.OriginalString.Contains("Strings/Strings.")).ToList();
                var themeDicts = dicts.Where(d => d.Source != null && d.Source.OriginalString.StartsWith("Themes/")).ToList();
                foreach (var td in themeDicts) dicts.Remove(td);
                dicts.Insert(0, new ResourceDictionary { Source = new Uri($"Themes/{selectedTheme}Theme.xaml", UriKind.Relative) });
                foreach (var ld in langDicts) if (!dicts.Contains(ld)) dicts.Add(ld);
                win.BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5)));
            }
        }

        private void InitLanguageSelection()
        {
            string code = "ru";
            if (File.Exists(LanguageConfigFile))
            {
                var txt = File.ReadAllText(LanguageConfigFile).Trim();
                if (_langCodes.Contains(txt)) code = txt;
            }
            LanguageComboBox.SelectedValue = code;
        }

        private void ApplyLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (LanguageComboBox.SelectedValue is not string code) return;
            try { File.WriteAllText(LanguageConfigFile, code); }
            catch (Exception ex)
            {
                (Application.Current.MainWindow as MainWindow)
                    ?.ShowNotification("Ошибка языка: " + ex.Message, NotificationType.Error);
            }
            App.LoadLanguage(code);
            LanguageComboBox.SelectedValue = code;
        }

        private void LoadSettings()
        {
            if (!File.Exists(SettingsFile)) { _config = new(); return; }
            try
            {
                var fromFile = JsonSerializer.Deserialize<AutoSaveConfig>(File.ReadAllText(SettingsFile));
                if (fromFile != null) _config = fromFile;
            }
            catch { _config = new(); }
        }

        private void ApplyToUI()
        {
            CbEnableAutoSave.IsChecked  = _config.Enabled;
            TbAutoSaveInterval.Text     = _config.IntervalMinutes.ToString();
            TbAutoSaveFolder.Text       = _config.Folder;
            TbAutoSavePattern.Text      = _config.FilePattern;
            CbLoadLastOnStart.IsChecked = _config.LoadLastOnStart;

            // Восстанавливаем видимость страниц
            var mw = Application.Current.MainWindow as MainWindow;
            var hidden = _config.HiddenPages ?? new System.Collections.Generic.List<string>();
            void SyncCb(CheckBox cb, string key)
            {
                bool vis = !hidden.Contains(key);
                cb.IsChecked = vis;
                mw?.SetPageVisible(key, vis);
            }
            SyncCb(CbShowMainPage,     "MainPage");
            SyncCb(CbShowDetails,      "Details");
            SyncCb(CbShowEquipment,    "Equipment");
            SyncCb(CbShowStats,        "Stats");
            SyncCb(CbShowActiveSkills, "ActiveSkills");
            SyncCb(CbShowPassive,      "PassiveSkills");
            SyncCb(CbShowProf,         "Proficiencies");
            SyncCb(CbShowAttacks,      "Attacks");

            // Восстанавливаем список кастомных листов
            LbCustomSheets.Items.Clear();
            foreach (var name in _config.CustomSheetNames ?? new())
                LbCustomSheets.Items.Add(name);
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Выберите папку для автосохранения",
                Filter = "Папка|*.folder", FileName = "Выберите папку",
                CheckFileExists = false, CheckPathExists = true,
                InitialDirectory = string.IsNullOrEmpty(TbAutoSaveFolder.Text) ? App.DataDir : TbAutoSaveFolder.Text
            };
            if (dlg.ShowDialog() == true)
            {
                var folder = Path.GetDirectoryName(dlg.FileName);
                if (!string.IsNullOrEmpty(folder)) TbAutoSaveFolder.Text = folder;
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            _config.Enabled = CbEnableAutoSave.IsChecked == true;
            if (int.TryParse(TbAutoSaveInterval.Text, out var mins)) _config.IntervalMinutes = mins;
            _config.Folder           = TbAutoSaveFolder.Text;
            _config.FilePattern      = TbAutoSavePattern.Text;
            _config.LoadLastOnStart  = CbLoadLastOnStart.IsChecked == true;

            // Сохраняем скрытые страницы
            _config.HiddenPages.Clear();
            foreach (CheckBox cb in new CheckBox[]
            {
                CbShowMainPage, CbShowDetails, CbShowEquipment, CbShowStats,
                CbShowActiveSkills, CbShowPassive, CbShowProf, CbShowAttacks
            })
                if (cb.IsChecked != true && cb.Tag is string key)
                    _config.HiddenPages.Add(key);

            // Сохраняем имена кастомных листов (SavedCustomSheets уже актуален)
            _config.CustomSheetNames.Clear();
            foreach (var item in LbCustomSheets.Items)
                _config.CustomSheetNames.Add(item.ToString()!);
            try
            {
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(_config, _jsonOpts));
                var mw = Application.Current.MainWindow as MainWindow;
                mw?.ShowNotification("Настройки сохранены", NotificationType.Success);
                mw?.LoadAutoSaveConfig();
                mw?.ApplyAutoSaveSettings();
                mw?.PersistAutoSaveConfig();  // синхронизируем SavedCustomSheets
            }
            catch (Exception ex)
            {
                (Application.Current.MainWindow as MainWindow)
                    ?.ShowNotification("Ошибка: " + ex.Message, NotificationType.Error);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e) => NavigationService?.GoBack();

        // ── Видимость страниц ─────────────────────────────────────────────────
        private void PageVisibility_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is string pageKey
                && Application.Current.MainWindow is MainWindow mw)
            {
                mw.SetPageVisible(pageKey, cb.IsChecked == true);
            }
        }

        // ── Пользовательские листы ────────────────────────────────────────────
        private void RefreshSheetList()
        {
            LbCustomSheets.Items.Clear();
        }

        private void AddSheet_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;

            var name = TbSheetName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                mw.ShowNotification("Укажите название листа", NotificationType.Warning);
                return;
            }

            var cols = new System.Collections.Generic.List<Models.CustomSheetColumn>();
            char[] seps = { '\n', '\r' };
            foreach (var line in TbSheetColumns.Text.Split(seps, StringSplitOptions.RemoveEmptyEntries))
            {
                var ln = line.Trim();
                if (string.IsNullOrEmpty(ln)) continue;
                var ci    = ln.IndexOf(':');
                var hdr   = ci > 0 ? ln[..ci].Trim()            : ln;
                var ctype = ci > 0 ? ln[(ci + 1)..].Trim().ToLower() : "text";
                cols.Add(new Models.CustomSheetColumn { Header = hdr, ColumnType = ctype });
            }

            if (cols.Count == 0)
            {
                mw.ShowNotification("Добавьте хотя бы одну колонку", NotificationType.Warning);
                return;
            }

            var newSheet = new Models.CustomSheet { Name = name, Columns = cols };
            mw.AddCustomSheet(newSheet);
            LbCustomSheets.Items.Add(name);
            // Запоминаем полное описание листа
            _config.SavedCustomSheets.RemoveAll(s => s.Name == name);
            _config.SavedCustomSheets.Add(newSheet);
            TbSheetName.Clear();
            TbSheetColumns.Clear();
            mw.ShowNotification($"Лист \u00ab{name}\u00bb добавлен", NotificationType.Success);
        }

        private void RemoveSheet_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;
            if (LbCustomSheets.SelectedItem is string name)
            {
                mw.RemoveCustomSheet(name);
                LbCustomSheets.Items.Remove(name);
                _config.SavedCustomSheets.RemoveAll(s => s.Name == name);
                mw.ShowNotification($"Лист \u00ab{name}\u00bb удалён", NotificationType.Info);
            }
        }
    }
}
