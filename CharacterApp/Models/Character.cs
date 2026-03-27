// Models/Character.cs  <-- ВАЖНО: namespace ниже = CharacterApp
namespace CharacterApp.Models
{
    public class Character
    {
        // --- базовые поля (как раньше) ---
        public string Hits { get; set; } = "";
        public int Defense { get; set; }
        public int Evasion { get; set; }
        public string SuperHits { get; set; } = "";
        public int Speed { get; set; }
        public int CarryCapacity { get; set; }
        public int Initiative { get; set; }
        public string Mastery { get; set; } = "";
        public string Class { get; set; } = "";
        public string Subclass { get; set; } = "";
        public int Exhaustion { get; set; }
        public int DeathSaves { get; set; }
        public int Vision { get; set; }
        public int Hearing { get; set; }
        public int Aura { get; set; }
        public string Mana { get; set; } = "";
        public string Stamina { get; set; } = "";
        public string CustomField1Label { get; set; } = "";
        public string CustomField1Value { get; set; } = "";
        public string CustomField2Label { get; set; } = "";
        public string CustomField2Value { get; set; } = "";
        public string CustomField3Label { get; set; } = "";
        public string CustomField3Value { get; set; } = "";
        public string CustomField4Label { get; set; } = "";
        public string CustomField4Value { get; set; } = "";
        public string PhotoPath { get; set; } = "";

        // PageDetails
        public string Backstory { get; set; } = "";
        public string Worldview { get; set; } = "";
        public string HeightWeight { get; set; } = "";
        public string BodySize { get; set; } = "";
        public int Age { get; set; }
        public string Appearance { get; set; } = "";
        public string StartBonus1 { get; set; } = "";
        public string StartBonus2 { get; set; } = "";
        public string StartBonus3 { get; set; } = "";
        public int Level { get; set; }
        public int Experience { get; set; }
        public string Awakening { get; set; } = "";
        public string Buff { get; set; } = "";
        public string Debuff { get; set; } = "";

        // ---------------- Legacy equipment fields (для обратной совместимости) ----------------
        public string HeadName { get; set; } = "";
        public string HeadImage { get; set; } = "";
        public bool HeadLocked { get; set; }

        public string BodyName { get; set; } = "";
        public string BodyImage { get; set; } = "";
        public bool BodyLocked { get; set; }

        public string HandsName { get; set; } = "";
        public string HandsImage { get; set; } = "";
        public bool HandsLocked { get; set; }

        public string BeltName { get; set; } = "";
        public string BeltImage { get; set; } = "";
        public bool BeltLocked { get; set; }

        public string LegsName { get; set; } = "";
        public string LegsImage { get; set; } = "";
        public bool LegsLocked { get; set; }

        public string Ring1Name { get; set; } = "";
        public string Ring1Image { get; set; } = "";
        public bool Ring1Locked { get; set; }

        public string Ring2Name { get; set; } = "";
        public string Ring2Image { get; set; } = "";
        public bool Ring2Locked { get; set; }

        public string AmuletName { get; set; } = "";
        public string AmuletImage { get; set; } = "";
        public bool AmuletLocked { get; set; }

        public string Ornament1Name { get; set; } = "";
        public string Ornament1Image { get; set; } = "";
        public bool Ornament1Locked { get; set; }

        public string Ornament2Name { get; set; } = "";
        public string Ornament2Image { get; set; } = "";
        public bool Ornament2Locked { get; set; }

        public string Artifact1Name { get; set; } = "";
        public string Artifact1Image { get; set; } = "";
        public bool Artifact1Locked { get; set; }

        public string Artifact2Name { get; set; } = "";
        public string Artifact2Image { get; set; } = "";
        public bool Artifact2Locked { get; set; }

        // ---------------- New richer properties (EquipmentItem) ----------------
        // правь/удаляй те, что не нужны — но оставь их, чтобы ItemEditor/EquipmentPage могли работать
        public EquipmentItem HeadItem { get; set; }
        public EquipmentItem BodyItem { get; set; }
        public EquipmentItem HandsItem { get; set; }
        public EquipmentItem BeltItem { get; set; }
        public EquipmentItem LegsItem { get; set; }
        public EquipmentItem Ring1Item { get; set; }
        public EquipmentItem Ring2Item { get; set; }
        public EquipmentItem AmuletItem { get; set; }
        public EquipmentItem Ornament1Item { get; set; }
        public EquipmentItem Ornament2Item { get; set; }
        public EquipmentItem Artifact1Item { get; set; }
        public EquipmentItem Artifact2Item { get; set; }

        // --------------- Утилита миграции legacy -> new ----------------
        public void NormalizeItemsFromLegacy()
        {
            if (HeadItem == null && (!string.IsNullOrEmpty(HeadName) || !string.IsNullOrEmpty(HeadImage)))
                HeadItem = new EquipmentItem { Name = HeadName ?? string.Empty, ImagePath = HeadImage ?? string.Empty };

            if (BodyItem == null && (!string.IsNullOrEmpty(BodyName) || !string.IsNullOrEmpty(BodyImage)))
                BodyItem = new EquipmentItem { Name = BodyName ?? string.Empty, ImagePath = BodyImage ?? string.Empty };

            if (HandsItem == null && (!string.IsNullOrEmpty(HandsName) || !string.IsNullOrEmpty(HandsImage)))
                HandsItem = new EquipmentItem { Name = HandsName ?? string.Empty, ImagePath = HandsImage ?? string.Empty };

            if (BeltItem == null && (!string.IsNullOrEmpty(BeltName) || !string.IsNullOrEmpty(BeltImage)))
                BeltItem = new EquipmentItem { Name = BeltName ?? string.Empty, ImagePath = BeltImage ?? string.Empty };

            if (LegsItem == null && (!string.IsNullOrEmpty(LegsName) || !string.IsNullOrEmpty(LegsImage)))
                LegsItem = new EquipmentItem { Name = LegsName ?? string.Empty, ImagePath = LegsImage ?? string.Empty };

            if (Ring1Item == null && (!string.IsNullOrEmpty(Ring1Name) || !string.IsNullOrEmpty(Ring1Image)))
                Ring1Item = new EquipmentItem { Name = Ring1Name ?? string.Empty, ImagePath = Ring1Image ?? string.Empty };

            if (Ring2Item == null && (!string.IsNullOrEmpty(Ring2Name) || !string.IsNullOrEmpty(Ring2Image)))
                Ring2Item = new EquipmentItem { Name = Ring2Name ?? string.Empty, ImagePath = Ring2Image ?? string.Empty };

            if (AmuletItem == null && (!string.IsNullOrEmpty(AmuletName) || !string.IsNullOrEmpty(AmuletImage)))
                AmuletItem = new EquipmentItem { Name = AmuletName ?? string.Empty, ImagePath = AmuletImage ?? string.Empty };

            if (Ornament1Item == null && (!string.IsNullOrEmpty(Ornament1Name) || !string.IsNullOrEmpty(Ornament1Image)))
                Ornament1Item = new EquipmentItem { Name = Ornament1Name ?? string.Empty, ImagePath = Ornament1Image ?? string.Empty };

            if (Ornament2Item == null && (!string.IsNullOrEmpty(Ornament2Name) || !string.IsNullOrEmpty(Ornament2Image)))
                Ornament2Item = new EquipmentItem { Name = Ornament2Name ?? string.Empty, ImagePath = Ornament2Image ?? string.Empty };

            if (Artifact1Item == null && (!string.IsNullOrEmpty(Artifact1Name) || !string.IsNullOrEmpty(Artifact1Image)))
                Artifact1Item = new EquipmentItem { Name = Artifact1Name ?? string.Empty, ImagePath = Artifact1Image ?? string.Empty };

            if (Artifact2Item == null && (!string.IsNullOrEmpty(Artifact2Name) || !string.IsNullOrEmpty(Artifact2Image)))
                Artifact2Item = new EquipmentItem { Name = Artifact2Name ?? string.Empty, ImagePath = Artifact2Image ?? string.Empty };
        }
    }
}
