using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

        /// <summary>Черты гуманоидов — отдельная подкатегория, максимум пять.</summary>
        public ObservableCollection<HumanoidTraitEntry> HumanoidTraits { get; }
            = new ObservableCollection<HumanoidTraitEntry>();

        public const int MaxHumanoidTraits = 5;

        public ProficienciesPage()
        {
            InitializeComponent();
            DataContext = this;
            HumanoidTraits.CollectionChanged += (_, _) => UpdateHumanoidCounter();
            Loaded += (_, _) => UpdateHumanoidCounter();
            Loaded += (_, _) => RegisterColorFields(new System.Collections.Generic.Dictionary<string, System.Windows.Controls.TextBox>
            {
                ["PRF_Trait4"] = Trait4Desc,
                ["PRF_Trait9"] = Trait9Desc,
                ["PRF_Trait18"] = Trait18Desc,
            });
        }

        // ── ISaveLoad ────────────────────────────────────────────────────────
        public void QuickSave() => (Application.Current.MainWindow as MainWindow)?.SaveAll();
        public void SaveAs()    => (Application.Current.MainWindow as MainWindow)?.SaveAllAs();
        public void LoadJSON()  => (Application.Current.MainWindow as MainWindow)?.LoadAll();

        // ── Сохранение в Character ───────────────────────────────────────────

        // ── Цвета полей ──────────────────────────────────────────────────────
        private CharacterApp.Models.Character? _currentChar;
        private System.Collections.Generic.Dictionary<string, System.Windows.Controls.TextBox>
            _colorFields = new();

        private void RegisterColorFields(
            System.Collections.Generic.Dictionary<string, System.Windows.Controls.TextBox> fields)
        {
            _colorFields = fields;
            var mw = () => App.Current.MainWindow as MainWindow;
            foreach (var (name, tb) in _colorFields)
                TextColorHelper.Register(tb, name, () => _currentChar, () => mw()?.MarkUnsaved());
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

        public void FillCharacter(Character c)
        {
            ProfGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            // Черты
            c.Trait4  = new TraitData { Description = Trait4Desc.Text,  IsAcquired = Trait4Check.IsChecked  == true };
            c.Trait9  = new TraitData { Description = Trait9Desc.Text,  IsAcquired = Trait9Check.IsChecked  == true };
            c.Trait18 = new TraitData { Description = Trait18Desc.Text, IsAcquired = Trait18Check.IsChecked == true };

            // Черты гуманоидов
            HumanoidGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
            c.HumanoidTraits.Clear();
            foreach (var t in HumanoidTraits)
                c.HumanoidTraits.Add(new TraitData
                {
                    Name        = t.Name,
                    Description = t.Description,
                    IsAcquired  = t.IsAcquired
                });

            // Владения
            c.Proficiencies.Clear();
            foreach (var p in Proficiencies)
                c.Proficiencies.Add(new ProficiencyData
                {
                    TypeIndex   = p.TypeIndex,
                    Description = p.Description,
                    Rating      = p.Rating
                });

            CollectColors(c);
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
            ApplyColors(c);

            // Черты гуманоидов
            HumanoidTraits.Clear();
            if (c.HumanoidTraits != null)
                foreach (var t in c.HumanoidTraits.Take(MaxHumanoidTraits))
                    HumanoidTraits.Add(new HumanoidTraitEntry
                    {
                        Name        = t.Name,
                        Description = t.Description,
                        IsAcquired  = t.IsAcquired
                    });

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
            UpdateHumanoidCounter();
        }

        public void ResetAll()
        {
            _loading = true;
            Trait4Desc.Text  = ""; Trait4Check.IsChecked  = false;
            Trait9Desc.Text  = ""; Trait9Check.IsChecked  = false;
            Trait18Desc.Text = ""; Trait18Check.IsChecked = false;
            HumanoidTraits.Clear();
            Proficiencies.Clear();
            _loading = false;
            UpdateHumanoidCounter();
        }

        // ── Черты гуманоидов ─────────────────────────────────────────────────

        private void UpdateHumanoidCounter()
        {
            if (TbHumanoidCount == null) return;
            TbHumanoidCount.Text = $"{HumanoidTraits.Count} из {MaxHumanoidTraits}";
        }

        private void BtnAddHumanoid_Click(object sender, RoutedEventArgs e)
        {
            if (HumanoidTraits.Count >= MaxHumanoidTraits)
            {
                (Application.Current.MainWindow as MainWindow)?.ShowNotification(
                    $"Черт гуманоида может быть не больше {MaxHumanoidTraits}", NotificationType.Warning);
                return;
            }

            var item = new HumanoidTraitEntry();
            HumanoidTraits.Add(item);
            HumanoidGrid.SelectedItem = item;
            HumanoidGrid.ScrollIntoView(item);
            HumanoidGrid.BeginEdit();
            (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }

        private void BtnRemoveHumanoid_Click(object sender, RoutedEventArgs e)
        {
            if (HumanoidGrid.SelectedItem is HumanoidTraitEntry sel)
            {
                HumanoidTraits.Remove(sel);
                (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
            }
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

    // ── ViewModel строки «Черты гуманоидов» ──────────────────────────────────
    public class HumanoidTraitEntry : INotifyPropertyChanged
    {
        private string _name        = "";
        private string _description = "";
        private bool   _isAcquired;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged();
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

        public bool IsAcquired
        {
            get => _isAcquired;
            set
            {
                if (_isAcquired == value) return;
                _isAcquired = value;
                OnPropertyChanged();
                MarkUnsaved();
            }
        }

        private static void MarkUnsaved()
            => (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
