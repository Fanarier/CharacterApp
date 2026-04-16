using System.ComponentModel;
using System.Runtime.CompilerServices;

public class ItemModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private int _rarity;
    private string _description = string.Empty;

    public string Name
    {
        get => _name;
        set { _name = value; OnChanged(); }
    }

    public int Rarity
    {
        get => _rarity;
        set { _rarity = value; OnChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
