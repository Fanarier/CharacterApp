using System;
using System.IO;
using System.Text.RegularExpressions;
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
            if (Application.Current.MainWindow is MainWindow mw)
            { mw.UpdateTitle(CharacterNameTextBox.Text.Trim()); mw.MarkUnsaved(); }
        }

        private void HitsTextBox_TextChanged(object sender, TextChangedEventArgs _) { SetTextColor(HitsTextBox, Colors.Green, Colors.Lime); (Application.Current.MainWindow as MainWindow)?.MarkUnsaved(); }
        private void SuperHitsTextBox_TextChanged(object sender, TextChangedEventArgs _) { SetTextColor(SuperHitsTextBox, Colors.SeaGreen, Colors.Lime); (Application.Current.MainWindow as MainWindow)?.MarkUnsaved(); }
        private void DefenseTextBox_TextChanged(object sender, TextChangedEventArgs _) { SetTextColor(DefenseTextBox, Colors.Blue, Colors.Lime); (Application.Current.MainWindow as MainWindow)?.MarkUnsaved(); }
        private void ManaTextBox_TextChanged(object sender, TextChangedEventArgs _) { SetTextColor(ManaTextBox, Colors.LightBlue, Colors.Lime); (Application.Current.MainWindow as MainWindow)?.MarkUnsaved(); }
        private void StaminaTextBox_TextChanged(object sender, TextChangedEventArgs _) { SetTextColor(StaminaTextBox, Colors.LightGreen, Colors.Lime); (Application.Current.MainWindow as MainWindow)?.MarkUnsaved(); }
        private void MasteryTextBox_TextChanged(object sender, TextChangedEventArgs _) { SetTextColor(MasteryTextBox, Colors.SeaGreen, Colors.Lime); (Application.Current.MainWindow as MainWindow)?.MarkUnsaved(); }

        private void Mastery_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb = sender as TextBox;
            e.Handled = tb?.SelectionStart == 0
                ? !Regex.IsMatch(e.Text, @"^[0-9+\-]+$")
                : !Regex.IsMatch(e.Text, @"^[0-9]+$");
        }

        private void SetTextColor(TextBox tb, Color primaryColor, Color specialPositive)
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
    }
}
