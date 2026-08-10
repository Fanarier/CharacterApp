using System.Windows.Data;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CharacterApp.Controls;
using CharacterApp.Models;

namespace CharacterApp.Pages
{
    public partial class PassiveSkillsPage : Page, ISaveLoad, IPageSearchable
    {
        public ObservableCollection<SkillEntry> Skills { get; } = new ObservableCollection<SkillEntry>();
        private readonly SearchFilterBar _searchBar = new();

        public PassiveSkillsPage()
        {
            InitializeComponent();
            DataContext = this;
            // Loaded срабатывает при каждом показе страницы, а строку поиска
            // достаточно собрать один раз — иначе фильтры дублируются
            Loaded += (_, _) => { if (!_searchReady) { _searchReady = true; InitSearch(); } };
        }

        private bool _searchReady;

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
        private void InitSearch()
        {
            // Фильтра по категории здесь не было, хотя колонка «Категория»
            // такая же, как в активных навыках
            _searchBar.AddDropdownFilter("Категория:",
                new[] { "Все категории", "Классовый (К)", "Расовый (P)" },
                (item, val) => item is SkillEntry se && se.CategoryDisplay == val);
            _searchBar.Attach(SkillsGrid, Skills);
            SearchBarHost.Content = _searchBar;
        }

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

        public void FilterItems(string query)
        {
            // Раньше здесь ставился собственный фильтр на ту же коллекцию, что
            // и у строки поиска на странице — они затирали друг друга.
            // Теперь запрос из бокового меню просто попадает в эту строку.
            _searchBar.SetSearchText(query ?? "");
        }

    }
}
