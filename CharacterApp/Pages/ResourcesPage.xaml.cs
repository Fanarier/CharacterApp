// Pages/ResourcesPage.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CharacterApp.Pages
{
    public class ResourceTracker
    {
        public string Id      { get; set; } = Guid.NewGuid().ToString();
        public string Name    { get; set; } = "Новый ресурс";
        public int    Current { get; set; }
        public int    Max     { get; set; } = 5;
        public string Color   { get; set; } = "#B565C1";
        public string ResetOn { get; set; } = "long"; // "long" | "short" | "never"
    }

    public class HpData
    {
        public int Current { get; set; }
        public int Max     { get; set; }
    }

    public partial class ResourcesPage : Page
    {
        private HpData _hp = new() { Current = 0, Max = 0 };
        private readonly List<ResourceTracker> _resources = new();

        public ResourcesPage() => InitializeComponent();

        // ── Public API ────────────────────────────────────────────────────────
        public HpData GetHpData() => _hp;
        public List<ResourceTracker> GetResources() => _resources.ToList();

        public void LoadData(HpData? hp, List<ResourceTracker>? resources)
        {
            _hp = hp ?? new();
            _resources.Clear();
            if (resources != null) _resources.AddRange(resources);
            RefreshHpUI();
            RebuildResourceCards();
        }

        // ── HP ────────────────────────────────────────────────────────────────
        private void HpBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            switch (btn.Tag?.ToString())
            {
                case "HpCurrent+": _hp.Current = Math.Min(_hp.Current + 1, _hp.Max); break;
                case "HpCurrent-": _hp.Current = Math.Max(_hp.Current - 1, 0); break;
                case "HpMax+":     _hp.Max++;                                    break;
                case "HpMax-":     _hp.Max = Math.Max(_hp.Max - 1, 0);           break;
            }
            RefreshHpUI(); Mark();
        }

        private void FullHeal_Click(object sender, RoutedEventArgs e)
        { _hp.Current = _hp.Max; RefreshHpUI(); Mark(); }

        private void RefreshHpUI()
        {
            TbHpCurrent.Text = _hp.Current.ToString();
            TbHpMax.Text     = _hp.Max.ToString();

            // Animate HP bar width
            double pct = _hp.Max > 0 ? (double)_hp.Current / _hp.Max : 0;
            pct = Math.Clamp(pct, 0, 1);

            // We'll update bar width when panel is measured
            HpBar.Tag = pct;
            HpBar.Loaded -= HpBar_UpdateWidth;
            HpBar.Loaded += HpBar_UpdateWidth;
            if (HpBar.IsLoaded) UpdateHpBarWidth();

            // Color: red < 25%, yellow < 50%, green otherwise
            HpBar.Background = pct < 0.25
                ? new SolidColorBrush(Color.FromRgb(220, 50, 50))
                : pct < 0.5
                    ? new SolidColorBrush(Color.FromRgb(220, 160, 30))
                    : new LinearGradientBrush(
                        Color.FromRgb(50, 190, 90),
                        Color.FromRgb(30, 150, 60), 0);
        }

        private void HpBar_UpdateWidth(object s, RoutedEventArgs e) => UpdateHpBarWidth();
        private void UpdateHpBarWidth()
        {
            double pct = (HpBar.Tag as double?) ?? 0;
            double parentW = (HpBar.Parent as Border)?.ActualWidth ?? 200;
            var anim = new DoubleAnimation(parentW * pct,
                TimeSpan.FromMilliseconds(350))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            HpBar.BeginAnimation(WidthProperty, anim);
        }

        // ── Custom resources ──────────────────────────────────────────────────
        private void AddResource_Click(object sender, RoutedEventArgs e)
        {
            var res = new ResourceTracker();
            _resources.Add(res);
            RebuildResourceCards();
            TbEmptyHint.Visibility = Visibility.Collapsed;
            Mark();
        }

        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _resources) r.Current = r.Max;
            RebuildResourceCards(); Mark();
        }

        private void RebuildResourceCards()
        {
            CustomResourcesList.Items.Clear();
            TbEmptyHint.Visibility = _resources.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            foreach (var res in _resources)
                CustomResourcesList.Items.Add(BuildCard(res));
        }

        private UIElement BuildCard(ResourceTracker res)
        {
            var accent = (Brush)new BrushConverter().ConvertFrom(res.Color)!;

            var outer = new Border
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new CornerRadius(10),
                Background = (Brush)FindResource("SurfaceBrush"),
                BorderBrush = (Brush)FindResource("BorderMedBrush"),
                BorderThickness = new Thickness(1),
            };

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBox = new TextBox
            {
                Text = res.Name, FontSize = 14, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 8),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            };
            nameBox.TextChanged += (_, _) => { res.Name = nameBox.Text; Mark(); };
            Grid.SetColumn(nameBox, 0);

            var delBtn = new Button { Content = "✕", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0) };
            delBtn.Click += (_, _) => { _resources.Remove(res); RebuildResourceCards(); Mark(); };
            Grid.SetColumn(delBtn, 1);
            header.Children.Add(nameBox);
            header.Children.Add(delBtn);

            // Pips
            var pips = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
            void RebuildPips()
            {
                pips.Children.Clear();
                for (int i = 0; i < res.Max; i++)
                {
                    int idx = i;
                    var pip = new Border
                    {
                        Width = 22, Height = 22, CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(3),
                        Background = i < res.Current ? accent
                            : (Brush)FindResource("Surface2Brush"),
                        BorderBrush = accent, BorderThickness = new Thickness(1.5),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    pip.MouseLeftButtonDown += (_, _) =>
                    {
                        res.Current = (idx < res.Current) ? idx : idx + 1;
                        res.Current = Math.Clamp(res.Current, 0, res.Max);
                        RebuildPips(); Mark();
                    };
                    pips.Children.Add(pip);
                }
            }
            RebuildPips();

            // Max counter
            var maxPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var minusMax = new Button { Content = "−", Width = 26, Height = 26, Margin = new Thickness(2), MinHeight = 0, Padding = new Thickness(0) };
            var maxLabel = new TextBlock { Text = $"Макс: {res.Max}", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0), Foreground = (Brush)FindResource("TextMutedBrush"), FontSize = 12 };
            var plusMax  = new Button { Content = "＋", Width = 26, Height = 26, Margin = new Thickness(2), MinHeight = 0, Padding = new Thickness(0) };
            minusMax.Click += (_, _) => { res.Max = Math.Max(res.Max - 1, 1); if (res.Current > res.Max) res.Current = res.Max; maxLabel.Text = $"Макс: {res.Max}"; RebuildPips(); Mark(); };
            plusMax.Click  += (_, _) => { res.Max = Math.Min(res.Max + 1, 40); maxLabel.Text = $"Макс: {res.Max}"; RebuildPips(); Mark(); };
            maxPanel.Children.Add(minusMax);
            maxPanel.Children.Add(maxLabel);
            maxPanel.Children.Add(plusMax);

            var stack = new StackPanel();
            stack.Children.Add(header);
            stack.Children.Add(pips);
            stack.Children.Add(maxPanel);
            outer.Child = stack;
            return outer;
        }

        private static void Mark()
            => (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
    }
}
