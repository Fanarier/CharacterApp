using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CharacterApp.Models;

namespace CharacterApp.Pages
{
    public partial class PassiveSkillsPage : Page, ISaveLoad
    {
        public ObservableCollection<SkillEntry> Skills { get; } = new ObservableCollection<SkillEntry>();

        public PassiveSkillsPage()
        {
            InitializeComponent();
            DataContext = this;
        }

        // ── ISaveLoad ────────────────────────────────────────────────────────
        public void QuickSave() => (Application.Current.MainWindow as MainWindow)?.SaveAll();
        public void SaveAs()    => (Application.Current.MainWindow as MainWindow)?.SaveAllAs();
        public void LoadJSON()  => (Application.Current.MainWindow as MainWindow)?.LoadAll();

        // ── Сохранение в Character ───────────────────────────────────────────
        public void FillCharacter(Character c)
        {
            SkillsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            c.PassiveSkills.Clear();
            foreach (var s in Skills)
                c.PassiveSkills.Add(new SkillData
                {
                    SkillName      = s.SkillName,
                    CategoryIndex  = s.CategoryIndex,
                    Description    = s.Description,
                    IsActiveSymbol = s.IsActiveSymbol
                });
        }

        // ── Загрузка из Character ────────────────────────────────────────────
        public void ApplyCharacter(Character c)
        {
            Skills.Clear();
            if (c.PassiveSkills == null) return;
            foreach (var sd in c.PassiveSkills)
                Skills.Add(new SkillEntry
                {
                    SkillName      = sd.SkillName,
                    CategoryIndex  = sd.CategoryIndex,
                    Description    = sd.Description,
                    IsActiveSymbol = sd.IsActiveSymbol
                });
        }

        public void ResetAll() => Skills.Clear();

        // ── Кнопки ──────────────────────────────────────────────────────────
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var item = new SkillEntry
            {
                SkillName      = "Новый навык",
                CategoryIndex  = 0,
                Description    = "",
                IsActiveSymbol = false
            };
            Skills.Add(item);
            SkillsGrid.SelectedItem = item;
            SkillsGrid.ScrollIntoView(item);
            SkillsGrid.BeginEdit();
            (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (SkillsGrid.SelectedItem is SkillEntry sel)
            {
                Skills.Remove(sel);
                (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            Skills.Clear();
            (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }

        // ── ToggleButton — явный коммит ──────────────────────────────────────
        private void StatusToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton tb
                && tb.DataContext is SkillEntry entry)
            {
                entry.IsActiveSymbol = tb.IsChecked == true;
                SkillsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: false);
                (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
            }
        }
    }
}
