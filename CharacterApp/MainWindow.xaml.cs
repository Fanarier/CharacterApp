using CharacterApp.Models; // <- важно
using CharacterApp.Pages;
using Microsoft.Win32;
using Newtonsoft.Json;
using Octokit;
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
        public static MainWindow Instance { get; private set; }

        private const string Owner = "your-github-username";
        private const string Repo = "your-repo-name";

        private readonly PageMain _mainPage;
        private readonly PageDetails _detailsPage;
        private readonly EquipmentPage _equipmentPage;
        private readonly ActiveSkillsPage _activeskills_Page;
        private string _lastJsonFilePath = string.Empty;

        private AutoSaveConfig _autoSaveConfig = new AutoSaveConfig();
        private DispatcherTimer _autoSaveTimer;

        private bool _sidebarOpen = true;
        private const double SidebarOpenWidth = 250;
        private const double SidebarClosedWidth = 60;
        private const int AnimDurationMs = 220;

        public MainWindow()
        {
            Instance = this;

            InitializeComponent();

            // создаём единственные экземпляры страниц
            _mainPage = new PageMain();
            _detailsPage = new PageDetails();
            _equipmentPage = new EquipmentPage();
            _activeskills_Page = new ActiveSkillsPage();

            // По умолчанию показываем главную страницу
            MainFrame.Navigate(_mainPage);

            LoadAutoSaveConfig();
            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            ApplyAutoSaveSettings();
        }

        // ---- helper: вернуть текущий отображаемый экземпляр EquipmentPage (если есть) ----
        private EquipmentPage GetShownEquipmentPage()
        {
            // Попробуем взять из фрейма (если там EquipmentPage)
            if (MainFrame?.Content is EquipmentPage shown) return shown;

            // fallback: используем поле _equipmentPage
            return _equipmentPage;
        }

        public void LoadAutoSaveConfig()
        {
            const string SettingsFile = "appsettings.json";
            if (File.Exists(SettingsFile))
            {
                try
                {
                    string jsonText = File.ReadAllText(SettingsFile);
                    _autoSaveConfig = System.Text.Json.JsonSerializer
                                          .Deserialize<AutoSaveConfig>(jsonText)
                                      ?? new AutoSaveConfig();
                }
                catch
                {
                    _autoSaveConfig = new AutoSaveConfig();
                }
            }
            else
            {
                _autoSaveConfig = new AutoSaveConfig();
            }
        }

        public void ApplyAutoSaveSettings()
        {
            _autoSaveTimer.Stop();
            if (_autoSaveConfig.Enabled
             && _autoSaveConfig.IntervalMinutes > 0
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
                // Собираем модель со всех страниц: main, details, equipment (из отображаемого экземпляра)
                var character = new Character();
                _mainPage.FillCharacter(character);
                _detailsPage.FillCharacter(character);

                var shownEquip = GetShownEquipmentPage();
                if (shownEquip != null) shownEquip.FillCharacter(character);
                else _equipmentPage.FillCharacter(character);

                var json = JsonConvert.SerializeObject(character, Formatting.Indented);

                var filename = string.Format(_autoSaveConfig.FilePattern, DateTime.Now);
                var path = Path.Combine(_autoSaveConfig.Folder, filename);
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

        // UI handlers (не менял логику анимаций)
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        public async void ShowNotification(string message, NotificationType type = NotificationType.Info)
        {
            var control = new NotificationControl(message, type);
            await control.ShowAsync(NotificationHost.Children);
        }

        private async void BtnToggleMenu_Click(object sender, RoutedEventArgs e) => await ToggleSidebarAsync(!_sidebarOpen);

        private async Task ToggleSidebarAsync(bool open)
        {
            if (open == _sidebarOpen) return;
            _sidebarOpen = open;

            const int fadeMs = 140;
            const int fadeInMs = 160;

            if (!open)
            {
                var tasks = new List<Task>();
                tasks.Add(AnimateOpacityAsync(MenuStack, MenuStack.Opacity, 0, fadeMs));
                tasks.Add(AnimateOpacityAsync(BottomButtonsPanel, BottomButtonsPanel.Opacity, 0, fadeMs));
                tasks.Add(AnimateOpacityAsync(TbMenuSearch, TbMenuSearch.Opacity, 0, fadeMs));

                foreach (var child in MenuStack.Children)
                {
                    if (child is Button btn && btn.Content is StackPanel sp)
                    {
                        foreach (var textPart in sp.Children.OfType<TextBlock>())
                        {
                            if (textPart.Visibility != Visibility.Visible)
                            {
                                textPart.Visibility = Visibility.Visible;
                                textPart.Opacity = 1;
                            }
                            tasks.Add(AnimateOpacityAsync(textPart, textPart.Opacity, 0, fadeMs));
                        }
                    }
                }
                tasks.Add(AnimateWidthAsync(Sidebar, SidebarClosedWidth, AnimDurationMs));
                await Task.WhenAll(tasks);

                foreach (var child in MenuStack.Children)
                {
                    if (child is Button btn && btn.Content is StackPanel sp)
                    {
                        foreach (var textPart in sp.Children.OfType<TextBlock>())
                        {
                            textPart.Visibility = Visibility.Collapsed;
                            textPart.Opacity = 1;
                        }
                    }
                }

                MenuStack.Visibility = Visibility.Collapsed;
                BottomButtonsPanel.Visibility = Visibility.Collapsed;
                TbMenuSearch.Visibility = Visibility.Collapsed;
            }
            else
            {
                MenuStack.Visibility = Visibility.Visible;
                BottomButtonsPanel.Visibility = Visibility.Visible;
                TbMenuSearch.Visibility = Visibility.Visible;

                foreach (var child in MenuStack.Children)
                {
                    if (child is Button btn && btn.Content is StackPanel sp)
                    {
                        foreach (var textPart in sp.Children.OfType<TextBlock>())
                        {
                            textPart.Visibility = Visibility.Visible;
                            textPart.Opacity = 0;
                        }
                    }
                }

                MenuStack.Opacity = 0;
                BottomButtonsPanel.Opacity = 0;
                TbMenuSearch.Opacity = 0;

                var tasks = new List<Task>
                {
                    AnimateWidthAsync(Sidebar, SidebarOpenWidth, AnimDurationMs),
                    AnimateOpacityAsync(MenuStack, 0, 1, fadeInMs),
                    AnimateOpacityAsync(BottomButtonsPanel, 0, 1, fadeInMs),
                    AnimateOpacityAsync(TbMenuSearch, 0, 1, fadeInMs)
                };

                foreach (var child in MenuStack.Children)
                {
                    if (child is Button btn && btn.Content is StackPanel sp)
                    {
                        foreach (var textPart in sp.Children.OfType<TextBlock>())
                        {
                            tasks.Add(AnimateOpacityAsync(textPart, 0, 1, fadeInMs));
                        }
                    }
                }

                await Task.WhenAll(tasks);

                MenuStack.Opacity = 1;
                BottomButtonsPanel.Opacity = 1;
                TbMenuSearch.Opacity = 1;
                foreach (var child in MenuStack.Children)
                {
                    if (child is Button btn && btn.Content is StackPanel sp)
                    {
                        foreach (var textPart in sp.Children.OfType<TextBlock>())
                        {
                            textPart.Opacity = 1;
                        }
                    }
                }
            }
        }

        private Task AnimateWidthAsync(FrameworkElement element, double to, int durationMs)
        {
            var tcs = new TaskCompletionSource<bool>();
            var anim = new DoubleAnimation { To = to, Duration = TimeSpan.FromMilliseconds(durationMs), AccelerationRatio = 0.2, DecelerationRatio = 0.2 };
            anim.Completed += (s, e) => tcs.TrySetResult(true);
            element.BeginAnimation(WidthProperty, anim);
            return tcs.Task;
        }

        private Task AnimateOpacityAsync(UIElement element, double from, double to, int durationMs)
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
            var query = TbMenuSearch.Text?.Trim().ToLowerInvariant() ?? string.Empty;
            foreach (var child in MenuStack.Children)
            {
                if (child is Button btn)
                {
                    string contentText = btn.Tag?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(contentText) && btn.Content is StackPanel sp)
                    {
                        var tb = sp.Children.OfType<TextBlock>().FirstOrDefault();
                        contentText = tb?.Text ?? string.Empty;
                    }

                    btn.Visibility = string.IsNullOrEmpty(query) || contentText.ToLowerInvariant().Contains(query)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else if (child is TextBlock tb)
                {
                    tb.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var client = new GitHubClient(new ProductHeaderValue("CharacterApp"));
                var releases = await client.Repository.Release.GetAll(Owner, Repo);
                var latest = releases.FirstOrDefault();
                if (latest == null)
                {
                    ShowNotification("Релизы не найдены", NotificationType.Info);
                    return;
                }

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var latestVersion = new Version(latest.TagName.TrimStart('v'));
                if (latestVersion <= currentVersion)
                {
                    ShowNotification("У вас уже установлена последняя версия", NotificationType.Info);
                    return;
                }

                if (MessageBox.Show($"Доступна версия {latest.TagName}. Скачать?",
                                    "Обновление", MessageBoxButton.YesNo, MessageBoxImage.Question)
                    != MessageBoxResult.Yes) return;

                var asset = latest.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                if (asset == null)
                {
                    ShowNotification("В релизе нет .exe-файла", NotificationType.Warning);
                    return;
                }

                var downloadUrl = asset.BrowserDownloadUrl;
                var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, asset.Name);

                using var http = new HttpClient();
                using var resp = await http.GetAsync(downloadUrl);
                using var fs = new FileStream(localPath, System.IO.FileMode.Create, FileAccess.Write);
                resp.EnsureSuccessStatusCode();
                await resp.Content.CopyToAsync(fs);

                ShowNotification($"Скачано обновление: {asset.Name}", NotificationType.Success);
            }
            catch (Exception ex)
            {
                ShowNotification("Ошибка при проверке обновлений:\n" + ex.Message, NotificationType.Error);
            }
        }

        // Navigation
        private void MainPage_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_mainPage);
        private void Details_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_detailsPage);
        private void Equipment_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_equipmentPage);

        private void ActiveSkills_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(_activeskills_Page);
        private void Settings_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(new SettingsPage());

        // Quick actions — приводим к глобальным сохранениям/загрузкам, чтобы избежать "локальных" загрузок страниц
        private void QuickSave_Click(object sender, RoutedEventArgs e) => SaveAll();

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            // Вместо вызова SaveAs у текущей страницы — выполняем глобальное SaveAs
            SaveAllAs();
        }

        private void LoadJSON_Click(object sender, RoutedEventArgs e)
        {
            // Вместо вызова LoadJSON у текущей страницы — выполняем глобальную загрузку,
            // чтобы обновить все страницы сразу (PageMain + PageDetails + EquipmentPage)
            LoadAll();
        }

        // Сохранение/загрузка всех данных
        public void SaveAll()
        {
            if (!string.IsNullOrEmpty(_lastJsonFilePath) && File.Exists(_lastJsonFilePath))
            {
                DoSave(_lastJsonFilePath);
            }
            else
            {
                SaveAllAs();
            }
            ShowNotification("Данные сохранены!", NotificationType.Success);
        }

        public void SaveAllAs()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "JSON файлы (*.json)|*.json",
                DefaultExt = ".json"
            };
            if (dlg.ShowDialog() == true)
            {
                _lastJsonFilePath = dlg.FileName;
                DoSave(_lastJsonFilePath);
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
                    var json = File.ReadAllText(_lastJsonFilePath);
                    var character = JsonConvert.DeserializeObject<Character>(json) ?? new Character();

                    // Нормализуем legacy-данные в полноценные EquipmentItem (если нужно)
                    character.NormalizeItemsFromLegacy();

                    // Применяем данные к всем страницам (и к отображаемому EquipmentPage)
                    _mainPage.ApplyCharacter(character);
                    _detailsPage.ApplyCharacter(character);

                    var shownEquip = GetShownEquipmentPage();
                    if (shownEquip != null)
                        shownEquip.ApplyCharacter(character);
                    else
                        _equipmentPage.ApplyCharacter(character);

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
            var character = new Character();
            _mainPage.FillCharacter(character);
            _detailsPage.FillCharacter(character);

            // используем отображаемый EquipmentPage (если есть), иначе экземпляр _equipmentPage
            var shownEquip = GetShownEquipmentPage();
            if (shownEquip != null)
                shownEquip.FillCharacter(character);
            else
                _equipmentPage.FillCharacter(character);

            var json = JsonConvert.SerializeObject(character, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }

    public interface ISaveLoad
    {
        void QuickSave();
        void SaveAs();
        void LoadJSON();
    }
}
