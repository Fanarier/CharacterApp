// Pages/EquipmentPage.cs
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CharacterApp.Controls;
using CharacterApp.Models;

namespace CharacterApp.Pages
{
    public partial class EquipmentPage : Page, ISaveLoad
    {
        private readonly List<string> _slotKeys = new List<string>
        {
            "Head","Body","Hands","Waist","Legs",
            "Ring1","Ring2","Amulet","Trinket1","Artifact1","Artifact2"
        };

        public EquipmentPage()
        {
            InitializeComponent();
            Loaded += EquipmentPage_Loaded;
        }

        private void EquipmentPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (SlotsHost.Items.Count == 0) InitializeSlots();
        }

        private void InitializeSlots()
        {
            SlotsHost.Items.Clear();
            foreach (var k in _slotKeys)
            {
                var slot = new EquipSlotControl
                {
                    SlotKey = k,
                    Title = k,
                    MinWidth = 140,
                    MinHeight = 170
                };
                SlotsHost.Items.Add(slot);
            }
        }

        // Вместо локальной реализации — делегируем MainWindow
        public void QuickSave() => (Application.Current.MainWindow as MainWindow)?.SaveAll();
        public void SaveAs() => (Application.Current.MainWindow as MainWindow)?.SaveAllAs();
        public void LoadJSON() => (Application.Current.MainWindow as MainWindow)?.LoadAll();

        public void ApplyCharacter(Character c)
        {
            if (c == null) return;

            // используем Item-версии, если они есть, иначе — legacy
            ApplyToSlot("Head", c.HeadItem ?? (string.IsNullOrEmpty(c.HeadName) && string.IsNullOrEmpty(c.HeadImage) ? null : new EquipmentItem { Name = c.HeadName, ImagePath = c.HeadImage }), c.HeadLocked);
            ApplyToSlot("Body", c.BodyItem ?? (string.IsNullOrEmpty(c.BodyName) && string.IsNullOrEmpty(c.BodyImage) ? null : new EquipmentItem { Name = c.BodyName, ImagePath = c.BodyImage }), c.BodyLocked);
            ApplyToSlot("Hands", c.HandsItem ?? (string.IsNullOrEmpty(c.HandsName) && string.IsNullOrEmpty(c.HandsImage) ? null : new EquipmentItem { Name = c.HandsName, ImagePath = c.HandsImage }), c.HandsLocked);
            ApplyToSlot("Waist", c.BeltItem ?? (string.IsNullOrEmpty(c.BeltName) && string.IsNullOrEmpty(c.BeltImage) ? null : new EquipmentItem { Name = c.BeltName, ImagePath = c.BeltImage }), c.BeltLocked);
            ApplyToSlot("Legs", c.LegsItem ?? (string.IsNullOrEmpty(c.LegsName) && string.IsNullOrEmpty(c.LegsImage) ? null : new EquipmentItem { Name = c.LegsName, ImagePath = c.LegsImage }), c.LegsLocked);

            ApplyToSlot("Ring1", c.Ring1Item ?? (string.IsNullOrEmpty(c.Ring1Name) && string.IsNullOrEmpty(c.Ring1Image) ? null : new EquipmentItem { Name = c.Ring1Name, ImagePath = c.Ring1Image }), c.Ring1Locked);
            ApplyToSlot("Ring2", c.Ring2Item ?? (string.IsNullOrEmpty(c.Ring2Name) && string.IsNullOrEmpty(c.Ring2Image) ? null : new EquipmentItem { Name = c.Ring2Name, ImagePath = c.Ring2Image }), c.Ring2Locked);
            ApplyToSlot("Amulet", c.AmuletItem ?? (string.IsNullOrEmpty(c.AmuletName) && string.IsNullOrEmpty(c.AmuletImage) ? null : new EquipmentItem { Name = c.AmuletName, ImagePath = c.AmuletImage }), c.AmuletLocked);

            ApplyToSlot("Trinket1", c.Ornament1Item ?? (string.IsNullOrEmpty(c.Ornament1Name) && string.IsNullOrEmpty(c.Ornament1Image) ? null : new EquipmentItem { Name = c.Ornament1Name, ImagePath = c.Ornament1Image }), c.Ornament1Locked);
            ApplyToSlot("Artifact1", c.Artifact1Item ?? (string.IsNullOrEmpty(c.Artifact1Name) && string.IsNullOrEmpty(c.Artifact1Image) ? null : new EquipmentItem { Name = c.Artifact1Name, ImagePath = c.Artifact1Image }), c.Artifact1Locked);
            ApplyToSlot("Artifact2", c.Artifact2Item ?? (string.IsNullOrEmpty(c.Artifact2Name) && string.IsNullOrEmpty(c.Artifact2Image) ? null : new EquipmentItem { Name = c.Artifact2Name, ImagePath = c.Artifact2Image }), c.Artifact2Locked);
        }

        public void FillCharacter(Character c)
        {
            if (c == null) return;

            // просто присваиваем Item-поля и поддерживаем legacy поля для обратной совместимости
            var head = GetSlotValue("Head");
            c.HeadItem = head;
            c.HeadName = head?.Name ?? string.Empty;
            c.HeadImage = head?.ImagePath ?? string.Empty;
            c.HeadLocked = GetSlotLocked("Head");

            var body = GetSlotValue("Body");
            c.BodyItem = body;
            c.BodyName = body?.Name ?? string.Empty;
            c.BodyImage = body?.ImagePath ?? string.Empty;
            c.BodyLocked = GetSlotLocked("Body");

            var hands = GetSlotValue("Hands");
            c.HandsItem = hands;
            c.HandsName = hands?.Name ?? string.Empty;
            c.HandsImage = hands?.ImagePath ?? string.Empty;
            c.HandsLocked = GetSlotLocked("Hands");

            var belt = GetSlotValue("Waist");
            c.BeltItem = belt;
            c.BeltName = belt?.Name ?? string.Empty;
            c.BeltImage = belt?.ImagePath ?? string.Empty;
            c.BeltLocked = GetSlotLocked("Waist");

            var legs = GetSlotValue("Legs");
            c.LegsItem = legs;
            c.LegsName = legs?.Name ?? string.Empty;
            c.LegsImage = legs?.ImagePath ?? string.Empty;
            c.LegsLocked = GetSlotLocked("Legs");

            var r1 = GetSlotValue("Ring1");
            c.Ring1Item = r1;
            c.Ring1Name = r1?.Name ?? string.Empty;
            c.Ring1Image = r1?.ImagePath ?? string.Empty;
            c.Ring1Locked = GetSlotLocked("Ring1");

            var r2 = GetSlotValue("Ring2");
            c.Ring2Item = r2;
            c.Ring2Name = r2?.Name ?? string.Empty;
            c.Ring2Image = r2?.ImagePath ?? string.Empty;
            c.Ring2Locked = GetSlotLocked("Ring2");

            var am = GetSlotValue("Amulet");
            c.AmuletItem = am;
            c.AmuletName = am?.Name ?? string.Empty;
            c.AmuletImage = am?.ImagePath ?? string.Empty;
            c.AmuletLocked = GetSlotLocked("Amulet");

            var t1 = GetSlotValue("Trinket1");
            c.Ornament1Item = t1;
            c.Ornament1Name = t1?.Name ?? string.Empty;
            c.Ornament1Image = t1?.ImagePath ?? string.Empty;
            c.Ornament1Locked = GetSlotLocked("Trinket1");

            var a1 = GetSlotValue("Artifact1");
            c.Artifact1Item = a1;
            c.Artifact1Name = a1?.Name ?? string.Empty;
            c.Artifact1Image = a1?.ImagePath ?? string.Empty;
            c.Artifact1Locked = GetSlotLocked("Artifact1");

            var a2 = GetSlotValue("Artifact2");
            c.Artifact2Item = a2;
            c.Artifact2Name = a2?.Name ?? string.Empty;
            c.Artifact2Image = a2?.ImagePath ?? string.Empty;
            c.Artifact2Locked = GetSlotLocked("Artifact2");
        }

        // ApplyToSlot / ClearSlotByKey / helpers — оставляем как есть (в твоём проекте они уже реализованы)
        public void ApplyToSlot(string key, EquipmentItem item, bool locked)
        {
            if (string.IsNullOrEmpty(key)) return;

            foreach (var obj in SlotsHost.Items)
            {
                if (obj is EquipSlotControl s && s.SlotKey == key)
                {
                    if (item == null)
                    {
                        s.ItemData = null;
                        s.ItemName = string.Empty;
                        s.ItemImagePath = string.Empty;
                        s.IsLocked = locked;
                    }
                    else
                    {
                        s.ItemData = item;
                        s.ItemName = item.Name ?? string.Empty;
                        s.ItemImagePath = item.ImagePath ?? string.Empty;
                        s.IsLocked = locked;
                    }
                    return;
                }
            }
        }

        public void ApplyToSlot(string key, string name, string imagePath, bool locked)
        {
            var item = string.IsNullOrEmpty(name) && string.IsNullOrEmpty(imagePath)
                ? null
                : new EquipmentItem { Name = name ?? string.Empty, ImagePath = imagePath ?? string.Empty };

            ApplyToSlot(key, item, locked);
        }

        public void ClearSlotByKey(string slotKey)
        {
            if (string.IsNullOrEmpty(slotKey)) return;
            foreach (var obj in SlotsHost.Items)
            {
                if (obj is EquipSlotControl s && s.SlotKey == slotKey)
                {
                    s.ItemData = null;
                    s.ItemName = string.Empty;
                    s.ItemImagePath = string.Empty;
                    return;
                }
            }
        }

        private EquipmentItem GetSlotValue(string key)
        {
            foreach (var obj in SlotsHost.Items)
            {
                if (obj is EquipSlotControl s && s.SlotKey == key) return s.ItemData;
            }
            return null;
        }

        private bool GetSlotLocked(string key)
        {
            foreach (var obj in SlotsHost.Items)
            {
                if (obj is EquipSlotControl s && s.SlotKey == key) return s.IsLocked;
            }
            return false;
        }
    }
}
