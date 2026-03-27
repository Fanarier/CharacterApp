using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace CharacterApp.Pages
{
    public partial class ActiveSkillsPage : Page
    {
        // Коллекция навыков, привязанная к DataGrid
        public ObservableCollection<SkillEntry> Skills { get; } = new ObservableCollection<SkillEntry>();

        public ActiveSkillsPage()
        {
            InitializeComponent();

            // Очистим Items (защита от возможных дизайнерских элементов)
            if (SkillsGrid != null && SkillsGrid.Items.Count > 0) SkillsGrid.Items.Clear();

            // Привяжем DataContext — ItemsSource в XAML ищет свойство Skills
            DataContext = this;

            // Подставим примерные записи один раз, если пусто (убери если не нужно)
            if (Skills.Count == 0)
            {
                Skills.Add(new SkillEntry { CategoryIndex = 0, Description = "Быстрая атака", IsActiveSymbol = true });
                Skills.Add(new SkillEntry { CategoryIndex = 1, Description = "Расовый импульс", IsActiveSymbol = false });
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var item = new SkillEntry { CategoryIndex = 0, Description = "Новый навык", IsActiveSymbol = false };
            Skills.Add(item);

            // выделение нового — чтобы пользователь мог сразу редактировать
            SkillsGrid.SelectedItem = item;
            SkillsGrid.ScrollIntoView(item);
            SkillsGrid.BeginEdit();
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (SkillsGrid.SelectedItem is SkillEntry sel)
            {
                Skills.Remove(sel);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            Skills.Clear();
        }
    }

    // Простой POCO моделирующий строку — реализация INotifyPropertyChanged для корректного обновления UI
    public class SkillEntry : INotifyPropertyChanged
    {
        private int _categoryIndex;
        private string _description = "";
        private bool _isActiveSymbol;

        public int CategoryIndex
        {
            get => _categoryIndex;
            set
            {
                if (_categoryIndex == value) return;
                _categoryIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CategoryDisplay));
            }
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

        // Отображаемое название категории (соглашение: 0 -> "Классовый (К)", 1 -> "Расовый (P)")
        public string CategoryDisplay => CategoryIndex == 1 ? "Расовый (P)" : "Классовый (К)";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
