using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CharacterApp.Pages
{
    // ── Model ─────────────────────────────────────────────────────────────────
    public class DiceRollEntry
    {
        public string Formula          { get; set; } = "";
        public string Detail           { get; set; } = "";
        public string Total            { get; set; } = "";
        public string Time             { get; set; } = "";
        public string Mode             { get; set; } = "";          // ADV / DIS / ""
        public string ModeBadgeVisible { get; set; } = "Collapsed"; // Visibility string for binding
        public Brush  ModeColor        { get; set; } = Brushes.Transparent;
    }

    // ── Page ──────────────────────────────────────────────────────────────────
    public partial class DicePage : Page
    {
        private static readonly Random _rng = new();
        private readonly ObservableCollection<DiceRollEntry> _history = new();

        private int    _diceCount = 1;
        private int    _modifier  = 0;
        private string _mode      = "normal"; // "adv" | "normal" | "dis"
        private bool   _rolling   = false;

        public DicePage()
        {
            InitializeComponent();
            HistoryList.ItemsSource = _history;
            UpdateModeUI();
        }

        // ══ MODE TOGGLE ══════════════════════════════════════════════════════
        private void ModeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            _mode = btn.Tag?.ToString() ?? "normal";
            UpdateModeUI();
        }

        private void UpdateModeUI()
        {
            // Highlight active mode button
            double[] opacities = _mode switch
            {
                "adv"    => [1.0, 0.45, 0.45],
                "dis"    => [0.45, 0.45, 1.0],
                _        => [0.45, 1.0, 0.45],
            };
            BtnAdv.Opacity    = opacities[0];
            BtnNormal.Opacity = opacities[1];
            BtnDis.Opacity    = opacities[2];

            // Mode bar
            (string hint, Color barCol, double barW) = _mode switch
            {
                "adv"  => ("Преимущество: 2 кубика одновременно, засчитывается старший", Color.FromRgb(76, 175, 114), 200d),
                "dis"  => ("Помеха: 2 кубика одновременно, засчитывается младший",       Color.FromRgb(208, 64, 96),  200d),
                _      => ("Обычный бросок — один результат",                          Color.FromRgb(181, 101, 193), 100d),
            };
            TbModeHint.Text = hint;
            ModeBarColor.Color = barCol;
            AnimateDouble(ModeBar, WidthProperty, barW, 250);
        }

        // ══ DICE BUTTONS ═════════════════════════════════════════════════════
        private async void DiceBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_rolling || sender is not Button btn) return;
            if (!int.TryParse(btn.Content?.ToString()?.TrimStart('d'), out int sides)) return;

            _rolling = true;
            await RollAsync(sides);
            _rolling = false;
        }

        private void Formula_KeyDown(object sender, KeyEventArgs e)
        { if (e.Key == Key.Enter) RollFormula_Click(sender, e); }

        private async void RollFormula_Click(object sender, RoutedEventArgs e)
        {
            if (_rolling) return;
            var formula = TbFormula.Text?.Trim() ?? "";
            var match   = Regex.Match(formula, @"^(\d*)d(\d+)([+-]\d+)?$", RegexOptions.IgnoreCase);
            if (!match.Success) { ShowError(formula); return; }

            int count = match.Groups[1].Value is "" or "0" ? 1 : int.Parse(match.Groups[1].Value);
            int sides = int.Parse(match.Groups[2].Value);
            int fmod  = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            count = Math.Clamp(count, 1, 30);

            _rolling = true;
            await RollAsync(sides, formulaCount: count, formulaMod: fmod, formulaStr: formula);
            _rolling = false;
        }

        // ══ CORE ROLL ════════════════════════════════════════════════════════
        private async Task RollAsync(int sides,
            int? formulaCount = null, int? formulaMod = null, string? formulaStr = null)
        {
            int count = formulaCount ?? _diceCount;
            int mod   = formulaMod   ?? _modifier;

            // ── Shake animation ──────────────────────────────────────────────
            ResultPanel.Visibility = Visibility.Visible;
            AdvDisRow.Visibility   = Visibility.Collapsed;
            ModeTagBorder.Visibility = Visibility.Collapsed;

            PlayShakeAnim();

            // Rapid number cycling (gives "rolling" feel)
            for (int i = 0; i < 18; i++)
            {
                TbRollResult.Text = _rng.Next(1, sides + 1).ToString();
                await Task.Delay(i < 8 ? 40 : i < 14 ? 65 : 90);
            }

            // ── Actual roll ───────────────────────────────────────────────────
            int finalTotal;
            string formula, detail, modeLabel = "";

            // Преимущество/помеха теперь работают и для бросков по формуле.
            // Раньше здесь стояло `formulaStr == null`, из-за чего формула
            // всегда кидалась обычным броском, даже с включённым режимом.
            bool isAdvDis = _mode != "normal";

            // «Натуралка» — значение одного зачтённого кубика без модификатора.
            // Нужна для крита/провала: раньше считали finalTotal - mod, из-за чего
            // 2d20 с суммой 20 подсвечивалось как крит, а натуральная 20
            // в мультиброске не замечалась вовсе. Для нескольких кубиков
            // понятия крита нет — оставляем null.
            int? natural = null;

            string modStr = mod > 0 ? $"+{mod}" : mod < 0 ? $"{mod}" : "";
            string countStr = count > 1 ? $"{count}" : "";
            string poolText = $"{countStr}d{sides}{modStr}";

            if (isAdvDis)
            {
                bool adv = _mode == "adv";

                // Кидаем весь пул дважды и берём лучший/худший ИТОГ.
                // Игрок видит оба броска целиком — как в Roll20 и Owlbear.
                var poolA = RollPool(count, sides);
                var poolB = RollPool(count, sides);
                int sumA  = poolA.Sum() + mod;
                int sumB  = poolB.Sum() + mod;

                bool firstWins = adv ? sumA >= sumB : sumA <= sumB;
                finalTotal = firstWins ? sumA : sumB;

                // Крит имеет смысл только когда в пуле один кубик
                if (count == 1) natural = (firstWins ? poolA : poolB)[0];

                formula   = $"{poolText} ({(adv ? "ADV" : "DIS")})";
                modeLabel = adv ? "ПРЕИМУЩЕСТВО" : "ПОМЕХА";
                detail    = $"Выбрано {finalTotal} из {sumA} и {sumB}";

                ShowAdvDisUI(sumA, sumB, firstWins,
                             BreakdownText(poolA, mod, count),
                             BreakdownText(poolB, mod, count));

                AddHistory(formula,
                           $"🎲 {sumA} {BreakdownText(poolA, mod, count)}  vs  " +
                           $"🎲 {sumB} {BreakdownText(poolB, mod, count)}",
                           finalTotal, adv ? "ADV" : "DIS");
            }
            else
            {
                var rolls  = RollPool(count, sides);
                finalTotal = rolls.Sum() + mod;
                if (count == 1) natural = rolls[0];
                formula = formulaStr ?? poolText;

                detail = count > 1
                    ? $"[{string.Join(" + ", rolls)}]{modStr}"
                    : mod != 0 ? $"{rolls[0]} {(mod > 0 ? "+" : "−")} {Math.Abs(mod)}"
                               : "";

                AddHistory(formula, string.IsNullOrEmpty(detail) ? "одиночный бросок" : detail, finalTotal, "");
            }

            // ── Reveal animation ──────────────────────────────────────────────
            TbRollResult.Text   = finalTotal.ToString();
            TbRollDetail.Text   = string.IsNullOrEmpty(detail) ? formula : $"{formula}  →  {detail}";

            // Крит/провал — только для одиночного d20 по натуральному значению
            bool isCrit = sides == 20 && natural == 20;
            bool isFail = sides == 20 && natural == 1;
            SetResultColor(isCrit ? "crit" : isFail ? "fail" : (_mode == "adv" ? "adv" : _mode == "dis" ? "dis" : "normal"));

            if (!string.IsNullOrEmpty(modeLabel))
            {
                ModeTagBorder.Visibility = Visibility.Visible;
                TbModeTag.Text = modeLabel;
                ModeTagBorder.Background = _mode == "adv"
                    ? new SolidColorBrush(Color.FromArgb(60, 76, 175, 114))
                    : new SolidColorBrush(Color.FromArgb(60, 208, 64, 96));
                TbModeTag.Foreground = _mode == "adv"
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 114))
                    : new SolidColorBrush(Color.FromRgb(208, 64, 96));
            }

            PlayRevealAnim(isCrit || isFail);
        }

        // ══ ANIMATIONS ═══════════════════════════════════════════════════════
        private void PlayShakeAnim()
        {
            TbRollResult.Opacity = 0.4;
            var sb = new Storyboard();

            // Horizontal shake on result text
            for (int i = 0; i < 6; i++)
            {
                double offset = (i % 2 == 0 ? 1 : -1) * (8 - i);
                var kf = new EasingDoubleKeyFrame(offset,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i * 80)),
                    new CubicEase { EasingMode = EasingMode.EaseInOut });
                var anim = new DoubleAnimationUsingKeyFrames();
                anim.KeyFrames.Add(kf);
                Storyboard.SetTargetName(anim, nameof(ResultTranslate));
                Storyboard.SetTargetProperty(anim, new PropertyPath(TranslateTransform.XProperty));
                sb.Children.Add(anim);
            }
            sb.Begin(this);

            // Scale pulse
            AnimateDoubleOnTarget(ResultScale, ScaleTransform.ScaleXProperty, 0.92, 1.0, 200);
            AnimateDoubleOnTarget(ResultScale, ScaleTransform.ScaleYProperty, 0.92, 1.0, 200);
        }

        private void PlayRevealAnim(bool dramatic)
        {
            // Reset translate
            AnimateDoubleOnTarget(ResultTranslate, TranslateTransform.XProperty, 0, 0, 200);

            // Pop scale
            var sx = new DoubleAnimationUsingKeyFrames();
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(0.4,  KeyTime.FromPercent(0)));
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(dramatic ? 1.4 : 1.2,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200)),
                new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = dramatic ? 0.6 : 0.3 }));
            sx.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)),
                new CubicEase { EasingMode = EasingMode.EaseOut }));

            ResultScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            ResultScale.BeginAnimation(ScaleTransform.ScaleYProperty, sx);

            // Fade in
            TbRollResult.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));

            // Glow pulse
            if (ResultPanel.Effect is System.Windows.Media.Effects.DropShadowEffect dse)
            {
                var pulse = new DoubleAnimation(dramatic ? 0.9 : 0.6, 0.3,
                    TimeSpan.FromMilliseconds(800)) { AutoReverse = false };
                dse.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, pulse);
            }
        }

        private void SetResultColor(string kind)
        {
            (Color c1, Color c2) = kind switch
            {
                "crit"   => (Color.FromRgb(255, 220, 50),  Color.FromRgb(240, 160, 20)),   // gold
                "fail"   => (Color.FromRgb(240, 80,  80),  Color.FromRgb(180, 40,  60)),   // red
                "adv"    => (Color.FromRgb(76, 220, 120),  Color.FromRgb(50, 160, 90)),    // green
                "dis"    => (Color.FromRgb(240, 100, 130), Color.FromRgb(200, 60,  90)),   // pink-red
                _        => (Color.FromRgb(224, 144, 240), Color.FromRgb(176, 96, 208)),   // purple
            };
            ResultGradStop1.Color = c1;
            ResultGradStop2.Color = c2;

            // Glow color
            if (ResultPanel.Effect is System.Windows.Media.Effects.DropShadowEffect dse)
                dse.Color = c1;
        }

        /// <summary>Список бросков пула: count кубиков по sides граней.</summary>
        private static List<int> RollPool(int count, int sides)
            => Enumerable.Range(0, count).Select(_ => _rng.Next(1, sides + 1)).ToList();

        /// <summary>
        /// Расшифровка броска пула: "[3 + 5] +2". Для одного кубика без
        /// модификатора возвращает пустую строку — там расшифровывать нечего.
        /// </summary>
        private static string BreakdownText(List<int> rolls, int mod, int count)
        {
            if (count == 1 && mod == 0) return "";
            string modStr = mod > 0 ? $" +{mod}" : mod < 0 ? $" {mod}" : "";
            return count > 1
                ? $"[{string.Join(" + ", rolls)}]{modStr}"
                : $"[{rolls[0]}]{modStr}";
        }

        /// <summary>
        /// Показывает оба броска. Раньше принимала два отдельных кубика и
        /// сравнивала их по значению — при равных числах «берётся» подсвечивалось
        /// у обоих. Теперь победитель передаётся явно, а под суммой выводится
        /// разбивка по кубикам, чтобы бросок по формуле было видно целиком.
        /// </summary>
        private void ShowAdvDisUI(int d1, int d2, bool firstWins,
                                  string breakdown1 = "", string breakdown2 = "")
        {
            AdvDisRow.Visibility = Visibility.Visible;

            bool advMode = _mode == "adv";
            Color winCol  = advMode ? Color.FromRgb(76, 175, 114)  : Color.FromRgb(208, 64, 96);
            Color loseCol = advMode ? Color.FromRgb(208, 64, 96)   : Color.FromRgb(76, 175, 114);

            TbDie1.Text       = d1.ToString();
            TbDie2.Text       = d2.ToString();
            TbDie1Label.Text  = firstWins ? "БЕРЁТСЯ ✓" : "ОТБРОС ✗";
            TbDie2Label.Text  = firstWins ? "ОТБРОС ✗"  : "БЕРЁТСЯ ✓";

            TbDie1Breakdown.Text       = breakdown1;
            TbDie2Breakdown.Text       = breakdown2;
            TbDie1Breakdown.Visibility = string.IsNullOrEmpty(breakdown1)
                ? Visibility.Collapsed : Visibility.Visible;
            TbDie2Breakdown.Visibility = string.IsNullOrEmpty(breakdown2)
                ? Visibility.Collapsed : Visibility.Visible;

            bool d1wins = firstWins;
            TbDie1.Foreground = new SolidColorBrush(d1wins ? winCol : loseCol);
            TbDie2.Foreground = new SolidColorBrush(d1wins ? loseCol : winCol);

            Die1Panel.Background = new SolidColorBrush(Color.FromArgb(30, (d1wins ? winCol : loseCol).R,
                (d1wins ? winCol : loseCol).G, (d1wins ? winCol : loseCol).B));
            Die2Panel.Background = new SolidColorBrush(Color.FromArgb(30, (d1wins ? loseCol : winCol).R,
                (d1wins ? loseCol : winCol).G, (d1wins ? loseCol : winCol).B));

            Die1Panel.BorderBrush = new SolidColorBrush(Color.FromArgb(80,
                (d1wins ? winCol : loseCol).R, (d1wins ? winCol : loseCol).G, (d1wins ? winCol : loseCol).B));
            Die1Panel.BorderThickness = new Thickness(1.5);
            Die2Panel.BorderBrush = new SolidColorBrush(Color.FromArgb(80,
                (d1wins ? loseCol : winCol).R, (d1wins ? loseCol : winCol).G, (d1wins ? loseCol : winCol).B));
            Die2Panel.BorderThickness = new Thickness(1.5);
        }

        // ══ COUNTER BUTTONS ═══════════════════════════════════════════════════
        private void CounterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            _diceCount = btn.Tag?.ToString() == "+"
                ? Math.Min(_diceCount + 1, 20)
                : Math.Max(_diceCount - 1, 1);
            TbDiceCount.Text = _diceCount.ToString();
        }

        private void ModBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            _modifier = btn.Tag?.ToString() == "+"
                ? Math.Min(_modifier + 1, 20)
                : Math.Max(_modifier - 1, -20);
            TbModifier.Text = _modifier.ToString("+#;-#;0");
            ModifierColor.Color = _modifier > 0
                ? Color.FromRgb(100, 220, 100)
                : _modifier < 0
                    ? Color.FromRgb(240, 90, 90)
                    : Color.FromRgb(144, 144, 176);
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            _history.Clear();
            TbEmptyHistory.Visibility = Visibility.Visible;
        }

        // ══ HELPERS ══════════════════════════════════════════════════════════
        private void ShowError(string formula)
        {
            ResultPanel.Visibility = Visibility.Visible;
            AdvDisRow.Visibility   = Visibility.Collapsed;
            TbRollDetail.Text  = $"Неверная формула: «{formula}»";
            TbRollResult.Text  = "?";
            SetResultColor("fail");
        }

        private void AddHistory(string formula, string detail, int total, string mode)
        {
            TbEmptyHistory.Visibility = Visibility.Collapsed;
            Brush modeBrush = mode switch
            {
                "ADV" => new SolidColorBrush(Color.FromRgb(50, 140, 90)),
                "DIS" => new SolidColorBrush(Color.FromRgb(180, 50, 80)),
                _     => Brushes.Transparent,
            };
            _history.Insert(0, new DiceRollEntry
            {
                Formula          = formula,
                Detail           = detail,
                Total            = total.ToString(),
                Time             = DateTime.Now.ToString("HH:mm:ss"),
                Mode             = mode,
                ModeBadgeVisible = string.IsNullOrEmpty(mode) ? "Collapsed" : "Visible",
                ModeColor        = modeBrush,
            });
            while (_history.Count > 40) _history.RemoveAt(_history.Count - 1);
        }

        // ── Animation utilities ────────────────────────────────────────────────
        private static void AnimateDouble(IAnimatable target,
            DependencyProperty dp, double to, int ms)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            target.BeginAnimation(dp, anim);
        }

        private static void AnimateDoubleOnTarget(Animatable target,
            DependencyProperty dp, double from, double to, int ms)
        {
            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            target.BeginAnimation(dp, anim);
        }
    }
}
