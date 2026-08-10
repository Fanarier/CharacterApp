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
        private readonly Pages.InventoryPage  _inventoryPage;
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

        private AppSettings    _appSettings    = new AppSettings();
        private readonly DispatcherTimer _autoSaveTimer;

        private bool _sidebarOpen = true;
        private const double SidebarOpenWidth   = 250;
        private const double SidebarClosedWidth = 56;
        private const int    AnimDurationMs     = 220;

        public MainWindow()
        {
            Instance = this;

            // Общий на всё приложение экземпляр настроек. Берём до создания
            // страниц: SettingsPage читает его через MainWindow.Instance.Settings
            _appSettings = App.Settings;

            InitializeComponent();

            _mainPage      = new PageMain();
            _detailsPage   = new PageDetails();
            _inventoryPage = new Pages.InventoryPage();
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

            _autoSaveTimer      = new DispatcherTimer();
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            ApplyAutoSaveSettings();

            // Восстанавливаем табы персонажей из прошлой сессии
            bool sessionRestored = InitCharacterSlots();

            // Если сессия восстановилась, листы уже созданы из самого персонажа.
            // Дёргать настройки поверх нельзя — иначе в меню приедут листы
            // другого персонажа, оставшиеся там с прошлого раза.
            if (!sessionRestored) RestoreCustomSheetsFromSettings();

            _ = ApplySidebarLayoutAsync(animate: false);

            // Загружаем последний файл если включена настройка.
            // Если сессия восстановлена — в ней уже актуальнее, не перетираем.
            if (!sessionRestored
                && _appSettings.LoadLastOnStart
                && !string.IsNullOrEmpty(_appSettings.LastFilePath)
                && System.IO.File.Exists(_appSettings.LastFilePath))
            {
                var pathToLoad = _appSettings.LastFilePath;
                ContentRendered += (_, _) => LoadFromPath(pathToLoad);
            }
        }

        /// <summary>
        /// Единственный объект настроек приложения. Страница настроек правит
        /// его напрямую и вызывает SaveSettings() — копий и ручной синхронизации
        /// между двумя классами и двумя файлами больше нет.
        /// </summary>
        public AppSettings Settings => _appSettings;

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
            // Во время восстановления слота страницы дёргают TextChanged —
            // это не правки пользователя, флаг ставить нельзя
            if (_slotsRestoring || _hasUnsavedChanges) return;
            SetUnsavedFlag(true);
        }

        private void MarkSaved() => SetUnsavedFlag(false);

        /// <summary>Единая точка правды: флаг + «звёздочка» в заголовке + состояние слота.</summary>
        private void SetUnsavedFlag(bool dirty)
        {
            _hasUnsavedChanges = dirty;

            var text = TitleBarText.Text;
            bool hasMarker = text.EndsWith(" *", StringComparison.Ordinal);
            if (dirty && !hasMarker)       TitleBarText.Text = text + " *";
            else if (!dirty && hasMarker)  TitleBarText.Text = text[..^2];

            if (_activeSlotIndex >= 0 && _activeSlotIndex < _characterSlots.Count
             && _characterSlots[_activeSlotIndex].HasChanges != dirty)
            {
                _characterSlots[_activeSlotIndex].HasChanges = dirty;
                RebuildCharacterTabs();   // перерисовать звёздочку на табе
            }
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
                Helpers.CharacterAssets.ExtractImages(c);
                _lastJsonFilePath = path;
                _slotsRestoring = true;
                DistributeCharacter(c);
                _slotsRestoring = false;
                UpdateTitle(c.CharacterName);
                MarkSaved();
                SyncActiveSlot();
            }
            catch (Exception ex)
            {
                Helpers.Log.Error($"не удалось открыть последний файл {path}", ex);
                ShowNotification("Ошибка загрузки: " + ex.Message, NotificationType.Error);
            }
        }

        private void SaveLastFilePath(string path)
        {
            if (!string.IsNullOrEmpty(path)) _appSettings.LastFilePath = path;
            SaveSettings();
        }

        /// <summary>Записывает настройки на диск (%AppData%\CharacterApp\config.json).</summary>
        public void SaveSettings()
        {
            try { _appSettings.Save(); }
            catch (Exception ex) { Helpers.Log.Error("не удалось сохранить настройки", ex); }
        }

        public void ApplyAutoSaveSettings()
        {
            _autoSaveTimer.Stop();
            if (_appSettings.AutoSaveEnabled
             && _appSettings.AutoSaveIntervalMinutes > 0
             && !string.IsNullOrEmpty(_appSettings.AutoSaveFolder)
             && Directory.Exists(_appSettings.AutoSaveFolder))
            {
                _autoSaveTimer.Interval = TimeSpan.FromMinutes(_appSettings.AutoSaveIntervalMinutes);
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
                var filename  = string.Format(_appSettings.AutoSaveFilePattern, DateTime.Now);
                var path      = Path.Combine(_appSettings.AutoSaveFolder, filename);
                Helpers.CharacterAssets.EmbedImages(character);
                var json      = JsonConvert.SerializeObject(character, Formatting.Indented);
                WriteFileSafely(path, json, keepBackup: false);

                // Здесь раньше стоял SaveLastFilePath(path): автосейв объявлял себя
                // «последним файлом», и при включённой загрузке последнего при старте
                // открывался снимок, а не рабочий файл персонажа. Снимок — резервная
                // копия, а не рабочий документ, LastFilePath он менять не должен.

                PruneOldAutoSaves();

                // Слепок сессии — чтобы при падении не потерялись соседние табы
                SyncActiveSlot();
                PersistCharacterSlots();

                ShowNotification($"Автосохранено: {filename}", NotificationType.Success);
            }
            catch (Exception ex)
            {
                ShowNotification("Ошибка автосохранения: " + ex.Message, NotificationType.Error);
            }
        }

        private const int MaxAutoSaveFiles = 5;

        /// <summary>
        /// Превращает шаблон имени автосейва в маску поиска:
        /// "autosave_{0:yyyyMMdd_HHmmss}.json" → "autosave_*.json".
        /// Нужно чтобы чистка НЕ трогала посторонние .json в той же папке.
        /// </summary>
        internal static string BuildAutoSaveMask(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return "";
            // Заменяем все плейсхолдеры {...} на одну звёздочку
            var mask = System.Text.RegularExpressions.Regex.Replace(pattern, @"\{[^}]*\}", "*");
            // Схлопываем "**" → "*"
            while (mask.Contains("**")) mask = mask.Replace("**", "*");
            return mask;
        }

        /// <summary>Удаляет старые автосейвы, оставляя MaxAutoSaveFiles свежих.</summary>
        private void PruneOldAutoSaves()
        {
            var mask = BuildAutoSaveMask(_appSettings.AutoSaveFilePattern);

            // Защита от катастрофы: если из шаблона получилась маска без собственного
            // префикса (например "*.json" или "*"), чистку не делаем вообще —
            // иначе рискуем стереть сохранения персонажей, лежащие в этой же папке.
            if (string.IsNullOrEmpty(mask) || mask.StartsWith("*", StringComparison.Ordinal))
                return;

            try
            {
                var stale = new DirectoryInfo(_appSettings.AutoSaveFolder)
                    .GetFiles(mask)
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Skip(MaxAutoSaveFiles);
                foreach (var f in stale)
                    try { f.Delete(); }
                    catch (Exception ex) { Helpers.Log.Warn($"не удалось удалить старый автосейв {f.Name}", ex); }
            }
            catch (Exception ex) { Helpers.Log.Warn("чистка автосейвов не удалась", ex); }
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
            if (_journalPage   != null) { c.JournalEntries = _journalPage.GetEntries(); _journalPage.CollectColors(c); }
            if (_resourcesPage != null) { _resourcesPage.CollectColors(c); c.HpData = _resourcesPage.GetHpData(); c.Resources = _resourcesPage.GetResources(); }
            _inventoryPage.SaveTo(c);
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
            _inventoryPage.LoadFrom(c);
            // Journal & Resources — apply lazily (create page if needed)
            _journalPage ??= new JournalPage();
            _journalPage.LoadEntries(c.JournalEntries ?? new());
            _journalPage.SetCharacter(c);
            _resourcesPage ??= new ResourcesPage();
            _resourcesPage.LoadData(c.HpData, c.Resources);
            _resourcesPage.SetCharacter(c);
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
                SyncActiveSlot();
                PersistCharacterSlots();
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
                SyncActiveSlot();
                PersistCharacterSlots();
                ShowNotification("Данные сохранены!", NotificationType.Success);
            }
        }

        public void LoadAll()
        {
            // Загрузка затирает текущего персонажа в активном табе.
            // Раньше это происходило молча — открыл файл и потерял правки.
            if (_hasUnsavedChanges)
            {
                var r = ConfirmYNC(
                    "В текущем персонаже есть несохранённые изменения.\nСохранить перед загрузкой другого?",
                    "Загрузка");
                if (r == ConfirmDialog.ConfirmResult.Cancel) return;
                if (r == ConfirmDialog.ConfirmResult.Yes)
                {
                    SaveAll();
                    // Не сохранилось (отменили диалог выбора файла) — не рискуем
                    if (_hasUnsavedChanges) return;
                }
            }

            var dlg = new OpenFileDialog { Filter = "JSON файлы (*.json)|*.json" };
            if (dlg.ShowDialog() != true) return;

            var previousPath = _lastJsonFilePath;
            try
            {
                var json      = File.ReadAllText(dlg.FileName);
                var character = JsonConvert.DeserializeObject<Character>(json) ?? new Character();
                character.NormalizeItemsFromLegacy();
                // Вложенные картинки — в кэш, чтобы страницы могли их нарисовать
                Helpers.CharacterAssets.ExtractImages(character);

                // Путь запоминаем только после успешного чтения: иначе битый файл
                // становился «последним» и Ctrl+S затирал бы его текущим персонажем
                _lastJsonFilePath = dlg.FileName;
                SaveLastFilePath(_lastJsonFilePath);

                _slotsRestoring = true;
                DistributeCharacter(character);
                _slotsRestoring = false;
                MarkSaved();
                SyncActiveSlot();
                RebuildCharacterTabs();
                PersistCharacterSlots();
                ShowNotification("Данные загружены!", NotificationType.Success);
            }
            catch (Exception ex)
            {
                _lastJsonFilePath = previousPath;
                Helpers.Log.Error($"не удалось загрузить {dlg.FileName}", ex);

                // Рядом может лежать резервная копия с прошлого сохранения
                var bak = dlg.FileName + ".bak";
                if (File.Exists(bak) &&
                    Confirm($"Файл не читается:\n{ex.Message}\n\n" +
                            "Рядом есть резервная копия предыдущего сохранения. Загрузить её?",
                            "Файл повреждён"))
                {
                    LoadFromPath(bak);
                    // Работаем с копией, но пишем по-прежнему в основной файл,
                    // чтобы случайно не сделать .bak рабочим документом
                    _lastJsonFilePath = dlg.FileName;
                    return;
                }

                ShowNotification("Ошибка при загрузке: " + ex.Message, NotificationType.Error);
            }
        }

        private void DoSave(string path)
        {
            var character = CollectCharacter();
            // Картинки уходят внутрь файла: один файл вместо файла и папки
            Helpers.CharacterAssets.EmbedImages(character);
            var json = JsonConvert.SerializeObject(character, Formatting.Indented);

            WriteFileSafely(path, json);
        }

        /// <summary>
        /// Записывает файл персонажа так, чтобы его нельзя было потерять.
        ///
        /// Раньше здесь был File.WriteAllText прямо поверх существующего файла:
        /// если запись обрывалась (питание, антивирус, кончилось место), от
        /// персонажа оставался огрызок, а прежней версии уже не существовало.
        ///
        /// Теперь: пишем во временный файл → проверяем, что он читается →
        /// прежнюю версию отводим в .bak → подменяем основной файл.
        /// В худшем случае у пользователя останется .bak с прошлым сохранением.
        /// </summary>
        private static void WriteFileSafely(string path, string json, bool keepBackup = true)
        {
            var tmp = path + ".tmp";
            var bak = path + ".bak";

            File.WriteAllText(tmp, json);

            // Дешёвая проверка: файл на диске и он не пустой
            var written = new FileInfo(tmp);
            if (!written.Exists || written.Length == 0)
                throw new IOException($"Временный файл {tmp} не записался");

            // Для автосейвов бэкап не нужен — там и так хранится пять снимков
            if (keepBackup && File.Exists(path))
            {
                try { File.Copy(path, bak, overwrite: true); }
                catch (Exception ex) { Helpers.Log.Warn("не удалось обновить .bak", ex); }
            }

            // Move с overwrite на одном томе атомарен
            File.Move(tmp, path, overwrite: true);
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

            _slotsRestoring = true;
            DistributeCharacter(new Character());
            _slotsRestoring = false;
            _lastJsonFilePath  = string.Empty;
            TitleBarText.Text  = "Espires Games";
            SetUnsavedFlag(false);
            SyncActiveSlot();
            RebuildCharacterTabs();
            PersistCharacterSlots();
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
        private void Close_Click    (object sender, RoutedEventArgs e) => Close();

        /// <summary>
        /// Единая точка выхода: срабатывает и на крестик, и на Alt+F4,
        /// и на закрытие из таскбара, и на завершение сессии Windows.
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel || _closeConfirmed) return;

            // Активный слот мог быть не единственным с правками
            SyncActiveSlot();
            bool dirty = _hasUnsavedChanges || _characterSlots.Any(s => s.HasChanges);
            if (!dirty) { PersistCharacterSlots(); return; }

            // NB: у ObservableCollection есть свойство Count, поэтому Linq-Count(предикат)
            // напрямую не вызвать — считаем через Where
            var dirtyNames = _characterSlots.Where(s => s.HasChanges)
                                            .Select(s => s.DisplayName).ToList();

            string msg;
            if (!_hasUnsavedChanges)
            {
                // Активный чист, но правки есть в других табах — сохранить их
                // одной кнопкой нельзя, поэтому честно предупреждаем
                msg = "Несохранённые изменения есть у:\n• "
                    + string.Join("\n• ", dirtyNames)
                    + "\n\nТабы восстановятся при следующем запуске,\nно в файлы .json они не записаны. Выйти?";
                if (!Confirm(msg, "Выход")) { e.Cancel = true; return; }
            }
            else
            {
                msg = dirtyNames.Count > 1
                    ? "Есть несохранённые изменения в нескольких персонажах.\nСохранить активного перед выходом?"
                    : "Есть несохранённые изменения. Сохранить перед выходом?";

                var r = ConfirmYNC(msg, "Выход");
                if (r == ConfirmDialog.ConfirmResult.Cancel) { e.Cancel = true; return; }
                if (r == ConfirmDialog.ConfirmResult.Yes)
                {
                    SaveAll();
                    // Пользователь мог нажать "Отмена" в диалоге выбора файла
                    if (_hasUnsavedChanges) { e.Cancel = true; return; }
                }
            }

            _closeConfirmed = true;
            PersistCharacterSlots();
        }

        private bool _closeConfirmed = false;

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
        private void Equipment_Click   (object sender, RoutedEventArgs e) => NavigateTo(_equipmentPage,  "builtin:Equipment");
        private void Inventory_Click   (object sender, RoutedEventArgs e) => NavigateTo(_inventoryPage, "builtin:Inventory");
        private void ActiveSkills_Click(object sender, RoutedEventArgs e) => NavigateTo(_skillsPage,    "builtin:ActiveSkills");
        private void PassiveSkills_Click(object sender, RoutedEventArgs e) => NavigateTo(_passivePage,  "builtin:PassiveSkills");
        private void Proficiencies_Click(object sender, RoutedEventArgs e) => NavigateTo(_profPage,     "builtin:Proficiencies");
        private void Attacks_Click     (object sender, RoutedEventArgs e) => NavigateTo(_attacksPage,   "builtin:Attacks");


        /// <summary>
        /// Кнопка бокового меню с иконкой и подписью. Собрана так же, как
        /// встроенные пункты в XAML, иначе кастомные листы пропадали бы
        /// в режиме «только иконки» и в свёрнутом сайдбаре.
        /// </summary>
        private Button CreateNavButton(string text, string tag, string iconKey)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            var icon = new System.Windows.Shapes.Path
            {
                Data              = (Geometry)FindResource(iconKey),
                Width             = 24,
                Height            = 24,
                Stretch           = Stretch.None,
                Margin            = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                StrokeThickness   = 1.5,
                Fill              = Brushes.Transparent,
            };
            icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,
                new System.Windows.Data.Binding("Foreground")
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1)
                });
            panel.Children.Add(icon);
            panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });

            return new Button
            {
                Content = panel,
                ToolTip = text,
                Margin  = new Thickness(0, 4, 0, 4),
                Tag     = tag,
                Style   = (Style)FindResource("SidebarNavButton")
            };
        }

        // ── Пользовательские листы ──────────────────────────────────────────

        // Восстанавливает кастомные страницы из настроек (без загрузки JSON персонажа)
        private void RestoreCustomSheetsFromSettings()
        {
            if (_appSettings.SavedCustomSheets == null) return;
            foreach (var sheet in _appSettings.SavedCustomSheets)
            {
                if (_customPages.ContainsKey(sheet.Name)) continue;
                var page = new CustomSheetPage(sheet);
                _customPages[sheet.Name] = page;
                var name = sheet.Name;
                var btn  = CreateNavButton(sheet.Name, "custom:" + sheet.Name, "IcoTable");
                btn.Click += (_, _) => { MainFrame.Navigate(_customPages[name]); HighlightActiveButton("custom:" + name); };
                int idx = MenuStack.Children.Count - 1;
                MenuStack.Children.Insert(idx, btn);
            }
            ApplyMenuButtonStyle();
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

            // Список листов в настройках — это снимок ТЕКУЩЕГО персонажа,
            // а не копилка. Раньше здесь стояло «Clear не делаем», и листы
            // накапливались: созданные у одного персонажа всплывали в меню
            // при загрузке другого. Кастомные листы принадлежат персонажу,
            // а от потери несохранённых их бережёт session.json.
            _appSettings.SavedCustomSheets.Clear();
            _appSettings.SavedCustomSheets.AddRange(c.CustomSheets);
            SaveSettings();

            foreach (var sheet in c.CustomSheets)
            {
                var page = new CustomSheetPage(sheet);
                page.ApplyCharacter(c);          // ← загружаем строки из JSON
                _customPages[sheet.Name] = page;
                var name = sheet.Name; // capture
                var btn  = CreateNavButton(sheet.Name, "custom:" + sheet.Name, "IcoTable");
                btn.Click += (_, _) => { MainFrame.Navigate(_customPages[name]); HighlightActiveButton("custom:" + name); };
                // Вставляем перед кнопкой Настройки
                int idx = MenuStack.Children.Count - 1;
                MenuStack.Children.Insert(idx, btn);
            }
            ApplyMenuButtonStyle();
        }

        public void AddCustomSheet(CustomSheet sheet)
        {
            // Создаём страницу
            var page = new CustomSheetPage(sheet);
            _customPages[sheet.Name] = page;

            // Запоминаем в конфиге чтобы восстановить при следующем запуске
            _appSettings.SavedCustomSheets.RemoveAll(s => s.Name == sheet.Name);
            _appSettings.SavedCustomSheets.Add(sheet);
            SaveSettings();

            // Кнопка в сайдбар
            var name = sheet.Name;
            var btn  = CreateNavButton(sheet.Name, "custom:" + sheet.Name, "IcoTable");
            btn.Click += (_, _) => { MainFrame.Navigate(_customPages[name]); HighlightActiveButton("custom:" + name); };
            int idx = MenuStack.Children.Count - 1;
            MenuStack.Children.Insert(idx, btn);
            ApplyMenuButtonStyle();

            MarkUnsaved();
            // Кладём лист в сессию сразу: если приложение закроют, не сохранив
            // персонажа, при следующем запуске лист вернётся вместе с табом
            SyncActiveSlot();
            PersistCharacterSlots();
        }

        public void RemoveCustomSheet(string name)
        {
            // 1. Убираем страницу из памяти
            _customPages.Remove(name);

            // 2. Убираем из конфига + сохраняем конфиг сразу
            _appSettings.SavedCustomSheets.RemoveAll(s => s.Name == name);
            SaveSettings();

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

            // Обновляем сессию, иначе удалённый лист вернётся после перезапуска
            SyncActiveSlot();
            PersistCharacterSlots();
        }

        // GetCurrentCharacter() удалён: это был неиспользуемый дубликат CollectCharacter(),
        // причём без журнала и трекера ресурсов — ловушка для будущего рефакторинга.

        public static System.Collections.Generic.IEnumerable<string> GetBuiltinPageNames()

        {
            return _builtinPageNames;
        }
        private static readonly string[] _builtinPageNames =
            { "MainPage", "Details", "Equipment", "Stats",
              "ActiveSkills", "PassiveSkills", "Proficiencies", "Attacks" };

        /// <summary>Возвращает имена всех активных кастомных листов.</summary>

        public Models.CustomSheet? GetCustomSheet(string name)
            => _customPages.TryGetValue(name, out var page) ? page.Sheet : null;

        public void UpdateCustomSheet(string oldName, string newName,
                                      System.Collections.Generic.List<string> newHeaders)
        {
            if (!_customPages.TryGetValue(oldName, out var page)) return;

            page.UpdateHeaders(newHeaders);

            if (oldName != newName)
            {
                page.UpdateTitle(newName);
                _customPages.Remove(oldName);
                _customPages[newName] = page;

                for (int i = 0; i < MenuStack.Children.Count; i++)
                {
                    if (MenuStack.Children[i] is Button b && b.Tag?.ToString() == "custom:" + oldName)
                    {
                        var cap = newName;
                        var nb  = CreateNavButton(newName, "custom:" + newName, "IcoTable");
                        nb.Click += (_, _) =>
                        {
                            MainFrame.Navigate(_customPages[cap]);
                            HighlightActiveButton("custom:" + cap);
                        };
                        MenuStack.Children.RemoveAt(i);
                        MenuStack.Children.Insert(i, nb);
                        ApplyMenuButtonStyle();
                        break;
                    }
                }
            }

            // Раньше правка листа жила только в памяти: после перезапуска
            // возвращались старое имя и старые колонки
            _appSettings.SavedCustomSheets.RemoveAll(s => s.Name == oldName || s.Name == newName);
            _appSettings.SavedCustomSheets.Add(page.Sheet);
            SaveSettings();

            MarkUnsaved();
        }

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

        /// <summary>
        /// Идёт ли сейчас анимация сворачивания. Без этого частые клики
        /// запускали несколько перекрывающихся анимаций: одна успевала
        /// спрятать список, другая — раскрыть панель, и сайдбар оставался
        /// широким и пустым.
        /// </summary>
        private bool _sidebarAnimating;

        private async void BtnToggleMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_sidebarAnimating) return;
            _sidebarOpen = !_sidebarOpen;
            await ApplySidebarLayoutAsync(animate: true);
        }

        /// <summary>
        /// Единственное место, которое решает, как выглядит сайдбар.
        ///
        /// Раньше это было размазано по трём методам: анимация сворачивания
        /// ставила одну видимость, применение вида пунктов — другую, и они
        /// спорили. В свёрнутом виде иконки исчезали, а нижние кнопки
        /// оставались. Теперь состояние считается один раз и целиком.
        /// </summary>
        private async Task ApplySidebarLayoutAsync(bool animate)
        {
            var mode = _appSettings.MenuButtonStyle;
            bool iconOnly = mode == MenuButtonStyles.IconOnly;
            bool textOnly = mode == MenuButtonStyles.TextOnly;

            // Подписи видны только в широкой панели и только если режим их не запрещает
            bool wide       = _sidebarOpen && !iconOnly;
            bool showLabels = wide;
            bool showIcons  = !textOnly;

            // В узкой полосе показывать нечего, кроме иконок: если их нет —
            // и панели быть незачем, поэтому текстовый режим сворачивается пустым
            double targetWidth = _sidebarOpen && !iconOnly ? SidebarOpenWidth : SidebarClosedWidth;

            // ── содержимое пунктов ────────────────────────────────────────────
            foreach (var child in MenuStack.Children)
            {
                if (child is not Button btn)
                {
                    // Заголовок «Меню» и разделители в узкой полосе только мешают
                    if (child is UIElement other) other.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
                    continue;
                }
                if (btn.Content is not StackPanel sp) continue;

                sp.HorizontalAlignment = showLabels ? HorizontalAlignment.Left : HorizontalAlignment.Center;

                // Вот из-за чего иконки пропадали в узкой полосе: отступы по 12
                // с каждой стороны плюс полоса прокрутки не оставляли места
                // под иконку, и её срезало по краю панели
                btn.Padding = showLabels ? new Thickness(12, 11, 12, 11)
                                         : new Thickness(0, 11, 0, 11);

                string label = "";
                foreach (UIElement el in sp.Children)
                {
                    if (el is System.Windows.Shapes.Path icon)
                    {
                        icon.Visibility = showIcons ? Visibility.Visible : Visibility.Collapsed;
                        icon.Margin     = showLabels ? new Thickness(0, 0, 8, 0) : new Thickness(0);
                    }
                    else if (el is TextBlock tb)
                    {
                        label = tb.Text;
                        tb.Visibility = showLabels ? Visibility.Visible : Visibility.Collapsed;
                        tb.Opacity    = 1;
                    }
                }
                // Без подписи единственная подсказка — всплывающая
                btn.ToolTip = showLabels ? null : (string.IsNullOrEmpty(label) ? null : label);
            }

            // ── блоки панели ──────────────────────────────────────────────────
            // Список пунктов скрываем только когда показывать в нём нечего
            bool anyMenuContent = showIcons || wide;
            MenuStack.Visibility = anyMenuContent ? Visibility.Visible : Visibility.Collapsed;
            MenuStack.Opacity    = 1;
            MenuStack.Margin     = showLabels ? new Thickness(4, 0, 4, 8) : new Thickness(0, 0, 0, 8);

            // Полоса прокрутки в узкой панели отъедает половину места под иконку
            MenuScroll.VerticalScrollBarVisibility = showLabels
                ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;

            TbMenuSearch.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
            TbMenuSearch.Opacity    = 1;

            // Выбор персонажа нужен в любом режиме — в узкой панели он
            // сжимается до значка, иначе сменить персонажа было бы нечем
            CharacterTabsBorder.Visibility = Visibility.Visible;
            CharacterTabsBorder.Opacity    = 1;
            TbCurrentCharacter.Visibility  = wide ? Visibility.Visible : Visibility.Collapsed;
            TbSelectorArrow.Visibility     = wide ? Visibility.Visible : Visibility.Collapsed;
            IconCurrentCharacter.Margin    = wide ? new Thickness(0, 0, 8, 0) : new Thickness(0);
            BtnCharacterSelector.Padding   = wide ? new Thickness(10, 0, 10, 0) : new Thickness(0);
            BtnCharacterSelector.HorizontalContentAlignment =
                wide ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;

            // Бургер: слева при широкой панели, по центру при узкой
            BtnToggleMenu.HorizontalAlignment = wide ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            BtnToggleMenu.Margin = wide ? new Thickness(4, 0, 0, 6) : new Thickness(0, 0, 0, 6);
            SidebarTopPanel.Margin = wide ? new Thickness(10, 10, 10, 4) : new Thickness(4, 10, 4, 4);

            // Нижние кнопки со своими иконками читаемы и в узкой полосе — прятать их незачем
            BottomButtonsPanel.Visibility = Visibility.Visible;
            BottomButtonsPanel.Opacity    = 1;
            ApplyActionButtonsLayout(showLabels);

            // ── ширина ────────────────────────────────────────────────────────
            if (Math.Abs(Sidebar.ActualWidth - targetWidth) < 0.5) return;

            if (!animate)
            {
                Sidebar.BeginAnimation(WidthProperty, null);
                Sidebar.Width = targetWidth;
                return;
            }

            _sidebarAnimating = true;
            try { await AnimateWidthAsync(Sidebar, targetWidth, AnimDurationMs); }
            finally { _sidebarAnimating = false; }
        }

        /// <summary>
        /// Кнопки сохранения внизу панели. Их подпись — второй Run внутри
        /// TextBlock, а Run нельзя просто спрятать: это не элемент интерфейса.
        /// Поэтому в узкой полосе очищаем его текст, запомнив исходный, — иначе
        /// подпись не влезает и выталкивает значок за край.
        /// </summary>
        private void ApplyActionButtonsLayout(bool showLabels)
        {
            foreach (var btn in FindActionButtons(BottomButtonsPanel))
            {
                if (btn.Content is not TextBlock tb || tb.Inlines.Count < 2) continue;

                var runs = tb.Inlines.OfType<System.Windows.Documents.Run>().ToList();
                if (runs.Count < 2) continue;

                var iconRun  = runs[0];
                var labelRun = runs[1];

                // Исходную подпись держим в Tag кнопки: ресурс языка мог смениться
                if (btn.Tag is not string saved || (showLabels && labelRun.Text.Length == 0))
                {
                    saved = labelRun.Text.Length > 0 ? labelRun.Text : (btn.Tag as string ?? "");
                    btn.Tag = saved;
                }

                labelRun.Text = showLabels ? saved : "";
                iconRun.Text  = showLabels ? iconRun.Text.TrimEnd() + "  " : iconRun.Text.Trim();

                tb.TextAlignment      = showLabels ? TextAlignment.Left : TextAlignment.Center;
                tb.HorizontalAlignment = showLabels ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                btn.ToolTip           = showLabels ? null : saved;
                btn.Padding           = showLabels ? new Thickness(12, 8, 12, 8)
                                                   : new Thickness(0, 8, 0, 8);
            }
        }

        /// <summary>
        /// Обход по логическому дереву, а не визуальному: метод вызывается
        /// в том числе из конструктора окна, когда визуальное дерево ещё
        /// не построено и обход по нему вернул бы пусто.
        /// </summary>
        private static IEnumerable<Button> FindActionButtons(DependencyObject root)
        {
            switch (root)
            {
                case Button btn:
                    yield return btn;
                    break;
                case Border border when border.Child != null:
                    foreach (var x in FindActionButtons(border.Child)) yield return x;
                    break;
                case Panel panel:
                    foreach (UIElement child in panel.Children)
                        foreach (var x in FindActionButtons(child)) yield return x;
                    break;
                case ContentControl cc when cc.Content is DependencyObject inner:
                    foreach (var x in FindActionButtons(inner)) yield return x;
                    break;
            }
        }

        /// <summary>Применяет выбранный в настройках вид пунктов меню.</summary>
        public async void ApplyMenuButtonStyle() => await ApplySidebarLayoutAsync(animate: true);

        private static Task AnimateWidthAsync(FrameworkElement element, double to, int durationMs)
        {
            var tcs  = new TaskCompletionSource<bool>();
            var anim = new DoubleAnimation { To = to, Duration = TimeSpan.FromMilliseconds(durationMs), AccelerationRatio = 0.2, DecelerationRatio = 0.2 };
            anim.Completed += (s, e) => tcs.TrySetResult(true);
            element.BeginAnimation(WidthProperty, anim);
            return tcs.Task;
        }


        /// <summary>
        /// Текст, по которому ищется кнопка меню.
        /// Раньше здесь первым делом брался Tag ("builtin:Inventory"), и поиск
        /// работал по служебным английским идентификаторам: запрос «Инвентарь»
        /// не находил ничего, а «Inv» находил. Теперь ищем по видимой надписи,
        /// а тег добавляем хвостом — на случай, если кто-то по привычке
        /// вбивает латиницу.
        /// </summary>
        private static string GetButtonSearchText(Button btn)
        {
            string visible = string.Empty;

            if (btn.Content is StackPanel sp)
            {
                foreach (UIElement spChild in sp.Children)
                    if (spChild is TextBlock spTb) { visible = spTb.Text; break; }
            }
            else
            {
                visible = btn.Content?.ToString() ?? string.Empty;
            }

            var tag = btn.Tag?.ToString() ?? string.Empty;
            // Отрезаем префикс "builtin:" / "custom:" — он в поиске только шумит
            int colon = tag.IndexOf(':');
            if (colon >= 0) tag = tag[(colon + 1)..];

            return visible + " " + tag;
        }

        /// <summary>Скрыта ли страница пользователем в настройках.</summary>
        private bool IsPageHiddenByUser(string? tag)
        {
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith("builtin:", StringComparison.Ordinal))
                return false;
            var key = tag["builtin:".Length..];
            return _appSettings.HiddenPages?.Contains(key) == true;
        }

        private void TbMenuSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query    = TbMenuSearch.Text?.Trim() ?? string.Empty;
            bool anyVisible = false;

            // Delegate search to the current page if it supports it
            if (MainFrame.Content is Pages.IPageSearchable searchablePage)
                searchablePage.FilterItems(query);

            bool searching = !string.IsNullOrEmpty(query);

            foreach (var child in MenuStack.Children)
            {
                if (child is Button btn)
                {
                    // Страницу, скрытую в настройках, поиск показывать не должен
                    if (IsPageHiddenByUser(btn.Tag?.ToString()))
                    {
                        btn.Visibility = Visibility.Collapsed;
                        continue;
                    }

                    bool visible = !searching ||
                        GetButtonSearchText(btn).Contains(query, StringComparison.OrdinalIgnoreCase);
                    btn.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                    if (visible) anyVisible = true;
                }
                else if (child is TextBlock tb2 && tb2.Tag?.ToString() != "NoResults")
                    tb2.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
                else if (child is Separator or Border)
                    // Заголовок «Меню» и разделители при поиске только мешают
                    ((UIElement)child).Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
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

                // GitHub отдаёт релизы по дате, а не по версии: берём максимальную
                // из тех, чей тег вообще парсится, и пропускаем черновики/пререлизы
                var candidates = releases
                    .Where(r => !r.Draft && !r.Prerelease)
                    .Select(r => new { Release = r, Ver = ParseTag(r.TagName) })
                    .Where(x => x.Ver != null)
                    .OrderByDescending(x => x.Ver)
                    .ToList();

                if (candidates.Count == 0)
                { ShowNotification("Релизы не найдены", NotificationType.Info); return; }

                var latest        = candidates[0].Release;
                var latestVersion = candidates[0].Ver!;
                var currentVersion = NormalizeVersion(
                    Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

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

        /// <summary>"v1.2.3" / "1.2" / "release-1.2.3" → Version, иначе null.</summary>
        private static Version? ParseTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(tag, @"\d+(\.\d+)*");
            if (!m.Success) return null;
            return Version.TryParse(NormalizeTagNumbers(m.Value), out var v)
                ? NormalizeVersion(v) : null;
        }

        /// <summary>Version.Parse требует минимум две компоненты: "2" → "2.0".</summary>
        private static string NormalizeTagNumbers(string s)
            => s.Contains('.') ? s : s + ".0";

        /// <summary>
        /// Приводит к виду Major.Minor.Build, чтобы 1.2.0 и 1.2.0.0 сравнивались как равные
        /// (у Version отсутствующая компонента равна −1 и ломает сравнение).
        /// </summary>
        private static Version NormalizeVersion(Version v)
            => new Version(v.Major, v.Minor, Math.Max(v.Build, 0));

        // ══════════════════════════════════════════════════════════════════════
        // MULTI-CHARACTER SUPPORT
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Файл сессии — табы персонажей между запусками.</summary>
        private static string SessionFile => Path.Combine(App.DataDir, "session.json");

        private bool _slotsRestoring = false;

        /// <summary>Восстанавливает слоты из прошлой сессии либо создаёт один пустой.</summary>
        /// <returns>true, если сессия была восстановлена с данными.</returns>
        private bool InitCharacterSlots()
        {
            _characterSlots.Clear();
            bool restored = false;

            try
            {
                if (File.Exists(SessionFile))
                {
                    var json = File.ReadAllText(SessionFile);
                    var state = JsonConvert.DeserializeObject<SessionState>(json);
                    if (state?.Slots is { Count: > 0 })
                    {
                        foreach (var s in state.Slots)
                        {
                            s.SavedCharacter?.NormalizeItemsFromLegacy();
                            // Картинки лежат в самой сессии — раскладываем в кэш
                            if (s.SavedCharacter != null)
                                Helpers.CharacterAssets.ExtractImages(s.SavedCharacter);
                            _characterSlots.Add(s);
                        }
                        _activeSlotIndex = Math.Clamp(state.ActiveIndex, 0, _characterSlots.Count - 1);
                        restored = _characterSlots.Any(s => s.SavedCharacter != null);
                    }
                }
            }
            catch (Exception ex)
            {
                // Битый session.json не должен мешать запуску — уводим в бэкап,
                // чтобы данные можно было достать руками, и стартуем с чистого листа
                _characterSlots.Clear();
                restored = false;
                _activeSlotIndex = 0;
                try { File.Move(SessionFile, SessionFile + ".bad", true); } catch { }
                Helpers.Log.Error("session.json не восстановился, файл сохранён как session.json.bad", ex);
            }

            if (_characterSlots.Count == 0)
            {
                _characterSlots.Add(new CharacterSlot { DisplayName = "Персонаж 1" });
                _activeSlotIndex = 0;
            }

            // Разливаем активного персонажа по страницам
            if (restored)
            {
                _slotsRestoring = true;
                var slot = _characterSlots[_activeSlotIndex];
                _lastJsonFilePath = slot.FilePath ?? "";
                DistributeCharacter(slot.SavedCharacter ?? new Character());
                _slotsRestoring = false;
                SetUnsavedFlag(slot.HasChanges);
            }

            RebuildCharacterTabs();
            return restored;
        }

        /// <summary>Записывает текущее состояние страниц в активный слот.</summary>
        private void SyncActiveSlot()
        {
            if (_activeSlotIndex < 0 || _activeSlotIndex >= _characterSlots.Count) return;
            var slot = _characterSlots[_activeSlotIndex];
            slot.SavedCharacter = CollectCharacter();
            slot.FilePath       = _lastJsonFilePath;
            slot.HasChanges     = _hasUnsavedChanges;
            slot.DisplayName    = string.IsNullOrWhiteSpace(slot.SavedCharacter.CharacterName)
                ? slot.DisplayName
                : slot.SavedCharacter.CharacterName;
        }

        /// <summary>Сохраняет все слоты в %AppData%\CharacterApp\session.json.</summary>
        public void PersistCharacterSlots()
        {
            if (_slotsRestoring) return;
            try
            {
                Directory.CreateDirectory(App.DataDir);
                // Вкладываем картинки и в сессию: кэш со временем чистится,
                // а по одним путям к нему табы после этого остались бы пустыми.
                // Повторное вложение почти бесплатно — уже вложенное пропускается.
                foreach (var slot in _characterSlots)
                    if (slot.SavedCharacter != null)
                        Helpers.CharacterAssets.EmbedImages(slot.SavedCharacter);

                var state = new SessionState
                {
                    ActiveIndex = _activeSlotIndex,
                    Slots       = _characterSlots.ToList()
                };
                // Пишем через временный файл — обрыв записи не убьёт сессию
                var tmp = SessionFile + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(state, Formatting.Indented));
                File.Move(tmp, SessionFile, true);
            }
            catch (Exception ex)
            {
                Helpers.Log.Error("не удалось сохранить session.json (табы персонажей)", ex);
            }
        }

        /// <summary>Сохраняет текущего персонажа в активный слот, переключается на targetIdx.</summary>
        public void SwitchToSlot(int targetIdx)
        {
            if (targetIdx == _activeSlotIndex) return;
            if (targetIdx < 0 || targetIdx >= _characterSlots.Count) return;

            // Уходя от персонажа с правками, предлагаем записать их в его файл.
            // Молча сохранять нельзя: случайную правку тогда не откатить.
            if (_hasUnsavedChanges)
            {
                var name = _characterSlots[_activeSlotIndex].DisplayName;
                var r = ConfirmYNC($"У персонажа «{name}» есть несохранённые изменения.\n" +
                                   "Записать их в файл перед переходом?", "Переключение");
                if (r == ConfirmDialog.ConfirmResult.Cancel) return;
                if (r == ConfirmDialog.ConfirmResult.Yes)
                {
                    SaveAll();
                    // Отменили выбор файла — остаёмся на месте, чтобы не потерять правки
                    if (_hasUnsavedChanges) return;
                }
            }

            // Save current
            SyncActiveSlot();

            // Switch
            _activeSlotIndex  = targetIdx;
            var slot          = _characterSlots[targetIdx];
            _lastJsonFilePath = slot.FilePath ?? "";

            // Rebuild pages for new character
            var c = slot.SavedCharacter ?? new Character();
            _slotsRestoring = true;
            DistributeCharacter(c);
            _slotsRestoring = false;

            // Восстанавливаем «звёздочку» именно этого персонажа, а не гасим её
            SetUnsavedFlag(slot.HasChanges);
            RebuildCharacterTabs();
            PersistCharacterSlots();
        }

        /// <summary>Добавляет новый пустой слот.</summary>
        public void AddCharacterSlot()
        {
            int num = _characterSlots.Count + 1;
            _characterSlots.Add(new CharacterSlot { DisplayName = $"Персонаж {num}" });

            // SwitchToSlot сам спросит про несохранённые правки текущего.
            // Если пользователь откажется от перехода, лишний слот убираем.
            int target = _characterSlots.Count - 1;
            SwitchToSlot(target);
            if (_activeSlotIndex != target)
            {
                _characterSlots.RemoveAt(target);
                RebuildCharacterTabs();
            }
        }

        /// <summary>Удаляет активный слот.</summary>
        public void RemoveActiveSlot()
        {
            if (_characterSlots.Count <= 1)
            {
                ShowNotification("Нельзя удалить единственного персонажа", NotificationType.Warning);
                return;
            }

            var victim = _characterSlots[_activeSlotIndex];
            var warn = victim.HasChanges
                ? $"Закрыть «{victim.DisplayName}»?\nЕсть несохранённые изменения — они будут потеряны."
                : $"Закрыть «{victim.DisplayName}»?";
            if (!Confirm(warn, "Закрытие персонажа")) return;

            _characterSlots.RemoveAt(_activeSlotIndex);
            _activeSlotIndex = Math.Max(0, _activeSlotIndex - 1);
            var slot = _characterSlots[_activeSlotIndex];
            _lastJsonFilePath = slot.FilePath ?? "";
            _slotsRestoring = true;
            DistributeCharacter(slot.SavedCharacter ?? new Character());
            _slotsRestoring = false;
            SetUnsavedFlag(slot.HasChanges);
            RebuildCharacterTabs();
            PersistCharacterSlots();
        }

        /// <summary>Обновляет имя таба активного слота по имени персонажа.</summary>
        public void UpdateActiveSlotName(string name)
        {
            if (_characterSlots.Count > _activeSlotIndex)
                _characterSlots[_activeSlotIndex].DisplayName =
                    string.IsNullOrWhiteSpace(name) ? $"Персонаж {_activeSlotIndex + 1}" : name;
            RebuildCharacterTabs();
        }

        /// <summary>
        /// Перестраивает выбор персонажа: строку с текущим и выпадающий список.
        /// Раньше здесь рисовался ряд табов — при десятке персонажей он
        /// переносился на несколько строк и занимал пол-панели.
        /// </summary>
        public void RebuildCharacterTabs()
        {
            if (_activeSlotIndex >= 0 && _activeSlotIndex < _characterSlots.Count)
            {
                var active = _characterSlots[_activeSlotIndex];
                TbCurrentCharacter.Text = active.DisplayName + (active.HasChanges ? " *" : "");
            }

            CharacterListPanel.Children.Clear();

            for (int i = 0; i < _characterSlots.Count; i++)
            {
                int idx     = i;
                var slot    = _characterSlots[i];
                bool active = i == _activeSlotIndex;

                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var line = new StackPanel { Margin = new Thickness(10, 6, 6, 6) };
                line.Children.Add(new TextBlock
                {
                    Text         = slot.DisplayName + (slot.HasChanges ? " *" : ""),
                    FontWeight   = active ? FontWeights.SemiBold : FontWeights.Normal,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth     = 170,
                });

                // Вторая строка — чтобы отличать двух «Персонаж 1» друг от друга
                var meta = DescribeSlot(slot);
                if (!string.IsNullOrEmpty(meta))
                    line.Children.Add(new TextBlock
                    {
                        Text       = meta,
                        FontSize   = 11,
                        Margin     = new Thickness(0, 2, 0, 0),
                        Foreground = (Brush)FindResource("TextMutedBrush"),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth   = 170,
                    });

                var pick = new Button
                {
                    Content = line,
                    Style   = (Style)FindResource(active ? "SidebarNavButtonActive" : "SidebarNavButton"),
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    ToolTip = string.IsNullOrEmpty(slot.FilePath) ? "Ещё не сохранён в файл" : slot.FilePath,
                };
                pick.Click += (_, _) => { CharacterPopup.IsOpen = false; SwitchToSlot(idx); };
                Grid.SetColumn(pick, 0);
                row.Children.Add(pick);

                if (_characterSlots.Count > 1)
                {
                    var close = new Button
                    {
                        Content = "✕",
                        Width = 26, Height = 26, MinHeight = 0,
                        Padding = new Thickness(0),
                        Margin = new Thickness(2, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        BorderThickness = new Thickness(0),
                        Background = Brushes.Transparent,
                        Foreground = (Brush)FindResource("TextMutedBrush"),
                        ToolTip = "Закрыть персонажа",
                    };
                    close.Click += (_, _) =>
                    {
                        CharacterPopup.IsOpen = false;
                        if (idx != _activeSlotIndex) SwitchToSlot(idx);
                        RemoveActiveSlot();
                    };
                    Grid.SetColumn(close, 1);
                    row.Children.Add(close);
                }

                CharacterListPanel.Children.Add(row);
            }
        }

        /// <summary>Класс и уровень персонажа — чтобы различать однофамильцев.</summary>
        private static string DescribeSlot(CharacterSlot slot)
        {
            var c = slot.SavedCharacter;
            if (c == null) return "";
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(c.Class)) parts.Add(c.Class.Trim());
            if (c.Level > 0) parts.Add($"ур. {c.Level}");
            return string.Join(" · ", parts);
        }

        private void CharacterSelector_Click(object sender, RoutedEventArgs e)
        {
            RebuildCharacterTabs();
            CharacterPopup.IsOpen = !CharacterPopup.IsOpen;
        }

        private void AddCharacter_Click(object sender, RoutedEventArgs e)
        {
            CharacterPopup.IsOpen = false;
            AddCharacterSlot();
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

    /// <summary>Состояние сессии — что лежало в табах при прошлом выходе.</summary>
    public class SessionState
    {
        public int                 ActiveIndex { get; set; }
        public List<CharacterSlot> Slots       { get; set; } = new();
    }
}
