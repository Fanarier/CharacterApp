using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using CharacterApp.Controls;
using CharacterApp.Models;

namespace CharacterApp.Pages
{
    public partial class StatsPage : Page
    {
        // Храним модификаторы атрибутов — нужны при пересчёте навыков
        private bool _initialized = false;
        private bool _subscribed   = false;  // защита от повторных подписок
        private readonly Dictionary<string, int> _mods = new()
        {
            ["STR"] = 0, ["DEX"] = 0, ["CON"] = 0,
            ["INT"] = 0, ["WIS"] = 0, ["CHA"] = 0
        };

        // Все IconRatingControl (заполняется в Loaded после InitializeComponent)
        private List<IconRatingControl> _allDots = new();

        public StatsPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_subscribed)
            {
                _subscribed = true;
                // Пересчёт при каждом показе страницы (BM/Уровень могли измениться)
                if (NavigationService != null)
                    NavigationService.Navigated += (_, _) => RecalcAll();
            }

            // Список всех рейтинг-контролей страницы
            _allDots = new List<IconRatingControl>
            {
                // Save-броски атрибутов
                StrSaveDot, ConSaveDot, IntSaveDot, ChaSaveDot,
                DexSaveDot, WisSaveDot,
                // Навыки
                AthProf,
                AcrProf, SlhProf, StlProf,
                EndProf,
                AnaProf, HisProf, MagProf, NatProf, RelProf, TecProf,
                AttProf, SurProf, MedProf, InsProf, AniProf, MenProf,
                PrfProf, ItmProf, DecProf, ChrProf, PrsProf,
            };

            // Подписываемся на изменение Value у каждого контроля
            if (!_subscribed)
            {
                var dpd = DependencyPropertyDescriptor.FromProperty(
                    IconRatingControl.ValueProperty, typeof(IconRatingControl));
                foreach (var dot in _allDots)
                    dpd.AddValueChanged(dot, (_, _) => { RecalcAll(); (Application.Current.MainWindow as MainWindow)?.MarkUnsaved(); });
            }

            _initialized = true;
            RecalcAll();
        }

        // ── Делегирование сохранения ──────────────────────────────────────────
        public static void QuickSave() => (Application.Current.MainWindow as MainWindow)?.SaveAll();
        public static void SaveAs()    => (Application.Current.MainWindow as MainWindow)?.SaveAllAs();
        public static void LoadJSON()  => (Application.Current.MainWindow as MainWindow)?.LoadAll();

        // ── Получение БМ и Уровня из главного окна ───────────────────────────
        private static int GetBM()
        {
            var mw = Application.Current.MainWindow as MainWindow;
            return mw?.GetCurrentBM() ?? 0;
        }

        private static int GetLevel()
        {
            var mw = Application.Current.MainWindow as MainWindow;
            return mw?.GetCurrentLevel() ?? 0;
        }

        // ── Парсинг целого числа ("+4" → 4, "-1" → -1) ───────────────────────
        private static int ParseInt(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Trim();
            if (s.StartsWith("+")) s = s[1..];
            return int.TryParse(s, out var v) ? v : 0;
        }

        private static string FmtBonus(int v) => v >= 0 ? $"+{v}" : v.ToString();

        // Цвет числа: положительное — зелёное, 0 — дефолт, отрицательное — красное
        private static void SetBonusColor(TextBlock tb, int value)
        {
            if (value > 0)       tb.Foreground = new SolidColorBrush(Color.FromRgb(34, 180, 76));
            else if (value < 0)  tb.Foreground = new SolidColorBrush(Colors.Crimson);
            else                 tb.ClearValue(TextBlock.ForegroundProperty);
        }

        // ── Пересчёт ─────────────────────────────────────────────────────────

        private void RecalcAll()
        {
            if (!_initialized || StrBase == null) return;
            int bm    = GetBM();
            int level = GetLevel();

            RecalcAttr("STR", StrBase, StrMod, StrSaveDot, StrSaveTotal);
            RecalcAttr("DEX", DexBase, DexMod, DexSaveDot, DexSaveTotal);
            RecalcAttr("CON", ConBase, ConMod, ConSaveDot, ConSaveTotal);
            RecalcAttr("INT", IntBase, IntMod, IntSaveDot, IntSaveTotal);
            RecalcAttr("WIS", WisBase, WisMod, WisSaveDot, WisSaveTotal);
            RecalcAttr("CHA", ChaBase, ChaMod, ChaSaveDot, ChaSaveTotal);

            RecalcSkill(AthTrain, AthProf, AthTotal, "STR", bm, level);

            RecalcSkill(AcrTrain, AcrProf, AcrTotal, "DEX", bm, level);
            RecalcSkill(SlhTrain, SlhProf, SlhTotal, "DEX", bm, level);
            RecalcSkill(StlTrain, StlProf, StlTotal, "DEX", bm, level);

            RecalcSkill(EndTrain, EndProf, EndTotal, "CON", bm, level);

            RecalcSkill(AnaTrain, AnaProf, AnaTotal, "INT", bm, level);
            RecalcSkill(HisTrain, HisProf, HisTotal, "INT", bm, level);
            RecalcSkill(MagTrain, MagProf, MagTotal, "INT", bm, level);
            RecalcSkill(NatTrain, NatProf, NatTotal, "INT", bm, level);
            RecalcSkill(RelTrain, RelProf, RelTotal, "INT", bm, level);
            RecalcSkill(TecTrain, TecProf, TecTotal, "INT", bm, level);

            RecalcSkill(AttTrain, AttProf, AttTotal, "WIS", bm, level);
            RecalcSkill(SurTrain, SurProf, SurTotal, "WIS", bm, level);
            RecalcSkill(MedTrain, MedProf, MedTotal, "WIS", bm, level);
            RecalcSkill(InsTrain, InsProf, InsTotal, "WIS", bm, level);
            RecalcSkill(AniTrain, AniProf, AniTotal, "WIS", bm, level);
            RecalcSkill(MenTrain, MenProf, MenTotal, "WIS", bm, level);

            RecalcSkill(PrfTrain, PrfProf, PrfTotal, "CHA", bm, level);
            RecalcSkill(ItmTrain, ItmProf, ItmTotal, "CHA", bm, level);
            RecalcSkill(DecTrain, DecProf, DecTotal, "CHA", bm, level);
            RecalcSkill(ChrTrain, ChrProf, ChrTotal, "CHA", bm, level);
            RecalcSkill(PrsTrain, PrsProf, PrsTotal, "CHA", bm, level);
        }

        private void RecalcAttr(string key,
                                 TextBox baseBox, TextBlock modText,
                                 IconRatingControl saveDot, TextBlock saveTotal)
        {
            int baseVal = ParseInt(baseBox.Text);
            int mod     = (int)Math.Floor((baseVal - 10) / 2.0);
            int bm      = GetBM();
            int save    = mod + (saveDot.Value >= 1 ? bm : 0);

            _mods[key]    = mod;
            modText.Text  = FmtBonus(mod);
            SetBonusColor(modText, mod);

            saveTotal.Text = FmtBonus(save);
            SetBonusColor(saveTotal, save);
        }

        private void RecalcSkill(TextBox trainBox, IconRatingControl profDot,
                                  TextBlock totalText, string attrKey, int bm, int level)
        {
            int training = ParseInt(trainBox.Text);
            int mod      = _mods.TryGetValue(attrKey, out var m) ? m : 0;
            int prof     = profDot.Value;

            int total = prof switch
            {
                1 => training + mod + bm,
                2 => training + mod + bm * 2,
                3 => (int)Math.Floor(training + mod + bm * 2.0 + level / 4.0),
                _ => training + mod   // 0 dots
            };

            totalText.Text = FmtBonus(total);
            SetBonusColor(totalText, total);
        }

        // ── TextChanged хендлеры ─────────────────────────────────────────────

        private void AttrBase_Changed(object sender, TextChangedEventArgs e) { RecalcAll(); (Application.Current.MainWindow as MainWindow)?.MarkUnsaved(); }
        private void Skill_Changed   (object sender, TextChangedEventArgs e) { RecalcAll(); (Application.Current.MainWindow as MainWindow)?.MarkUnsaved(); }

        // ── Сохранение / загрузка ────────────────────────────────────────────

        public void FillCharacter(Character c)
        {
            var s = c.Stats ??= new StatsData();

            s.StrBase = ParseInt(StrBase.Text); s.StrSaveProf = StrSaveDot.Value >= 1;
            s.DexBase = ParseInt(DexBase.Text); s.DexSaveProf = DexSaveDot.Value >= 1;
            s.ConBase = ParseInt(ConBase.Text); s.ConSaveProf = ConSaveDot.Value >= 1;
            s.IntBase = ParseInt(IntBase.Text); s.IntSaveProf = IntSaveDot.Value >= 1;
            s.WisBase = ParseInt(WisBase.Text); s.WisSaveProf = WisSaveDot.Value >= 1;
            s.ChaBase = ParseInt(ChaBase.Text); s.ChaSaveProf = ChaSaveDot.Value >= 1;

            s.AthT = ParseInt(AthTrain.Text); s.AthP = AthProf.Value;
            s.AcrT = ParseInt(AcrTrain.Text); s.AcrP = AcrProf.Value;
            s.SlhT = ParseInt(SlhTrain.Text); s.SlhP = SlhProf.Value;
            s.StlT = ParseInt(StlTrain.Text); s.StlP = StlProf.Value;
            s.EndT = ParseInt(EndTrain.Text); s.EndP = EndProf.Value;
            s.AnaT = ParseInt(AnaTrain.Text); s.AnaP = AnaProf.Value;
            s.HisT = ParseInt(HisTrain.Text); s.HisP = HisProf.Value;
            s.MagT = ParseInt(MagTrain.Text); s.MagP = MagProf.Value;
            s.NatT = ParseInt(NatTrain.Text); s.NatP = NatProf.Value;
            s.RelT = ParseInt(RelTrain.Text); s.RelP = RelProf.Value;
            s.TecT = ParseInt(TecTrain.Text); s.TecP = TecProf.Value;
            s.AttT = ParseInt(AttTrain.Text); s.AttP = AttProf.Value;
            s.SurT = ParseInt(SurTrain.Text); s.SurP = SurProf.Value;
            s.MedT = ParseInt(MedTrain.Text); s.MedP = MedProf.Value;
            s.InsT = ParseInt(InsTrain.Text); s.InsP = InsProf.Value;
            s.AniT = ParseInt(AniTrain.Text); s.AniP = AniProf.Value;
            s.MenT = ParseInt(MenTrain.Text); s.MenP = MenProf.Value;
            s.PrfT = ParseInt(PrfTrain.Text); s.PrfP = PrfProf.Value;
            s.ItmT = ParseInt(ItmTrain.Text); s.ItmP = ItmProf.Value;
            s.DecT = ParseInt(DecTrain.Text); s.DecP = DecProf.Value;
            s.ChrT = ParseInt(ChrTrain.Text); s.ChrP = ChrProf.Value;
            s.PrsT = ParseInt(PrsTrain.Text); s.PrsP = PrsProf.Value;
        }

        public void ApplyCharacter(Character c)
        {
            var s = c.Stats ?? new StatsData();

            StrBase.Text = s.StrBase.ToString(); StrSaveDot.Value = s.StrSaveProf ? 1 : 0;
            DexBase.Text = s.DexBase.ToString(); DexSaveDot.Value = s.DexSaveProf ? 1 : 0;
            ConBase.Text = s.ConBase.ToString(); ConSaveDot.Value = s.ConSaveProf ? 1 : 0;
            IntBase.Text = s.IntBase.ToString(); IntSaveDot.Value = s.IntSaveProf ? 1 : 0;
            WisBase.Text = s.WisBase.ToString(); WisSaveDot.Value = s.WisSaveProf ? 1 : 0;
            ChaBase.Text = s.ChaBase.ToString(); ChaSaveDot.Value = s.ChaSaveProf ? 1 : 0;

            AthTrain.Text = s.AthT.ToString(); AthProf.Value = s.AthP;
            AcrTrain.Text = s.AcrT.ToString(); AcrProf.Value = s.AcrP;
            SlhTrain.Text = s.SlhT.ToString(); SlhProf.Value = s.SlhP;
            StlTrain.Text = s.StlT.ToString(); StlProf.Value = s.StlP;
            EndTrain.Text = s.EndT.ToString(); EndProf.Value = s.EndP;
            AnaTrain.Text = s.AnaT.ToString(); AnaProf.Value = s.AnaP;
            HisTrain.Text = s.HisT.ToString(); HisProf.Value = s.HisP;
            MagTrain.Text = s.MagT.ToString(); MagProf.Value = s.MagP;
            NatTrain.Text = s.NatT.ToString(); NatProf.Value = s.NatP;
            RelTrain.Text = s.RelT.ToString(); RelProf.Value = s.RelP;
            TecTrain.Text = s.TecT.ToString(); TecProf.Value = s.TecP;
            AttTrain.Text = s.AttT.ToString(); AttProf.Value = s.AttP;
            SurTrain.Text = s.SurT.ToString(); SurProf.Value = s.SurP;
            MedTrain.Text = s.MedT.ToString(); MedProf.Value = s.MedP;
            InsTrain.Text = s.InsT.ToString(); InsProf.Value = s.InsP;
            AniTrain.Text = s.AniT.ToString(); AniProf.Value = s.AniP;
            MenTrain.Text = s.MenT.ToString(); MenProf.Value = s.MenP;
            PrfTrain.Text = s.PrfT.ToString(); PrfProf.Value = s.PrfP;
            ItmTrain.Text = s.ItmT.ToString(); ItmProf.Value = s.ItmP;
            DecTrain.Text = s.DecT.ToString(); DecProf.Value = s.DecP;
            ChrTrain.Text = s.ChrT.ToString(); ChrProf.Value = s.ChrP;
            PrsTrain.Text = s.PrsT.ToString(); PrsProf.Value = s.PrsP;

            RecalcAll();
        }

        public void ResetAll()
        {
            ApplyCharacter(new Character());
        }
    }
}
