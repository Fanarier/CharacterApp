using CharacterApp.Dialogs;
using CharacterApp.Models;
using CharacterApp.Pages;
using Microsoft.Win32;
using Newtonsoft.Json;
using Octokit;
using WinApp = System.Windows.Application;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CharacterApp
{
    public partial class MainWindow : Window
    {
        private static readonly System.Text.Json.JsonSerializerOptions _jsonSaveOpts
            = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        public static MainWindow Instance { get; private set; } = null!;

        // ── Команды (Ctrl+S / Ctrl+O) ────────────────────────────────────────
        public static readonly RoutedUICommand SaveCommand = new RoutedUICommand(
            "Сохранить", "Save", typeof(MainWindow),
            new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control) });

        public static readonly RoutedUICommand OpenCommand = new RoutedUICommand(
            "Загрузить", "Open", typeof(MainWindow),
            new InputGestureCollection { new KeyGesture(Key.O, ModifierKeys.Control) });

        private const string GitHubOwner = "Fanarier";
        private const string GitHubRepo  = "CharacterApp";

        private readonly PageMain         _mainPage;
        private readonly PageDetails      _detailsPage;
        private readonly EquipmentPage    _equipmentPage;
        private readonly ActiveSkillsPage  _skillsPage;
        private readonly PassiveSkillsPage  _passivePage;
        private readonly ProficienciesPage  _profPage;
        private readonly AttacksPage         _attacksPage;
        // Пользовательские листы: имя → страница
        private readonly System.Collections.Generic.Dictionary<string, CustomSheetPage> _customPages = new();
        private readonly SettingsPage     _settingsPage;  
        private readonly StatsPage        _statsPage;

        private string _lastJsonFilePath = string.Empty;
        private bool   _hasUnsavedChanges = false;

        private AutoSaveConfig _autoSaveConfig = new AutoSaveConfig();
        private readonly DispatcherTimer _autoSaveTimer;

        private bool _sidebarOpen = true;
        private const double SidebarOpenWidth   = 250;
        private const double SidebarClosedWidth = 60;
        private const int    AnimDurationMs     = 220;

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();

            _mainPage      = new PageMain();
            _detailsPage   = new PageDetails();
            _equipmentPage = new EquipmentPage();
            _skillsPage    = new ActiveSkillsPage();
            _passivePage   = new PassiveSkillsPage();
            _profPage      = new ProficienciesPage();
            _attacksPage   = new AttacksPage();
            _settingsPage  = new SettingsPage();
            _statsPage     = new StatsPage();

            // Привязываем команды
            CommandBindings.Add(new CommandBinding(SaveCommand, (_, __) => SaveAll()));
            CommandBindings.Add(new CommandBinding(OpenCommand, (_, __) => LoadAll()));

            MainFrame.Navigate(_mainPage);

            LoadAutoSaveConfig();
            _autoSaveTimer      = new DispatcherTimer();
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            ApplyAutoSaveSettings();
            RestoreCustomSheetsFromSettings();

            // Загружаем последний файл если включена настройка
            if (_autoSaveConfig is AutoSaveConfig cfg2
                && cfg2.LoadLastOnStart
                && !string.IsNullOrEmpty(cfg2.LastFilePath)
                && System.IO.File.Exists(cfg2.LastFilePath))
            {
                // ContentRendered гарантирует что MainFrame готов к навигации
                var pathToLoad = cfg2.LastFilePath;
                ContentRendered += (_, _) => LoadFromPath(pathToLoad);
            }
        }

        // ── Заголовок и маркер несохранённых данных ───────────────────────────

        public void UpdateTitle(string characterName)
        {
            var name   = string.IsNullOrWhiteSpace(characterName) ? "Персонаж" : characterName;
            var marker = _hasUnsavedChanges ? " *" : "";
            TitleBarText.Text = $"Espires Games  —  {name}{marker}";
        }

        public void MarkUnsaved()
        {
            if (_hasUnsavedChanges) return;
            _hasUnsavedChanges = true;
            var current = TitleBarText.Text;
            if (!current.EndsWith(" *")) TitleBarText.Text = current + " *";
        }

        private void MarkSaved()
        {
            _hasUnsavedChanges = false;
           
            if (TitleBarText.Text.EndsWith(" *"))
                TitleBarText.Text = TitleBarText.Text[..^2];
        }

        // ── AutoSave ─────────────────────────────────────────────────────────



        // Загрузка из конкретного пути (используется при autoload last file)
        private void LoadFromPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                var json = File.ReadAllText(path);
                var c    = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.Character>(json);
                if (c == null) return;
                c.NormalizeItemsFromLegacy();
                _lastJsonFilePath = path;
                DistributeCharacter(c);
                UpdateTitle(c.CharacterName);
                MarkSaved();
            }
            catch (Exception ex)
            {
                ShowNotification("Ошибка загрузки: " + ex.Message, NotificationType.Error);
            }
        }

        private void SaveLastFilePath(string path)
        {
            if (_autoSaveConfig == null) return;
            if (!string.IsNullOrEmpty(path)) _autoSaveConfig.LastFilePath = path;
            PersistAutoSaveConfig();
        }

        public void PersistAutoSaveConfig()
        {
            if (_autoSaveConfig == null) return;
            var sf = System.IO.Path.Combine(App.DataDir, "appsettings.json");
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(
                    _autoSaveConfig, _jsonSaveOpts);
                System.IO.File.WriteAllText(sf, json);
            }
            catch { }
        }

        public void LoadAutoSaveConfig()
        {
            var SettingsFile = System.IO.Path.Combine(App.DataDir, "appsettings.json");
            if (File.Exists(SettingsFile))
            {
                try
                {
                    var jsonText = File.ReadAllText(SettingsFile);
                    _autoSaveConfig = System.Text.Json.JsonSerializer
                                          .Deserialize<AutoSaveConfig>(jsonText)
                                      ?? new AutoSaveConfig();
                }
                catch { _autoSaveConfig = new AutoSaveConfig(); }
            }
            else { _autoSaveConfig = new AutoSaveConfig(); }
        }

        public void ApplyAutoSaveSettings()
        {
            _autoSaveTimer.Stop();
            if (_autoSaveConfig.Enabled
             && _autoSaveConfig.IntervalMinutes > 0
             && !string.IsNullOrEmpty(_autoSaveConfig.Folder)
             && Directory.Exists(_autoSaveConfig.Folder))
            {
                _autoSaveTimer.Interval = TimeSpan.FromMinutes(_autoSaveConfig.IntervalMinutes);
                _autoSaveTimer.Start();
            }
        }

        private void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var character = CollectCharacter();
                var json      = JsonConvert.SerializeObject(character, Formatting.Indented);
                var filename  = string.Format(_autoSaveConfig.FilePattern, DateTime.Now);
                var path      = Path.Combine(_autoSaveConfig.Folder, filename);
                File.WriteAllText(path, json);

                var files = new DirectoryInfo(_autoSaveConfig.Folder)
                    .GetFiles("*.json")
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .Skip(5);
                foreach (var f in files) try { f.Delete(); } catch { }

                ShowNotification($"Автосохранено: {filename}", NotificationType.Success);
            }
            catch (Exception ex)
            {
                ShowNotification("Ошибка автосохранения: " + ex.Message, NotificationType.Error);
            }
        }

        // ── Сохранение / загрузка ─────────────────────────────────────────────

        private Character CollectCharacter()
        {
            var c = new Character();
            _mainPage.FillCharacter(c);
            _detailsPage.FillCharacter(c);
            _equipmentPage.FillCharacter(c);
            _skillsPage.FillCharacter(c);
            _passivePage.FillCharacter(c);
            _profPage.FillCharacter(c);
            _attacksPage.FillCharacter(c);
            foreach (var kv in _customPages) kv.Value.FillCharacter(c);
            _statsPage.FillCharacter(c);
            return c;
        }

        private void DistributeCharacter(Character c)
        {
            _mainPage.ApplyCharacter(c);
            _detailsPage.ApplyCharacter(c);
            _equipmentPage.ApplyCharacter(c);
            _skillsPage.ApplyCharacter(c);
            _passivePage.ApplyCharacter(c);
            _profPage.ApplyCharacter(c);
            _attacksPage.ApplyCharacter(c);
            RebuildCustomPages(c);
            _statsPage.ApplyCharacter(c);
            UpdateTitle(c.CharacterName);
        }

        public void SaveAll()
        {
            if (!string.IsNullOrEmpty(_lastJsonFilePath) && File.Exists(_lastJsonFilePath))
            {
                DoSave(_lastJsonFilePath);
                MarkSaved();
                ShowNotification("Данные сохранены!", NotificationType.Success);
            }
            else
            {
                SaveAllAs();
            }
        }

        public void SaveAllAs()
        {
            var dlg = new SaveFileDialog { Filter = "JSON файлы (*.json)|*.json", DefaultExt = ".json" };
            if (dlg.ShowDialog() == true)
            {
                _lastJsonFilePath = dlg.FileName;
                DoSave(_lastJsonFilePath);
                MarkSaved();
                ShowNotification("Данные сохранены!", NotificationType.Success);
            }
        }

        public void LoadAll()
        {
            var dlg = new OpenFileDialog { Filter = "JSON файлы (*.json)|*.json" };
            if (dlg.ShowDialog() == true)
            {
                _lastJsonFilePath = dlg.FileName;
                try
                {
                    var json      = File.ReadAllText(_lastJsonFilePath);
                    var character = JsonConvert.DeserializeObject<Character>(json) ?? new Character();
                    character.NormalizeItemsFromLegacy();
                    DistributeCharacter(character);
                    MarkSaved();
                    ShowNotification("Данные загружены!", NotificationType.Success);
                }
                catch (Exception ex)
                {
                    ShowNotification("Ошибка при загрузке: " + ex.Message, NotificationType.Error);
                }
            }
        }

        private void DoSave(string path)
        {
            var json = JsonConvert.SerializeObject(CollectCharacter(), Formatting.Indented);
            File.WriteAllText(path, json);
        }


        // ── Хелперы для StatsPage (нужны BM и Уровень) ───────────────────────

        /// <summary>Парсит Мастерство как число: "+4" → 4, "4" → 4, "-1" → -1.</summary>
        public int GetCurrentBM()
        {
            var c = new Character();
            _mainPage.FillCharacter(c);
            var s = c.Mastery?.Trim() ?? "";
            if (s.StartsWith("+")) s = s[1..];
            return int.TryParse(s, out var v) ? v : 0;
        }

        /// <summary>Возвращает текущий уровень персонажа.</summary>
        public int GetCurrentLevel()
        {
            var c = new Character();
            _detailsPage.FillCharacter(c);
            return c.Level;
        }


        // ── Кастомные диалоги подтверждения ──────────────────────────────────
        private static bool Confirm(string message, string title = "",
                                    ConfirmDialogIcon icon = ConfirmDialogIcon.Warning)
        {
            var dlg = new ConfirmDialog(message, title, ConfirmMode.YesNo, icon) { Owner = WinApp.Current.MainWindow };
            dlg.ShowDialog();
            return dlg.Result == ConfirmDialog.ConfirmResult.Yes;
        }

        private static ConfirmDialog.ConfirmResult ConfirmYNC(string message, string title = "")
        {
            var dlg = new ConfirmDialog(message, title, ConfirmMode.YesNoCancel, ConfirmDialogIcon.Question) { Owner = WinApp.Current.MainWindow };
            dlg.ShowDialog();
            return dlg.Result;
        }

        // ── Сброс ────────────────────────────────────────────────────────────

        public void ResetAll()
        {
            if (!Confirm("Сбросить все данные персонажа?\nНесохранённые изменения будут потеряны.", "Сброс данных")) return;

            DistributeCharacter(new Character());
            _lastJsonFilePath  = string.Empty;
            _hasUnsavedChanges = false;
            TitleBarText.Text  = "Espires Games";
            ShowNotification("Данные сброшены", NotificationType.Info);
        }

        // ── UI ───────────────────────────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { Maximize_Click(sender, e); return; }
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Minimize_Click (object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click (object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void Close_Click    (object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var r = ConfirmYNC("Есть несохранённые изменения. Сохранить перед выходом?", "Выход");
                if (r == ConfirmDialog.ConfirmResult.Cancel) return;
                if (r == ConfirmDialog.ConfirmResult.Yes) SaveAll();
            }
            Close();
        }

        public async void ShowNotification(string message, NotificationType type = NotificationType.Info)
        {
            var control = new NotificationControl(message, type);
            await control.ShowAsync(NotificationHost.Children);
        }

        // ── Навигация ─────────────────────────────────────────────────────────

        private void MainPage_Click   (object sender, RoutedEventArgs e) => MainFrame.Navigate(_mainPage);
        private void Details_Click    (object sender, RoutedEventArgs e) => MainFrame.Navigate(_detailsPage);
        private void Equipment_Click  (object sender, RoutedEventArgs e) => MainFrame.Navigate(_equipmentPage);
        private void ActiveSkills_Click (object sender, RoutedEventArgs e) => MainFrame.Navigate(_skillsPage);
        private void PassiveSkills_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_passivePage);
        private void Proficiencies_Click  (object sender, RoutedEventArgs e) => MainFrame.Navigate(_profPage);
        private void Attacks_Click        (object sender, RoutedEventArgs e) => MainFrame.Navigate(_attacksPage);

        // ── Пользовательские листы ──────────────────────────────────────────

        // Восстанавливает кастомные страницы из appsettings (без загрузки JSON персонажа)
        private void RestoreCustomSheetsFromSettings()
        {
            if (_autoSaveConfig?.SavedCustomSheets == null) return;
            foreach (var sheet in _autoSaveConfig.SavedCustomSheets)
            {
                if (_customPages.ContainsKey(sheet.Name)) continue;
                var page = new CustomSheetPage(sheet);
                _customPages[sheet.Name] = page;
                var name = sheet.Name;
                var btn  = new Button
                {
                    Content = sheet.Name,
                    Margin  = new System.Windows.Thickness(0, 4, 0, 4),
                    Tag     = "custom:" + sheet.Name
                };
                btn.Click += (_, _) => MainFrame.Navigate(_customPages[name]);
                int idx = MenuStack.Children.Count - 1;
                MenuStack.Children.Insert(idx, btn);
            }
        }

        private void RebuildCustomPages(Character c)
        {
            // Удаляем старые кастомные кнопки из сайдбара
            var toRemove = new System.Collections.Generic.List<System.Windows.UIElement>();
            foreach (System.Windows.UIElement ch in MenuStack.Children)
                if (ch is Button btn && btn.Tag is string tag && tag.StartsWith("custom:", System.StringComparison.Ordinal))
                    toRemove.Add(ch);
            foreach (var el in toRemove) MenuStack.Children.Remove(el);
            _customPages.Clear();

            // Создаём страницы и кнопки по данным Character
            // Синхронизируем SavedCustomSheets: добавляем листы из JSON
            // (Clear не делаем — чтобы не потерять листы которые ещё не в JSON)
            if (_autoSaveConfig != null)
            {
                foreach (var sheet in c.CustomSheets)
                {
                    _autoSaveConfig.SavedCustomSheets.RemoveAll(s => s.Name == sheet.Name);
                    _autoSaveConfig.SavedCustomSheets.Add(sheet);
                }
                PersistAutoSaveConfig();
            }

            foreach (var sheet in c.CustomSheets)
            {
                var page = new CustomSheetPage(sheet);
                _customPages[sheet.Name] = page;
                var name = sheet.Name; // capture
                var btn  = new Button
                {
                    Content = sheet.Name,
                    Margin  = new System.Windows.Thickness(0, 4, 0, 4),
                    Tag     = "custom:" + sheet.Name
                };
                btn.Click += (_, _) => MainFrame.Navigate(_customPages[name]);
                // Вставляем перед кнопкой Настройки
                int idx = MenuStack.Children.Count - 1;
                MenuStack.Children.Insert(idx, btn);
            }
        }

        public void AddCustomSheet(CustomSheet sheet)
        {
            // Создаём страницу
            var page = new CustomSheetPage(sheet);
            _customPages[sheet.Name] = page;

            // Запоминаем в конфиге чтобы восстановить при следующем запуске
            _autoSaveConfig.SavedCustomSheets.RemoveAll(s => s.Name == sheet.Name);
            _autoSaveConfig.SavedCustomSheets.Add(sheet);
            PersistAutoSaveConfig();

            // Кнопка в сайдбар
            var name = sheet.Name;
            var btn  = new Button
            {
                Content = sheet.Name,
                Margin  = new System.Windows.Thickness(0, 4, 0, 4),
                Tag     = "custom:" + sheet.Name
            };
            btn.Click += (_, _) => MainFrame.Navigate(_customPages[name]);
            int idx = MenuStack.Children.Count - 1;
            MenuStack.Children.Insert(idx, btn);

            MarkUnsaved();
        }

        public void RemoveCustomSheet(string name)
        {
            // 1. Убираем страницу из памяти
            _customPages.Remove(name);

            // 2. Убираем из конфига + сохраняем конфиг сразу
            _autoSaveConfig?.SavedCustomSheets?.RemoveAll(s => s.Name == name);
            PersistAutoSaveConfig();

            // 3. Убираем кнопку из сайдбара
            var toRemove = new System.Collections.Generic.List<System.Windows.UIElement>();
            foreach (System.Windows.UIElement ch in MenuStack.Children)
                if (ch is Button btn && btn.Tag?.ToString() == "custom:" + name)
                    toRemove.Add(ch);
            foreach (var el in toRemove) MenuStack.Children.Remove(el);

            // 4. Если есть открытый файл — немедленно пересохраняем JSON персонажа
            //    чтобы CustomSheets в файле тоже обновился (без этого при загрузке
            //    RebuildCustomPages восстановит лист из JSON)
            if (!string.IsNullOrEmpty(_lastJsonFilePath) && File.Exists(_lastJsonFilePath))
            {
                DoSave(_lastJsonFilePath);
                MarkSaved();
            }
            else
            {
                MarkUnsaved();
            }
        }

        // Собирает текущий Character из всех страниц
        private Character GetCurrentCharacter()
        {
            var c = new Character();
            _mainPage.FillCharacter(c);
            _detailsPage.FillCharacter(c);
            _equipmentPage.FillCharacter(c);
            _skillsPage.FillCharacter(c);
            _passivePage.FillCharacter(c);
            _profPage.FillCharacter(c);
            _attacksPage.FillCharacter(c);
            foreach (var kv in _customPages) kv.Value.FillCharacter(c);
            _statsPage.FillCharacter(c);
            return c;
        }

        public static System.Collections.Generic.IEnumerable<string> GetBuiltinPageNames()

        {
            return _builtinPageNames;
        }
        private static readonly string[] _builtinPageNames =
            { "MainPage", "Details", "Equipment", "Stats",
              "ActiveSkills", "PassiveSkills", "Proficiencies", "Attacks" };

        public void SetPageVisible(string pageKey, bool visible)
        {
            foreach (System.Windows.UIElement ch in MenuStack.Children)
            {
                if (ch is Button btn && btn.Tag?.ToString() == "builtin:" + pageKey)
                {
                    btn.Visibility = visible
                        ? System.Windows.Visibility.Visible
                        : System.Windows.Visibility.Collapsed;
                    break;
                }
            }
        }
        private void Settings_Click   (object sender, RoutedEventArgs e) => MainFrame.Navigate(_settingsPage);
        private void Stats_Click       (object sender, RoutedEventArgs e) => MainFrame.Navigate(_statsPage);

        private void QuickSave_Click  (object sender, RoutedEventArgs e) => SaveAll();
        private void SaveAs_Click     (object sender, RoutedEventArgs e) => SaveAllAs();
        private void LoadJSON_Click   (object sender, RoutedEventArgs e) => LoadAll();
        private void ResetAll_Click   (object sender, RoutedEventArgs e) => ResetAll();

        // ── Сайдбар ───────────────────────────────────────────────────────────

        private async void BtnToggleMenu_Click(object sender, RoutedEventArgs e)
            => await ToggleSidebarAsync(!_sidebarOpen);

        private async Task ToggleSidebarAsync(bool open)
        {
            if (open == _sidebarOpen) return;
            _sidebarOpen = open;

            const int fadeMs   = 140;
            const int fadeInMs = 160;

            if (!open)
            {
                var tasks = new List<Task>
                {
                    AnimateOpacityAsync(MenuStack,          MenuStack.Opacity,          0, fadeMs),
                    AnimateOpacityAsync(BottomButtonsPanel, BottomButtonsPanel.Opacity, 0, fadeMs),
                    AnimateOpacityAsync(TbMenuSearch,       TbMenuSearch.Opacity,       0, fadeMs)
                };
                foreach (var child in MenuStack.Children)
                    if (child is Button btn && btn.Content is StackPanel sp)
                        foreach (UIElement tbEl in sp.Children) if (tbEl is TextBlock tb)
                        {
                            if (tb.Visibility != Visibility.Visible) { tb.Visibility = Visibility.Visible; tb.Opacity = 1; }
                            tasks.Add(AnimateOpacityAsync(tb, tb.Opacity, 0, fadeMs));
                        }

                tasks.Add(AnimateWidthAsync(Sidebar, SidebarClosedWidth, AnimDurationMs));
                await Task.WhenAll(tasks);

                foreach (var child in MenuStack.Children)
                    if (child is Button btn && btn.Content is StackPanel sp)
                        foreach (UIElement tbEl in sp.Children) if (tbEl is TextBlock tb)
                        { tb.Visibility = Visibility.Collapsed; tb.Opacity = 1; }

                MenuStack.Visibility          = Visibility.Collapsed;
                BottomButtonsPanel.Visibility  = Visibility.Collapsed;
                TbMenuSearch.Visibility        = Visibility.Collapsed;
            }
            else
            {
                MenuStack.Visibility          = Visibility.Visible;
                BottomButtonsPanel.Visibility  = Visibility.Visible;
                TbMenuSearch.Visibility        = Visibility.Visible;

                foreach (var child in MenuStack.Children)
                    if (child is Button btn && btn.Content is StackPanel sp)
                        foreach (UIElement tbEl in sp.Children) if (tbEl is TextBlock tb)
                        { tb.Visibility = Visibility.Visible; tb.Opacity = 0; }

                MenuStack.Opacity = 0; BottomButtonsPanel.Opacity = 0; TbMenuSearch.Opacity = 0;

                var tasks = new List<Task>
                {
                    AnimateWidthAsync   (Sidebar,            SidebarOpenWidth, AnimDurationMs),
                    AnimateOpacityAsync (MenuStack,          0, 1, fadeInMs),
                    AnimateOpacityAsync (BottomButtonsPanel, 0, 1, fadeInMs),
                    AnimateOpacityAsync (TbMenuSearch,       0, 1, fadeInMs)
                };
                foreach (var child in MenuStack.Children)
                    if (child is Button btn && btn.Content is StackPanel sp)
                        foreach (UIElement tbEl in sp.Children) if (tbEl is TextBlock tb)
                            tasks.Add(AnimateOpacityAsync(tb, 0, 1, fadeInMs));

                await Task.WhenAll(tasks);

                MenuStack.Opacity = 1; BottomButtonsPanel.Opacity = 1; TbMenuSearch.Opacity = 1;
                foreach (var child in MenuStack.Children)
                    if (child is Button btn && btn.Content is StackPanel sp)
                        foreach (UIElement tbEl in sp.Children) if (tbEl is TextBlock tb)
                            tb.Opacity = 1;
            }
        }

        private static Task AnimateWidthAsync(FrameworkElement element, double to, int durationMs)
        {
            var tcs  = new TaskCompletionSource<bool>();
            var anim = new DoubleAnimation { To = to, Duration = TimeSpan.FromMilliseconds(durationMs), AccelerationRatio = 0.2, DecelerationRatio = 0.2 };
            anim.Completed += (s, e) => tcs.TrySetResult(true);
            element.BeginAnimation(WidthProperty, anim);
            return tcs.Task;
        }

        private static Task AnimateOpacityAsync(UIElement element, double from, double to, int durationMs)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (element == null) { tcs.SetResult(true); return tcs.Task; }
            if (element.Visibility == Visibility.Collapsed && to > 0) element.Visibility = Visibility.Visible;
            var anim = new DoubleAnimation { From = from, To = to, Duration = TimeSpan.FromMilliseconds(durationMs), AccelerationRatio = 0.2, DecelerationRatio = 0.2 };
            anim.Completed += (s, e) => tcs.TrySetResult(true);
            element.BeginAnimation(UIElement.OpacityProperty, anim);
            return tcs.Task;
        }

        private void TbMenuSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = TbMenuSearch.Text?.Trim() ?? string.Empty;
            foreach (var child in MenuStack.Children)
            {
                if (child is Button btn)
                {
                    string contentText = btn.Tag?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(contentText) && btn.Content is StackPanel sp)
                        foreach (UIElement spChild in sp.Children)
                            if (spChild is TextBlock spTb) { contentText = spTb.Text; break; }
                    if (string.IsNullOrEmpty(contentText))
                        contentText = btn.Content?.ToString() ?? string.Empty;

                    btn.Visibility = string.IsNullOrEmpty(query) ||
                                     contentText.Contains(query, StringComparison.OrdinalIgnoreCase)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else if (child is TextBlock tb2)
                    tb2.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ── Обновления ───────────────────────────────────────────────────────

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowNotification("Проверка обновлений...", NotificationType.Info);
                var client   = new GitHubClient(new ProductHeaderValue("CharacterApp"));
                var releases = await client.Repository.Release.GetAll(GitHubOwner, GitHubRepo);
                var latest   = releases.FirstOrDefault();
                if (latest == null) { ShowNotification("Релизы не найдены", NotificationType.Info); return; }

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (!Version.TryParse(latest.TagName.TrimStart('v'), out var latestVersion))
                { ShowNotification("Не удалось распознать версию релиза", NotificationType.Warning); return; }

                if (latestVersion <= currentVersion)
                { ShowNotification("У вас уже установлена последняя версия", NotificationType.Success); return; }

                if (!Confirm(
                    $"Доступна версия {latest.TagName}. Открыть страницу релиза?",
                    "Обновление", ConfirmDialogIcon.Info)) return;

                // Открываем страницу релиза в браузере — пользователь скачивает сам
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = latest.HtmlUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowNotification("Ошибка при проверке обновлений: " + ex.Message, NotificationType.Error);
            }
        }
    }
}
