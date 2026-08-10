using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CharacterApp
{
    // Класс AutoSaveConfig удалён: он дублировал AppSettings поле в поле,
    // писался во второй файл (appsettings.json) и синхронизировался вручную
    // в обе стороны. Теперь единственный источник — AppSettings в config.json,
    // а страница настроек правит тот же самый объект, что живёт в MainWindow.
    // Старый appsettings.json по-прежнему читается один раз при миграции,
    // см. AppSettings.MigrateOldSettings.

    public partial class SettingsPage : Page
    {
        /// <summary>
        /// Тот же экземпляр настроек, что и у MainWindow — не копия.
        /// Именно свойство, а не поле: страница создаётся внутри конструктора
        /// MainWindow, и снимок, взятый в этот момент, мог бы устареть.
        /// Статическое, потому что обращения идут и из статических обработчиков
        /// (шрифт, акцентный цвет), а источник всё равно один — MainWindow.Instance.
        /// </summary>
        private static AppSettings _config =>
            MainWindow.Instance?.Settings ?? _fallbackConfig;
        private static readonly AppSettings _fallbackConfig = new();

        /// <summary>
        /// Записывает настройки. Раньше цвет, шрифт и тема каждый раз делали
        /// свой AppSettings.Load(), правили копию и сохраняли её — чужие
        /// несохранённые правки при этом затирались.
        ///
        /// Сохраняем сам объект, а не через Application.Current.MainWindow:
        /// если каст к MainWindow не проходил, вызов молча превращался
        /// в no-op и настройки не записывались вообще.
        /// </summary>
        private static void SaveConfig()
        {
            try { _config.Save(); }
            catch (Exception ex)
            {
                Helpers.Log.Error("не удалось сохранить настройки", ex);
                (Application.Current.MainWindow as MainWindow)
                    ?.ShowNotification("Не удалось сохранить настройки: " + ex.Message,
                                       NotificationType.Error);
            }
        }

        private static readonly string[] _langCodes = { "ru", "en", "jp" };

        public SettingsPage()
        {
            InitializeComponent();
            InitThemeSelection();
            InitLanguageSelection();
            ApplyToUI();
            // Обновляем страницу каждый раз при открытии: настройки могли
            // измениться в другом месте (кастомные листы, автосейв)
            IsVisibleChanged += (_, e) =>
            {
                if (!(bool)e.NewValue) return;
                ApplyToUI();
                InitAccentColorPanel();
                InitFontSettings();
            };
        }

        private void InitThemeSelection()
        {
            var theme = _config.SelectedTheme;
            RbLight.IsChecked = theme != "Dark";
            RbDark.IsChecked  = theme == "Dark";
        }

        private async void ConfirmTheme_Click(object sender, RoutedEventArgs e)
        {
            string selectedTheme = RbDark.IsChecked == true ? "Dark" : "Light";

            _config.SelectedTheme = selectedTheme;
            SaveConfig();

            if (Application.Current.MainWindow is Window win)
            {
                win.BeginAnimation(Window.OpacityProperty,
                    new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3)));
                await Task.Delay(300);

                // Замена словаря на месте — языковые словари трогать не нужно
                App.ApplyTheme(selectedTheme);

                win.BeginAnimation(Window.OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5)));
            }
        }

        private void InitLanguageSelection()
        {
            var code = _config.SelectedLanguage;
            if (!_langCodes.Contains(code)) code = "ru";
            LanguageComboBox.SelectedValue = code;
        }

        private void ApplyLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (LanguageComboBox.SelectedValue is not string code) return;

            _config.SelectedLanguage = code;
            SaveConfig();

            App.LoadLanguage(code);
            LanguageComboBox.SelectedValue = code;
        }

        private void ApplyToUI()
        {
            CbEnableAutoSave.IsChecked  = _config.AutoSaveEnabled;
            TbAutoSaveInterval.Text     = _config.AutoSaveIntervalMinutes.ToString();
            TbAutoSaveFolder.Text       = _config.AutoSaveFolder;
            TbAutoSavePattern.Text      = _config.AutoSaveFilePattern;
            CbLoadLastOnStart.IsChecked = _config.LoadLastOnStart;

            // Вид пунктов меню
            _suppressMenuStyle = true;
            RbMenuIconText.IsChecked = _config.MenuButtonStyle == MenuButtonStyles.IconAndText;
            RbMenuText.IsChecked     = _config.MenuButtonStyle == MenuButtonStyles.TextOnly;
            RbMenuIcon.IsChecked     = _config.MenuButtonStyle == MenuButtonStyles.IconOnly;
            _suppressMenuStyle = false;

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
            SyncCb(CbShowInventory,    "Inventory");

            // Восстанавливаем список кастомных листов из реального состояния MainWindow
            RefreshSheetList();
        }

        /// <summary>Чтобы установка галочек в ApplyToUI не сохраняла настройки.</summary>
        private bool _suppressMenuStyle;

        private void MenuStyle_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressMenuStyle) return;

            _config.MenuButtonStyle =
                RbMenuText.IsChecked == true ? MenuButtonStyles.TextOnly :
                RbMenuIcon.IsChecked == true ? MenuButtonStyles.IconOnly :
                                               MenuButtonStyles.IconAndText;
            SaveConfig();
            MainWindow.Instance?.ApplyMenuButtonStyle();
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
            _config.AutoSaveEnabled = CbEnableAutoSave.IsChecked == true;
            if (int.TryParse(TbAutoSaveInterval.Text, out var mins))
                _config.AutoSaveIntervalMinutes = mins;
            _config.AutoSaveFolder      = TbAutoSaveFolder.Text;
            _config.AutoSaveFilePattern = TbAutoSavePattern.Text;
            _config.LoadLastOnStart     = CbLoadLastOnStart.IsChecked == true;

            // Сохраняем скрытые страницы
            _config.HiddenPages.Clear();
            foreach (CheckBox cb in new CheckBox[]
            {
                CbShowMainPage, CbShowDetails, CbShowEquipment, CbShowStats,
                CbShowActiveSkills, CbShowPassive, CbShowProf, CbShowAttacks, CbShowInventory
            })
                if (cb.IsChecked != true && cb.Tag is string key)
                    _config.HiddenPages.Add(key);

            // Сохраняем имена кастомных листов (SavedCustomSheets уже актуален)
            _config.CustomSheetNames.Clear();
            foreach (var item in LbCustomSheets.Items)
                _config.CustomSheetNames.Add(item.ToString()!);

            var mw = Application.Current.MainWindow as MainWindow;
            // _config — тот же объект, что и mw.Settings, отдельная запись не нужна
            SaveConfig();
            mw?.ApplyAutoSaveSettings();
            mw?.ShowNotification("Настройки сохранены", NotificationType.Success);

            WarnIfRiskyAutoSavePattern(mw);
        }

        /// <summary>
        /// Чистка старых автосейвов работает по маске, выведенной из шаблона имени.
        /// Если шаблон начинается сразу с даты, маска получается вида "*.json" —
        /// отличить свои снимки от чужих файлов невозможно, и чистка отключается.
        /// Молча копить снимки без предупреждения нечестно.
        /// </summary>
        private void WarnIfRiskyAutoSavePattern(MainWindow? mw)
        {
            if (mw == null || !_config.AutoSaveEnabled) return;
            var mask = MainWindow.BuildAutoSaveMask(_config.AutoSaveFilePattern);
            if (string.IsNullOrEmpty(mask) || mask.StartsWith("*", StringComparison.Ordinal))
                mw.ShowNotification(
                    "Шаблон без своего префикса — старые автосейвы удаляться не будут. " +
                    "Добавь текст в начало, например autosave_{0:yyyyMMdd_HHmmss}.json",
                    NotificationType.Warning);
        }

        private void Back_Click(object sender, RoutedEventArgs e) => NavigationService?.GoBack();

        // ── Accent Color ──────────────────────────────────────────────────────
        private static readonly string[] _presetAccents =
        {
            "#B565C1", "#5E8FBF", "#4CAF72", "#E08030",
            "#D04060", "#50A8A0", "#8070CC", "#C09030"
        };

        private void InitAccentColorPanel()
        {
            // Очищаем перед добавлением — иначе дублируются при каждом открытии страницы
            AccentColorPanel.Children.Clear();

            // Подставляем текущий цвет из настроек
            var savedHex = _config.AccentColorHex;
            if (!string.IsNullOrWhiteSpace(savedHex))
                TbAccentHex.Text = savedHex;
            else if (string.IsNullOrWhiteSpace(TbAccentHex.Text))
                TbAccentHex.Text = "#B565C1";

            foreach (var hex in _presetAccents)
            {
                var swatch = new System.Windows.Shapes.Rectangle
                {
                    Width = 28, Height = 28,
                    RadiusX = 6, RadiusY = 6,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    Margin = new Thickness(3),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = hex,
                    Stroke = (Brush)FindResource("BorderBrush"),
                    StrokeThickness = 1
                };
                var h = hex;
                swatch.MouseLeftButtonDown += (_, _) =>
                {
                    TbAccentHex.Text = h;
                    UpdateAccentPreview(h);
                    ApplyAccent(h);
                };
                AccentColorPanel.Children.Add(swatch);
            }
            UpdateAccentPreview(TbAccentHex.Text);
        }

        private void TbAccentHex_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                ApplyAccentColor_Click(sender, e);
            else
                UpdateAccentPreview(TbAccentHex.Text);
        }

        private void ApplyAccentColor_Click(object sender, RoutedEventArgs e)
            => ApplyAccent(TbAccentHex.Text);

        private void ResetAccentColor_Click(object sender, RoutedEventArgs e)
        {
            TbAccentHex.Text = "#B565C1";
            ApplyAccent("#B565C1");
        }

        private void UpdateAccentPreview(string hex)
        {
            try
            {
                AccentPreview.Background =
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
            catch { /* пользователь ещё дописывает hex в поле — это нормально */ }
        }

        private static void ApplyAccent(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);

                var newValues = new System.Collections.Generic.Dictionary<string, object>
                {
                    // Core accent brushes
                    ["AccentBrush"]       = new SolidColorBrush(color),
                    ["AccentLightBrush"]  = new SolidColorBrush(Lighten(color, 0.2f)),
                    ["AccentDimBrush"]    = new SolidColorBrush(Color.FromArgb(28,  color.R, color.G, color.B)),
                    ["AccentGlowBrush"]   = new SolidColorBrush(Color.FromArgb(56,  color.R, color.G, color.B)),
                    ["AccentGradient"]    = new LinearGradientBrush(Darken(color, 0.2f),  Lighten(color, 0.2f), 0),
                    ["AccentGradientV"]   = new LinearGradientBrush(Lighten(color, 0.1f), Darken(color, 0.1f), 90),
                    ["BorderAccentBrush"] = new SolidColorBrush(Color.FromArgb(64,  color.R, color.G, color.B)),
                    ["AccentGlow"]        = new System.Windows.Media.Effects.DropShadowEffect
                                           { BlurRadius = 14, ShadowDepth = 0, Color = color, Opacity = 0.30 },
                    ["SmallGlow"]         = new System.Windows.Media.Effects.DropShadowEffect
                                           { BlurRadius = 7,  ShadowDepth = 0, Color = color, Opacity = 0.28  },
                    // Title bar + burger menu + sidebar separators
                    ["BurgerLineBrush"]              = new LinearGradientBrush(Lighten(color, 0.15f), color, 0),
                    ["MenuTitleBrush"]               = new LinearGradientBrush(Lighten(color, 0.15f), color, 0),
                    ["SidebarSeparatorBrush"]        = new SolidColorBrush(Color.FromArgb(18,  color.R, color.G, color.B)),
                    ["SidebarBottomSeparatorBrush"]  = new SolidColorBrush(Color.FromArgb(17,  color.R, color.G, color.B)),
                    // Active nav item (SidebarNavButtonActive)
                    ["NavActiveBgBrush"]  = new SolidColorBrush(Color.FromArgb(42,  color.R, color.G, color.B)),
                    ["NavActiveBarBrush"] = new SolidColorBrush(color),
                    ["NavHoverBgBrush"]   = new SolidColorBrush(Color.FromArgb(18,  color.R, color.G, color.B)),
                };

                // 1. Обновляем ВНУТРИ словаря темы — это основной источник DynamicResource в стилях
                var merged = Application.Current.Resources.MergedDictionaries;
                ResourceDictionary? themeDict = null;
                foreach (var d in merged)
                    if (d.Source != null && d.Source.OriginalString.Contains("Theme.xaml"))
                    { themeDict = d; break; }

                if (themeDict != null)
                    foreach (var kv in newValues)
                        if (themeDict.Contains(kv.Key)) themeDict[kv.Key] = kv.Value;

                // 2. Также на уровне Application — перекрывает всё (для элементов вне темы)
                var res = Application.Current.Resources;
                foreach (var kv in newValues) res[kv.Key] = kv.Value;

                // 3. Сохраняем для следующей сессии
                App.CurrentAccentHex   = hex;
                _config.AccentColorHex = hex;
                SaveConfig();

                // 4. Перестраиваем динамически созданные табы персонажей
                (Application.Current.MainWindow as MainWindow)?.RebuildCharacterTabs();
            }
            catch (Exception ex)
            {
                CharacterApp.Helpers.Log.Warn($"не удалось применить акцентный цвет '{hex}'", ex);
                (Application.Current.MainWindow as MainWindow)
                    ?.ShowNotification("Не удалось применить цвет: " + ex.Message, NotificationType.Error);
            }
        }

        private static Color Lighten(Color c, float amt) => Color.FromRgb(
            (byte)Math.Min(255, c.R + 255 * amt),
            (byte)Math.Min(255, c.G + 255 * amt),
            (byte)Math.Min(255, c.B + 255 * amt));

        private static Color Darken(Color c, float amt) => Color.FromRgb(
            (byte)Math.Max(0, c.R - 255 * amt),
            (byte)Math.Max(0, c.G - 255 * amt),
            (byte)Math.Max(0, c.B - 255 * amt));

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
            if (Application.Current.MainWindow is MainWindow mw)
                foreach (var name in mw.GetCustomSheetNames())
                    LbCustomSheets.Items.Add(name);
        }

        private void RefreshSheetList_Click(object sender, RoutedEventArgs e)
            => RefreshSheetList();

        private static readonly System.Collections.Generic.HashSet<string> _validColTypes
            = new() { "text", "number", "toggle" };

        private void AddSheet_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;

            var name = TbSheetName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                mw.ShowNotification("Укажите название листа", NotificationType.Warning);
                return;
            }

            if (mw.GetCustomSheetNames().Contains(name))
            {
                mw.ShowNotification($"Лист «{name}» уже существует", NotificationType.Warning);
                return;
            }

            var cols   = new System.Collections.Generic.List<Models.CustomSheetColumn>();
            var errors = new System.Collections.Generic.List<string>();
            char[] seps = { '\n', '\r' };
            int lineNum = 0;

            foreach (var line in TbSheetColumns.Text.Split(seps, StringSplitOptions.RemoveEmptyEntries))
            {
                var ln = line.Trim();
                if (string.IsNullOrEmpty(ln)) continue;
                lineNum++;

                var ci = ln.IndexOf(':');
                if (ci <= 0)
                {
                    errors.Add($"Строка {lineNum}: нужен формат «Название:тип»");
                    continue;
                }

                var hdr   = ln[..ci].Trim();
                var ctype = ln[(ci + 1)..].Trim().ToLower();

                if (string.IsNullOrEmpty(hdr))
                {
                    errors.Add($"Строка {lineNum}: название колонки пустое");
                    continue;
                }

                if (!_validColTypes.Contains(ctype))
                {
                    errors.Add($"Строка {lineNum}: тип «{ctype}» неверный — только text, number, toggle");
                    continue;
                }

                cols.Add(new Models.CustomSheetColumn { Header = hdr, ColumnType = ctype });
            }

            if (errors.Count > 0)
            {
                mw.ShowNotification(errors[0], NotificationType.Warning);
                return;
            }

            if (cols.Count == 0)
            {
                mw.ShowNotification("Добавьте хотя бы одну колонку", NotificationType.Warning);
                return;
            }

            var newSheet = new Models.CustomSheet { Name = name, Columns = cols };
            // AddCustomSheet сам пишет лист в настройки и сохраняет их —
            // раньше здесь была вторая, отдельная копия списка
            mw.AddCustomSheet(newSheet);
            LbCustomSheets.Items.Add(name);
            TbSheetName.Clear();
            TbSheetColumns.Clear();
            mw.ShowNotification($"Лист \u00ab{name}\u00bb добавлен", NotificationType.Success);
        }

        private void LbCustomSheets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;

            if (LbCustomSheets.SelectedItem is string name)
            {
                BdrEditSheet.Visibility = Visibility.Visible;
                TbRenameSheetName.Text  = name;

                var sheet = mw.GetCustomSheet(name);
                if (sheet != null)
                    TbRenameColumns.Text = string.Join("\n", sheet.Columns.Select(c => c.Header));
            }
            else
            {
                BdrEditSheet.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyRename_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;
            if (LbCustomSheets.SelectedItem is not string oldName) return;

            var newName = TbRenameSheetName.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                mw.ShowNotification("Укажите название листа", NotificationType.Warning);
                return;
            }

            if (newName != oldName && mw.GetCustomSheetNames().Contains(newName))
            {
                mw.ShowNotification($"Лист «{newName}» уже существует", NotificationType.Warning);
                return;
            }

            var newHeaders = new System.Collections.Generic.List<string>();
            char[] seps = { '\n', '\r' };
            foreach (var line in TbRenameColumns.Text.Split(seps, StringSplitOptions.RemoveEmptyEntries))
            {
                var ln = line.Trim();
                if (!string.IsNullOrEmpty(ln)) newHeaders.Add(ln);
            }

            if (newHeaders.Count == 0)
            {
                mw.ShowNotification("Укажите хотя бы один заголовок колонки", NotificationType.Warning);
                return;
            }

            mw.UpdateCustomSheet(oldName, newName, newHeaders);

            // Обновляем список
            int idx = LbCustomSheets.Items.IndexOf(oldName);
            if (idx >= 0) LbCustomSheets.Items[idx] = newName;
            LbCustomSheets.SelectedItem = newName;

            mw.ShowNotification($"Лист «{oldName}» обновлён", NotificationType.Success);
        }

        private void RemoveSheet_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mw) return;
            if (LbCustomSheets.SelectedItem is string name)
            {
                mw.RemoveCustomSheet(name);   // сам чистит список в настройках
                LbCustomSheets.Items.Remove(name);
                mw.ShowNotification($"Лист \u00ab{name}\u00bb удалён", NotificationType.Info);
            }
        }
        // ── Font settings ──────────────────────────────────────────────────────
        private void InitFontSettings()
        {
            CbFontFamily.SelectionChanged -= FontFamily_Changed;
            CbFontFamily.Items.Clear();

            var fonts = new[] {
                "Segoe UI", "Arial", "Calibri", "Cambria", "Century Gothic",
                "Comic Sans MS", "Consolas", "Courier New", "Georgia",
                "Impact", "Palatino Linotype", "Tahoma", "Times New Roman",
                "Trebuchet MS", "Verdana"
            };
            foreach (var f in fonts) CbFontFamily.Items.Add(f);

            CbFontFamily.SelectedItem = _config.AppFontFamily;
            SlFontSize.Value          = _config.AppFontSize;
            TbFontSizeLabel.Text      = $"{_config.AppFontSize:0} пт";
            CbFontFamily.SelectionChanged += FontFamily_Changed;
        }

        private void FontFamily_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CbFontFamily?.SelectedItem is not string family) return;
            _config.AppFontFamily = family;
            SaveConfig();
            App.ApplyFontSettings(family, _config.AppFontSize);
        }

        private void FontSize_Changed(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            double sz = Math.Round(e.NewValue);
            if (TbFontSizeLabel == null) return;
            TbFontSizeLabel.Text = $"{sz:0} пт";
            _config.AppFontSize = sz;
            SaveConfig();
            App.ApplyFontSettings(_config.AppFontFamily, sz);
        }

        private void FontDefault_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _config.AppFontFamily = "Segoe UI";
            _config.AppFontSize   = 13;
            SaveConfig();
            App.ApplyFontSettings("Segoe UI", 13);
            InitFontSettings();
        }

    }
}
