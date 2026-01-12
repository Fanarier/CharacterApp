using System.Windows;
using System.Windows.Controls;
using CharacterApp;

namespace CharacterApp
{
    public partial class PageDetails : Page, ISaveLoad
    {
        public PageDetails()
        {
            InitializeComponent();
        }

        // Делегирую
        public void QuickSave() => (Application.Current.MainWindow as MainWindow)?.SaveAll();
        public void SaveAs() => (Application.Current.MainWindow as MainWindow)?.SaveAllAs();
        public void LoadJSON() => (Application.Current.MainWindow as MainWindow)?.LoadAll();

        // Применить данные
        public void ApplyCharacter(Character c)
        {
            BackstoryTextBox.Text = c.Backstory;
            WorldviewTextBox.Text = c.Worldview;
            HeightWeightTextBox.Text = c.HeightWeight;
            BodySizeTextBox.Text = c.BodySize;
            AgeTextBox.Text = c.Age.ToString();
            AppearanceTextBox.Text = c.Appearance;
            StartBonus1TextBox.Text = c.StartBonus1;
            StartBonus2TextBox.Text = c.StartBonus2;
            StartBonus3TextBox.Text = c.StartBonus3;
            LevelTextBox.Text = c.Level.ToString();
            ExperienceTextBox.Text = c.Experience.ToString();
            AwakeningTextBox.Text = c.Awakening;
            BuffTextBox.Text = c.Buff;
            DebuffTextBox.Text = c.Debuff;
        }

        // Собрать данные
        public void FillCharacter(Character c)
        {
            c.Backstory = BackstoryTextBox.Text.Trim();
            c.Worldview = WorldviewTextBox.Text.Trim();
            c.HeightWeight = HeightWeightTextBox.Text.Trim();
            c.BodySize = BodySizeTextBox.Text.Trim();
            c.Age = int.TryParse(AgeTextBox.Text, out var a) ? a : 0;
            c.Appearance = AppearanceTextBox.Text.Trim();
            c.StartBonus1 = StartBonus1TextBox.Text.Trim();
            c.StartBonus2 = StartBonus2TextBox.Text.Trim();
            c.StartBonus3 = StartBonus3TextBox.Text.Trim();
            c.Level = int.TryParse(LevelTextBox.Text, out var lvl) ? lvl : 0;
            c.Experience = int.TryParse(ExperienceTextBox.Text, out var xp) ? xp : 0;
            c.Awakening = AwakeningTextBox.Text.Trim();
            c.Buff = BuffTextBox.Text.Trim();
            c.Debuff = DebuffTextBox.Text.Trim();
        }
    }
}