using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CharacterApp
{
    public partial class ColorPickerWindow : Window
    {
        public Color SelectedColor { get; private set; } = Colors.White;
        public string? PresetHex   { get; set; }

        private static readonly string[] Palette = {
            // Whites / grays / blacks
            "#FFFFFF","#F0F0F0","#C8C8C8","#909090","#606060","#303030","#000000",
            // Reds
            "#FF6B6B","#E03060","#C02040","#FF4444","#990000",
            // Oranges / yellows
            "#FFB347","#FFD700","#FFA500","#FF8C00","#E09030",
            // Greens
            "#4CAF72","#50C878","#228B22","#006400","#90EE90",
            // Blues
            "#70B0F0","#4A90D9","#1E90FF","#0000CD","#191970",
            // Purples
            "#B565C1","#9B59B6","#8040C0","#6A0DAD","#E0B0FF",
            // Pinks
            "#FF69B4","#FF1493","#DB7093","#FFB6C1",
            // Cyans / teals
            "#50A8A0","#20B2AA","#008B8B","#00CED1","#7FFFD4",
        };

        public ColorPickerWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object s, RoutedEventArgs e)
        {
            BuildPalette();
            if (!string.IsNullOrEmpty(PresetHex))
            {
                TbHex.Text = PresetHex;
                TryApplyHex(PresetHex);
            }
        }

        private void BuildPalette()
        {
            foreach (var hex in Palette)
            {
                var h = hex;
                var rect = new Border
                {
                    Width = 24, Height = 24, CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(2),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = h
                };
                try { rect.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(h)); }
                catch { rect.Background = Brushes.Gray; }

                rect.MouseLeftButtonDown += (_, _) =>
                {
                    TbHex.Text = h;
                    TryApplyHex(h);
                };
                PalettePanel.Children.Add(rect);
            }
        }

        private void TbHex_TextChanged(object s, TextChangedEventArgs e)
        {
            TryApplyHex(TbHex.Text);
        }

        private void TryApplyHex(string hex)
        {
            if (PreviewColor == null || TbHexError == null) return; // not yet loaded
            var h = hex.Trim();
            if (!h.StartsWith('#')) h = '#' + h;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(h);
                PreviewColor.Color = color;
                SelectedColor = color;
                TbHexError.Visibility = Visibility.Collapsed;
            }
            catch
            {
                TbHexError.Visibility = Visibility.Visible;
            }
        }

        private void Ok_Click(object s, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object s, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
