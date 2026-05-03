// Pages/DicePage.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace CharacterApp.Pages
{
    public class DiceRollEntry
    {
        public string Formula { get; set; } = "";
        public string Detail  { get; set; } = "";
        public string Total   { get; set; } = "";
        public string Time    { get; set; } = "";
    }

    public partial class DicePage : Page
    {
        private static readonly Random _rng = new();
        private readonly ObservableCollection<DiceRollEntry> _history = new();

        private int _diceCount = 1;
        private int _modifier  = 0;

        public DicePage()
        {
            InitializeComponent();
            HistoryList.ItemsSource = _history;
        }

        // ── Dice button click ────────────────────────────────────────────────
        private void DiceBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int sides)) return;

            var rolls  = Enumerable.Range(0, _diceCount)
                                   .Select(_ => _rng.Next(1, sides + 1))
                                   .ToList();
            int sum    = rolls.Sum() + _modifier;
            string countStr = _diceCount > 1 ? $"{_diceCount}" : "";
            string modStr   = _modifier > 0 ? $"+{_modifier}" : _modifier < 0 ? $"{_modifier}" : "";
            string formula  = $"{countStr}d{sides}{modStr}";
            string detail   = _diceCount > 1
                ? $"[{string.Join(" + ", rolls)}]{modStr}"
                : modStr != "" ? $"{rolls[0]} {(_modifier > 0 ? "+" : "-")} {Math.Abs(_modifier)}" : "";

            ShowResult(formula, detail, sum);
            AddHistory(formula, detail, sum);
        }

        // ── Formula roll ─────────────────────────────────────────────────────
        private void RollFormula_Click(object sender, RoutedEventArgs e)
        {
            var formula = TbFormula.Text?.Trim() ?? "";
            var match   = Regex.Match(formula, @"^(\d*)d(\d+)([+-]\d+)?$",
                                       RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                ShowResult("Ошибка формулы", $"«{formula}» не распознана", 0);
                return;
            }

            int count  = match.Groups[1].Value is "" or "0" ? 1 : int.Parse(match.Groups[1].Value);
            int sides  = int.Parse(match.Groups[2].Value);
            int fmod   = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            count      = Math.Clamp(count, 1, 30);

            var rolls  = Enumerable.Range(0, count)
                                   .Select(_ => _rng.Next(1, sides + 1))
                                   .ToList();
            int total  = rolls.Sum() + fmod;
            string detail = count > 1 ? $"[{string.Join(" + ", rolls)}]" : "";
            if (fmod != 0) detail += $" {(fmod > 0 ? "+" : "-")} {Math.Abs(fmod)}";

            ShowResult(formula, detail, total);
            AddHistory(formula, detail, total);
        }

        // ── Counter buttons ──────────────────────────────────────────────────
        private void CounterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            _diceCount = btn.Tag?.ToString() == "+" ? Math.Min(_diceCount + 1, 20)
                                                     : Math.Max(_diceCount - 1, 1);
            TbDiceCount.Text = _diceCount.ToString();
        }

        private void ModBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            _modifier = btn.Tag?.ToString() == "+" ? Math.Min(_modifier + 1, 20)
                                                    : Math.Max(_modifier - 1, -20);
            TbModifier.Text = _modifier.ToString("+#;-#;0");

            // Color: green positive, red negative, muted zero
            ModifierColor.Color = _modifier > 0
                ? System.Windows.Media.Color.FromRgb(100, 220, 100)
                : _modifier < 0
                    ? System.Windows.Media.Color.FromRgb(240, 90, 90)
                    : System.Windows.Media.Color.FromRgb(120, 128, 176);
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            _history.Clear();
            TbEmptyHistory.Visibility = Visibility.Visible;
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private void ShowResult(string formula, string detail, int total)
        {
            ResultPanel.Visibility = Visibility.Visible;
            TbRollDetail.Text      = string.IsNullOrEmpty(detail) ? formula : $"{formula}  →  {detail}";
            TbRollResult.Text      = total.ToString();

            // Bounce animation on result number
            var scaleUp = new DoubleAnimationUsingKeyFrames();
            scaleUp.KeyFrames.Add(new EasingDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            scaleUp.KeyFrames.Add(new EasingDoubleKeyFrame(1.25,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160)),
                new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }));
            scaleUp.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)),
                new CubicEase { EasingMode = EasingMode.EaseOut }));

            ResultScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleUp);
            ResultScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleUp);

            // Glow pulse on result panel
            var glowIn = new DoubleAnimation(0.6, 0.2, TimeSpan.FromMilliseconds(600));
            glowIn.AutoReverse = true;
            if (ResultPanel.Effect is System.Windows.Media.Effects.DropShadowEffect dse)
                dse.BeginAnimation(
                    System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, glowIn);
        }

        private void AddHistory(string formula, string detail, int total)
        {
            TbEmptyHistory.Visibility = Visibility.Collapsed;
            _history.Insert(0, new DiceRollEntry
            {
                Formula = formula,
                Detail  = string.IsNullOrEmpty(detail) ? "одиночный бросок" : detail,
                Total   = total.ToString(),
                Time    = DateTime.Now.ToString("HH:mm:ss")
            });
            // Keep last 30
            while (_history.Count > 30) _history.RemoveAt(_history.Count - 1);
        }
    }
}
