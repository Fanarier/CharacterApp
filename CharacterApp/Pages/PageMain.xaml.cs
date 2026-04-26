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

        public PageMain() { InitializeComponent(); }

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
        }

        // ── TextChanged хендлеры ─────────────────────────────────────────────

        private void CharacterName_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Обновляем заголовок окна — имя персонажа там видно всегда
            if (Application.Current.MainWindow is MainWindow mw)
                mw.UpdateTitle(CharacterNameTextBox.Text.Trim());
        }

        private void HitsTextBox_TextChanged    (object sender, TextChangedEventArgs e) => SetTextColor(HitsTextBox,    Colors.Green,    Colors.Lime);
        private void SuperHitsTextBox_TextChanged(object sender, TextChangedEventArgs e) => SetTextColor(SuperHitsTextBox, Colors.SeaGreen, Colors.Lime);
        private void DefenseTextBox_TextChanged  (object sender, TextChangedEventArgs e) => SetTextColor(DefenseTextBox,  Colors.Blue,     Colors.Lime);
        private void ManaTextBox_TextChanged     (object sender, TextChangedEventArgs e) => SetTextColor(ManaTextBox,     Colors.LightBlue, Colors.Lime);
        private void StaminaTextBox_TextChanged  (object sender, TextChangedEventArgs e) => SetTextColor(StaminaTextBox,  Colors.LightGreen, Colors.Lime);
        private void MasteryTextBox_TextChanged  (object sender, TextChangedEventArgs e) => SetTextColor(MasteryTextBox,  Colors.SeaGreen, Colors.Lime);

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

    }
}