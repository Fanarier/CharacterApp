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
        // Trinket2 = Ornament2 (второй слот украшений)
        private readonly List<string> _slotKeys = new List<string>
        {
            "Head","Body","Hands","Waist","Legs",
            "Ring1","Ring2","Amulet","Trinket1","Trinket2","Artifact1","Artifact2"
        };

        // Русские названия слотов
        private static readonly Dictionary<string, string> SlotTitles = new Dictionary<string, string>
        {
            { "Head",     "Голова"      },
            { "Body",     "Тело"        },
            { "Hands",    "Руки"        },
            { "Waist",    "Пояс"        },
            { "Legs",     "Ноги"        },
            { "Ring1",    "Кольцо 1"    },
            { "Ring2",    "Кольцо 2"    },
            { "Amulet",   "Амулет"      },
            { "Trinket1", "Украшение 1" },
            { "Trinket2", "Украшение 2" },
            { "Artifact1","Артефакт 1"  },
            { "Artifact2","Артефакт 2"  },
        };

        public EquipmentPage()
        {
            InitializeComponent();
            InitializeSlots();
        }

        private void InitializeSlots()
        {
            SlotsHost.Items.Clear();
            foreach (var k in _slotKeys)
            {
                var slot = new EquipSlotControl
                {
                    SlotKey  = k,
                    Title    = SlotTitles.TryGetValue(k, out var t) ? t : k,
                    MinWidth  = 140,
                    MinHeight = 170
                };
                SlotsHost.Items.Add(slot);
            }
        }

        public void QuickSave() => (System.Windows.Application.Current.MainWindow as MainWindow)?.SaveAll();
        public void SaveAs()    => (System.Windows.Application.Current.MainWindow as MainWindow)?.SaveAllAs();
        public void LoadJSON()  => (System.Windows.Application.Current.MainWindow as MainWindow)?.LoadAll();

        public void ApplyCharacter(Character c)
        {
            if (c == null) return;
            ApplyToSlot("Head",     c.HeadItem,      c.HeadLocked);
            ApplyToSlot("Body",     c.BodyItem,      c.BodyLocked);
            ApplyToSlot("Hands",    c.HandsItem,     c.HandsLocked);
            ApplyToSlot("Waist",    c.BeltItem,      c.BeltLocked);
            ApplyToSlot("Legs",     c.LegsItem,      c.LegsLocked);
            ApplyToSlot("Ring1",    c.Ring1Item,     c.Ring1Locked);
            ApplyToSlot("Ring2",    c.Ring2Item,     c.Ring2Locked);
            ApplyToSlot("Amulet",   c.AmuletItem,    c.AmuletLocked);
            ApplyToSlot("Trinket1", c.Ornament1Item, c.Ornament1Locked);
            ApplyToSlot("Trinket2", c.Ornament2Item, c.Ornament2Locked);
            ApplyToSlot("Artifact1",c.Artifact1Item, c.Artifact1Locked);
            ApplyToSlot("Artifact2",c.Artifact2Item, c.Artifact2Locked);
        }

        public void FillCharacter(Character c)
        {
            if (c == null) return;

            var head = GetSlotValue("Head");
            c.HeadItem = head; c.HeadName = head?.Name ?? ""; c.HeadImage = head?.ImagePath ?? ""; c.HeadLocked = GetSlotLocked("Head");

            var body = GetSlotValue("Body");
            c.BodyItem = body; c.BodyName = body?.Name ?? ""; c.BodyImage = body?.ImagePath ?? ""; c.BodyLocked = GetSlotLocked("Body");

            var hands = GetSlotValue("Hands");
            c.HandsItem = hands; c.HandsName = hands?.Name ?? ""; c.HandsImage = hands?.ImagePath ?? ""; c.HandsLocked = GetSlotLocked("Hands");

            var belt = GetSlotValue("Waist");
            c.BeltItem = belt; c.BeltName = belt?.Name ?? ""; c.BeltImage = belt?.ImagePath ?? ""; c.BeltLocked = GetSlotLocked("Waist");

            var legs = GetSlotValue("Legs");
            c.LegsItem = legs; c.LegsName = legs?.Name ?? ""; c.LegsImage = legs?.ImagePath ?? ""; c.LegsLocked = GetSlotLocked("Legs");

            var r1 = GetSlotValue("Ring1");
            c.Ring1Item = r1; c.Ring1Name = r1?.Name ?? ""; c.Ring1Image = r1?.ImagePath ?? ""; c.Ring1Locked = GetSlotLocked("Ring1");

            var r2 = GetSlotValue("Ring2");
            c.Ring2Item = r2; c.Ring2Name = r2?.Name ?? ""; c.Ring2Image = r2?.ImagePath ?? ""; c.Ring2Locked = GetSlotLocked("Ring2");

            var am = GetSlotValue("Amulet");
            c.AmuletItem = am; c.AmuletName = am?.Name ?? ""; c.AmuletImage = am?.ImagePath ?? ""; c.AmuletLocked = GetSlotLocked("Amulet");

            var t1 = GetSlotValue("Trinket1");
            c.Ornament1Item = t1; c.Ornament1Name = t1?.Name ?? ""; c.Ornament1Image = t1?.ImagePath ?? ""; c.Ornament1Locked = GetSlotLocked("Trinket1");

            var t2 = GetSlotValue("Trinket2");
            c.Ornament2Item = t2; c.Ornament2Name = t2?.Name ?? ""; c.Ornament2Image = t2?.ImagePath ?? ""; c.Ornament2Locked = GetSlotLocked("Trinket2");

            var a1 = GetSlotValue("Artifact1");
            c.Artifact1Item = a1; c.Artifact1Name = a1?.Name ?? ""; c.Artifact1Image = a1?.ImagePath ?? ""; c.Artifact1Locked = GetSlotLocked("Artifact1");

            var a2 = GetSlotValue("Artifact2");
            c.Artifact2Item = a2; c.Artifact2Name = a2?.Name ?? ""; c.Artifact2Image = a2?.ImagePath ?? ""; c.Artifact2Locked = GetSlotLocked("Artifact2");
        }

        public void ResetAll()
        {
            foreach (var obj in SlotsHost.Items)
                if (obj is EquipSlotControl s)
                { s.ItemData = null; s.ItemName = ""; s.ItemImagePath = ""; s.IsLocked = false; }
        }

        public void ApplyToSlot(string key, EquipmentItem? item, bool locked)
        {
            if (string.IsNullOrEmpty(key)) return;
            foreach (var obj in SlotsHost.Items)
                if (obj is EquipSlotControl s && s.SlotKey == key)
                { s.ItemData = item; s.ItemName = item?.Name ?? ""; s.ItemImagePath = item?.ImagePath ?? ""; s.IsLocked = locked; return; }
        }

        public void ClearSlotByKey(string slotKey)
        {
            if (string.IsNullOrEmpty(slotKey)) return;
            foreach (var obj in SlotsHost.Items)
                if (obj is EquipSlotControl s && s.SlotKey == slotKey)
                { s.ItemData = null; s.ItemName = ""; s.ItemImagePath = ""; return; }
        }

        private EquipmentItem? GetSlotValue(string key)
        {
            foreach (var obj in SlotsHost.Items)
                if (obj is EquipSlotControl s && s.SlotKey == key) return s.ItemData;
            return null;
        }

        private bool GetSlotLocked(string key)
        {
            foreach (var obj in SlotsHost.Items)
                if (obj is EquipSlotControl s && s.SlotKey == key) return s.IsLocked;
            return false;
        }
    }
}
