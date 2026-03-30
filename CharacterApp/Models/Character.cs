// Models/Character.cs
using System.Collections.Generic;

namespace CharacterApp.Models
{
    /// <summary>DTO одной строки таблицы навыков — сериализуется в JSON.</summary>
    public class SkillData
    {
        public string SkillName      { get; set; } = "";
        public int    CategoryIndex  { get; set; }
        public string Description    { get; set; } = "";
        public bool   IsActiveSymbol { get; set; }
    }


    /// <summary>Данные страницы "Характеристики" — атрибуты и навыки.</summary>
    public class StatsData
    {
        // ── Атрибуты: базовое значение + есть ли точка у спасброска ─────────
        public int  StrBase { get; set; } = 10; public bool StrSaveProf { get; set; }
        public int  DexBase { get; set; } = 10; public bool DexSaveProf { get; set; }
        public int  ConBase { get; set; } = 10; public bool ConSaveProf { get; set; }
        public int  IntBase { get; set; } = 10; public bool IntSaveProf { get; set; }
        public int  WisBase { get; set; } = 10; public bool WisSaveProf { get; set; }
        public int  ChaBase { get; set; } = 10; public bool ChaSaveProf { get; set; }

        // ── Навыки: T = значение тренировки, P = точки владения (0-3) ───────
        // Сила
        public int AthT { get; set; } public int AthP { get; set; }
        // Ловкость
        public int AcrT { get; set; } public int AcrP { get; set; }
        public int SlhT { get; set; } public int SlhP { get; set; }
        public int StlT { get; set; } public int StlP { get; set; }
        // Телосложение
        public int EndT { get; set; } public int EndP { get; set; }
        // Интеллект
        public int AnaT { get; set; } public int AnaP { get; set; }
        public int HisT { get; set; } public int HisP { get; set; }
        public int MagT { get; set; } public int MagP { get; set; }
        public int NatT { get; set; } public int NatP { get; set; }
        public int RelT { get; set; } public int RelP { get; set; }
        public int TecT { get; set; } public int TecP { get; set; }
        // Мудрость
        public int AttT { get; set; } public int AttP { get; set; }
        public int SurT { get; set; } public int SurP { get; set; }
        public int MedT { get; set; } public int MedP { get; set; }
        public int InsT { get; set; } public int InsP { get; set; }
        public int AniT { get; set; } public int AniP { get; set; }
        public int MenT { get; set; } public int MenP { get; set; }
        // Харизма
        public int PrfT { get; set; } public int PrfP { get; set; }
        public int ItmT { get; set; } public int ItmP { get; set; }
        public int DecT { get; set; } public int DecP { get; set; }
        public int ChrT { get; set; } public int ChrP { get; set; }
        public int PrsT { get; set; } public int PrsP { get; set; }
    }

    public class Character
    {
        // ── Главная страница ──────────────────────────────────────────────────
        public string CharacterName  { get; set; } = "";
        public string Hits          { get; set; } = "";
        public int    Defense       { get; set; }
        public int    Evasion       { get; set; }
        public string SuperHits     { get; set; } = "";
        public int    Speed         { get; set; }
        public int    CarryCapacity { get; set; }
        public int    Initiative    { get; set; }
        public string Mastery       { get; set; } = "";
        public string Class         { get; set; } = "";
        public string Subclass      { get; set; } = "";
        public int    Exhaustion    { get; set; }
        public int    DeathSaves    { get; set; }
        public int    Vision        { get; set; }
        public int    Hearing       { get; set; }
        public int    Aura          { get; set; }
        public string Mana          { get; set; } = "";
        public string Stamina       { get; set; } = "";

        public string CustomField1Label { get; set; } = "";
        public string CustomField1Value { get; set; } = "";
        public string CustomField2Label { get; set; } = "";
        public string CustomField2Value { get; set; } = "";
        public string CustomField3Label { get; set; } = "";
        public string CustomField3Value { get; set; } = "";
        public string CustomField4Label { get; set; } = "";
        public string CustomField4Value { get; set; } = "";

        public string PhotoPath { get; set; } = "";

        // ── Предыстория ───────────────────────────────────────────────────────
        public string Backstory    { get; set; } = "";
        public string Worldview    { get; set; } = "";
        public string HeightWeight { get; set; } = "";
        public string BodySize     { get; set; } = "";
        public int    Age          { get; set; }
        public string Appearance   { get; set; } = "";
        public string StartBonus1  { get; set; } = "";
        public string StartBonus2  { get; set; } = "";
        public string StartBonus3  { get; set; } = "";
        public int    Level        { get; set; }
        public int    Experience   { get; set; }
        public string Awakening    { get; set; } = "";
        public string Buff         { get; set; } = "";
        public string Debuff       { get; set; } = "";

        // ── Страница характеристик ────────────────────────────────────────────
        public StatsData Stats { get; set; } = new StatsData();

        // ── Навыки ────────────────────────────────────────────────────────────
        public List<SkillData> Skills { get; set; } = new List<SkillData>();

        // ── Экипировка (новые поля) ───────────────────────────────────────────
        public EquipmentItem? HeadItem      { get; set; }
        public EquipmentItem? BodyItem      { get; set; }
        public EquipmentItem? HandsItem     { get; set; }
        public EquipmentItem? BeltItem      { get; set; }
        public EquipmentItem? LegsItem      { get; set; }
        public EquipmentItem? Ring1Item     { get; set; }
        public EquipmentItem? Ring2Item     { get; set; }
        public EquipmentItem? AmuletItem    { get; set; }
        public EquipmentItem? Ornament1Item { get; set; }
        public EquipmentItem? Ornament2Item { get; set; }
        public EquipmentItem? Artifact1Item { get; set; }
        public EquipmentItem? Artifact2Item { get; set; }

        // ── Экипировка (legacy — нужны EquipmentPage + обратная совместимость) ─
        public string HeadName        { get; set; } = "";
        public string HeadImage       { get; set; } = "";
        public bool   HeadLocked      { get; set; }
        public string BodyName        { get; set; } = "";
        public string BodyImage       { get; set; } = "";
        public bool   BodyLocked      { get; set; }
        public string HandsName       { get; set; } = "";
        public string HandsImage      { get; set; } = "";
        public bool   HandsLocked     { get; set; }
        public string BeltName        { get; set; } = "";
        public string BeltImage       { get; set; } = "";
        public bool   BeltLocked      { get; set; }
        public string LegsName        { get; set; } = "";
        public string LegsImage       { get; set; } = "";
        public bool   LegsLocked      { get; set; }
        public string Ring1Name       { get; set; } = "";
        public string Ring1Image      { get; set; } = "";
        public bool   Ring1Locked     { get; set; }
        public string Ring2Name       { get; set; } = "";
        public string Ring2Image      { get; set; } = "";
        public bool   Ring2Locked     { get; set; }
        public string AmuletName      { get; set; } = "";
        public string AmuletImage     { get; set; } = "";
        public bool   AmuletLocked    { get; set; }
        public string Ornament1Name   { get; set; } = "";
        public string Ornament1Image  { get; set; } = "";
        public bool   Ornament1Locked { get; set; }
        public string Ornament2Name   { get; set; } = "";
        public string Ornament2Image  { get; set; } = "";
        public bool   Ornament2Locked { get; set; }
        public string Artifact1Name   { get; set; } = "";
        public string Artifact1Image  { get; set; } = "";
        public bool   Artifact1Locked { get; set; }
        public string Artifact2Name   { get; set; } = "";
        public string Artifact2Image  { get; set; } = "";
        public bool   Artifact2Locked { get; set; }

        // ── Миграция legacy → EquipmentItem ──────────────────────────────────
        public void NormalizeItemsFromLegacy()
        {
            if (HeadItem == null && (!string.IsNullOrEmpty(HeadName) || !string.IsNullOrEmpty(HeadImage)))
                HeadItem = new EquipmentItem { Name = HeadName, ImagePath = HeadImage };
            if (BodyItem == null && (!string.IsNullOrEmpty(BodyName) || !string.IsNullOrEmpty(BodyImage)))
                BodyItem = new EquipmentItem { Name = BodyName, ImagePath = BodyImage };
            if (HandsItem == null && (!string.IsNullOrEmpty(HandsName) || !string.IsNullOrEmpty(HandsImage)))
                HandsItem = new EquipmentItem { Name = HandsName, ImagePath = HandsImage };
            if (BeltItem == null && (!string.IsNullOrEmpty(BeltName) || !string.IsNullOrEmpty(BeltImage)))
                BeltItem = new EquipmentItem { Name = BeltName, ImagePath = BeltImage };
            if (LegsItem == null && (!string.IsNullOrEmpty(LegsName) || !string.IsNullOrEmpty(LegsImage)))
                LegsItem = new EquipmentItem { Name = LegsName, ImagePath = LegsImage };
            if (Ring1Item == null && (!string.IsNullOrEmpty(Ring1Name) || !string.IsNullOrEmpty(Ring1Image)))
                Ring1Item = new EquipmentItem { Name = Ring1Name, ImagePath = Ring1Image };
            if (Ring2Item == null && (!string.IsNullOrEmpty(Ring2Name) || !string.IsNullOrEmpty(Ring2Image)))
                Ring2Item = new EquipmentItem { Name = Ring2Name, ImagePath = Ring2Image };
            if (AmuletItem == null && (!string.IsNullOrEmpty(AmuletName) || !string.IsNullOrEmpty(AmuletImage)))
                AmuletItem = new EquipmentItem { Name = AmuletName, ImagePath = AmuletImage };
            if (Ornament1Item == null && (!string.IsNullOrEmpty(Ornament1Name) || !string.IsNullOrEmpty(Ornament1Image)))
                Ornament1Item = new EquipmentItem { Name = Ornament1Name, ImagePath = Ornament1Image };
            if (Ornament2Item == null && (!string.IsNullOrEmpty(Ornament2Name) || !string.IsNullOrEmpty(Ornament2Image)))
                Ornament2Item = new EquipmentItem { Name = Ornament2Name, ImagePath = Ornament2Image };
            if (Artifact1Item == null && (!string.IsNullOrEmpty(Artifact1Name) || !string.IsNullOrEmpty(Artifact1Image)))
                Artifact1Item = new EquipmentItem { Name = Artifact1Name, ImagePath = Artifact1Image };
            if (Artifact2Item == null && (!string.IsNullOrEmpty(Artifact2Name) || !string.IsNullOrEmpty(Artifact2Image)))
                Artifact2Item = new EquipmentItem { Name = Artifact2Name, ImagePath = Artifact2Image };
        }
    }
}
