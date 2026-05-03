// Controls/DiceRollerControl.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace CharacterApp.Controls
{
    public partial class DiceRollerControl : UserControl
    {
        private static readonly Random _rng = new();
        private readonly ObservableCollection<string> _history = new();

        public DiceRollerControl()
        {
            InitializeComponent();
            HistoryList.ItemsSource = _history;
        }

        // Клик по кубику (d4, d6 и т.д.)
        private void DiceBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int sides) || sides <= 0) return;

            int.TryParse(TbModifier.Text?.Trim(), out int mod);
            int roll   = _rng.Next(1, sides + 1);
            int total  = roll + mod;

            string modStr = mod != 0 ? (mod > 0 ? $" + {mod}" : $" - {Math.Abs(mod)}") : "";
            ShowResult($"d{sides}{modStr}", $"{roll}", total);
        }

        // Бросок по формуле (NdX+M)
        private void RollFormula_Click(object sender, RoutedEventArgs e)
        {
            var formula = TbFormula.Text?.Trim() ?? "";
            int.TryParse(TbModifier.Text?.Trim(), out int extraMod);

            // Парсим формулу: optional NdX (+/- M)
            var match = Regex.Match(formula, @"^(\d*)d(\d+)([+-]\d+)?$",
                                    RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                ShowResult("Ошибка", "?", 0);
                return;
            }

            int count  = match.Groups[1].Value is "" or "0" ? 1
                         : int.Parse(match.Groups[1].Value);
            int sides  = int.Parse(match.Groups[2].Value);
            int fMod   = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            int total  = fMod + extraMod;

            count = Math.Clamp(count, 1, 20);

            var rolls = Enumerable.Range(0, count)
                                  .Select(_ => _rng.Next(1, sides + 1))
                                  .ToList();
            total += rolls.Sum();

            string detail = rolls.Count == 1
                ? $"{formula}"
                : $"{formula}  [{string.Join(", ", rolls)}]";

            ShowResult(detail, "", total);
        }

        private void ShowResult(string detail, string rollStr, int total)
        {
            TbRollDetail.Text  = detail;
            TbRollResult.Text  = total.ToString();
            ResultBorder.Visibility = Visibility.Visible;

            // Animate result pop
            TbRollResult.RenderTransformOrigin = new Point(0.5, 0.5);
            TbRollResult.RenderTransform = new System.Windows.Media.ScaleTransform(1, 1);
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80)), new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
            TbRollResult.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, anim);
            TbRollResult.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, anim);

            // Add to history
            string historyEntry = $"{detail} = {total}";
            _history.Insert(0, historyEntry);
            while (_history.Count > 5) _history.RemoveAt(_history.Count - 1);
        }
    }
}
