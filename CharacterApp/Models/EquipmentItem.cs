// Models/EquipmentItem.cs
namespace CharacterApp.Models
{
    public class EquipmentItem
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Путь к картинке на диске. Живёт только в памяти — по нему контролы
        /// рисуют изображение. В файл персонажа уходит ImageBase64.
        /// </summary>
        public string ImagePath { get; set; } = string.Empty;

        /// <summary>
        /// Сама картинка, закодированная в текст. Раньше рядом с файлом
        /// персонажа создавалась папка с картинками, и передавать его
        /// приходилось вместе с ней. Теперь всё лежит внутри одного файла.
        /// </summary>
        public string ImageBase64 { get; set; } = string.Empty;

        // данные, которые сохраняет ItemEditorWindow
        public string Rarity { get; set; } = string.Empty;
        public string Stats { get; set; } = string.Empty;
        public string Effects { get; set; } = string.Empty;
    }
}
