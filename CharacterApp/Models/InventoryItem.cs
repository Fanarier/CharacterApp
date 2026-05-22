using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CharacterApp.Models
{
    public class InventoryItem : INotifyPropertyChanged
    {
        private string _imageBase64  = "";
        private string _name         = "Новый предмет";
        private string _description  = "";
        private string _rarity       = "Обычный";
        private int    _quantity     = 1;
        private string _notes        = "";
        private bool   _equipped     = false;

        public string ImageBase64  { get => _imageBase64;  set { _imageBase64  = value; OnPropertyChanged(); } }
        public string Name         { get => _name;         set { _name         = value; OnPropertyChanged(); } }
        public string Description  { get => _description;  set { _description  = value; OnPropertyChanged(); } }
        public string Rarity       { get => _rarity;       set { _rarity       = value; OnPropertyChanged(); } }
        public int    Quantity     { get => _quantity;     set { _quantity     = value; OnPropertyChanged(); } }
        public string Notes        { get => _notes;        set { _notes        = value; OnPropertyChanged(); } }
        public bool   Equipped     { get => _equipped;     set { _equipped     = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
