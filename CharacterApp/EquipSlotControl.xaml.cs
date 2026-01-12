using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CharacterApp
{
    public partial class EquipSlotControl : UserControl
    {
        public EquipSlotControl()
        {
            InitializeComponent();

            BtnChoose.Click += BtnChoose_Click;
            Loaded += EquipSlotControl_Loaded;
        }

        private void EquipSlotControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateVisual();
        }

        #region DependencyProperties

        // ключ слота (например "Head", "Body" и т.д.)
        public static readonly DependencyProperty SlotKeyProperty =
            DependencyProperty.Register(nameof(SlotKey), typeof(string), typeof(EquipSlotControl), new PropertyMetadata(string.Empty));

        public string SlotKey
        {
            get => (string)GetValue(SlotKeyProperty);
            set => SetValue(SlotKeyProperty, value);
        }

        // Заголовок/название слота (вместо SlotLabel)
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(EquipSlotControl), new PropertyMetadata(string.Empty, OnTitleChanged));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EquipSlotControl c)
                c.TitleText.Text = e.NewValue?.ToString() ?? string.Empty;
        }

        // Опциональный слот
        public static readonly DependencyProperty IsOptionalProperty =
            DependencyProperty.Register(nameof(IsOptional), typeof(bool), typeof(EquipSlotControl), new PropertyMetadata(false, OnIsOptionalChanged));

        public bool IsOptional
        {
            get => (bool)GetValue(IsOptionalProperty);
            set => SetValue(IsOptionalProperty, value);
        }

        private static void OnIsOptionalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EquipSlotControl c)
                c.OptionalBadge.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        // Locked state
        public static readonly DependencyProperty IsLockedProperty =
            DependencyProperty.Register(nameof(IsLocked), typeof(bool), typeof(EquipSlotControl), new PropertyMetadata(false, OnIsLockedChanged));

        public bool IsLocked
        {
            get => (bool)GetValue(IsLockedProperty);
            set => SetValue(IsLockedProperty, value);
        }

        private static void OnIsLockedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EquipSlotControl c)
                c.UpdateLockImages();
        }

        // Путь к выбранной картинке предмета
        public static readonly DependencyProperty ItemImagePathProperty =
            DependencyProperty.Register(nameof(ItemImagePath), typeof(string), typeof(EquipSlotControl), new PropertyMetadata(string.Empty, OnItemImagePathChanged));

        public string ItemImagePath
        {
            get => (string)GetValue(ItemImagePathProperty);
            set => SetValue(ItemImagePathProperty, value);
        }

        private static void OnItemImagePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EquipSlotControl c)
                c.UpdateImage();
        }

        // Имя предмета
        public static readonly DependencyProperty ItemNameProperty =
            DependencyProperty.Register(nameof(ItemName), typeof(string), typeof(EquipSlotControl), new PropertyMetadata(string.Empty, OnItemNameChanged));

        public string ItemName
        {
            get => (string)GetValue(ItemNameProperty);
            set => SetValue(ItemNameProperty, value);
        }

        private static void OnItemNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EquipSlotControl c)
                c.TxtName.Text = string.IsNullOrEmpty((string)e.NewValue) ? "—" : (string)e.NewValue;
        }

        #endregion

        private void UpdateVisual()
        {
            TitleText.Text = Title ?? string.Empty;
            OptionalBadge.Visibility = IsOptional ? Visibility.Visible : Visibility.Collapsed;
            UpdateImage();
            UpdateLockImages();
            TxtName.Text = string.IsNullOrWhiteSpace(ItemName) ? "—" : ItemName;
        }

        private void UpdateImage()
        {
            // Если указан путь и файл существует - загрузить
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
                    SetPlaceholder();
                }
            }
            else
            {
                ImgItem.Source = null;
                SetPlaceholder();
            }
        }

        private void SetPlaceholder()
        {
            // берём строку из ресурсов темы (ключ EquipPlaceholder) и подставляем как ImageSource
            try
            {
                if (Application.Current.Resources.Contains("EquipPlaceholder"))
                {
                    var pathObj = Application.Current.Resources["EquipPlaceholder"] as string;
                    if (!string.IsNullOrEmpty(pathObj))
                    {
                        var uri = new Uri(pathObj, UriKind.RelativeOrAbsolute);
                        ImgPlaceholder.Source = new BitmapImage(uri);
                        ImgPlaceholder.Visibility = Visibility.Visible;
                        return;
                    }
                }
            }
            catch
            {
                // fallback: скрыть плейсхолдер
            }
            ImgPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void UpdateLockImages()
        {
            // Загружаем Lock/Unlock и переключаем видимость
            try
            {
                string lockPath = null, unlockPath = null;
                if (Application.Current.Resources.Contains("LockIcon"))
                    lockPath = Application.Current.Resources["LockIcon"] as string;
                if (Application.Current.Resources.Contains("UnlockIcon"))
                    unlockPath = Application.Current.Resources["UnlockIcon"] as string;

                if (!string.IsNullOrEmpty(lockPath))
                    ImgLocked.Source = new BitmapImage(new Uri(lockPath, UriKind.RelativeOrAbsolute));
                if (!string.IsNullOrEmpty(unlockPath))
                    ImgUnlocked.Source = new BitmapImage(new Uri(unlockPath, UriKind.RelativeOrAbsolute));
            }
            catch
            {
                // ignore
            }

            ImgLocked.Visibility = IsLocked ? Visibility.Visible : Visibility.Collapsed;
            ImgUnlocked.Visibility = IsLocked ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BtnChoose_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog() == true)
            {
                ItemImagePath = dlg.FileName;
                if (string.IsNullOrWhiteSpace(ItemName))
                    ItemName = Path.GetFileNameWithoutExtension(dlg.FileName);
            }
        }
    }
}
