// Models/EquipmentItem.cs
namespace CharacterApp.Models
{
    public class EquipmentItem
    {
        public string Name { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;

        // данные, которые сохраняет ItemEditorWindow
        public string Rarity { get; set; } = string.Empty;
        public string Stats { get; set; } = string.Empty;
        public string Effects { get; set; } = string.Empty;
    }
}
