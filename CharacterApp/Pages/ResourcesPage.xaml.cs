using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace CharacterApp.Pages
{
    // ── Models ────────────────────────────────────────────────────────────────
    public class ResourceTracker
    {
        public string Id      { get; set; } = Guid.NewGuid().ToString();
        public string Name    { get; set; } = "Новый ресурс";
        public int    Current { get; set; }
        public int    Max     { get; set; } = 5;
        public string Color   { get; set; } = "#B565C1";
        public string ResetOn { get; set; } = "long";   // long | short | never
        public string Style   { get; set; } = "pips";   // pips | number | ammo | boxes
        public string Notes   { get; set; } = "";
        public string Icon    { get; set; } = "🎯";
    }

    public class HpData
    {
        public int  Current { get; set; }
        public int  Max     { get; set; }
        public int  Temp    { get; set; }
        public bool DS_S1   { get; set; }
        public bool DS_S2   { get; set; }
        public bool DS_S3   { get; set; }
        public bool DS_F1   { get; set; }
        public bool DS_F2   { get; set; }
        public bool DS_F3   { get; set; }
    }

    // ── Page ──────────────────────────────────────────────────────────────────
    public partial class ResourcesPage : Page
    {
        private HpData _hp = new() { Max = 10, Current = 10 };
        private readonly List<ResourceTracker> _resources = new();

        private static readonly string[] Presets =
        {
            "#B565C1","#5E8FBF","#4CAF72","#E08030",
            "#D04060","#50A8A0","#8070CC","#C09030","#F07070","#70B0F0"
        };

        public ResourcesPage() => InitializeComponent();

        // ── Public API ────────────────────────────────────────────────────────
        public HpData GetHpData() => _hp;
        public List<ResourceTracker> GetResources() => _resources.ToList();

        public void LoadData(HpData? hp, List<ResourceTracker>? res)
        {
            _hp = hp ?? new() { Max = 10, Current = 10 };
            _resources.Clear();
            if (res != null) _resources.AddRange(res);
            RefreshHpUI();
            BindDeathSaves();
            RebuildList();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HP
        // ══════════════════════════════════════════════════════════════════════
        private void RefreshHpUI()
        {
            TbHpCurrent.Text = _hp.Current.ToString();
            TbHpMax.Text     = _hp.Max.ToString();
            TbHpTemp.Text    = _hp.Temp.ToString();
            AnimateHpBar();
        }

        private void AnimateHpBar()
        {
            if (!HpBarTrack.IsLoaded) { HpBarTrack.Loaded += (_, _) => AnimateHpBar(); return; }
            double pct = _hp.Max > 0 ? Math.Clamp((double)_hp.Current / _hp.Max, 0, 1) : 0;
            var anim = new DoubleAnimation(HpBarTrack.ActualWidth * pct,
                TimeSpan.FromMilliseconds(400))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            HpFill.BeginAnimation(WidthProperty, anim);
            HpFill.Background = pct < 0.25
                ? new SolidColorBrush(Color.FromRgb(210, 50, 50))
                : pct < 0.5
                    ? new SolidColorBrush(Color.FromRgb(220, 150, 30))
                    : new LinearGradientBrush(Color.FromRgb(50, 190, 90), Color.FromRgb(30, 150, 60), 0);
        }

        // HP text input — validate and update on LostFocus / Enter
        private void HpBox_KeyDown(object s, KeyEventArgs e)
        { if (e.Key == Key.Enter) CommitHpBoxes(); }
        private void HpBox_LostFocus(object s, RoutedEventArgs e) => CommitHpBoxes();

        private void CommitHpBoxes()
        {
            if (int.TryParse(TbHpMax.Text,     out var mx))  { _hp.Max     = Math.Max(0, mx);  }
            if (int.TryParse(TbHpCurrent.Text, out var cur)) { _hp.Current = Math.Clamp(cur, 0, _hp.Max); }
            if (int.TryParse(TbHpTemp.Text,    out var tmp)) { _hp.Temp    = Math.Max(0, tmp); }
            RefreshHpUI(); Mark();
        }

        private void HpQuickChange(int delta)
        {
            _hp.Current = Math.Clamp(_hp.Current + delta, 0, _hp.Max);
            RefreshHpUI(); Mark();
        }

        private void BtnHpMinus_Click(object s, RoutedEventArgs e) => HpQuickChange(-1);
        private void BtnHpPlus_Click (object s, RoutedEventArgs e) => HpQuickChange(+1);

        private void BtnApplyChange_Click(object s, RoutedEventArgs e)
        {
            if (!int.TryParse(TbChangeAmount.Text, out var amt) || amt == 0) return;
            bool isDamage = CbChangeType.SelectedIndex == 1;

            if (isDamage)
            {
                int dmg = Math.Abs(amt);
                if (_hp.Temp > 0)
                {
                    int absorbed = Math.Min(_hp.Temp, dmg);
                    _hp.Temp -= absorbed; dmg -= absorbed;
                }
                _hp.Current = Math.Max(_hp.Current - dmg, 0);
            }
            else
            {
                _hp.Current = Math.Min(_hp.Current + Math.Abs(amt), _hp.Max);
            }

            TbChangeAmount.Text = "";
            RefreshHpUI(); Mark();
        }

        private void TbChangeAmount_KeyDown(object s, KeyEventArgs e)
        { if (e.Key == Key.Enter) BtnApplyChange_Click(s, e); }

        private void BtnFullHeal_Click(object s, RoutedEventArgs e)
        { _hp.Current = _hp.Max; RefreshHpUI(); Mark(); }

        // ══════════════════════════════════════════════════════════════════════
        //  Death Saves
        // ══════════════════════════════════════════════════════════════════════
        private void BindDeathSaves()
        {
            DsS1.IsChecked = _hp.DS_S1; DsS2.IsChecked = _hp.DS_S2; DsS3.IsChecked = _hp.DS_S3;
            DsF1.IsChecked = _hp.DS_F1; DsF2.IsChecked = _hp.DS_F2; DsF3.IsChecked = _hp.DS_F3;

            void Bind(CheckBox cb, Action<bool> set) {
                cb.Checked   += (_, _) => { set(true);  Mark(); };
                cb.Unchecked += (_, _) => { set(false); Mark(); };
            }
            Bind(DsS1, v => _hp.DS_S1 = v); Bind(DsS2, v => _hp.DS_S2 = v); Bind(DsS3, v => _hp.DS_S3 = v);
            Bind(DsF1, v => _hp.DS_F1 = v); Bind(DsF2, v => _hp.DS_F2 = v); Bind(DsF3, v => _hp.DS_F3 = v);
        }

        private void BtnResetDeathSaves_Click(object s, RoutedEventArgs e)
        {
            _hp.DS_S1 = _hp.DS_S2 = _hp.DS_S3 = false;
            _hp.DS_F1 = _hp.DS_F2 = _hp.DS_F3 = false;
            DsS1.IsChecked = DsS2.IsChecked = DsS3.IsChecked = false;
            DsF1.IsChecked = DsF2.IsChecked = DsF3.IsChecked = false;
            Mark();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Rests
        // ══════════════════════════════════════════════════════════════════════
        private void BtnRestoreAll_Click(object s, RoutedEventArgs e)
        {
            foreach (var r in _resources) r.Current = r.Max;
            RebuildList(); Mark();
            (App.Current.MainWindow as MainWindow)
                ?.ShowNotification("🔄 Все ресурсы восстановлены до максимума", NotificationType.Info);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Resource list
        // ══════════════════════════════════════════════════════════════════════
        private void BtnAddResource_Click(object s, RoutedEventArgs e)
        {
            var r = new ResourceTracker
            {
                Color = App.CurrentAccentHex is { Length: > 0 } h ? h : "#B565C1"
            };
            _resources.Add(r);
            RebuildList(); Mark();
        }

        private void RebuildList()
        {
            ResourceList.Children.Clear();
            EmptyHint.Visibility = _resources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TbResCount.Text = _resources.Count > 0 ? $"{_resources.Count} ресурс(ов)" : "";

            foreach (var r in _resources)
                ResourceList.Children.Add(BuildCard(r));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Card builder — full accent colorization + icon + hex input
        // ══════════════════════════════════════════════════════════════════════
        private static readonly string[] IconPresets =
        {
            "🎯","🦊","🐺","🐉","🧙","⚡","🏹","🗡️","🛡️","💎",
            "🔮","🌟","✨","🎭","🎲","🌙","☀️","🔥","💧","🌿",
            "⚔️","🏔️","🦋","🌺","💫","❤️","💀","🎪","🧿","⚗️"
        };

        private Border BuildCard(ResourceTracker res)
        {
            // ── Helpers ──────────────────────────────────────────────────────
            Color AccentC() {
                try { return (Color)ColorConverter.ConvertFromString(res.Color); }
                catch { return (Color)ColorConverter.ConvertFromString("#B565C1"); }
            }
            SolidColorBrush AccentB()   => new(AccentC());
            SolidColorBrush AccentDim() => new(Color.FromArgb(45, AccentC().R, AccentC().G, AccentC().B));
            SolidColorBrush AccentMid() => new(Color.FromArgb(120, AccentC().R, AccentC().G, AccentC().B));
            SolidColorBrush AccentBg()  => new(Color.FromArgb(25, AccentC().R, AccentC().G, AccentC().B));
            SolidColorBrush AccentBorder() => new(Color.FromArgb(90, AccentC().R, AccentC().G, AccentC().B));

            // ── Outer card ───────────────────────────────────────────────────
            var card = new Border
            {
                Margin          = new Thickness(0, 0, 0, 10),
                CornerRadius    = new CornerRadius(14),
                Background      = (Brush)FindResource("SurfaceBrush"),
                BorderBrush     = AccentBorder(),
                BorderThickness = new Thickness(1.5),
                Effect          = new DropShadowEffect
                {
                    BlurRadius = 14, ShadowDepth = 2,
                    Color = AccentC(), Opacity = 0.18
                }
            };

            var root = new StackPanel();
            card.Child = root;

            // ── Header ───────────────────────────────────────────────────────
            var header = new Border
            {
                Padding         = new Thickness(14, 10, 14, 10),
                Background      = AccentBg(),
                CornerRadius    = new CornerRadius(14, 14, 0, 0),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(50, AccentC().R, AccentC().G, AccentC().B)),
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            root.Children.Add(header);

            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition());
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Child = headerRow;

            // Icon button (opens picker)
            var iconBtn = new Button
            {
                Content         = res.Icon, FontSize = 16,
                Width           = 32, Height = 32, MinHeight = 0, Padding = new Thickness(0),
                Background      = AccentDim(),
                BorderBrush     = AccentMid(),
                BorderThickness = new Thickness(1),
                Foreground      = AccentB(),
                Cursor          = Cursors.Hand, Margin = new Thickness(0, 0, 10, 0),
                ToolTip         = "Выбрать иконку",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBtn, 0);

            // Name
            var nameBox = new TextBox
            {
                Text = res.Name, FontSize = 14, FontWeight = FontWeights.SemiBold,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = (Brush)FindResource("TextBrush"), VerticalAlignment = VerticalAlignment.Center,
            };
            nameBox.TextChanged += (_, _) => { res.Name = nameBox.Text; Mark(); };
            Grid.SetColumn(nameBox, 1);

            // ResetOn UI removed (rest system simplified)
            var resetCombo = new UIElement(); // placeholder - not added to grid

            // Delete
            var del = new Button
            {
                Content = "✕", Padding = new Thickness(8, 4, 8, 4), MinHeight = 0,
                Background = new SolidColorBrush(Color.FromArgb(40, 200, 50, 50)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 200, 50, 50)),
                BorderThickness = new Thickness(1),
                Foreground = new SolidColorBrush(Color.FromRgb(240, 90, 90)),
                VerticalAlignment = VerticalAlignment.Center
            };
            del.Click += (_, _) => { _resources.Remove(res); RebuildList(); Mark(); };
            Grid.SetColumn(del, 2);

            headerRow.Children.Add(iconBtn);
            headerRow.Children.Add(nameBox);
            headerRow.Children.Add(del);

            // ── Body ─────────────────────────────────────────────────────────
            var body = new StackPanel { Margin = new Thickness(14, 10, 14, 14) };
            root.Children.Add(body);

            // ── Icon picker popup ────────────────────────────────────────────
            var iconPickerPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 10)
            };
            foreach (var emoji in IconPresets)
            {
                var em = emoji;
                var eb = new Border
                {
                    Width = 34, Height = 34, CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(2), Cursor = Cursors.Hand,
                    Background = res.Icon == em ? AccentDim() : (Brush)FindResource("Surface2Brush"),
                    BorderBrush = res.Icon == em ? AccentB() : (Brush)FindResource("BorderMedBrush"),
                    BorderThickness = new Thickness(1.5)
                };
                eb.Child = new TextBlock { Text = em, FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center };
                eb.MouseLeftButtonDown += (_, _) =>
                {
                    res.Icon = em;
                    iconBtn.Content = em;
                    iconPickerPanel.Visibility = Visibility.Collapsed;
                    foreach (Border sib in iconPickerPanel.Children.OfType<Border>()) {
                        bool active = (sib.Child is TextBlock t && t.Text == em);
                        sib.Background   = active ? AccentDim() : (Brush)FindResource("Surface2Brush");
                        sib.BorderBrush  = active ? AccentB()   : (Brush)FindResource("BorderMedBrush");
                    }
                    Mark();
                };
                iconPickerPanel.Children.Add(eb);
            }
            iconBtn.Click += (_, _) =>
                iconPickerPanel.Visibility = iconPickerPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed : Visibility.Visible;
            body.Children.Add(iconPickerPanel);

            // ── Style picker ──────────────────────────────────────────────────
            var stylePicker = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            UIElement displayArea = null!;

            void AddStyleBtn(string tag, string label, string tip)
            {
                var b = new Border
                {
                    Padding = new Thickness(10, 5, 10, 5), CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand,
                    Background      = res.Style == tag ? AccentB() : (Brush)FindResource("Surface2Brush"),
                    BorderBrush     = res.Style == tag ? AccentMid() : (Brush)FindResource("BorderMedBrush"),
                    BorderThickness = new Thickness(1), ToolTip = tip
                };
                var tb = new TextBlock { Text = label, FontSize = 12,
                    Foreground = res.Style == tag ? Brushes.White : (Brush)FindResource("TextMutedBrush") };
                b.Child = tb;
                b.MouseLeftButtonDown += (_, _) =>
                {
                    res.Style = tag;
                    foreach (Border sib in stylePicker.Children.OfType<Border>()) {
                        bool active = (sib == b);
                        sib.Background   = active ? AccentB()   : (Brush)FindResource("Surface2Brush");
                        sib.BorderBrush  = active ? AccentMid() : (Brush)FindResource("BorderMedBrush");
                        if (sib.Child is TextBlock t)
                            t.Foreground = active ? Brushes.White : (Brush)FindResource("TextMutedBrush");
                    }
                    body.Children.Remove(displayArea);
                    displayArea = BuildDisplayArea(res, AccentB, AccentC, body);
                    body.Children.Add(displayArea);
                    Mark();
                };
                stylePicker.Children.Add(b);
            }
            AddStyleBtn("pips",   "⭕ Кружки",  "Заполненные/пустые кружки");
            AddStyleBtn("ammo",   "🟫 Ячейки",  "Патроны, слоты заклинаний");
            AddStyleBtn("boxes",  "☑ Флажки",  "Чекбоксы");
            AddStyleBtn("number", "🔢 Числа",   "Числовой счётчик");
            body.Children.Add(stylePicker);

            // ── Display area ──────────────────────────────────────────────────
            displayArea = BuildDisplayArea(res, AccentB, AccentC, body);
            body.Children.Add(displayArea);

            // ── Color row: swatches + HEX input ──────────────────────────────
            var colorRow = new StackPanel { Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 6), VerticalAlignment = VerticalAlignment.Center };

            // Swatches
            var swatchPanel = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (var hex in Presets)
            {
                var h = hex;
                var sw = new Border
                {
                    Width = 18, Height = 18, CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(2), Cursor = Cursors.Hand, ToolTip = h,
                    BorderBrush = res.Color == h ? Brushes.White : Brushes.Transparent,
                    BorderThickness = new Thickness(2),
                };
                try { sw.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(h)); } catch { }
                sw.MouseLeftButtonDown += (_, _) =>
                {
                    res.Color = h;
                    foreach (Border s in swatchPanel.Children.OfType<Border>())
                        s.BorderBrush = (s == sw) ? Brushes.White : Brushes.Transparent;
                    RebuildSingleCard();
                };
                swatchPanel.Children.Add(sw);
            }
            colorRow.Children.Add(swatchPanel);

            // HEX input
            var hexInput = new TextBox
            {
                Text = res.Color, Width = 76, Height = 22, FontSize = 11,
                Margin = new Thickness(8, 0, 0, 0),
                Background = (Brush)FindResource("Surface2Brush"),
                BorderBrush = AccentMid(), BorderThickness = new Thickness(1),
                Foreground = (Brush)FindResource("TextBrush"), Padding = new Thickness(4, 1, 4, 1),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Введите HEX цвет и нажмите Enter"
            };
            void ApplyHex(string raw) {
                var h2 = raw.Trim();
                if (!h2.StartsWith('#')) h2 = '#' + h2;
                try {
                    ColorConverter.ConvertFromString(h2); // validate
                    res.Color = h2;
                    hexInput.Text = h2;
                    // Update swatch borders
                    foreach (Border s in swatchPanel.Children.OfType<Border>())
                        s.BorderBrush = Brushes.Transparent;
                    RebuildSingleCard();
                } catch {
                    hexInput.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
            hexInput.KeyDown += (_, e) => { if (e.Key == Key.Enter) ApplyHex(hexInput.Text); };
            hexInput.LostFocus += (_, _) => ApplyHex(hexInput.Text);
            colorRow.Children.Add(hexInput);

            body.Children.Add(colorRow);

            // ── Notes ──────────────────────────────────────────────────────────
            var notesBox = new TextBox
            {
                Text = string.IsNullOrWhiteSpace(res.Notes) ? "Заметки (необязательно)…" : res.Notes,
                FontSize = 11, Margin = new Thickness(0, 4, 0, 0),
                Background = (Brush)FindResource("Surface2Brush"),
                BorderBrush = AccentBorder(), BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5), TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true, MaxHeight = 60,
                Foreground = string.IsNullOrWhiteSpace(res.Notes)
                    ? (Brush)FindResource("TextMutedBrush")
                    : (Brush)FindResource("TextBrush"),
                ToolTip = "Заметки к ресурсу"
            };
            notesBox.GotFocus += (_, _) => {
                if (notesBox.Text == "Заметки (необязательно)…")
                { notesBox.Text = ""; notesBox.Foreground = (Brush)FindResource("TextBrush"); }
            };
            notesBox.LostFocus += (_, _) => {
                if (string.IsNullOrWhiteSpace(notesBox.Text))
                { notesBox.Text = "Заметки (необязательно)…"; notesBox.Foreground = (Brush)FindResource("TextMutedBrush"); }
                else { res.Notes = notesBox.Text; Mark(); }
            };
            body.Children.Add(notesBox);

            // ── Rebuild helper (for color change) ─────────────────────────────
            void RebuildSingleCard() {
                var idx = ResourceList.Children.IndexOf(card);
                if (idx >= 0) {
                    ResourceList.Children.RemoveAt(idx);
                    ResourceList.Children.Insert(idx, BuildCard(res));
                }
                Mark();
            }

            return card;
        }
        // ── Display area per style ───────────────────────────────────────────
        private UIElement BuildDisplayArea(ResourceTracker res,
            Func<SolidColorBrush> accentB, Func<Color> accentC, StackPanel _body)
        {
            return res.Style switch
            {
                "number" => BuildNumberArea(res, accentB),
                "ammo"   => BuildAmmoArea(res, accentB, accentC),
                "boxes"  => BuildBoxesArea(res, accentB, accentC),
                _        => BuildPipsArea(res, accentB, accentC),
            };
        }

        // ── NUMBER style ─────────────────────────────────────────────────────
        private UIElement BuildNumberArea(ResourceTracker res, Func<SolidColorBrush> accentB)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());

            // Current
            var curPanel = BuildNumBox("ТЕКУЩЕЕ", res.Current.ToString(), accentB,
                val => { res.Current = Math.Clamp(val, 0, res.Max); Mark(); return res.Current; });
            Grid.SetColumn(curPanel, 0);

            // Separator
            var sep = new TextBlock { Text = "/", FontSize = 28, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextDimBrush"),
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 10, 10) };
            Grid.SetColumn(sep, 1);

            // Max
            var maxPanel = BuildNumBox("МАКСИМУМ", res.Max.ToString(), (SolidColorBrush?)null,
                val => { res.Max = Math.Max(1, val); if (res.Current > res.Max) res.Current = res.Max; Mark(); return res.Max; });
            Grid.SetColumn(maxPanel, 2);

            g.Children.Add(curPanel); g.Children.Add(sep); g.Children.Add(maxPanel);
            return g;
        }

        private StackPanel BuildNumBox(string label, string value, Func<SolidColorBrush>? accentB, Func<int,int> onCommit)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = label, FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4)
            });

            var numBox = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = (Brush)FindResource("Surface2Brush"),
                BorderBrush = accentB?.Invoke() ?? (Brush)FindResource("BorderMedBrush"),
                BorderThickness = new Thickness(1.5), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 4)
            };
            var tb = new TextBox
            {
                Text = value, FontSize = 32, FontWeight = FontWeights.Bold,
                MinWidth = 80, TextAlignment = TextAlignment.Center,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = accentB?.Invoke() ?? (Brush)FindResource("TextBrush"),
            };
            tb.LostFocus += (_, _) => { if (int.TryParse(tb.Text, out var v)) tb.Text = onCommit(v).ToString(); else tb.Text = "0"; };
            tb.KeyDown   += (_, e)  => { if (e.Key == Key.Enter && int.TryParse(tb.Text, out var v)) tb.Text = onCommit(v).ToString(); };
            numBox.Child = tb;
            sp.Children.Add(numBox);

            // +/- row
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            void QBtn(string txt, int delta) {
                var b = new Button { Content = txt, Padding = new Thickness(10, 4, 10, 4), MinHeight = 0,
                    Margin = new Thickness(2, 0, 2, 0) };
                b.Click += (_, _) => {
                    if (int.TryParse(tb.Text, out var v)) tb.Text = onCommit(v + delta).ToString();
                };
                btnRow.Children.Add(b);
            }
            QBtn("−5", -5); QBtn("−1", -1); QBtn("+1", +1); QBtn("+5", +5);
            sp.Children.Add(btnRow);
            return sp;
        }

        private StackPanel BuildNumBox(string label, string value, SolidColorBrush? accent, Func<int,int> onCommit)
            => BuildNumBox(label, value, accent == null ? null : (Func<SolidColorBrush>)(() => accent), onCommit);

        // ── PIPS style ───────────────────────────────────────────────────────
        private UIElement BuildPipsArea(ResourceTracker res, Func<SolidColorBrush> accentB, Func<Color> accentC)
        {
            var root = new StackPanel();

            // Max spinner
            var maxRow = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 8) };
            maxRow.Children.Add(new TextBlock { Text = "Макс:", FontSize = 11,
                Foreground = (Brush)FindResource("TextMutedBrush"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0) });

            var maxTb = new TextBox { Text = res.Max.ToString(), Width = 40, FontSize = 12, FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center, Background = (Brush)FindResource("Surface2Brush"),
                BorderBrush = (Brush)FindResource("BorderMedBrush"), BorderThickness = new Thickness(1),
                Padding = new Thickness(2), VerticalAlignment = VerticalAlignment.Center };

            WrapPanel? pipsWrap = null;
            void RefreshPips() {
                pipsWrap!.Children.Clear();
                for (int i = 0; i < res.Max; i++) {
                    int idx = i;
                    var pip = new Border {
                        Width = 30, Height = 30, CornerRadius = new CornerRadius(15),
                        Margin = new Thickness(3),
                        Background = i < res.Current ? accentB() : (Brush)FindResource("Surface2Brush"),
                        BorderBrush = accentB(), BorderThickness = new Thickness(2),
                        Cursor = Cursors.Hand, ToolTip = $"{idx + 1} / {res.Max}"
                    };
                    pip.Effect = i < res.Current
                        ? new DropShadowEffect { BlurRadius = 8, ShadowDepth = 0, Color = accentC(), Opacity = 0.6 }
                        : null;
                    pip.MouseLeftButtonDown += (_, _) => {
                        res.Current = (idx < res.Current) ? idx : idx + 1;
                        res.Current = Math.Clamp(res.Current, 0, res.Max);
                        RefreshPips(); Mark();
                    };
                    pip.MouseRightButtonDown += (_, _) => {
                        res.Current = Math.Max(res.Current - 1, 0);
                        RefreshPips(); Mark();
                    };
                    pipsWrap!.Children.Add(pip);
                }
                // current / max label
                if (pipsWrap.Children.Count > 0 || res.Max == 0) {
                    var lbl = new TextBlock {
                        Text = $"  {res.Current} / {res.Max}",
                        FontSize = 12, FontWeight = FontWeights.Bold,
                        Foreground = accentB(),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    pipsWrap.Children.Add(lbl);
                }
            }

            maxTb.LostFocus += (_, _) =>
            {
                if (int.TryParse(maxTb.Text, out var v)) { res.Max = Math.Clamp(v, 1, 40); if (res.Current > res.Max) res.Current = res.Max; }
                maxTb.Text = res.Max.ToString(); RefreshPips(); Mark();
            };
            maxTb.KeyDown += (_, e) => { if (e.Key == Key.Enter) maxTb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); };

            var maxMinus = new Button { Content = "−", Width = 24, Height = 24, MinHeight = 0, Padding = new Thickness(0) };
            var maxPlus  = new Button { Content = "+", Width = 24, Height = 24, MinHeight = 0, Padding = new Thickness(0), Margin = new Thickness(4, 0, 0, 0) };
            maxMinus.Click += (_, _) => { res.Max = Math.Max(res.Max - 1, 1); if (res.Current > res.Max) res.Current = res.Max; maxTb.Text = res.Max.ToString(); RefreshPips(); Mark(); };
            maxPlus.Click  += (_, _) => { res.Max = Math.Min(res.Max + 1, 40); maxTb.Text = res.Max.ToString(); RefreshPips(); Mark(); };

            maxRow.Children.Add(maxMinus);
            maxRow.Children.Add(maxTb);
            maxRow.Children.Add(maxPlus);
            root.Children.Add(maxRow);

            pipsWrap = new WrapPanel { Orientation = Orientation.Horizontal };
            RefreshPips();
            root.Children.Add(pipsWrap);

            // Quick reset
            var resetRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            var fillAll  = new Button { Content = "Заполнить всё", Height = 30, Padding = new Thickness(10, 0, 10, 0), MinHeight = 0, Margin = new Thickness(0, 0, 6, 0) };
            var clearAll = new Button { Content = "Сбросить всё",  Height = 30, Padding = new Thickness(10, 0, 10, 0), MinHeight = 0 };
            fillAll.Click  += (_, _) => { res.Current = res.Max; RefreshPips(); Mark(); };
            clearAll.Click += (_, _) => { res.Current = 0; RefreshPips(); Mark(); };
            resetRow.Children.Add(fillAll);
            resetRow.Children.Add(clearAll);
            root.Children.Add(resetRow);

            return root;
        }

        // ── AMMO style ───────────────────────────────────────────────────────
        private UIElement BuildAmmoArea(ResourceTracker res, Func<SolidColorBrush> accentB, Func<Color> accentC)
        {
            var root = new StackPanel();

            // Max row
            var maxRow = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 8) };
            maxRow.Children.Add(new TextBlock { Text = "Макс:", FontSize = 11,
                Foreground = (Brush)FindResource("TextMutedBrush"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0) });
            var maxTb = new TextBox { Text = res.Max.ToString(), Width = 40, FontSize = 12, FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center, Background = (Brush)FindResource("Surface2Brush"),
                BorderBrush = (Brush)FindResource("BorderMedBrush"), BorderThickness = new Thickness(1),
                Padding = new Thickness(2), VerticalAlignment = VerticalAlignment.Center };

            WrapPanel? ammoWrap = null;
            void RefreshAmmo() {
                ammoWrap!.Children.Clear();
                for (int i = 0; i < res.Max; i++) {
                    int idx = i;
                    var cell = new Border {
                        Width = 22, Height = 22, CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(2),
                        Background = i < res.Current ? accentB() : (Brush)FindResource("Surface2Brush"),
                        BorderBrush = accentB(), BorderThickness = new Thickness(1.5),
                        Cursor = Cursors.Hand
                    };
                    if (i < res.Current)
                        cell.Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 0, Color = accentC(), Opacity = 0.5 };
                    cell.MouseLeftButtonDown += (_, _) => {
                        res.Current = (idx < res.Current) ? idx : idx + 1;
                        RefreshAmmo(); Mark();
                    };
                    ammoWrap!.Children.Add(cell);
                }
                // label
                ammoWrap.Children.Add(new TextBlock {
                    Text = $"  {res.Current} / {res.Max}", FontSize = 12, FontWeight = FontWeights.Bold,
                    Foreground = accentB(), VerticalAlignment = VerticalAlignment.Center
                });
            }

            maxTb.LostFocus += (_, _) => {
                if (int.TryParse(maxTb.Text, out var v)) { res.Max = Math.Clamp(v, 1, 60); if (res.Current > res.Max) res.Current = res.Max; }
                maxTb.Text = res.Max.ToString(); RefreshAmmo(); Mark();
            };
            var maxMinus = new Button { Content = "−", Width = 24, Height = 24, MinHeight = 0, Padding = new Thickness(0) };
            var maxPlus  = new Button { Content = "+", Width = 24, Height = 24, MinHeight = 0, Padding = new Thickness(0), Margin = new Thickness(4, 0, 0, 0) };
            maxMinus.Click += (_, _) => { res.Max = Math.Max(res.Max - 1, 1); if (res.Current > res.Max) res.Current = res.Max; maxTb.Text = res.Max.ToString(); RefreshAmmo(); Mark(); };
            maxPlus.Click  += (_, _) => { res.Max = Math.Min(res.Max + 1, 60); maxTb.Text = res.Max.ToString(); RefreshAmmo(); Mark(); };
            maxRow.Children.Add(maxMinus); maxRow.Children.Add(maxTb); maxRow.Children.Add(maxPlus);
            root.Children.Add(maxRow);

            ammoWrap = new WrapPanel();
            RefreshAmmo();
            root.Children.Add(ammoWrap);

            // Use/reload row
            var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var useTb = new TextBox { Text = "1", Width = 40, FontSize = 12, TextAlignment = TextAlignment.Center,
                Background = (Brush)FindResource("Surface2Brush"),
                BorderBrush = (Brush)FindResource("BorderMedBrush"), BorderThickness = new Thickness(1),
                Padding = new Thickness(2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            var useBtn    = new Button { Content = "⚡ Использовать", Height = 30, Padding = new Thickness(10, 0, 10, 0), MinHeight = 0, Margin = new Thickness(0, 0, 6, 0) };
            var reloadBtn = new Button { Content = "🔄 Перезарядить", Height = 30, Padding = new Thickness(10, 0, 10, 0), MinHeight = 0 };
            useBtn.Click    += (_, _) => { if (int.TryParse(useTb.Text, out var n)) { res.Current = Math.Max(res.Current - n, 0); RefreshAmmo(); Mark(); } };
            reloadBtn.Click += (_, _) => { res.Current = res.Max; RefreshAmmo(); Mark(); };
            actionRow.Children.Add(new TextBlock { Text = "Кол-во:", FontSize = 11,
                Foreground = (Brush)FindResource("TextMutedBrush"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0) });
            actionRow.Children.Add(useTb);
            actionRow.Children.Add(useBtn);
            actionRow.Children.Add(reloadBtn);
            root.Children.Add(actionRow);
            return root;
        }

        // ── BOXES (checkbox) style ────────────────────────────────────────────
        private UIElement BuildBoxesArea(ResourceTracker res, Func<SolidColorBrush> accentB, Func<Color> accentC)
        {
            var root = new StackPanel();

            var maxRow = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 8) };
            maxRow.Children.Add(new TextBlock { Text = "Пунктов:", FontSize = 11,
                Foreground = (Brush)FindResource("TextMutedBrush"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0) });
            var maxTb = new TextBox { Text = res.Max.ToString(), Width = 40, FontSize = 12, FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center, Background = (Brush)FindResource("Surface2Brush"),
                BorderBrush = (Brush)FindResource("BorderMedBrush"), BorderThickness = new Thickness(1),
                Padding = new Thickness(2), VerticalAlignment = VerticalAlignment.Center };

            var boxWrap = new WrapPanel();
            void RefreshBoxes() {
                // Sync checked state from res.Current
                var boxes = boxWrap.Children.OfType<Border>().ToList();
                // Ensure count matches
                while (boxWrap.Children.Count < res.Max) {
                    int idx = boxWrap.Children.Count;
                    var bx = new Border {
                        Width = 28, Height = 28, CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(3), Cursor = Cursors.Hand,
                        Background = (Brush)FindResource("Surface2Brush"),
                        BorderBrush = accentB(), BorderThickness = new Thickness(2)
                    };
                    var checkMark = new TextBlock { Text = "✓", FontSize = 14, FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = accentB(), Visibility = Visibility.Collapsed };
                    bx.Child = checkMark;
                    bx.MouseLeftButtonDown += (_, _) => {
                        int curIdx = boxWrap.Children.IndexOf(bx);
                        // Toggle: if checked, uncheck from here
                        res.Current = (curIdx < res.Current) ? curIdx : curIdx + 1;
                        res.Current = Math.Clamp(res.Current, 0, res.Max);
                        UpdateBoxVisuals();
                        Mark();
                    };
                    boxWrap.Children.Add(bx);
                }
                while (boxWrap.Children.Count > res.Max) boxWrap.Children.RemoveAt(boxWrap.Children.Count - 1);
                UpdateBoxVisuals();
            }
            void UpdateBoxVisuals() {
                for (int i = 0; i < boxWrap.Children.Count; i++) {
                    if (boxWrap.Children[i] is not Border bx) continue;
                    bool chk = i < res.Current;
                    bx.Background = chk ? new SolidColorBrush(Color.FromArgb(30, accentC().R, accentC().G, accentC().B))
                        : (Brush)FindResource("Surface2Brush");
                    bx.Effect = chk ? new DropShadowEffect { BlurRadius = 6, ShadowDepth = 0, Color = accentC(), Opacity = 0.5 } : null;
                    if (bx.Child is TextBlock cm) cm.Visibility = chk ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            RefreshBoxes();

            maxTb.LostFocus += (_, _) => {
                if (int.TryParse(maxTb.Text, out var v)) { res.Max = Math.Clamp(v, 1, 40); if (res.Current > res.Max) res.Current = res.Max; }
                maxTb.Text = res.Max.ToString(); RefreshBoxes(); Mark();
            };
            var mm = new Button { Content = "−", Width = 24, Height = 24, MinHeight = 0, Padding = new Thickness(0) };
            var mp = new Button { Content = "+", Width = 24, Height = 24, MinHeight = 0, Padding = new Thickness(0), Margin = new Thickness(4, 0, 0, 0) };
            mm.Click += (_, _) => { res.Max = Math.Max(res.Max - 1, 1); if (res.Current > res.Max) res.Current = res.Max; maxTb.Text = res.Max.ToString(); RefreshBoxes(); Mark(); };
            mp.Click += (_, _) => { res.Max = Math.Min(res.Max + 1, 40); maxTb.Text = res.Max.ToString(); RefreshBoxes(); Mark(); };
            maxRow.Children.Add(mm); maxRow.Children.Add(maxTb); maxRow.Children.Add(mp);
            root.Children.Add(maxRow);
            root.Children.Add(boxWrap);

            var resetRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            var clearAll = new Button { Content = "Снять все", Height = 30, Padding = new Thickness(10, 0, 10, 0), MinHeight = 0 };
            clearAll.Click += (_, _) => { res.Current = 0; UpdateBoxVisuals(); Mark(); };
            resetRow.Children.Add(clearAll);
            root.Children.Add(resetRow);
            return root;
        }

        private static void Mark()
            => (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
    }
}
