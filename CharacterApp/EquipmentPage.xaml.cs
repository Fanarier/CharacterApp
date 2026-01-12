using System;
using System.IO;
using System.Windows.Controls;

namespace CharacterApp
{
    public partial class EquipmentPage : Page
    {
        public EquipmentPage()
        {
            InitializeComponent();

            // по умолчанию заблокируем опционные слоты:
            Slot_Orn1.IsLocked = true;
            Slot_Orn2.IsLocked = true;
            Slot_Art2.IsLocked = true;
        }

        // Заполнить UI из модели Character
        public void ApplyCharacter(Character c)
        {
            Slot_Head.ItemName = c.HeadName; Slot_Head.ImagePath = c.HeadImage; Slot_Head.IsLocked = c.HeadLocked;
            Slot_Body.ItemName = c.BodyName; Slot_Body.ImagePath = c.BodyImage; Slot_Body.IsLocked = c.BodyLocked;
            Slot_Hands.ItemName = c.HandsName; Slot_Hands.ImagePath = c.HandsImage; Slot_Hands.IsLocked = c.HandsLocked;
            Slot_Belt.ItemName = c.BeltName; Slot_Belt.ImagePath = c.BeltImage; Slot_Belt.IsLocked = c.BeltLocked;
            Slot_Legs.ItemName = c.LegsName; Slot_Legs.ImagePath = c.LegsImage; Slot_Legs.IsLocked = c.LegsLocked;

            Slot_Ring1.ItemName = c.Ring1Name; Slot_Ring1.ImagePath = c.Ring1Image; Slot_Ring1.IsLocked = c.Ring1Locked;
            Slot_Ring2.ItemName = c.Ring2Name; Slot_Ring2.ImagePath = c.Ring2Image; Slot_Ring2.IsLocked = c.Ring2Locked;
            Slot_Amulet.ItemName = c.AmuletName; Slot_Amulet.ImagePath = c.AmuletImage; Slot_Amulet.IsLocked = c.AmuletLocked;

            Slot_Orn1.ItemName = c.Ornament1Name; Slot_Orn1.ImagePath = c.Ornament1Image; Slot_Orn1.IsLocked = c.Ornament1Locked;
            Slot_Orn2.ItemName = c.Ornament2Name; Slot_Orn2.ImagePath = c.Ornament2Image; Slot_Orn2.IsLocked = c.Ornament2Locked;

            Slot_Art1.ItemName = c.Artifact1Name; Slot_Art1.ImagePath = c.Artifact1Image; Slot_Art1.IsLocked = c.Artifact1Locked;
            Slot_Art2.ItemName = c.Artifact2Name; Slot_Art2.ImagePath = c.Artifact2Image; Slot_Art2.IsLocked = c.Artifact2Locked;
        }

        // Забрать значения из UI в Character
        public void FillCharacter(Character c)
        {
            c.HeadName = Slot_Head.ItemName; c.HeadImage = NormalizePath(Slot_Head.ImagePath); c.HeadLocked = Slot_Head.IsLocked;
            c.BodyName = Slot_Body.ItemName; c.BodyImage = NormalizePath(Slot_Body.ImagePath); c.BodyLocked = Slot_Body.IsLocked;
            c.HandsName = Slot_Hands.ItemName; c.HandsImage = NormalizePath(Slot_Hands.ImagePath); c.HandsLocked = Slot_Hands.IsLocked;
            c.BeltName = Slot_Belt.ItemName; c.BeltImage = NormalizePath(Slot_Belt.ImagePath); c.BeltLocked = Slot_Belt.IsLocked;
            c.LegsName = Slot_Legs.ItemName; c.LegsImage = NormalizePath(Slot_Legs.ImagePath); c.LegsLocked = Slot_Legs.IsLocked;

            c.Ring1Name = Slot_Ring1.ItemName; c.Ring1Image = NormalizePath(Slot_Ring1.ImagePath); c.Ring1Locked = Slot_Ring1.IsLocked;
            c.Ring2Name = Slot_Ring2.ItemName; c.Ring2Image = NormalizePath(Slot_Ring2.ImagePath); c.Ring2Locked = Slot_Ring2.IsLocked;
            c.AmuletName = Slot_Amulet.ItemName; c.AmuletImage = NormalizePath(Slot_Amulet.ImagePath); c.AmuletLocked = Slot_Amulet.IsLocked;

            c.Ornament1Name = Slot_Orn1.ItemName; c.Ornament1Image = NormalizePath(Slot_Orn1.ImagePath); c.Ornament1Locked = Slot_Orn1.IsLocked;
            c.Ornament2Name = Slot_Orn2.ItemName; c.Ornament2Image = NormalizePath(Slot_Orn2.ImagePath); c.Ornament2Locked = Slot_Orn2.IsLocked;

            c.Artifact1Name = Slot_Art1.ItemName; c.Artifact1Image = NormalizePath(Slot_Art1.ImagePath); c.Artifact1Locked = Slot_Art1.IsLocked;
            c.Artifact2Name = Slot_Art2.ItemName; c.Artifact2Image = NormalizePath(Slot_Art2.ImagePath); c.Artifact2Locked = Slot_Art2.IsLocked;
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            // сохраняем абсолютный путь. По желанию можно копировать картинки в папку приложения.
            return path;
        }
    }
}
