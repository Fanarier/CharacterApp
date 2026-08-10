using System.Collections.Generic;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CharacterApp.Models;
using Microsoft.Win32;

namespace CharacterApp.Pages
{
    public partial class PageMain : Page, ISaveLoad
    {
        private string photoPath = string.Empty;
        private CharacterApp.Models.Character? _currentChar;
        private Dictionary<string, TextBox>    _colorFields = new();


        public PageMain()
        {
            InitializeComponent();
            Loaded += (_, _) => RegisterColorFields();
        }

        public void QuickSave() => (Application.Current.MainWindow as MainWindow)?.SaveAll();
        public void SaveAs()    => (Application.Current.MainWindow as MainWindow)?.SaveAllAs();
        public void LoadJSON()  => (Application.Current.MainWindow as MainWindow)?.LoadAll();

        public void ApplyCharacter(Character c)
        {
            CharacterNameTextBox.Text = c.CharacterName;
            HitsTextBox.Text          = c.Hits;
            DefenseTextBox.Text       = c.Defense.ToString();
            EvasionTextBox.Text       = c.Evasion.ToString();
            SuperHitsTextBox.Text     = c.SuperHits;
            SpeedTextBox.Text         = c.Speed.ToString();
            CarryTextBox.Text         = c.CarryCapacity.ToString();
            InitiativeTextBox.Text    = c.Initiative.ToString();
            MasteryTextBox.Text       = c.Mastery;
            ClassTextBox.Text         = c.Class;
            SubclassTextBox.Text      = c.Subclass;
            ExhaustionControl.Value   = c.Exhaustion;
            DeathSavesControl.Value   = c.DeathSaves;
            VisionControl.Value       = c.Vision;
            HearingControl.Value      = c.Hearing;
            AuraControl.Value         = c.Aura;
            ManaTextBox.Text          = c.Mana;
            StaminaTextBox.Text       = c.Stamina;
            CustomField1Label.Text    = c.CustomField1Label;
            CustomField1Value.Text    = c.CustomField1Value;
            CustomField2Label.Text    = c.CustomField2Label;
            CustomField2Value.Text    = c.CustomField2Value;
            CustomField3Label.Text    = c.CustomField3Label;
            CustomField3Value.Text    = c.CustomField3Value;
            CustomField4Label.Text    = c.CustomField4Label;
            CustomField4Value.Text    = c.CustomField4Value;

            photoPath = c.PhotoPath;
            CharacterImage.Source = !string.IsNullOrEmpty(photoPath) && File.Exists(photoPath)
                ? new BitmapImage(new Uri(photoPath, UriKind.RelativeOrAbsolute))
                : null;

            // Треугольник развития
            TbBodyDev.Text   = c.BodyDev.ToString();
            TbMindDev.Text   = c.MindDev.ToString();
            TbSpiritDev.Text = c.SpiritDev.ToString();
            UpdateTriangleChart();
            ApplyFieldColors(c);
        }

        public void FillCharacter(Character c)
        {
            c.CharacterName  = CharacterNameTextBox.Text.Trim();
            c.Hits           = HitsTextBox.Text.Trim();
            c.Defense        = int.TryParse(DefenseTextBox.Text, out var d)  ? d  : 0;
            c.Evasion        = int.TryParse(EvasionTextBox.Text, out var ev) ? ev : 0;
            c.SuperHits      = SuperHitsTextBox.Text.Trim();
            c.Speed          = int.TryParse(SpeedTextBox.Text, out var s)    ? s  : 0;
            c.CarryCapacity  = int.TryParse(CarryTextBox.Text, out var ca)   ? ca : 0;
            c.Initiative     = int.TryParse(InitiativeTextBox.Text, out var i) ? i : 0;
            c.Mastery        = MasteryTextBox.Text.Trim();
            c.Class          = ClassTextBox.Text.Trim();
            c.Subclass       = SubclassTextBox.Text.Trim();
            c.Exhaustion     = ExhaustionControl.Value;
            c.DeathSaves     = DeathSavesControl.Value;
            c.Vision         = VisionControl.Value;
            c.Hearing        = HearingControl.Value;
            c.Aura           = AuraControl.Value;
            c.Mana           = ManaTextBox.Text.Trim();
            c.Stamina        = StaminaTextBox.Text.Trim();
            c.CustomField1Label = CustomField1Label.Text.Trim();
            c.CustomField1Value = CustomField1Value.Text.Trim();
            c.CustomField2Label = CustomField2Label.Text.Trim();
            c.CustomField2Value = CustomField2Value.Text.Trim();
            c.CustomField3Label = CustomField3Label.Text.Trim();
            c.CustomField3Value = CustomField3Value.Text.Trim();
            c.CustomField4Label = CustomField4Label.Text.Trim();
            c.CustomField4Value = CustomField4Value.Text.Trim();
            c.PhotoPath = photoPath;

            // Треугольник развития
            c.BodyDev   = int.TryParse(TbBodyDev.Text,   out var bd) ? bd : 0;
            c.MindDev   = int.TryParse(TbMindDev.Text,   out var md) ? md : 0;
            c.SpiritDev = int.TryParse(TbSpiritDev.Text, out var sd) ? sd : 0;
            CollectColors(c);
        }

        // ── TextChanged хендлеры ─────────────────────────────────────────────

        private void CharacterName_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Обновляем заголовок окна — имя персонажа там видно всегда
            if (Application.Current.MainWindow is MainWindow mw)
                mw.UpdateTitle(CharacterNameTextBox.Text.Trim());
        }

        private void RegisterColorFields()
        {
            _colorFields = new Dictionary<string, TextBox>
            {
                ["CharName"]    = CharacterNameTextBox,
                ["Class"]       = ClassTextBox,
                ["Subclass"]    = SubclassTextBox,
                ["Hits"]        = HitsTextBox,
                ["Defense"]     = DefenseTextBox,
                ["SuperHits"]   = SuperHitsTextBox,
                ["Evasion"]     = EvasionTextBox,
                ["Speed"]       = SpeedTextBox,
                ["Mastery"]     = MasteryTextBox,
                ["Mana"]        = ManaTextBox,
                ["Stamina"]     = StaminaTextBox,

                // Эти поля были пропущены — правый клик по ним не давал
                // выбрать цвет, хотя по соседним давал
                ["Initiative"]  = InitiativeTextBox,
                ["Carry"]       = CarryTextBox,

                // Треугольник развития
                ["BodyDev"]     = TbBodyDev,
                ["MindDev"]     = TbMindDev,
                ["SpiritDev"]   = TbSpiritDev,

                // Пользовательские поля — и подпись, и значение
                ["Custom1Lbl"]  = CustomField1Label,
                ["Custom1Val"]  = CustomField1Value,
                ["Custom2Lbl"]  = CustomField2Label,
                ["Custom2Val"]  = CustomField2Value,
                ["Custom3Lbl"]  = CustomField3Label,
                ["Custom3Val"]  = CustomField3Value,
                ["Custom4Lbl"]  = CustomField4Label,
                ["Custom4Val"]  = CustomField4Value,
            };
            foreach (var (name, tb) in _colorFields)
                TextColorHelper.Register(tb, name,
                    () => _currentChar,
                    () => (App.Current.MainWindow as MainWindow)?.MarkUnsaved());
            // Страницу могли открыть уже ПОСЛЕ загрузки персонажа: тогда
            // ApplyColors отработал на пустом словаре и цвета не применились.
            // Красим сразу после регистрации, если персонаж уже известен.
            if (_currentChar != null) TextColorHelper.Apply(_colorFields, _currentChar);
        }

        public void ApplyFieldColors(CharacterApp.Models.Character c)
        {
            _currentChar = c;
            TextColorHelper.Apply(_colorFields, c);
        }

        // ── Цвета полей ──────────────────────────────────────────────────────
        // ApplyColors удалён: он дублировал ApplyFieldColors и никем не вызывался.

        private void CollectColors(CharacterApp.Models.Character c)
        {
            foreach (var (key, tb) in _colorFields)
            {
                var src = System.Windows.DependencyPropertyHelper
                    .GetValueSource(tb, System.Windows.Controls.TextBox.ForegroundProperty)
                    .BaseValueSource;
                if (src == System.Windows.BaseValueSource.Local &&
                    tb.Foreground is System.Windows.Media.SolidColorBrush b)
                    c.FieldColors[key] =
                        $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}";
                else
                    c.FieldColors.Remove(key);
            }
        }


        public void ClearFieldColors()
        {
            _currentChar = null;
            foreach (var tb in _colorFields.Values)
                tb.ClearValue(TextBox.ForegroundProperty);
        }


        private void HitsTextBox_TextChanged    (object sender, TextChangedEventArgs e) { /* color now via right-click */ }
        private void SuperHitsTextBox_TextChanged(object sender, TextChangedEventArgs e) { /* color now via right-click */ }
        private void DefenseTextBox_TextChanged  (object sender, TextChangedEventArgs e) { /* color now via right-click */ }
        private void ManaTextBox_TextChanged     (object sender, TextChangedEventArgs e) { /* color now via right-click */ }
        private void StaminaTextBox_TextChanged  (object sender, TextChangedEventArgs e) { /* color now via right-click */ }
        private void MasteryTextBox_TextChanged  (object sender, TextChangedEventArgs e) { /* color now via right-click */ }

        // Уже заменён на NumericOnly_PreviewTextInput в xaml — оставлен как fallback
        private static void Mastery_PreviewTextInput(object _, TextCompositionEventArgs e)
            => e.Handled = e.Text.Length != 1 || !char.IsDigit(e.Text[0]);

        private static void SetTextColor(TextBox tb, Color primaryColor, Color specialPositive)
        {
            if (tb == null) return;
            var text = tb.Text.Trim();
            if (string.IsNullOrEmpty(text) || text == "0")
                tb.ClearValue(TextBox.ForegroundProperty);
            else if (text.StartsWith('+'))
                tb.Foreground = new SolidColorBrush(specialPositive);
            else if (text.StartsWith('-'))
                tb.Foreground = new SolidColorBrush(Colors.Red);
            else
                tb.Foreground = new SolidColorBrush(primaryColor);
        }

        // ── Фото ────────────────────────────────────────────────────────────

        private void AddPhoto_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Image Files|*.jpg;*.png;*.bmp" };
            if (dlg.ShowDialog() != true) return;
            photoPath = dlg.FileName;
            CharacterImage.Source = new BitmapImage(new Uri(photoPath, UriKind.RelativeOrAbsolute));
            (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }

        private void ViewPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (CharacterImage.Source == null)
            { (Application.Current.MainWindow as MainWindow)?.ShowNotification("Фото не добавлено", NotificationType.Warning); return; }
            var win = new Window
            {
                Title   = "Просмотр фото",
                Width   = 600, Height = 600,
                Content = new Image { Source = CharacterImage.Source, Stretch = System.Windows.Media.Stretch.Uniform },
                Owner   = Application.Current.MainWindow
            };
            win.ShowDialog();
        }

        private void DeletePhoto_Click(object sender, RoutedEventArgs e)
        {
            CharacterImage.Source = null;
            photoPath = string.Empty;
            (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }
        // Общий хендлер для числовых полей (Evasion, Speed, Carry, Initiative)

        // ── Валидация числовых полей ────────────────────────────────────────
        private void NumericOnly_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                var proposed = tb.Text.Insert(tb.CaretIndex, e.Text);
                e.Handled = !IsValidInteger(proposed);
            }
        }

        private bool IsValidInteger(string text)
        {
            if (string.IsNullOrEmpty(text) || text == "-") return true;
            return int.TryParse(text, out _);
        }


        private void AnyNumericField_TextChanged(object sender, TextChangedEventArgs e)
            => (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();

        // ── Треугольник развития ─────────────────────────────────────────────

        private void TriDev_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateTriangleChart();
            (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }

        private void UpdateTriangleChart()
        {
            if (TriangleChart == null) return;
            TriangleChart.BodyValue   = int.TryParse(TbBodyDev.Text,   out var b) ? Math.Clamp(b, 0, 8) : 0;
            TriangleChart.MindValue   = int.TryParse(TbMindDev.Text,   out var m) ? Math.Clamp(m, 0, 8) : 0;
            TriangleChart.SpiritValue = int.TryParse(TbSpiritDev.Text, out var s) ? Math.Clamp(s, 0, 8) : 0;
            TriangleChart.InvalidateVisual();
        }

    }
}