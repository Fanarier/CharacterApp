// Helpers/PdfExporter.cs — QuestPDF full export
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterApp.Models;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CharacterApp.Helpers
{
    public static class PdfExporter
    {
        static PdfExporter() => QuestPDF.Settings.License = LicenseType.Community;

        public static void Export(Character c)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Экспорт в PDF", Filter = "PDF (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                FileName = $"{Clean(c.CharacterName ?? "Персонаж")}_{DateTime.Now:yyyyMMdd}"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                new CharacterDoc(c).GeneratePdf(dlg.FileName);
                System.Windows.MessageBox.Show($"PDF сохранён:\n{dlg.FileName}", "Готово",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Ошибка: " + ex.Message, "Ошибка",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        private static string Clean(string s) =>
            string.Join("_", s.Split(Path.GetInvalidFileNameChars()));
    }

    internal class CharacterDoc : IDocument
    {
        private readonly Character _c;
        private const string Purple = "#7B4FA6";
        private const string Blue   = "#3A6FA0";
        private const string Muted  = "#888888";
        private const string Line   = "#DDDDDD";
        private const string AltRow = "#F8F5FF";

        public CharacterDoc(Character c) => _c = c;
        public DocumentMetadata GetMetadata() => new() { Title = _c.CharacterName ?? "Персонаж" };
        public DocumentSettings GetSettings() => new() { ContentDirection = ContentDirection.LeftToRight };

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.Header().Element(Header);
                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    CoreStats(col);
                    RatingSection(col);
                    Attributes(col);
                    SavingThrows(col);
                    SkillTable(col);
                    RadarChart(col);
                    if (_c.Skills?.Count > 0)     Skills(col, "Активные навыки",   _c.Skills);
                    if (_c.PassiveSkills?.Count > 0) Skills(col, "Пассивные навыки", _c.PassiveSkills);
                    if (_c.Attacks?.Count > 0)    Attacks(col);
                    Traits(col);
                    HumanoidTraits(col);
                    Proficiencies(col);
                    Equipment(col);
                    Inventory(col);
                    CustomSheets(col);
                    TextBlock(col, "Предыстория",  _c.Backstory);
                    TextBlock(col, "Мировоззрение",_c.Worldview);
                    TextBlock(col, "Внешность",    _c.Appearance);
                    TextBlock(col, "Пробуждение",  _c.Awakening);
                    TextBlock(col, "Баф",          _c.Buff);
                    TextBlock(col, "Дебаф",        _c.Debuff);
                    if (_c.JournalEntries?.Count > 0) Journal(col);
                });
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("CharacterApp  •  ").FontSize(8).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    t.Span(" / ").FontSize(8).FontColor(Muted);
                    t.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        }

        // ── Header ────────────────────────────────────────────────────────────
        void Header(IContainer e) => e.PaddingBottom(8).Column(col =>
        {
            col.Spacing(2);
            // Name + photo on same row
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_c.CharacterName ?? "Безымянный")
                             .FontSize(22).Bold().FontColor(Purple);
                    c.Item().Text(t =>
                    {
                        t.Span(_c.Class ?? "").FontSize(12).FontColor(Blue);
                        if (!string.IsNullOrEmpty(_c.Subclass))
                            t.Span($" / {_c.Subclass}").FontSize(12).FontColor(Blue);
                        t.Span($"   Ур.{_c.Level}  Опыт:{_c.Experience}").FontSize(11).FontColor(Muted);
                    });
                    // Detail fields
                    var details = new (string l, string v)[]
                    {
                        ("Рост/Вес", _c.HeightWeight), ("Размер тела", _c.BodySize),
                        ("Возраст",  _c.Age > 0 ? _c.Age.ToString() : "-"),
                        ("Стартовый бонус 1", _c.StartBonus1),
                        ("Стартовый бонус 2", _c.StartBonus2),
                        ("Стартовый бонус 3", _c.StartBonus3),
                    };
                    foreach (var (l, v) in details)
                        if (!string.IsNullOrWhiteSpace(v))
                            c.Item().Text($"{l}: {v}").FontSize(10).FontColor(Muted);
                });

                // Portrait photo
                if (!string.IsNullOrEmpty(_c.PhotoPath) && File.Exists(_c.PhotoPath))
                {
                    row.ConstantItem(90).PaddingLeft(8).Image(_c.PhotoPath)
                       .FitArea();
                }
            });
            col.Item().LineHorizontal(1.5f).LineColor(Purple);
        });

        // ── Core stats ────────────────────────────────────────────────────────
        void CoreStats(ColumnDescriptor col)
        {
            SH(col, "Основные характеристики", Purple);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2); cd.RelativeColumn(2);
                    cd.RelativeColumn(2); cd.RelativeColumn(2);
                });
                SC(t, "Хиты",            _c.Hits ?? "-");
                SC(t, "КД",              _c.Defense.ToString());
                SC(t, "Сверх-хиты",     _c.SuperHits ?? "-");
                SC(t, "Уклонение",       _c.Evasion.ToString());
                SC(t, "Скорость",        _c.Speed.ToString());
                SC(t, "Инициатива",      _c.Initiative.ToString());
                SC(t, "Грузоподъём.",    _c.CarryCapacity.ToString());
                SC(t, "Мастерство",      _c.Mastery ?? "-");
                if (!string.IsNullOrEmpty(_c.Mana))
                { SC(t, "Мана",          _c.Mana); SC(t, "", ""); }
            });
        }

        // ── Rating (exhaustion, death saves, perception) ─────────────────────
        void RatingSection(ColumnDescriptor col)
        {
            SH(col, "Рейтинговые характеристики", Purple);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(4); });
                RatingRow(t, "Изнурение",           _c.Exhaustion, 5, "■");
                RatingRow(t, "Спасброски от смерти", _c.DeathSaves, 3, "♥");
                RatingRow(t, "Восприятие (Зрение)",  _c.Vision,    3, "◉");
                RatingRow(t, "Восприятие (Слух)",    _c.Hearing,   3, "◈");
                RatingRow(t, "Восприятие (Аура)",    _c.Aura,      3, "✦");
            });
        }
        static void RatingRow(TableDescriptor t, string label, int val, int max, string icon)
        {
            t.Cell().Padding(3).Text(label).FontSize(10).FontColor("#444");
            t.Cell().Padding(3).Text(txt =>
            {
                for (int i = 0; i < max; i++)
                {
                    if (i < val) txt.Span(icon + " ").FontSize(12).FontColor(Purple);
                    else         txt.Span("○ ").FontSize(12).FontColor("#CCCCCC");
                }
            });
        }

        // ── Attributes ────────────────────────────────────────────────────────
        // ── Спасброски и навыки ──────────────────────────────────────────────
        // Формулы повторяют StatsPage: там же считаются значения на экране.
        // В файле хранятся только тренировка и точки владения, итог выводится.

        private int Mastery()
        {
            var s = (_c.Mastery ?? "").Trim();
            if (s.StartsWith("+")) s = s[1..];
            return int.TryParse(s, out var v) ? v : 0;
        }

        private static int AttrMod(int baseVal) => (int)Math.Floor((baseVal - 10) / 2.0);

        private static string Fmt(int v) => (v >= 0 ? "+" : "") + v;

        /// <summary>Точки владения так же, как на экране: заполненные и пустые.</summary>
        private static string Dots(int value, int max = 3)
            => new string('◆', Math.Clamp(value, 0, max)) + new string('◇', max - Math.Clamp(value, 0, max));

        void SavingThrows(ColumnDescriptor col)
        {
            var s  = _c.Stats;
            int bm = Mastery();

            var rows = new (string Name, int Base, bool Prof)[]
            {
                ("Сила",         s.StrBase, s.StrSaveProf),
                ("Ловкость",     s.DexBase, s.DexSaveProf),
                ("Телосложение", s.ConBase, s.ConSaveProf),
                ("Интеллект",    s.IntBase, s.IntSaveProf),
                ("Мудрость",     s.WisBase, s.WisSaveProf),
                ("Харизма",      s.ChaBase, s.ChaSaveProf),
            };

            SH(col, "Спасброски", Blue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(3); cd.ConstantColumn(60); cd.ConstantColumn(60);
                    cd.RelativeColumn(3); cd.ConstantColumn(60); cd.ConstantColumn(60);
                });
                t.Header(h =>
                {
                    HC(h, "Атрибут"); HC(h, "Владение"); HC(h, "Итог");
                    HC(h, "Атрибут"); HC(h, "Владение"); HC(h, "Итог");
                });

                bool alt = false;
                for (int i = 0; i < rows.Length; i += 2)
                {
                    string bg = alt ? AltRow : "#FFFFFF";
                    for (int k = 0; k < 2 && i + k < rows.Length; k++)
                    {
                        var r = rows[i + k];
                        int total = AttrMod(r.Base) + (r.Prof ? bm : 0);
                        DC(t, bg, r.Name);
                        DC(t, bg, r.Prof ? "◆" : "◇");
                        DC(t, bg, Fmt(total));
                    }
                    alt = !alt;
                }
            });
        }

        void SkillTable(ColumnDescriptor col)
        {
            var s     = _c.Stats;
            int bm    = Mastery();
            int level = _c.Level;

            int Total(int training, int attrBase, int prof) => prof switch
            {
                1 => training + AttrMod(attrBase) + bm,
                2 => training + AttrMod(attrBase) + bm * 2,
                3 => (int)Math.Floor(training + AttrMod(attrBase) + bm * 2.0 + level / 4.0),
                _ => training + AttrMod(attrBase),
            };

            var rows = new (string Group, string Name, int Train, int Prof, int AttrBase)[]
            {
                ("Сила",         "Атлетика",            s.AthT, s.AthP, s.StrBase),
                ("Ловкость",     "Акробатика",          s.AcrT, s.AcrP, s.DexBase),
                ("Ловкость",     "Ловкость рук",        s.SlhT, s.SlhP, s.DexBase),
                ("Ловкость",     "Скрытность",          s.StlT, s.StlP, s.DexBase),
                ("Телосложение", "Выдержка/Здоровье",   s.EndT, s.EndP, s.ConBase),
                ("Интеллект",    "Анализ/Исследование", s.AnaT, s.AnaP, s.IntBase),
                ("Интеллект",    "История/Память",      s.HisT, s.HisP, s.IntBase),
                ("Интеллект",    "Магия/Мистика",       s.MagT, s.MagP, s.IntBase),
                ("Интеллект",    "Природа/Фауна",       s.NatT, s.NatP, s.IntBase),
                ("Интеллект",    "Религия/Оккультизм",  s.RelT, s.RelP, s.IntBase),
                ("Интеллект",    "Технология/Наука",    s.TecT, s.TecP, s.IntBase),
                ("Мудрость",     "Внимание",            s.AttT, s.AttP, s.WisBase),
                ("Мудрость",     "Выживание",           s.SurT, s.SurP, s.WisBase),
                ("Мудрость",     "Медицина",            s.MedT, s.MedP, s.WisBase),
                ("Мудрость",     "Проницательность",    s.InsT, s.InsP, s.WisBase),
                ("Мудрость",     "Уход за животными",   s.AniT, s.AniP, s.WisBase),
                ("Мудрость",     "Наставничество",      s.MenT, s.MenP, s.WisBase),
                ("Харизма",      "Выступление/Лидер",   s.PrfT, s.PrfP, s.ChaBase),
                ("Харизма",      "Устрашение",          s.ItmT, s.ItmP, s.ChaBase),
                ("Харизма",      "Обман",               s.DecT, s.DecP, s.ChaBase),
                ("Харизма",      "Очарование",          s.ChrT, s.ChrP, s.ChaBase),
                ("Харизма",      "Убеждение",           s.PrsT, s.PrsP, s.ChaBase),
            };

            SH(col, "Навыки", Blue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(4); cd.RelativeColumn(3);
                    cd.ConstantColumn(60); cd.ConstantColumn(70); cd.ConstantColumn(50);
                });
                t.Header(h =>
                {
                    HC(h, "Навык"); HC(h, "Атрибут"); HC(h, "Трен."); HC(h, "Владение"); HC(h, "Итог");
                });

                bool alt = false;
                foreach (var r in rows)
                {
                    string bg = alt ? AltRow : "#FFFFFF";
                    DC(t, bg, r.Name);
                    DC(t, bg, r.Group);
                    DC(t, bg, r.Train == 0 ? "—" : r.Train.ToString());
                    DC(t, bg, Dots(r.Prof));
                    DC(t, bg, Fmt(Total(r.Train, r.AttrBase, r.Prof)));
                    alt = !alt;
                }
            });
        }

        void Attributes(ColumnDescriptor col)
        {
            SH(col, "Атрибуты", Purple);
            var s = _c.Stats;
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn();
                    cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn();
                });
                foreach (var (name, val) in new[]
                {
                    ("СИЛ", s.StrBase), ("ЛОВ", s.DexBase), ("ВЫН", s.ConBase),
                    ("ИНТ", s.IntBase), ("МДР", s.WisBase), ("ХАР", s.ChaBase)
                })
                {
                    t.Cell().AlignCenter().Padding(4).Column(c =>
                    {
                        c.Item().AlignCenter().Text(name).FontSize(10).Bold().FontColor(Blue);
                        c.Item().AlignCenter().Text(val.ToString()).FontSize(16).Bold().FontColor(Purple);
                        int mod = (val - 10) / 2;
                        c.Item().AlignCenter().Text((mod >= 0 ? "+" : "") + mod)
                         .FontSize(10).FontColor(Muted);
                    });
                }
            });
        }

        // ── Radar chart — SVG implementation (Canvas API removed in QuestPDF 2024.3+)
        void RadarChart(ColumnDescriptor col)
        {
            var s = _c.Stats;
            int[] vals   = { s.StrBase, s.DexBase, s.ConBase, s.IntBase, s.WisBase, s.ChaBase };
            string[] lbl = { "СИЛ", "ЛОВ", "ВЫН", "ИНТ", "МДР", "ХАР" };
            string svg   = BuildRadarSvg(vals, lbl, 280, 200);

            SH(col, "Профиль атрибутов", Blue);
            col.Item().Width(280).Height(200).Svg(svg);
        }

        static string BuildRadarSvg(int[] vals, string[] labels, float W, float H)
        {
            float cx = W / 2, cy = H / 2;
            float r  = Math.Min(cx, cy) - 28;
            int   n  = vals.Length;

            var sb = new System.Text.StringBuilder();
            sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{W}' height='{H}'>");

            // Grid rings
            for (int ring = 1; ring <= 5; ring++)
            {
                float rr = r * ring / 5f;
                sb.Append($"<polygon points='{HexPoints(cx, cy, rr, n)}' fill='none' stroke='#DDDDDD' stroke-width='0.8'/>");
            }

            // Axes + labels
            for (int i = 0; i < n; i++)
            {
                float angle = (float)(Math.PI / 2 + 2 * Math.PI * i / n);
                float ex = cx + r * (float)Math.Cos(angle);
                float ey = cy - r * (float)Math.Sin(angle);
                sb.Append($"<line x1='{F(cx)}' y1='{F(cy)}' x2='{F(ex)}' y2='{F(ey)}' stroke='#CCCCCC' stroke-width='0.6' stroke-dasharray='4,3'/>");

                float lx = cx + (r + 16) * (float)Math.Cos(angle);
                float ly = cy - (r + 16) * (float)Math.Sin(angle);
                sb.Append($"<text x='{F(lx)}' y='{F(ly + 4)}' text-anchor='middle' font-size='10' fill='#666666' font-family='Segoe UI'>{labels[i]}</text>");
            }

            // Value polygon
            string pts = ValuePoints(cx, cy, r, vals, n);
            sb.Append($"<polygon points='{pts}' fill='#7B4FA6' fill-opacity='0.2' stroke='#7B4FA6' stroke-width='1.8'/>");

            // Dots + value labels
            for (int i = 0; i < n; i++)
            {
                float angle = (float)(Math.PI / 2 + 2 * Math.PI * i / n);
                float pct   = Math.Clamp(vals[i], 0, 30) / 30f;
                float px    = cx + r * pct * (float)Math.Cos(angle);
                float py    = cy - r * pct * (float)Math.Sin(angle);
                sb.Append($"<circle cx='{F(px)}' cy='{F(py)}' r='3.5' fill='#7B4FA6'/>");
                sb.Append($"<text x='{F(px + 5)}' y='{F(py - 6)}' font-size='9' fill='#7B4FA6' font-family='Segoe UI'>{vals[i]}</text>");
            }

            sb.Append("</svg>");
            return sb.ToString();
        }

        static string HexPoints(float cx, float cy, float r, int n)
        {
            var pts = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++)
            {
                float a = (float)(Math.PI / 2 + 2 * Math.PI * i / n);
                if (i > 0) pts.Append(' ');
                pts.Append($"{F(cx + r * (float)Math.Cos(a))},{F(cy - r * (float)Math.Sin(a))}");
            }
            return pts.ToString();
        }

        static string ValuePoints(float cx, float cy, float r, int[] vals, int n)
        {
            var pts = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++)
            {
                float a   = (float)(Math.PI / 2 + 2 * Math.PI * i / n);
                float pct = Math.Clamp(vals[i], 0, 30) / 30f;
                if (i > 0) pts.Append(' ');
                pts.Append($"{F(cx + r * pct * (float)Math.Cos(a))},{F(cy - r * pct * (float)Math.Sin(a))}");
            }
            return pts.ToString();
        }

        static string F(float v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                // ── Skills table ──────────────────────────────────────────────────────
        void Skills(ColumnDescriptor col, string title, List<SkillData> list)
        {
            SH(col, title, Blue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(5); });
                t.Header(h =>
                {
                    h.Cell().Background(Purple).Padding(4).Text("Навык").Bold().FontSize(10).FontColor(Colors.White);
                    h.Cell().Background(Purple).Padding(4).Text("Описание").Bold().FontSize(10).FontColor(Colors.White);
                });
                bool alt = false;
                foreach (var sk in list)
                {
                    string bg = alt ? AltRow : "#FFFFFF";
                    DC(t, bg, sk.SkillName);
                    DC(t, bg, sk.Description);
                    alt = !alt;
                }
            });
        }

        // ── Attacks ───────────────────────────────────────────────────────────
        // ── Черты 4 / 9 / 18 уровня ──────────────────────────────────────────
        void Traits(ColumnDescriptor col)
        {
            var rows = new (string Label, TraitData? Data)[]
            {
                ("Черта (4 уровень)",  _c.Trait4),
                ("Черта (9 уровень)",  _c.Trait9),
                ("Черта (18 уровень)", _c.Trait18),
            };
            if (rows.All(r => string.IsNullOrWhiteSpace(r.Data?.Description))) return;

            SH(col, "Черты", Blue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(5); cd.ConstantColumn(24); });
                t.Header(h =>
                {
                    HC(h, "Черта"); HC(h, "Описание"); HC(h, "✓");
                });
                bool alt = false;
                foreach (var (label, data) in rows)
                {
                    string bg = alt ? AltRow : "#FFFFFF";
                    DC(t, bg, label);
                    DC(t, bg, data?.Description ?? "");
                    DC(t, bg, data?.IsAcquired == true ? "✓" : "");
                    alt = !alt;
                }
            });
        }

        // ── Черты гуманоидов ─────────────────────────────────────────────────
        void HumanoidTraits(ColumnDescriptor col)
        {
            var list = _c.HumanoidTraits;
            if (list == null || list.Count == 0) return;

            SH(col, "Черты гуманоидов", Blue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(5); cd.ConstantColumn(24); });
                t.Header(h => { HC(h, "Название"); HC(h, "Описание"); HC(h, "✓"); });
                bool alt = false;
                foreach (var tr in list)
                {
                    string bg = alt ? AltRow : "#FFFFFF";
                    DC(t, bg, tr.Name);
                    DC(t, bg, tr.Description);
                    DC(t, bg, tr.IsAcquired ? "✓" : "");
                    alt = !alt;
                }
            });
        }

        // ── Владения и языки ─────────────────────────────────────────────────
        void Proficiencies(ColumnDescriptor col)
        {
            var list = _c.Proficiencies;
            if (list == null || list.Count == 0) return;

            SH(col, "Владения и языки", Blue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(5); cd.ConstantColumn(60); });
                t.Header(h => { HC(h, "Тип"); HC(h, "Описание"); HC(h, "Уровень"); });
                bool alt = false;
                foreach (var p in list)
                {
                    string bg = alt ? AltRow : "#FFFFFF";
                    DC(t, bg, p.TypeIndex == 1 ? "Язык" : "Владение");
                    DC(t, bg, p.Description);
                    // Уровень владения 0..3 — ромбиками, как на экране
                    DC(t, bg, new string('◆', Math.Clamp(p.Rating, 0, 3))
                            + new string('◇', 3 - Math.Clamp(p.Rating, 0, 3)));
                    alt = !alt;
                }
            });
        }

        // ── Инвентарь и деньги ───────────────────────────────────────────────
        void Inventory(ColumnDescriptor col)
        {
            var list = _c.Inventory;
            bool hasCoins = _c.GoldCoins != 0 || _c.SilverCoins != 0 || _c.CopperCoins != 0;
            if ((list == null || list.Count == 0) && !hasCoins) return;

            SH(col, "Инвентарь", Blue);

            if (hasCoins)
                col.Item().PaddingBottom(4).Text(
                    $"Золото: {_c.GoldCoins}    Серебро: {_c.SilverCoins}    Медь: {_c.CopperCoins}")
                   .FontSize(10).Bold();

            if (list == null || list.Count == 0) return;

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(3); cd.ConstantColumn(40);
                    cd.RelativeColumn(2); cd.RelativeColumn(5);
                });
                t.Header(h => { HC(h, "Предмет"); HC(h, "Кол-во"); HC(h, "Редкость"); HC(h, "Описание"); });
                bool alt = false;
                foreach (var it in list)
                {
                    string bg = alt ? AltRow : "#FFFFFF";
                    DC(t, bg, it.Name + (it.Equipped ? "  (надет)" : ""));
                    DC(t, bg, it.Quantity.ToString());
                    DC(t, bg, it.Rarity);
                    DC(t, bg, string.IsNullOrWhiteSpace(it.Notes)
                              ? it.Description
                              : $"{it.Description}\n{it.Notes}");
                    alt = !alt;
                }
            });
        }

        // ── Пользовательские листы ───────────────────────────────────────────
        void CustomSheets(ColumnDescriptor col)
        {
            var sheets = _c.CustomSheets;
            if (sheets == null || sheets.Count == 0) return;

            foreach (var sheet in sheets)
            {
                if (sheet.Columns == null || sheet.Columns.Count == 0) continue;
                SH(col, sheet.Name, Purple);
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        foreach (var _ in sheet.Columns) cd.RelativeColumn();
                    });
                    t.Header(h =>
                    {
                        foreach (var c in sheet.Columns) HC(h, c.Header);
                    });
                    bool alt = false;
                    foreach (var row in sheet.Rows ?? new List<CustomSheetRow>())
                    {
                        string bg = alt ? AltRow : "#FFFFFF";
                        for (int i = 0; i < sheet.Columns.Count; i++)
                        {
                            var cell = row.Cells != null && i < row.Cells.Count ? row.Cells[i] : "";
                            // toggle-колонки в файле хранятся как "True"/"False"
                            if (sheet.Columns[i].ColumnType == "toggle")
                                cell = bool.TryParse(cell, out var on) && on ? "✓" : "";
                            DC(t, bg, cell);
                        }
                        alt = !alt;
                    }
                });
            }
        }

        void Attacks(ColumnDescriptor col)
        {
            SH(col, "Атаки", Blue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(5); });
                t.Header(h =>
                {
                    h.Cell().Background(Purple).Padding(4).Text("Тип").Bold().FontSize(10).FontColor(Colors.White);
                    h.Cell().Background(Purple).Padding(4).Text("Описание").Bold().FontSize(10).FontColor(Colors.White);
                });
                bool alt = false;
                foreach (var a in _c.Attacks)
                {
                    string bg = alt ? AltRow : "#FFFFFF";
                    DC(t, bg, a.AttackType);
                    DC(t, bg, a.Description);
                    alt = !alt;
                }
            });
        }

        // ── Equipment ─────────────────────────────────────────────────────────
        void Equipment(ColumnDescriptor col)
        {
            var slots = new (string label, EquipmentItem? item)[]
            {
                ("Голова",     _c.HeadItem),  ("Тело",       _c.BodyItem),
                ("Руки",       _c.HandsItem), ("Пояс",       _c.BeltItem),
                ("Ноги",       _c.LegsItem),  ("Кольцо 1",   _c.Ring1Item),
                ("Кольцо 2",   _c.Ring2Item), ("Амулет",     _c.AmuletItem),
                ("Украшение 1",_c.Ornament1Item), ("Украшение 2",_c.Ornament2Item),
                ("Артефакт 1", _c.Artifact1Item), ("Артефакт 2", _c.Artifact2Item),
            };
            bool any = false;
            foreach (var (_, item) in slots)
                if (item != null && (!string.IsNullOrEmpty(item.Name) || !string.IsNullOrEmpty(item.ImagePath)))
                { any = true; break; }
            if (!any) return;

            SH(col, "Снаряжение", Purple);

            // 3 columns of cards
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                foreach (var (label, item) in slots)
                {
                    t.Cell().Padding(4).Border(0.5f).BorderColor(Line).Column(card =>
                    {
                        card.Spacing(3);
                        // Slot label
                        card.Item().Text(label).FontSize(9).Bold().FontColor(Blue);

                        // Image
                        bool hasImg = item != null && !string.IsNullOrEmpty(item.ImagePath)
                                      && File.Exists(item.ImagePath);
                        if (hasImg)
                            card.Item().Height(70).Image(item!.ImagePath).FitArea();
                        else
                            card.Item().Height(70).Background("#F5F5F5")
                                .AlignCenter().AlignMiddle()
                                .Text("—").FontSize(18).FontColor("#CCCCCC");

                        if (item == null) return;
                        // Name
                        if (!string.IsNullOrEmpty(item.Name))
                            card.Item().Text(item.Name).FontSize(10).Bold();
                        // Rarity
                        if (!string.IsNullOrEmpty(item.Rarity))
                            card.Item().Text($"Редкость: {item.Rarity}").FontSize(9).FontColor(Purple);
                        // Stats
                        if (!string.IsNullOrEmpty(item.Stats))
                            card.Item().Text(item.Stats).FontSize(9).FontColor("#444");
                        // Effects
                        if (!string.IsNullOrEmpty(item.Effects))
                            card.Item().Text(item.Effects).FontSize(9).FontColor(Muted).Italic();
                    });
                }
            });
        }

        // ── Text block ────────────────────────────────────────────────────────
        void TextBlock(ColumnDescriptor col, string title, string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            SH(col, title, Blue);
            col.Item().Text(text).FontSize(11).LineHeight(1.4f);
        }

        // ── Journal ───────────────────────────────────────────────────────────
        void Journal(ColumnDescriptor col)
        {
            SH(col, "Журнал сессий", Purple);
            foreach (var e in _c.JournalEntries)
            {
                col.Item().Column(ec =>
                {
                    ec.Spacing(2);
                    ec.Item().Text(t =>
                    {
                        t.Span(e.Title).Bold().FontSize(12).FontColor(Blue);
                        t.Span($"  {e.DateStr}").FontSize(10).FontColor(Muted);
                    });
                    if (!string.IsNullOrWhiteSpace(e.Tags))
                        ec.Item().Text($"Теги: {e.Tags}").FontSize(10).FontColor(Muted).Italic();
                    if (!string.IsNullOrWhiteSpace(e.Content))
                        ec.Item().Text(e.Content).FontSize(11).LineHeight(1.4f);
                    ec.Item().LineHorizontal(0.5f).LineColor(Line);
                });
            }
        }

        // ── Tiny helpers ──────────────────────────────────────────────────────
        static void SH(ColumnDescriptor col, string title, string color) =>
            col.Item().PaddingTop(8).PaddingBottom(3).Row(row =>
            {
                row.ConstantItem(3).Height(14).Background(color);
                row.RelativeItem().PaddingLeft(6)
                   .Text(title).Bold().FontSize(11.5f).FontColor(color);
            });

        static void SC(TableDescriptor t, string label, string value)
        {
            t.Cell().Padding(3).Text(label).FontSize(10).FontColor("#555");
            t.Cell().Padding(3).Text(value).FontSize(11).Bold();
        }

        /// <summary>Ячейка шапки таблицы — чтобы не повторять оформление в каждом разделе.</summary>
        static void HC(TableCellDescriptor h, string text) =>
            h.Cell().Background(Purple).Padding(4)
             .Text(text ?? "").Bold().FontSize(10).FontColor(Colors.White);

        static void DC(TableDescriptor t, string bg, string text) =>
            t.Cell().Background(bg).Padding(4).BorderBottom(0.5f).BorderColor(Line)
             .Text(text ?? "").FontSize(10);
    }
}
