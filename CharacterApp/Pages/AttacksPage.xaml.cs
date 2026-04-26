using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CharacterApp.Models;

namespace CharacterApp.Pages
{
    public partial class AttacksPage : Page, ISaveLoad
    {
        public ObservableCollection<AttackEntry> Attacks { get; } = new();

        public AttacksPage()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void QuickSave() => (Application.Current.MainWindow as MainWindow)?.SaveAll();
        public void SaveAs()    => (Application.Current.MainWindow as MainWindow)?.SaveAllAs();
        public void LoadJSON()  => (Application.Current.MainWindow as MainWindow)?.LoadAll();

        public void FillCharacter(Character c)
        {
            AttacksGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
            c.Attacks.Clear();
            foreach (var a in Attacks)
                c.Attacks.Add(new AttackData
                {
                    AttackType  = a.AttackType,
                    Description = a.Description,
                    IsActive    = a.IsActive
                });
        }

        public void ApplyCharacter(Character c)
        {
            Attacks.Clear();
            if (c.Attacks == null) return;
            foreach (var a in c.Attacks)
                Attacks.Add(new AttackEntry
                {
                    AttackType  = a.AttackType,
                    Description = a.Description,
                    IsActive    = a.IsActive
                });
        }

        public void ResetAll() => Attacks.Clear();

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var item = new AttackEntry { AttackType = "Атака", Description = "", IsActive = false };
            Attacks.Add(item);
            AttacksGrid.SelectedItem = item;
            AttacksGrid.ScrollIntoView(item);
            AttacksGrid.BeginEdit();
            (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (AttacksGrid.SelectedItem is AttackEntry sel)
            {
                Attacks.Remove(sel);
                (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            Attacks.Clear();
            (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
        }

        private void StatusToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb && tb.DataContext is AttackEntry entry)
            {
                entry.IsActive = tb.IsChecked == true;
                AttacksGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: false);
                (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();
            }
        }
    }

    public class AttackEntry : INotifyPropertyChanged
    {
        private string _attackType  = "Атака";
        private string _description = "";
        private bool   _isActive;

        public string AttackType
        {
            get => _attackType;
            set { if (_attackType == value) return; _attackType = value; OnPropertyChanged(); Mark(); }
        }
        public string Description
        {
            get => _description;
            set { if (_description == value) return; _description = value; OnPropertyChanged(); Mark(); }
        }
        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive == value) return; _isActive = value; OnPropertyChanged(); Mark(); }
        }

        private static void Mark() => (Application.Current.MainWindow as MainWindow)?.MarkUnsaved();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
