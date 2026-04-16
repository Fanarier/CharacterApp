using CharacterApp.Dialogs;
using CharacterApp.Models;
using CharacterApp.Pages;
using Microsoft.Win32;
using Newtonsoft.Json;
using Octokit;
using WinApp = System.Windows.Application;
using System.IO;
using System.Net.Http;
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
        private readonly ActiveSkillsPage _skillsPage;
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

        public void LoadAutoSaveConfig()
        {
            const string SettingsFile = "appsettings.json";
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
            _statsPage.FillCharacter(c);
            return c;
        }

        private void DistributeCharacter(Character c)
        {
            _mainPage.ApplyCharacter(c);
            _detailsPage.ApplyCharacter(c);
            _equipmentPage.ApplyCharacter(c);
            _skillsPage.ApplyCharacter(c);
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
        private void ActiveSkills_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_skillsPage);
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
                var client   = new GitHubClient(new ProductHeaderValue("CharacterApp"));
                var releases = await client.Repository.Release.GetAll(GitHubOwner, GitHubRepo);
                var latest   = releases.FirstOrDefault();
                if (latest == null) { ShowNotification("Релизы не найдены", NotificationType.Info); return; }

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var latestVersion  = new Version(latest.TagName.TrimStart('v'));
                if (latestVersion <= currentVersion)
                { ShowNotification("У вас уже установлена последняя версия", NotificationType.Info); return; }

                if (!Confirm($"Доступна версия {latest.TagName}. Скачать?", "Обновление", ConfirmDialogIcon.Info)) return;

                var asset = latest.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                if (asset == null) { ShowNotification("В релизе нет .exe-файла", NotificationType.Warning); return; }

                var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, asset.Name);
                using var http = new HttpClient();
                using var resp = await http.GetAsync(asset.BrowserDownloadUrl);
                resp.EnsureSuccessStatusCode();
                using var fs = new FileStream(localPath, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                await resp.Content.CopyToAsync(fs);

                ShowNotification($"Скачано обновление: {asset.Name}", NotificationType.Success);
            }
            catch (Exception ex)
            {
                ShowNotification("Ошибка при проверке обновлений:\n" + ex.Message, NotificationType.Error);
            }
        }
    }

    public interface ISaveLoad
    {
        void QuickSave();
        void SaveAs();
        void LoadJSON();
    }
}
