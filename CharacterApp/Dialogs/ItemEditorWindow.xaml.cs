using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using CharacterApp.Models;

namespace CharacterApp.Dialogs
{
    public partial class ItemEditorWindow : Window
    {
        public EquipmentItem ResultItem { get; private set; }
        private EquipmentItem _working;

        public ItemEditorWindow(EquipmentItem item)
        {
            InitializeComponent();

            _working = item != null
                ? new EquipmentItem
                {
                    Name = item.Name,
                    Rarity = item.Rarity,
                    Stats = item.Stats,
                    Effects = item.Effects,
                    ImagePath = item.ImagePath
                }
                : new EquipmentItem();

            TbName.Text = _working.Name ?? string.Empty;
            TbRarity.Text = _working.Rarity ?? string.Empty;
            TbStats.Text = _working.Stats ?? string.Empty;
            TbEffects.Text = _working.Effects ?? string.Empty;
            TbImagePath.Text = _working.ImagePath ?? string.Empty;

            RefreshPreview();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Выберите изображение для предмета"
            };
            if (dlg.ShowDialog() == true)
            {
                TbImagePath.Text = dlg.FileName;
                _working.ImagePath = dlg.FileName;
                RefreshPreview();
            }
        }

        private void RefreshPreview()
        {
            try
            {
                if (!string.IsNullOrEmpty(TbImagePath.Text) && File.Exists(TbImagePath.Text))
                {
                    PreviewImage.Source = new BitmapImage(new Uri(TbImagePath.Text, UriKind.RelativeOrAbsolute));
                }
                else if (Application.Current.Resources.Contains("EquipPlaceholder") &&
                         Application.Current.Resources["EquipPlaceholder"] is string placeholder)
                {
                    PreviewImage.Source = new BitmapImage(new Uri(placeholder, UriKind.RelativeOrAbsolute));
                }
                else
                {
                    PreviewImage.Source = null;
                }
            }
            catch
            {
                PreviewImage.Source = null;
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            _working.Name = TbName.Text.Trim();
            _working.Rarity = TbRarity.Text.Trim();
            _working.Stats = TbStats.Text.Trim();
            _working.Effects = TbEffects.Text.Trim();
            _working.ImagePath = TbImagePath.Text.Trim();

            ResultItem = _working;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
