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
using System.Windows.Media;
using System.Threading.Tasks;
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

        private readonly PageMain          _mainPage;
        private readonly PageDetails       _detailsPage;
        private readonly EquipmentPage     _equipmentPage;
        private readonly ActiveSkillsPage  _skillsPage;
        private readonly PassiveSkillsPage _passivePage;
        private readonly ProficienciesPage _profPage;
        private readonly AttacksPage       _attacksPage;
        private readonly DicePage          _dicePage;
        // Lazy pages
        private JournalPage?   _journalPage;
        private ResourcesPage? _resourcesPage;
        private readonly System.Collections.Generic.Dictionary<string, CustomSheetPage> _customPages = new();
        private readonly SettingsPage _settingsPage;
        private readonly StatsPage    _statsPage;

        // История навигации для кнопки "Назад"
        private System.Windows.Controls.Page? _previousPage;
        private string _previousTag = "";
        private System.Windows.Controls.Page? _currentPage;
        private string _currentTag = "";

        // ── Несколько персонажей ──────────────────────────────────────────────
        private readonly System.Collections.ObjectModel.ObservableCollection<CharacterSlot>
            _characterSlots = new();
        private int _activeSlotIndex = 0;

        private string _lastJsonFilePath = string.Empty;
        private bool   _hasUnsavedChanges = false;

        private AutoSaveConfig _autoSaveConfig = new AutoSaveConfig();
        private AppSettings    _appSettings    = new AppSettings();
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
            _dicePage      = new DicePage();

            // Привязываем команды
            CommandBindings.Add(new CommandBinding(SaveCommand, (_, __) => SaveAll()));
            CommandBindings.Add(new CommandBinding(OpenCommand, (_, __) => LoadAll()));

            // Register keyboard shortcuts in code-behind (avoids x:Static XAML designer errors)
            InputBindings.Add(new KeyBinding(SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control)));
            InputBindings.Add(new KeyBinding(OpenCommand, new KeyGesture(Key.O, ModifierKeys.Control)));

            MainFrame.Navigate(_mainPage);
            HighlightActiveButton("builtin:MainPage");
            InitCharacterSlots();

            // Загружаем единые настройки и синхронизируем в AutoSaveConfig
            _appSettings    = AppSettings.Load();
            SyncSettingsToAutoSaveConfig();
            _autoSaveTimer      = new DispatcherTimer();
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            ApplyAutoSaveSettings();
            RestoreCustomSheetsFromSettings();

            // Загружаем последний файл если включена настройка
            if (_appSettings.LoadLastOnStart
                && !string.IsNullOrEmpty(_appSettings.LastFilePath)
                && System.IO.File.Exists(_appSettings.LastFilePath))
            {
                var pathToLoad = _appSettings.LastFilePath;
                ContentRendered += (_, _) => LoadFromPath(pathToLoad);
            }
        }

        // Синхронизация нового AppSettings → старый AutoSaveConfig (обратная совместимость)
        private void SyncSettingsToAutoSaveConfig()
        {
            _autoSaveConfig.Enabled         = _appSettings.AutoSaveEnabled;
            _autoSaveConfig.IntervalMinutes  = _appSettings.AutoSaveIntervalMinutes;
            _autoSaveConfig.Folder           = _appSettings.AutoSaveFolder;
            _autoSaveConfig.FilePattern      = _appSettings.AutoSaveFilePattern;
            _autoSaveConfig.LoadLastOnStart  = _appSettings.LoadLastOnStart;
            _autoSaveConfig.LastFilePath     = _appSettings.LastFilePath;
            _autoSaveConfig.HiddenPages      = _appSettings.HiddenPages;
            _autoSaveConfig.CustomSheetNames = _appSettings.CustomSheetNames;
            _autoSaveConfig.SavedCustomSheets = _appSettings.SavedCustomSheets;
        }

        private void SyncAutoSaveConfigToSettings()
        {
            _appSettings.AutoSaveEnabled         = _autoSaveConfig.Enabled;
            _appSettings.AutoSaveIntervalMinutes = _autoSaveConfig.IntervalMinutes;
            _appSettings.AutoSaveFolder          = _autoSaveConfig.Folder;
            _appSettings.AutoSaveFilePattern     = _autoSaveConfig.FilePattern;
            _appSettings.LoadLastOnStart         = _autoSaveConfig.LoadLastOnStart;
            _appSettings.LastFilePath            = _autoSaveConfig.LastFilePath;
            _appSettings.HiddenPages             = _autoSaveConfig.HiddenPages;
            _appSettings.CustomSheetNames        = _autoSaveConfig.CustomSheetNames;
            _appSettings.SavedCustomSheets       = _autoSaveConfig.SavedCustomSheets;
        }

        // ── Заголовок и маркер несохранённых данных ───────────────────────────

        public void UpdateTitle(string characterName)
        {
            var name   = string.IsNullOrWhiteSpace(characterName) ? "Персонаж" : characterName;
            var marker = _hasUnsavedChanges ? " *" : "";
            TitleBarText.Text = $"Espires Games  —  {name}{marker}";
            // Sync tab label
            UpdateActiveSlotName(name);
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
            // Save to old file for backward compat
            var sf = System.IO.Path.Combine(App.DataDir, "appsettings.json");
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(
                    _autoSaveConfig, _jsonSaveOpts);
                System.IO.File.WriteAllText(sf, json);
            }
            catch { }
            // Also sync to unified settings and save
            SyncAutoSaveConfigToSettings();
            try { _appSettings.Save(); } catch { }
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
            // Пропускаем автосохранение если данные не изменились
            if (!_hasUnsavedChanges) return;

            try
            {
                var character = CollectCharacter();
                var json      = JsonConvert.SerializeObject(character, Formatting.Indented);
                var filename  = string.Format(_autoSaveConfig.FilePattern, DateTime.Now);
                var path      = Path.Combine(_autoSaveConfig.Folder, filename);
                File.WriteAllText(path, json);
                SaveLastFilePath(path);

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
            // Journal & Resources (only if pages were opened)
            if (_journalPage   != null) c.JournalEntries = _journalPage.GetEntries();
            if (_resourcesPage != null) { c.HpData = _resourcesPage.GetHpData(); c.Resources = _resourcesPage.GetResources(); }
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
            // Journal & Resources — apply lazily (create page if needed)
            _journalPage ??= new JournalPage();
            _journalPage.LoadEntries(c.JournalEntries ?? new());
            _resourcesPage ??= new ResourcesPage();
            _resourcesPage.LoadData(c.HpData, c.Resources);
            UpdateTitle(c.CharacterName);
        }

        public void SaveAll()
        {
            if (!string.IsNullOrEmpty(_lastJsonFilePath))
            {
                if (!File.Exists(_lastJsonFilePath))
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Файл сохранения не найден:\n{_lastJsonFilePath}\n\nВыбрать новое место сохранения?",
                        "Файл не найден", System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);
                    if (result == System.Windows.MessageBoxResult.Yes)
                        SaveAllAs();
                    return;
                }
                DoSave(_lastJsonFilePath);
                SaveLastFilePath(_lastJsonFilePath);
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
                SaveLastFilePath(_lastJsonFilePath);   // ← запоминаем путь
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
                SaveLastFilePath(_lastJsonFilePath);   // ← запоминаем путь
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

        private async void NavigateTo(System.Windows.Controls.Page page, string tag)
        {
            // Track history for back button
            if (_currentPage != null && _currentPage != page)
            {
                _previousPage = _currentPage;
                _previousTag  = _currentTag;
            }
            _currentPage = page;
            _currentTag  = tag;

            // Show back button only when there's history
            BtnBack.Visibility = _previousPage != null
                ? Visibility.Visible : Visibility.Collapsed;

            // Fade out current page
            if (MainFrame.Opacity > 0)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
                MainFrame.BeginAnimation(OpacityProperty, fadeOut);
                await Task.Delay(120);
            }

            MainFrame.Navigate(page);
            HighlightActiveButton(tag);

            // Fade in new page
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
            MainFrame.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_previousPage == null) return;
            var target    = _previousPage;
            var targetTag = _previousTag;
            _previousPage = null;
            _previousTag  = "";
            NavigateTo(target, targetTag);
        }

        private void HighlightActiveButton(string activeTag)
        {
            foreach (var ch in MenuStack.Children)
            {
                if (ch is not Button btn) continue;
                var tag = btn.Tag?.ToString() ?? "";
                bool isActive = tag == activeTag;
                btn.Style = isActive
                    ? (Style)FindResource("SidebarNavButtonActive")
                    : (Style)FindResource("SidebarNavButton");
            }
        }

        private void MainPage_Click    (object sender, RoutedEventArgs e) => NavigateTo(_mainPage,      "builtin:MainPage");
        private void Details_Click     (object sender, RoutedEventArgs e) => NavigateTo(_detailsPage,   "builtin:Details");
        private void Equipment_Click   (object sender, RoutedEventArgs e) => NavigateTo(_equipmentPage, "builtin:Equipment");
        private void ActiveSkills_Click(object sender, RoutedEventArgs e) => NavigateTo(_skillsPage,    "builtin:ActiveSkills");
        private void PassiveSkills_Click(object sender, RoutedEventArgs e) => NavigateTo(_passivePage,  "builtin:PassiveSkills");
        private void Proficiencies_Click(object sender, RoutedEventArgs e) => NavigateTo(_profPage,     "builtin:Proficiencies");
        private void Attacks_Click     (object sender, RoutedEventArgs e) => NavigateTo(_attacksPage,   "builtin:Attacks");

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
                    Tag     = "custom:" + sheet.Name,
                    Style   = (Style)FindResource("SidebarNavButton")
                };
                btn.Click += (_, _) => { MainFrame.Navigate(_customPages[name]); HighlightActiveButton("custom:" + name); };
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
                page.ApplyCharacter(c);          // ← загружаем строки из JSON
                _customPages[sheet.Name] = page;
                var name = sheet.Name; // capture
                var btn  = new Button
                {
                    Content = sheet.Name,
                    Margin  = new System.Windows.Thickness(0, 4, 0, 4),
                    Tag     = "custom:" + sheet.Name,
                    Style   = (Style)FindResource("SidebarNavButton")
                };
                btn.Click += (_, _) => { MainFrame.Navigate(_customPages[name]); HighlightActiveButton("custom:" + name); };
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
                Tag     = "custom:" + sheet.Name,
                Style   = (Style)FindResource("SidebarNavButton")
            };
            btn.Click += (_, _) => { MainFrame.Navigate(_customPages[name]); HighlightActiveButton("custom:" + name); };
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

        /// <summary>Возвращает имена всех активных кастомных листов.</summary>
        public System.Collections.Generic.IEnumerable<string> GetCustomSheetNames()
            => _customPages.Keys;

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
        private void Settings_Click   (object sender, RoutedEventArgs e) => NavigateTo(_settingsPage, "builtin:Settings");
        private void Stats_Click      (object sender, RoutedEventArgs e) => NavigateTo(_statsPage,    "builtin:Stats");
        private void Dice_Click       (object sender, RoutedEventArgs e) => NavigateTo(_dicePage,     "builtin:Dice");
        private void Journal_Click    (object sender, RoutedEventArgs e)
        {
            _journalPage ??= new JournalPage();
            NavigateTo(_journalPage, "builtin:Journal");
        }
        private void Resources_Click  (object sender, RoutedEventArgs e)
        {
            _resourcesPage ??= new ResourcesPage();
            NavigateTo(_resourcesPage, "builtin:Resources");
        }

        private void ExportXps_Click(object sender, RoutedEventArgs e)
            => Helpers.PdfExporter.Export(CollectCharacter());

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
                    AnimateOpacityAsync(MenuStack,            MenuStack.Opacity,            0, fadeMs),
                    AnimateOpacityAsync(BottomButtonsPanel,   BottomButtonsPanel.Opacity,   0, fadeMs),
                    AnimateOpacityAsync(TbMenuSearch,         TbMenuSearch.Opacity,         0, fadeMs),
                    AnimateOpacityAsync(CharacterTabsBorder,  CharacterTabsBorder.Opacity,  0, fadeMs),
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

                MenuStack.Visibility           = Visibility.Collapsed;
                BottomButtonsPanel.Visibility  = Visibility.Collapsed;
                TbMenuSearch.Visibility        = Visibility.Collapsed;
                CharacterTabsBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                MenuStack.Visibility           = Visibility.Visible;
                BottomButtonsPanel.Visibility  = Visibility.Visible;
                TbMenuSearch.Visibility        = Visibility.Visible;
                CharacterTabsBorder.Visibility = Visibility.Visible;
                CharacterTabsBorder.Opacity    = 0;

                foreach (var child in MenuStack.Children)
                    if (child is Button btn && btn.Content is StackPanel sp)
                        foreach (UIElement tbEl in sp.Children) if (tbEl is TextBlock tb)
                        { tb.Visibility = Visibility.Visible; tb.Opacity = 0; }

                MenuStack.Opacity = 0; BottomButtonsPanel.Opacity = 0; TbMenuSearch.Opacity = 0;

                var tasks = new List<Task>
                {
                    AnimateWidthAsync   (Sidebar,              SidebarOpenWidth, AnimDurationMs),
                    AnimateOpacityAsync (MenuStack,            0, 1, fadeInMs),
                    AnimateOpacityAsync (BottomButtonsPanel,   0, 1, fadeInMs),
                    AnimateOpacityAsync (TbMenuSearch,         0, 1, fadeInMs),
                    AnimateOpacityAsync (CharacterTabsBorder,  0, 1, fadeInMs),
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
            var query    = TbMenuSearch.Text?.Trim() ?? string.Empty;
            bool anyVisible = false;

            // Delegate search to the current page if it supports it
            if (MainFrame.Content is Pages.IPageSearchable searchablePage)
                searchablePage.FilterItems(query);

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

                    bool visible = string.IsNullOrEmpty(query) ||
                                   contentText.Contains(query, StringComparison.OrdinalIgnoreCase);
                    btn.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                    if (visible) anyVisible = true;
                }
                else if (child is TextBlock tb2)
                    tb2.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Показываем/скрываем плашку "Ничего не найдено"
            var noResultsLbl = MenuStack.Children.OfType<TextBlock>()
                                         .FirstOrDefault(t => t.Tag?.ToString() == "NoResults");
            if (noResultsLbl == null && !string.IsNullOrEmpty(query))
            {
                noResultsLbl = new TextBlock
                {
                    Tag        = "NoResults",
                    Text       = "Ничего не найдено",
                    Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush"),
                    FontSize   = 12,
                    Margin     = new Thickness(16, 8, 0, 4),
                    Visibility = Visibility.Collapsed
                };
                MenuStack.Children.Add(noResultsLbl);
            }
            if (noResultsLbl != null)
                noResultsLbl.Visibility = (!anyVisible && !string.IsNullOrEmpty(query))
                    ? Visibility.Visible : Visibility.Collapsed;
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

        // ══════════════════════════════════════════════════════════════════════
        // MULTI-CHARACTER SUPPORT
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Инициализирует первый слот при старте.</summary>
        private void InitCharacterSlots()
        {
            _characterSlots.Clear();
            _characterSlots.Add(new CharacterSlot { DisplayName = "Персонаж 1" });
            _activeSlotIndex = 0;
            RebuildCharacterTabs();
        }

        /// <summary>Сохраняет текущего персонажа в активный слот, переключается на targetIdx.</summary>
        public void SwitchToSlot(int targetIdx)
        {
            if (targetIdx == _activeSlotIndex) return;
            if (targetIdx < 0 || targetIdx >= _characterSlots.Count) return;

            // Save current
            _characterSlots[_activeSlotIndex].SavedCharacter = CollectCharacter();
            _characterSlots[_activeSlotIndex].FilePath       = _lastJsonFilePath;
            _characterSlots[_activeSlotIndex].HasChanges     = _hasUnsavedChanges;

            // Switch
            _activeSlotIndex  = targetIdx;
            var slot          = _characterSlots[targetIdx];
            _lastJsonFilePath = slot.FilePath ?? "";

            // Rebuild pages for new character
            var c = slot.SavedCharacter ?? new Character();
            DistributeCharacter(c);
            MarkSaved();
            RebuildCharacterTabs();
        }

        /// <summary>Добавляет новый пустой слот.</summary>
        public void AddCharacterSlot()
        {
            // Save current first
            if (_characterSlots.Count > 0)
                _characterSlots[_activeSlotIndex].SavedCharacter = CollectCharacter();

            int num = _characterSlots.Count + 1;
            _characterSlots.Add(new CharacterSlot { DisplayName = $"Персонаж {num}" });
            SwitchToSlot(_characterSlots.Count - 1);
        }

        /// <summary>Удаляет активный слот.</summary>
        public void RemoveActiveSlot()
        {
            if (_characterSlots.Count <= 1)
            {
                ShowNotification("Нельзя удалить единственного персонажа", NotificationType.Warning);
                return;
            }
            _characterSlots.RemoveAt(_activeSlotIndex);
            _activeSlotIndex = Math.Max(0, _activeSlotIndex - 1);
            var c = _characterSlots[_activeSlotIndex].SavedCharacter ?? new Character();
            _lastJsonFilePath = _characterSlots[_activeSlotIndex].FilePath ?? "";
            DistributeCharacter(c);
            MarkSaved();
            RebuildCharacterTabs();
        }

        /// <summary>Обновляет имя таба активного слота по имени персонажа.</summary>
        public void UpdateActiveSlotName(string name)
        {
            if (_characterSlots.Count > _activeSlotIndex)
                _characterSlots[_activeSlotIndex].DisplayName =
                    string.IsNullOrWhiteSpace(name) ? $"Персонаж {_activeSlotIndex + 1}" : name;
            RebuildCharacterTabs();
        }

        /// <summary>Перестраивает панель табов в сайдбаре.</summary>
        public void RebuildCharacterTabs()
        {
            CharacterTabsPanel.Children.Clear();
            for (int i = 0; i < _characterSlots.Count; i++)
            {
                int idx    = i;
                var slot   = _characterSlots[i];
                bool active = i == _activeSlotIndex;

                var tab = new Border
                {
                    MinWidth        = 0,
                    MaxWidth        = 160,
                    Padding         = new Thickness(8, 5, 8, 5),
                    Margin          = new Thickness(2, 0, 2, 0),
                    CornerRadius    = new CornerRadius(8, 8, 0, 0),
                    Cursor          = System.Windows.Input.Cursors.Hand,
                    ToolTip         = slot.DisplayName,
                };
                tab.Background = active
                    ? (Brush)FindResource("AccentGradientV")
                    : (Brush)FindResource("SurfaceBrush");
                tab.BorderBrush = (Brush)FindResource("BorderAccentBrush");
                tab.BorderThickness = new Thickness(1, 1, 1, active ? 0 : 1);

                var lbl = new TextBlock
                {
                    Text           = slot.DisplayName,
                    FontSize       = 11.5,
                    Foreground     = active ? Brushes.White : (Brush)FindResource("TextMutedBrush"),
                    TextTrimming   = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    MaxWidth       = 120,
                };
                tab.Child = lbl;
                tab.MouseLeftButtonDown += (_, _) => SwitchToSlot(idx);
                CharacterTabsPanel.Children.Add(tab);
            }

            // Add "+" button
            var addBtn = new Border
            {
                Width = 28, Height = 28,
                Margin = new Thickness(2, 0, 0, 0),
                CornerRadius = new CornerRadius(7),
                Background = (Brush)FindResource("AccentDimBrush"),
                BorderBrush = (Brush)FindResource("BorderAccentBrush"),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Добавить персонажа",
                Child = new TextBlock
                {
                    Text = "＋", FontSize = 14,
                    Foreground = (Brush)FindResource("AccentLightBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                }
            };
            addBtn.MouseLeftButtonDown += (_, _) => AddCharacterSlot();
            CharacterTabsPanel.Children.Add(addBtn);
        }
    }

    // ── Data class for one character slot ─────────────────────────────────────
    public class CharacterSlot
    {
        public string     DisplayName    { get; set; } = "Персонаж";
        public string?    FilePath       { get; set; }
        public bool       HasChanges     { get; set; }
        public Character? SavedCharacter { get; set; }
    }
}
