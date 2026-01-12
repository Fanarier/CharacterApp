namespace CharacterApp
{
    public class Character
    {
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

        // Для PageDetails
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

        // Equipment slots
        public string HeadName { get; set; }
        public string HeadImage { get; set; }
        public bool HeadLocked { get; set; }

        public string BodyName { get; set; }
        public string BodyImage { get; set; }
        public bool BodyLocked { get; set; }

        public string HandsName { get; set; }
        public string HandsImage { get; set; }
        public bool HandsLocked { get; set; }

        public string BeltName { get; set; }
        public string BeltImage { get; set; }
        public bool BeltLocked { get; set; }

        public string LegsName { get; set; }
        public string LegsImage { get; set; }
        public bool LegsLocked { get; set; }

        public string Ring1Name { get; set; }
        public string Ring1Image { get; set; }
        public bool Ring1Locked { get; set; }

        public string Ring2Name { get; set; }
        public string Ring2Image { get; set; }
        public bool Ring2Locked { get; set; }

        public string AmuletName { get; set; }
        public string AmuletImage { get; set; }
        public bool AmuletLocked { get; set; }

        public string Ornament1Name { get; set; }
        public string Ornament1Image { get; set; }
        public bool Ornament1Locked { get; set; }

        public string Ornament2Name { get; set; }
        public string Ornament2Image { get; set; }
        public bool Ornament2Locked { get; set; }

        public string Artifact1Name { get; set; }
        public string Artifact1Image { get; set; }
        public bool Artifact1Locked { get; set; }

        public string Artifact2Name { get; set; }
        public string Artifact2Image { get; set; }
        public bool Artifact2Locked { get; set; }

    }
}
