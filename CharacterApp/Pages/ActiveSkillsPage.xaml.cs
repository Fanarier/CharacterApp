using System.Windows.Data;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using CharacterApp.Controls;
using CharacterApp.Models;

namespace CharacterApp.Pages
{
    public partial class ActiveSkillsPage : Page, IPageSearchable
    {
        public ObservableCollection<SkillEntry> Skills { get; } = new ObservableCollection<SkillEntry>();
        private readonly SearchFilterBar _searchBar = new();

        public ActiveSkillsPage()
        {
            InitializeComponent();
            DataContext = this;
            // Loaded срабатывает при каждом показе страницы, а строку поиска
            // достаточно собрать один раз — иначе фильтры дублируются
            Loaded += (_, _) => { if (!_searchReady) { _searchReady = true; InitSearch(); } };
        }

        private bool _searchReady;

        public void FillCharacter(Character c)
        {
            SkillsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            c.Skills.Clear();
            foreach (var s in Skills)
                c.Skills.Add(new SkillData
                {
                    SkillName      = s.SkillName,
                    CategoryIndex  = s.CategoryIndex,
                    Description    = s.Description,
                    IsActiveSymbol = s.IsActiveSymbol
                });
        }

        public void ApplyCharacter(Character c)
        {
            Skills.Clear();
            if (c.Skills == null) return;
            foreach (var sd in c.Skills)
                Skills.Add(new SkillEntry
                {
                    SkillName      = sd.SkillName,
                    CategoryIndex  = sd.CategoryIndex,
                    Description    = sd.Description,
                    IsActiveSymbol = sd.IsActiveSymbol
                });
        }

        public void ResetAll() => Skills.Clear();

        private void InitSearch()
        {
            _searchBar.AddDropdownFilter("Категория:", new[] { "Все категории", "Классовый (К)", "Расовый (P)" },
                (item, val) => item is SkillEntry se && se.CategoryDisplay == val);
            _searchBar.Attach(SkillsGrid, Skills);
            SearchBarHost.Content = _searchBar;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var item = new SkillEntry { SkillName = "Новый навык", CategoryIndex = 0, Description = "", IsActiveSymbol = false };
            Skills.Add(item);
            SkillsGrid.SelectedItem = item;
            SkillsGrid.ScrollIntoView(item);
            SkillsGrid.BeginEdit();
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (SkillsGrid.SelectedItem is SkillEntry sel) Skills.Remove(sel);
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e) => Skills.Clear();

        // ToggleButton в DataGrid требует явного коммита при изменении
        private void StatusToggle_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton tb
                && tb.DataContext is SkillEntry entry)
            {
                entry.IsActiveSymbol = tb.IsChecked == true;
                SkillsGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: false);
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
    public class SkillEntry : INotifyPropertyChanged
    {
        private string _skillName     = "";
        private int    _categoryIndex;
        private string _description   = "";
        private bool   _isActiveSymbol;

        public string SkillName
        {
            get => _skillName;
            set { if (_skillName == value) return; _skillName = value; OnPropertyChanged(); }
        }

        public int CategoryIndex
        {
            get => _categoryIndex;
            set { if (_categoryIndex == value) return; _categoryIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(CategoryDisplay)); }
        }

        public string Description
        {
            get => _description;
            set { if (_description == value) return; _description = value; OnPropertyChanged(); }
        }

        public bool IsActiveSymbol
        {
            get => _isActiveSymbol;
            set { if (_isActiveSymbol == value) return; _isActiveSymbol = value; OnPropertyChanged(); }
        }

        public string CategoryDisplay => CategoryIndex == 1 ? "Расовый (P)" : "Классовый (К)";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    }
}
