using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using CharacterApp.Models;

namespace CharacterApp.Pages
{
    public partial class InventoryPage : Page, IPageSearchable
    {
        // ── Rarity definitions ────────────────────────────────────────────────
        private static readonly (string Name, string Hex, string TextHex, string? Note)[] Rarities =
        {
            ("Обычный",    "#555560", "#C8C8D0", null),
            ("Необычный",  "#1E5C30", "#4CAF72", null),
            ("Редкий",     "#1A3D7A", "#4A90D9", null),
            ("Эпический",  "#4A1A80", "#A060E0", null),
            ("Легендарный","#703010", "#E08020", "Не продаётся"),
            ("Артефакт",   "#6E1020", "#E04060", "Копий не существует"),
        };

        private readonly ObservableCollection<InventoryItem> _items = new();
        private InventoryItem? _selected;
        private bool _suppressDetailUpdate;
        private string _filterRarity = "";
        private string _filterSearch = "";

        public InventoryPage()
        {
            InitializeComponent();
            _items.CollectionChanged += (_, _) => RefreshStats();
            BuildRarityFilter();
            BuildRarityPicker();
        }

        // ── ISaveLoad helpers ─────────────────────────────────────────────────
        public void SaveTo(Character c)
        {
            c.Inventory.Clear();
            c.Inventory.AddRange(_items);
            c.GoldCoins   = int.TryParse(TbGold.Text,   out var g)  ? g  : 0;
            c.SilverCoins = int.TryParse(TbSilver.Text, out var s)  ? s  : 0;
            c.CopperCoins = int.TryParse(TbCopper.Text, out var cu) ? cu : 0;
        }

        public void LoadFrom(Character c)
        {
            _items.Clear();
            foreach (var item in c.Inventory) { item.PropertyChanged += Item_Changed; _items.Add(item); }
            TbGold.Text   = c.GoldCoins.ToString();
            TbSilver.Text = c.SilverCoins.ToString();
            TbCopper.Text = c.CopperCoins.ToString();
            RebuildList();
        }

        public void ResetAll()
        {
            _items.Clear();
            TbGold.Text = TbSilver.Text = TbCopper.Text = "0";
            RebuildList();
        }

        // ── IPageSearchable ───────────────────────────────────────────────────
        public void FilterItems(string query)
        {
            _filterSearch = query;
            RebuildList();
        }

        // ── Rarity filter combobox ────────────────────────────────────────────
        private void BuildRarityFilter()
        {
            CbRarityFilter.Items.Add(new ComboBoxItem { Content = "Любая редкость", Tag = "" });
            foreach (var (name, _, textHex, _) in Rarities)
            {
                var item = new ComboBoxItem { Content = name, Tag = name };
                item.Foreground = HexBrush(textHex);
                CbRarityFilter.Items.Add(item);
            }
            CbRarityFilter.SelectedIndex = 0;
        }

        private void RarityFilter_Changed(object s, SelectionChangedEventArgs e)
        {
            _filterRarity = (CbRarityFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
            RebuildList();
        }

        private void Search_Changed(object s, TextChangedEventArgs e)
        {
            _filterSearch = TbSearch.Text?.Trim() ?? "";
            RebuildList();
        }

        // ── Rarity picker (in detail panel) ──────────────────────────────────
        private void BuildRarityPicker()
        {
            RarityPicker.Children.Clear();  // WrapPanel — wraps automatically
            foreach (var (name, bgHex, textHex, _) in Rarities)
            {
                var n = name;
                var pill = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 5, 5),
                    Padding = new Thickness(8, 4, 8, 4),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = HexBrush(bgHex + "60"),
                    BorderBrush = HexBrush(textHex),
                    BorderThickness = new Thickness(1.5),
                    ToolTip = name
                };
                pill.Child = new TextBlock { Text = name, FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = HexBrush(textHex) };
                pill.MouseLeftButtonDown += (_, _) =>
                {
                    if (_selected == null) return;
                    _selected.Rarity = n;
                    UpdateDetailRarity(_selected);
                    RebuildList();
                    Mark();
                };
                RarityPicker.Children.Add(pill);
            }
        }

        // ── List rendering ────────────────────────────────────────────────────
        private void RebuildList()
        {
            ItemsList.Children.Clear();
            var filtered = _items.Where(i =>
            {
                if (!string.IsNullOrEmpty(_filterRarity) && i.Rarity != _filterRarity) return false;
                if (!string.IsNullOrEmpty(_filterSearch) &&
                    !i.Name.Contains(_filterSearch, StringComparison.OrdinalIgnoreCase) &&
                    !i.Description.Contains(_filterSearch, StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }).ToList();

            foreach (var item in filtered)
                ItemsList.Children.Add(BuildRow(item));

            if (filtered.Count == 0)
            {
                var hint = new TextBlock
                {
                    Text = _items.Count == 0 ? "Нажмите «＋ Добавить предмет» чтобы начать" : "Ничего не найдено",
                    FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 32, 0, 0),
                    Foreground = (Brush)FindResource("TextDimBrush")
                };
                ItemsList.Children.Add(hint);
            }

            RefreshStats();
        }

        private UIElement BuildRow(InventoryItem item)
        {
            bool isSelected = item == _selected;
            var (_, bgHex, textHex, note) = GetRarityDef(item.Rarity);

            var row = new Border
            {
                Padding = new Thickness(14, 8, 14, 8),
                Background = isSelected
                    ? new SolidColorBrush(Color.FromArgb(40,
                        ParseHexColor(textHex).R, ParseHexColor(textHex).G, ParseHexColor(textHex).B))
                    : Brushes.Transparent,
                BorderBrush = (Brush)FindResource("BorderMedBrush"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Cursor = System.Windows.Input.Cursors.Hand,
            };

            if (isSelected)
            {
                row.BorderBrush = new SolidColorBrush(Color.FromArgb(80,
                    ParseHexColor(textHex).R, ParseHexColor(textHex).G, ParseHexColor(textHex).B));
            }

            row.MouseLeftButtonDown += (_, _) => SelectItem(item);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            // ── Image thumbnail ──
            var imgBorder = new Border
            {
                Width = 52, Height = 52, CornerRadius = new CornerRadius(8),
                Background = (Brush)FindResource("Surface2Brush"),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60,
                    ParseHexColor(textHex).R, ParseHexColor(textHex).G, ParseHexColor(textHex).B)),
                BorderThickness = new Thickness(1.5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            if (!string.IsNullOrEmpty(item.ImageBase64))
            {
                var img = new System.Windows.Controls.Image
                {
                    Stretch = Stretch.UniformToFill,
                    Source = Base64ToImage(item.ImageBase64)
                };
                imgBorder.Child = img;
                imgBorder.ClipToBounds = true;
            }
            else
            {
                imgBorder.Child = new TextBlock { Text = "🖼", FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center };
            }
            Grid.SetColumn(imgBorder, 0);

            // ── Name ──
            var nameBlock = new TextBlock
            {
                Text = item.Name, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameBlock, 1);

            // ── Description ──
            var descBlock = new TextBlock
            {
                Text = item.Description, FontSize = 12,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(descBlock, 2);

            // ── Rarity badge ──
            var rarityBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(8, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(60,
                    ParseHexColor(textHex).R, ParseHexColor(textHex).G, ParseHexColor(textHex).B)),
                BorderBrush = HexBrush(textHex),
                BorderThickness = new Thickness(1)
            };
            var rarityText = new StackPanel { Orientation = Orientation.Horizontal };
            rarityText.Children.Add(new TextBlock
            {
                Text = item.Rarity, FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = HexBrush(textHex)
            });
            if (note != null)
                rarityText.Children.Add(new TextBlock
                {
                    Text = $"  · {note}", FontSize = 10,
                    Foreground = (Brush)FindResource("TextDimBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                });
            rarityBorder.Child = rarityText;
            Grid.SetColumn(rarityBorder, 3);

            // ── Qty ──
            var qtyBlock = new TextBlock
            {
                Text = item.Quantity.ToString(), FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(qtyBlock, 4);

            // ── Equipped ──
            var equippedIcon = new TextBlock
            {
                Text = item.Equipped ? "✅" : "⬜",
                FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(equippedIcon, 5);

            grid.Children.Add(imgBorder);
            grid.Children.Add(nameBlock);
            grid.Children.Add(descBlock);
            grid.Children.Add(rarityBorder);
            grid.Children.Add(qtyBlock);
            grid.Children.Add(equippedIcon);
            row.Child = grid;

            // Hover effect
            row.MouseEnter += (_, _) =>
            {
                if (item != _selected)
                    row.Background = new SolidColorBrush(Color.FromArgb(20,
                        ParseHexColor(textHex).R, ParseHexColor(textHex).G, ParseHexColor(textHex).B));
            };
            row.MouseLeave += (_, _) =>
            {
                if (item != _selected) row.Background = Brushes.Transparent;
            };

            return row;
        }

        // ── Selection ─────────────────────────────────────────────────────────
        private void SelectItem(InventoryItem item)
        {
            _selected = item;
            RebuildList();
            ShowDetail(item);
        }

        private void ShowDetail(InventoryItem item)
        {
            _suppressDetailUpdate = true;
            TbDetailName.Text   = item.Name;
            TbDetailDesc.Text   = item.Description;
            TbDetailNotes.Text  = item.Notes;
            TbDetailQty.Text    = item.Quantity.ToString();
            CbDetailEquipped.IsChecked = item.Equipped;
            UpdateDetailRarity(item);
            UpdateDetailImage(item);
            _suppressDetailUpdate = false;
        }

        private void UpdateDetailRarity(InventoryItem item)
        {
            var (_, bgHex, textHex, note) = GetRarityDef(item.Rarity);
            DetailRarityBorder.Background = HexBrush(bgHex + "60");
            DetailRarityBorder.BorderBrush = HexBrush(textHex);
            DetailRarityBorder.BorderThickness = new Thickness(1.5);
            TbDetailRarity.Text = item.Rarity;
            TbDetailRarity.Foreground = HexBrush(textHex);
            TbDetailRarityNote.Text = note != null ? $"· {note}" : "";
        }

        private void UpdateDetailImage(InventoryItem item)
        {
            if (!string.IsNullOrEmpty(item.ImageBase64))
            {
                DetailImage.Source = Base64ToImage(item.ImageBase64);
                DetailImage.Visibility = Visibility.Visible;
                DetailImagePlaceholder.Visibility = Visibility.Collapsed;
                BtnViewImg.Visibility = BtnRemoveImg.Visibility = Visibility.Visible;
            }
            else
            {
                DetailImage.Source = null;
                DetailImage.Visibility = Visibility.Collapsed;
                DetailImagePlaceholder.Visibility = Visibility.Visible;
                BtnViewImg.Visibility = BtnRemoveImg.Visibility = Visibility.Collapsed;
            }
        }

        // ── Detail events ─────────────────────────────────────────────────────
        private void DetailName_Changed(object s, TextChangedEventArgs e)
        {
            if (_suppressDetailUpdate || _selected == null) return;
            _selected.Name = TbDetailName.Text;
            RebuildList(); Mark();
        }
        private void DetailDesc_Changed(object s, TextChangedEventArgs e)
        {
            if (_suppressDetailUpdate || _selected == null) return;
            _selected.Description = TbDetailDesc.Text;
            RebuildList(); Mark();
        }
        private void DetailNotes_Changed(object s, TextChangedEventArgs e)
        {
            if (_suppressDetailUpdate || _selected == null) return;
            _selected.Notes = TbDetailNotes.Text; Mark();
        }
        private void Equipped_Changed(object s, RoutedEventArgs e)
        {
            if (_suppressDetailUpdate || _selected == null) return;
            _selected.Equipped = CbDetailEquipped.IsChecked == true;
            RebuildList(); Mark();
        }
        private void QtyMinus_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            _selected.Quantity = Math.Max(1, _selected.Quantity - 1);
            _suppressDetailUpdate = true;
            TbDetailQty.Text = _selected.Quantity.ToString();
            _suppressDetailUpdate = false;
            RebuildList(); Mark();
        }
        private void QtyPlus_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            _selected.Quantity++;
            _suppressDetailUpdate = true;
            TbDetailQty.Text = _selected.Quantity.ToString();
            _suppressDetailUpdate = false;
            RebuildList(); Mark();
        }

        // Direct text input for quantity
        private void QtyText_PreviewInput(object s, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Allow digits only
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void QtyText_Changed(object s, TextChangedEventArgs e)
        {
            if (_suppressDetailUpdate || _selected == null) return;
            if (int.TryParse(TbDetailQty.Text, out var v) && v >= 1)
            {
                _selected.Quantity = v;
                RebuildList(); Mark();
            }
        }

        private void QtyText_LostFocus(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            // Clamp and normalize on focus loss
            if (!int.TryParse(TbDetailQty.Text, out var v) || v < 1) v = 1;
            _selected.Quantity = v;
            _suppressDetailUpdate = true;
            TbDetailQty.Text = v.ToString();
            _suppressDetailUpdate = false;
            RebuildList(); Mark();
        }

        // ── Image ─────────────────────────────────────────────────────────────
        private void ChangeImage_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            var dlg = new OpenFileDialog
            {
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|Все файлы|*.*",
                Title  = "Выберите изображение предмета"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var bytes = File.ReadAllBytes(dlg.FileName);
                _selected.ImageBase64 = Convert.ToBase64String(bytes);
                UpdateDetailImage(_selected);
                RebuildList(); Mark();
            }
            catch { }
        }

        private void ViewImage_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null || string.IsNullOrEmpty(_selected.ImageBase64)) return;
            var win = new Window
            {
                Title = _selected.Name, WindowStyle = WindowStyle.SingleBorderWindow,
                Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow
            };
            win.Content = new System.Windows.Controls.Image
            {
                Source = Base64ToImage(_selected.ImageBase64),
                Stretch = Stretch.Uniform
            };
            win.ShowDialog();
        }

        private void RemoveImage_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            _selected.ImageBase64 = "";
            UpdateDetailImage(_selected);
            RebuildList(); Mark();
        }

        // ── CRUD ──────────────────────────────────────────────────────────────
        private void AddItem_Click(object s, RoutedEventArgs e)
        {
            var item = new InventoryItem();
            item.PropertyChanged += Item_Changed;
            _items.Add(item);
            _selected = item;
            RebuildList();
            ShowDetail(item);
            Mark();
        }

        private void DeleteItem_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            _items.Remove(_selected);
            _selected = null;
            RebuildList();
            ClearDetail();
            Mark();
        }

        private void ClearDetail()
        {
            TbDetailName.Text  = "";
            TbDetailDesc.Text  = "";
            TbDetailNotes.Text = "";
        }

        private void Item_Changed(object? s, System.ComponentModel.PropertyChangedEventArgs e)
        {
            RebuildList();
            Mark();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void RefreshStats()
        {
            TbItemCount.Text = _items.Count > 0 ? $"({_items.Count} предм.)" : "";
        }

        private void Wallet_Changed(object s, TextChangedEventArgs e) => Mark();

        private static (string Name, string BgHex, string TextHex, string? Note)
            GetRarityDef(string rarity)
        {
            foreach (var r in Rarities)
                if (r.Name == rarity) return r;
            return Rarities[0];
        }

        private static SolidColorBrush HexBrush(string hex)
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { return new SolidColorBrush(Colors.Gray); }
        }

        private static Color ParseHexColor(string hex)
        {
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { return Colors.Gray; }
        }

        private static BitmapImage Base64ToImage(string b64)
        {
            var bytes = Convert.FromBase64String(b64);
            var bmp = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private static void Mark()
            => (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
    }
}
