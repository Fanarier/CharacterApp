using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using CharacterApp.Models;

namespace CharacterApp.Pages
{
    public partial class ProficienciesPage : Page, ISaveLoad
    {
        private bool _loading;

        public ObservableCollection<ProficiencyEntry> Proficiencies { get; }
            = new ObservableCollection<ProficiencyEntry>();

        public ProficienciesPage()
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
            ProfGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            // Черты
            c.Trait4  = new TraitData { Description = Trait4Desc.Text,  IsAcquired = Trait4Check.IsChecked  == true };
            c.Trait9  = new TraitData { Description = Trait9Desc.Text,  IsAcquired = Trait9Check.IsChecked  == true };
            c.Trait18 = new TraitData { Description = Trait18Desc.Text, IsAcquired = Trait18Check.IsChecked == true };

            // Владения
            c.Proficiencies.Clear();
            foreach (var p in Proficiencies)
                c.Proficiencies.Add(new ProficiencyData
                {
                    TypeIndex   = p.TypeIndex,
                    Description = p.Description,
                    Rating      = p.Rating
                });
        }

        // ── Загрузка из Character ────────────────────────────────────────────
        public void ApplyCharacter(Character c)
        {
            _loading = true;

            // Черты
            Trait4Desc.Text  = c.Trait4?.Description  ?? "";
            Trait9Desc.Text  = c.Trait9?.Description  ?? "";
            Trait18Desc.Text = c.Trait18?.Description ?? "";
            Trait4Check.IsChecked  = c.Trait4?.IsAcquired  ?? false;
            Trait9Check.IsChecked  = c.Trait9?.IsAcquired  ?? false;
            Trait18Check.IsChecked = c.Trait18?.IsAcquired ?? false;

            // Владения
            Proficiencies.Clear();
            if (c.Proficiencies != null)
                foreach (var p in c.Proficiencies)
                    Proficiencies.Add(new ProficiencyEntry
                    {
                        TypeIndex   = p.TypeIndex,
                        Description = p.Description,
                        Rating      = p.Rating
                    });

            _loading = false;
        }

        public void ResetAll()
        {
            _loading = true;
            Trait4Desc.Text  = ""; Trait4Check.IsChecked  = false;
            Trait9Desc.Text  = ""; Trait9Check.IsChecked  = false;
            Trait18Desc.Text = ""; Trait18Check.IsChecked = false;
            Proficiencies.Clear();
            _loading = false;
        }

        // ── Черты — изменения ────────────────────────────────────────────────
        private void TraitField_Changed(object sender, RoutedEventArgs e)
        {
            if (!_loading)
                (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }

        // ── Кнопки таблицы ──────────────────────────────────────────────────
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var item = new ProficiencyEntry { TypeIndex = 0, Description = "", Rating = 0 };
            Proficiencies.Add(item);
            ProfGrid.SelectedItem = item;
            ProfGrid.ScrollIntoView(item);
            ProfGrid.BeginEdit();
            (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (ProfGrid.SelectedItem is ProficiencyEntry sel)
            {
                Proficiencies.Remove(sel);
                (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            Proficiencies.Clear();
            (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }
    }

    // ── ViewModel строки таблицы ─────────────────────────────────────────────
    public class ProficiencyEntry : INotifyPropertyChanged
    {
        private int    _typeIndex;
        private string _description = "";
        private int    _rating;

        public int TypeIndex
        {
            get => _typeIndex;
            set
            {
                if (_typeIndex == value) return;
                _typeIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TypeDisplay));
                MarkUnsaved();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description == value) return;
                _description = value;
                OnPropertyChanged();
                MarkUnsaved();
            }
        }

        public int Rating
        {
            get => _rating;
            set
            {
                if (_rating == value) return;
                _rating = value;
                OnPropertyChanged();
                MarkUnsaved();
            }
        }

        public string TypeDisplay => TypeIndex == 1 ? "Язык" : "Владение";

        private static void MarkUnsaved()
            => (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
