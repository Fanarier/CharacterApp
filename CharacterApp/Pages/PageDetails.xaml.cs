using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CharacterApp.Models;

namespace CharacterApp.Pages
{
    public partial class PageDetails : Page, ISaveLoad
    {
        private bool _loading = false;   // подавляем MarkUnsaved при ApplyCharacter

        public PageDetails() { InitializeComponent(); Loaded += (_, _) => RegisterColorFields(); }

        public void QuickSave() => (Application.Current.MainWindow as MainWindow)?.SaveAll();
        public void SaveAs()    => (Application.Current.MainWindow as MainWindow)?.SaveAllAs();
        public void LoadJSON()  => (Application.Current.MainWindow as MainWindow)?.LoadAll();

        public void ApplyCharacter(Character c)
        {
            _loading = true;
            RaceTextBox.Text         = c.Race;
            BackstoryTextBox.Text    = c.Backstory;
            WorldviewTextBox.Text    = c.Worldview;
            HeightWeightTextBox.Text = c.HeightWeight;
            BodySizeTextBox.Text     = c.BodySize;
            AgeTextBox.Text          = c.Age.ToString();
            AppearanceTextBox.Text   = c.Appearance;
            StartBonus1TextBox.Text  = c.StartBonus1;
            StartBonus2TextBox.Text  = c.StartBonus2;
            StartBonus3TextBox.Text  = c.StartBonus3;
            LevelTextBox.Text        = c.Level.ToString();
            ExperienceTextBox.Text   = c.Experience.ToString();
            AwakeningTextBox.Text    = c.Awakening;
            BuffTextBox.Text         = c.Buff;
            DebuffTextBox.Text       = c.Debuff;
            ApplyColors(c);
            _loading = false;
        }

        public void FillCharacter(Character c)
        {
            c.Race         = RaceTextBox.Text.Trim();
            c.Backstory    = BackstoryTextBox.Text.Trim();
            c.Worldview    = WorldviewTextBox.Text.Trim();
            c.HeightWeight = HeightWeightTextBox.Text.Trim();
            c.BodySize     = BodySizeTextBox.Text.Trim();
            c.Age          = int.TryParse(AgeTextBox.Text, out var a)   ? a   : 0;
            c.Appearance   = AppearanceTextBox.Text.Trim();
            c.StartBonus1  = StartBonus1TextBox.Text.Trim();
            c.StartBonus2  = StartBonus2TextBox.Text.Trim();
            c.StartBonus3  = StartBonus3TextBox.Text.Trim();
            c.Level        = int.TryParse(LevelTextBox.Text, out var lvl) ? lvl : 0;
            c.Experience   = int.TryParse(ExperienceTextBox.Text, out var xp) ? xp : 0;
            c.Awakening    = AwakeningTextBox.Text.Trim();
            c.Buff         = BuffTextBox.Text.Trim();
            c.Debuff       = DebuffTextBox.Text.Trim();
            CollectColors(c);
        }

        // ── Цвета полей ──────────────────────────────────────────────────────
        private CharacterApp.Models.Character? _currentChar;
        private System.Collections.Generic.Dictionary<string, System.Windows.Controls.TextBox>
            _colorFields = new();

        private void RegisterColorFields()
        {
            _colorFields = new System.Collections.Generic.Dictionary<string, System.Windows.Controls.TextBox>
            {
                ["PD_Race"]        = RaceTextBox,
                ["PD_Backstory"]   = BackstoryTextBox,
                ["PD_Worldview"]   = WorldviewTextBox,
                ["PD_HeightWeight"]= HeightWeightTextBox,
                ["PD_BodySize"]    = BodySizeTextBox,
                ["PD_Age"]         = AgeTextBox,
                ["PD_Appearance"]  = AppearanceTextBox,
                ["PD_Bonus1"]      = StartBonus1TextBox,
                ["PD_Bonus2"]      = StartBonus2TextBox,
                ["PD_Bonus3"]      = StartBonus3TextBox,
                ["PD_Level"]       = LevelTextBox,
                ["PD_Experience"]  = ExperienceTextBox,
                ["PD_Awakening"]   = AwakeningTextBox,
                ["PD_Buff"]        = BuffTextBox,
                ["PD_Debuff"]      = DebuffTextBox,
            };
            var mw = () => App.Current.MainWindow as MainWindow;
            foreach (var (name, tb) in _colorFields)
                TextColorHelper.Register(tb, name,
                    () => _currentChar,
                    () => mw()?.MarkUnsaved());
            // Страницу могли открыть уже ПОСЛЕ загрузки персонажа: тогда
            // ApplyColors отработал на пустом словаре и цвета не применились.
            // Красим сразу после регистрации, если персонаж уже известен.
            if (_currentChar != null) TextColorHelper.Apply(_colorFields, _currentChar);
        }

        private void ApplyColors(CharacterApp.Models.Character c)
        {
            _currentChar = c;
            TextColorHelper.Apply(_colorFields, c);
        }

        private void CollectColors(CharacterApp.Models.Character c)
        {
            foreach (var (key, tb) in _colorFields)
            {
                var src = System.Windows.DependencyPropertyHelper
                    .GetValueSource(tb, System.Windows.Controls.TextBox.ForegroundProperty)
                    .BaseValueSource;
                if (src == System.Windows.BaseValueSource.Local &&
                    tb.Foreground is System.Windows.Media.SolidColorBrush b)
                    c.FieldColors[key] = $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}";
                else
                    c.FieldColors.Remove(key);
            }
        }

        // ── MarkUnsaved на любое изменение поля ─────────────────────────────
        private void AnyField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_loading)
                (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }
    }
}
