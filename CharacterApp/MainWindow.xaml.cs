using Octokit;
using Newtonsoft.Json;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Windows.Controls;

namespace CharacterApp
{

    public partial class MainWindow : Window
    {
        private const string Owner = "your-github-username";
        private const string Repo = "your-repo-name";

        private readonly PageMain _mainPage;
        private readonly PageDetails _detailsPage;
        private readonly EquipmentPage _equipmentPage;
        private string _lastJsonFilePath = string.Empty;

        private AutoSaveConfig _autoSaveConfig = new AutoSaveConfig();
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;

        private bool _sidebarOpen = true;
        private const double SidebarOpenWidth = 250;
        private const double SidebarClosedWidth = 60;
        private const int AnimDurationMs = 220;



        public MainWindow()
        {
            InitializeComponent();
            _mainPage = new PageMain();
            _detailsPage = new PageDetails();
            _equipmentPage = new EquipmentPage();
            MainFrame.Navigate(_mainPage);

            LoadAutoSaveConfig();
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            ApplyAutoSaveSettings();
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
                // Собираем модель
                var character = new Character();
                _mainPage.FillCharacter(character);
                _detailsPage.FillCharacter(character);
                var json = JsonConvert.SerializeObject(character, Formatting.Indented);

                // Сохраняем в новую версию
                var filename = string.Format(_autoSaveConfig.FilePattern, DateTime.Now);
                var path = Path.Combine(_autoSaveConfig.Folder, filename);
                File.WriteAllText(path, json);

                // Удаляем старые, оставляем только 5 последних
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


        // Перетаскивание
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        // Свернуть окно
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // Закрыть окно
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Показ уведомления
        public async void ShowNotification(string message, NotificationType type = NotificationType.Info)
        {
            var control = new NotificationControl(message, type);
            await control.ShowAsync(NotificationHost.Children);
        }

        private async void BtnToggleMenu_Click(object sender, RoutedEventArgs e)
        {
            await ToggleSidebarAsync(!_sidebarOpen);
        }

        private async Task ToggleSidebarAsync(bool open)
        {
            if (open == _sidebarOpen) return;
            _sidebarOpen = open;

            const int fadeMs = 140;
            const int fadeInMs = 160;

            if (!open)
            {
                // Собираем задачи: одновременно анимируем opacity всех видимых частей + ширину
                var tasks = new List<Task>();

                // main containers
                tasks.Add(AnimateOpacityAsync(MenuStack, MenuStack.Opacity, 0, fadeMs));
                tasks.Add(AnimateOpacityAsync(BottomButtonsPanel, BottomButtonsPanel.Opacity, 0, fadeMs));
                tasks.Add(AnimateOpacityAsync(TbMenuSearch, TbMenuSearch.Opacity, 0, fadeMs));

                // все TextBlock внутри кнопок (анимируем их исчезновение плавно)
                foreach (var child in MenuStack.Children)
                {
                    if (child is Button btn && btn.Content is StackPanel sp)
                    {
                        foreach (var textPart in sp.Children.OfType<TextBlock>())
                        {
                            // убедимся что текст видим перед анимацией
                            if (textPart.Visibility != Visibility.Visible)
                            {
                                textPart.Visibility = Visibility.Visible;
                                textPart.Opacity = 1;
                            }
                            tasks.Add(AnimateOpacityAsync(textPart, textPart.Opacity, 0, fadeMs));
                        }
                    }
                }

                // анимируем ширину сайдбара параллельно
                tasks.Add(AnimateWidthAsync(Sidebar, SidebarClosedWidth, AnimDurationMs));

                // дождёмся всех анимаций
                await Task.WhenAll(tasks);

                // после анимации – скрываем элементы (visibility), оставив иконки
                foreach (var child in MenuStack.Children)
                {
                    if (child is Button btn && btn.Content is StackPanel sp)
                    {
                        foreach (var textPart in sp.Children.OfType<TextBlock>())
                        {
                            textPart.Visibility = Visibility.Collapsed;
                            textPart.Opacity = 1; // сброс на случай повторного открытия
                        }
                    }
                }

                MenuStack.Visibility = Visibility.Collapsed;
                BottomButtonsPanel.Visibility = Visibility.Collapsed;
                TbMenuSearch.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Перед открытием: выставляем видимость контейнеров и готовим тексты к анимации
                MenuStack.Visibility = Visibility.Visible;
                BottomButtonsPanel.Visibility = Visibility.Visible;
                TbMenuSearch.Visibility = Visibility.Visible;

                // Подготовим все текстовые части: сделаем их видимыми, но с opacity = 0
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

                // Подготовим контейнеры к анимации (начальное значение opacity = 0)
                MenuStack.Opacity = 0;
                BottomButtonsPanel.Opacity = 0;
                TbMenuSearch.Opacity = 0;

                // Одновременно запускаем анимацию ширины и плавный fade-in для всех частей + текстов
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
                            // анимация текста из 0 -> 1
                            tasks.Add(AnimateOpacityAsync(textPart, 0, 1, fadeInMs));
                        }
                    }
                }

                await Task.WhenAll(tasks);

                // Убедимся, что все opacity возвращены в 1 (безопасный сброс)
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
            // show/hide buttons (keep first TextBlock headers visible when no query)
            foreach (var child in MenuStack.Children)
            {
                if (child is Button btn)
                {
                    // button content might be StackPanel(Image + TextBlock)
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
                    // keep header visible when no query
                    tb.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }



        // Проверка обновлений (не робит пока, поменять)
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
        // Навигация
        private void MainPage_Click(object sender, RoutedEventArgs e)
            => MainFrame.Navigate(_mainPage);

        private void Details_Click(object sender, RoutedEventArgs e)
            => MainFrame.Navigate(_detailsPage);

        private void Equipment_Click(object sender, RoutedEventArgs e)
    => MainFrame.Navigate(_equipmentPage);

        private void Settings_Click(object sender, RoutedEventArgs e)
            => MainFrame.Navigate(new SettingsPage());

        private void QuickSave_Click(object sender, RoutedEventArgs e)
        {
            SaveAll();
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content is ISaveLoad saveLoad)
                saveLoad.SaveAs();
            else
                ShowNotification("Текущая страница не поддерживает сохранение данных", NotificationType.Info);
        }

        private void LoadJSON_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content is ISaveLoad saveLoad)
                saveLoad.LoadJSON();
            else
                ShowNotification("Текущая страница не поддерживает загрузку данных", NotificationType.Info);
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
                    _mainPage.ApplyCharacter(character);
                    _detailsPage.ApplyCharacter(character);
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
            var json = JsonConvert.SerializeObject(character, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}

    public interface ISaveLoad
    {
        void QuickSave();
        void SaveAs();
        void LoadJSON();
    }
