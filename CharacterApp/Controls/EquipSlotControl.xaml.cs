using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CharacterApp.Models;
using Microsoft.Win32;

namespace CharacterApp.Controls
{
    public partial class EquipSlotControl : UserControl
    {
        private Point _dragStart;
        private const double DRAG_THRESHOLD = 6;

        public EquipSlotControl()
        {
            InitializeComponent();
            Loaded += EquipSlotControl_Loaded;
        }

        private void EquipSlotControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateVisual();
            ApplyThemeLockIcon();
        }

        #region DependencyProperties

        public static readonly DependencyProperty SlotKeyProperty =
            DependencyProperty.Register(nameof(SlotKey), typeof(string), typeof(EquipSlotControl), new PropertyMetadata(string.Empty));
        public string SlotKey { get => (string)GetValue(SlotKeyProperty); set => SetValue(SlotKeyProperty, value); }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(EquipSlotControl), new PropertyMetadata(string.Empty, OnTitleChanged));
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EquipSlotControl c && c.TitleText != null) c.TitleText.Text = e.NewValue?.ToString() ?? string.Empty;
        }

        public static readonly DependencyProperty IsLockedProperty =
            DependencyProperty.Register(nameof(IsLocked), typeof(bool), typeof(EquipSlotControl), new PropertyMetadata(false, OnIsLockedChanged));
        public bool IsLocked { get => (bool)GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }
        private static void OnIsLockedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EquipSlotControl c) c.ApplyLockState();
        }

        public static readonly DependencyProperty ItemImagePathProperty =
            DependencyProperty.Register(nameof(ItemImagePath), typeof(string), typeof(EquipSlotControl), new PropertyMetadata(string.Empty, OnItemImagePathChanged));
        public string ItemImagePath { get => (string)GetValue(ItemImagePathProperty); set => SetValue(ItemImagePathProperty, value); }
        private static void OnItemImagePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EquipSlotControl c) c.UpdateImage();
        }

        public static readonly DependencyProperty ItemNameProperty =
            DependencyProperty.Register(nameof(ItemName), typeof(string), typeof(EquipSlotControl), new PropertyMetadata(string.Empty, OnItemNameChanged));
        public string ItemName { get => (string)GetValue(ItemNameProperty); set => SetValue(ItemNameProperty, value); }
        private static void OnItemNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EquipSlotControl c && c.TxtName != null)
                c.TxtName.Text = string.IsNullOrEmpty((string)e.NewValue) ? "—" : (string)e.NewValue;
        }

        public static readonly DependencyProperty ItemDataProperty =
            DependencyProperty.Register(nameof(ItemData), typeof(EquipmentItem), typeof(EquipSlotControl), new PropertyMetadata(null, OnItemDataChanged));
        public EquipmentItem ItemData { get => (EquipmentItem)GetValue(ItemDataProperty); set => SetValue(ItemDataProperty, value); }
        private static void OnItemDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EquipSlotControl c)
            {
                var it = e.NewValue as EquipmentItem;
                if (it != null)
                {
                    c.ItemName = it.Name;
                    c.ItemImagePath = it.ImagePath;
                }
                else
                {
                    c.ItemName = string.Empty;
                    c.ItemImagePath = string.Empty;
                }
                c.UpdateVisual();
            }
        }

        #endregion

        private void UpdateVisual()
        {
            TitleText.Text = Title ?? string.Empty;
            TxtName.Text = string.IsNullOrWhiteSpace(ItemName) ? "—" : ItemName;
            UpdateImage();
            ApplyLockState();
        }

        private void UpdateImage()
        {
            if (!string.IsNullOrEmpty(ItemImagePath) && File.Exists(ItemImagePath))
            {
                try
                {
                    ImgItem.Source = new BitmapImage(new Uri(ItemImagePath, UriKind.RelativeOrAbsolute));
                    ImgPlaceholder.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    ImgItem.Source = null;
                    ImgPlaceholder.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ImgItem.Source = null;
                ImgPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void ApplyThemeLockIcon()
        {
            // Возьмём ресурсы LockIcon / UnlockIcon (строки с pack URI) и установим картинку
            try
            {
                var lockKey = IsLocked ? "LockIcon" : "UnlockIcon";
                object obj = null;
                if (Application.Current.Resources.Contains(lockKey))
                    obj = Application.Current.Resources[lockKey];
                else
                {
                    // fallback: попытаться в merged dictionaries
                    foreach (var d in Application.Current.Resources.MergedDictionaries)
                    {
                        if (d.Contains(lockKey)) { obj = d[lockKey]; break; }
                    }
                }

                if (obj is string uriStr && !string.IsNullOrEmpty(uriStr))
                {
                    try
                    {
                        LockBtnImage.Source = new BitmapImage(new Uri(uriStr, UriKind.RelativeOrAbsolute));
                    }
                    catch { LockBtnImage.Source = null; }
                }
            }
            catch { /* silent */ }
        }

        private void ApplyLockState()
        {
            // визуал: полупрозрачный + показать overlay
            MainBorder.Opacity = IsLocked ? 0.55 : 1.0;
            LockedOverlay.Visibility = IsLocked ? Visibility.Visible : Visibility.Collapsed;

            // Кнопки: при блокировке доступны только просмотр и разблокировка
            BtnChoose.IsEnabled = !IsLocked;
            BtnEdit.IsEnabled = !IsLocked;
            BtnClear.IsEnabled = !IsLocked;
            BtnView.IsEnabled = true; // view всегда доступен
            LockBtn.IsEnabled = true;

            // обновим иконку
            ApplyThemeLockIcon();
        }

        private void LockBtn_Click(object sender, RoutedEventArgs e)
        {
            IsLocked = !IsLocked;
        }

        private void BtnChoose_Click(object sender, RoutedEventArgs e)
        {
            if (IsLocked) { Notify("Слот заблокирован"); return; }
            var dlg = new OpenFileDialog { Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp", Title = "Выберите изображение" };
            if (dlg.ShowDialog() == true)
            {
                ItemImagePath = dlg.FileName;
                if (string.IsNullOrWhiteSpace(ItemName))
                    ItemName = Path.GetFileNameWithoutExtension(dlg.FileName);

                if (ItemData == null) ItemData = new EquipmentItem { Name = ItemName, ImagePath = ItemImagePath };
                else { ItemData.Name = ItemName; ItemData.ImagePath = ItemImagePath; }
                UpdateVisual();
            }
        }

        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ItemImagePath) || !File.Exists(ItemImagePath))
            {
                Notify("Фото не добавлено");
                return;
            }
            var win = new Window
            {
                Title = ItemName ?? "Просмотр",
                Content = new Image { Source = new BitmapImage(new Uri(ItemImagePath, UriKind.RelativeOrAbsolute)), Stretch = System.Windows.Media.Stretch.Uniform },
                Width = 800,
                Height = 600,
                Owner = Application.Current.MainWindow
            };
            win.ShowDialog();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (IsLocked) { Notify("Слот заблокирован"); return; }
            OpenEditor();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (IsLocked) { Notify("Слот заблокирован"); return; }
            if (MessageBox.Show("Удалить предмет из слота?", "Подтвердите", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            ItemData = null;
            ItemName = string.Empty;
            ItemImagePath = string.Empty;
            UpdateVisual();
        }

        private void OpenEditor()
        {
            try
            {
                var editor = new ItemEditorWindow(ItemData ?? new EquipmentItem());
                editor.Owner = Application.Current.MainWindow;
                var res = editor.ShowDialog();
                if (res == true)
                {
                    var updated = editor.ResultItem;
                    if (updated != null)
                    {
                        ItemData = updated;
                        ItemName = updated.Name ?? string.Empty;
                        ItemImagePath = updated.ImagePath ?? string.Empty;
                        UpdateVisual();
                    }
                }
            }
            catch (Exception ex)
            {
                Notify("Ошибка открытия редактора: " + ex.Message);
            }
        }

        private void Notify(string msg)
        {
            var main = Application.Current.MainWindow as MainWindow;
            main?.ShowNotification(msg, NotificationType.Info);
        }

        #region Drag & Drop (базовое)

        private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(this);
        }

        private void MainBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _dragStart.X) > DRAG_THRESHOLD || Math.Abs(pos.Y - _dragStart.Y) > DRAG_THRESHOLD)
            {
                // Только если есть предмет и слот не пуст
                if (ItemData == null) return;
                try
                {
                    var payload = new DragPayload { SlotKey = this.SlotKey, ItemJson = JsonSerializer.Serialize(ItemData) };
                    var data = new DataObject();
                    data.SetData("EquipSlot.Payload", JsonSerializer.Serialize(payload));
                    DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
                }
                catch { }
            }
        }

        private void MainBorder_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("EquipSlot.Payload"))
                e.Effects = DragDropEffects.Move;
        }

        private void MainBorder_DragLeave(object sender, DragEventArgs e) { /* no-op */ }

        private void MainBorder_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("EquipSlot.Payload")) return;
            try
            {
                var json = e.Data.GetData("EquipSlot.Payload") as string;
                if (string.IsNullOrEmpty(json)) return;
                var payload = JsonSerializer.Deserialize<DragPayload>(json);
                if (payload == null) return;

                // Если слот заблокирован — не принимаем
                if (IsLocked) { Notify("Слот заблокирован"); return; }

                // Получим предмет
                var item = JsonSerializer.Deserialize<EquipmentItem>(payload.ItemJson);

                // Swap: если у нас был предмет, то положим его обратно в источник (через EquipmentPage API)
                var page = FindParentPage();
                if (page is Pages.EquipmentPage ep)
                {
                    // сохраняем текущ
                    var current = this.ItemData;
                    // ставим пришедший
                    this.ItemData = item;
                    this.ItemName = item?.Name ?? string.Empty;
                    this.ItemImagePath = item?.ImagePath ?? string.Empty;
                    UpdateVisual();

                    // если payload.SlotKey отличается — вернуть старый предмет на исходный слот (swap)
                    if (payload.SlotKey != this.SlotKey)
                    {
                        if (current != null)
                        {
                            // отправляем весь EquipmentItem обратно в исходный слот
                            ep.ApplyToSlot(payload.SlotKey, current, false);
                        }
                        else
                        {
                            ep.ClearSlotByKey(payload.SlotKey);
                        }
                    }
                }
            }
            catch { }
        }


        private Page FindParentPage()
        {
            DependencyObject p = this;
            while (p != null)
            {
                if (p is Page page) return page;
                p = System.Windows.Media.VisualTreeHelper.GetParent(p);
            }
            return null;
        }

        #endregion

        // Вспомогательные классы для drag payload
        private class DragPayload { public string SlotKey { get; set; } public string ItemJson { get; set; } }

    }
}
