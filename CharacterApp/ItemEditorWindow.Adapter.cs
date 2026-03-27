// ItemEditorWindow.Adapter.cs
using CharacterApp.Dialogs;
using CharacterApp.Models;

namespace CharacterApp
{
    // Простой адаптер, чтобы рефлексия могла найти тип "CharacterApp.ItemEditorWindow"
    // и при этом использовать реальную реализацию в Dialogs.ItemEditorWindow.
    public class ItemEditorWindow : Dialogs.ItemEditorWindow
    {
        public ItemEditorWindow(EquipmentItem item) : base(item) { }
    }
}
