// Helpers/CharacterAssets.cs
// Делает JSON персонажа переносимым между компьютерами.
//
// Проблема: PhotoPath и EquipmentItem.ImagePath хранились как абсолютные пути
// (C:\Users\Вася\Downloads\меч.png). Скинул файл кенту — картинки не открылись.
//
// Решение: при сохранении картинки копируются в папку "<имя файла>_assets"
// рядом с JSON, а в файл пишется относительный путь. При загрузке путь
// разворачивается обратно в абсолютный. В памяти пути всегда абсолютные,
// поэтому остальной код приложения трогать не пришлось.
//
// Старые файлы с абсолютными путями продолжают работать: если путь абсолютный
// и файл на месте — он просто используется как есть.

using System;
using System.Collections.Generic;
using System.IO;
using CharacterApp.Models;

namespace CharacterApp.Helpers
{
    public static class CharacterAssets
    {
        public const string AssetsSuffix = "_assets";

        /// <summary>Имя папки ресурсов для конкретного файла персонажа.</summary>
        public static string AssetsFolderName(string jsonPath)
            => Path.GetFileNameWithoutExtension(jsonPath) + AssetsSuffix;

        // ── Доступ к полям с картинками ──────────────────────────────────────
        // Ключ = стабильное имя файла в папке ресурсов (чтобы пересохранение
        // перезаписывало ту же картинку, а не плодило копии).
        //
        // ВАЖНО: EquipmentPage.FillCharacter кладёт в Character ССЫЛКИ на те же
        // объекты EquipmentItem, которые живут в слотах экипировки на экране.
        // Если менять у них ImagePath напрямую, у контрола на экране путь тоже
        // станет относительным, File.Exists вернёт false — и картинка предмета
        // пропадёт сразу после сохранения. Поэтому сеттер не мутирует предмет,
        // а подставляет в Character его копию.
        private static IEnumerable<(string Key, Func<string> Get, Action<string> Set)>
            ImageFields(Character c)
        {
            // PhotoPath — обычная строка в самом Character, копия безопасна
            yield return ("portrait", () => c.PhotoPath, v => c.PhotoPath = v);

            foreach (var (key, get, set) in EquipmentSlots(c))
            {
                var item = get();
                if (item == null) continue;
                var captured = item;
                yield return (key,
                              () => captured.ImagePath,
                              v => set(WithImagePath(captured, v)));
            }
        }

        /// <summary>Копия предмета с другим путём к картинке. Оригинал не трогаем.</summary>
        private static EquipmentItem WithImagePath(EquipmentItem src, string imagePath)
            => new EquipmentItem
            {
                Name      = src.Name,
                ImagePath = imagePath,
                Rarity    = src.Rarity,
                Stats     = src.Stats,
                Effects   = src.Effects,
            };

        private static IEnumerable<(string Key, Func<EquipmentItem?> Get, Action<EquipmentItem?> Set)>
            EquipmentSlots(Character c)
        {
            yield return ("head",      () => c.HeadItem,      v => c.HeadItem      = v);
            yield return ("body",      () => c.BodyItem,      v => c.BodyItem      = v);
            yield return ("hands",     () => c.HandsItem,     v => c.HandsItem     = v);
            yield return ("belt",      () => c.BeltItem,      v => c.BeltItem      = v);
            yield return ("legs",      () => c.LegsItem,      v => c.LegsItem      = v);
            yield return ("ring1",     () => c.Ring1Item,     v => c.Ring1Item     = v);
            yield return ("ring2",     () => c.Ring2Item,     v => c.Ring2Item     = v);
            yield return ("amulet",    () => c.AmuletItem,    v => c.AmuletItem    = v);
            yield return ("ornament1", () => c.Ornament1Item, v => c.Ornament1Item = v);
            yield return ("ornament2", () => c.Ornament2Item, v => c.Ornament2Item = v);
            yield return ("artifact1", () => c.Artifact1Item, v => c.Artifact1Item = v);
            yield return ("artifact2", () => c.Artifact2Item, v => c.Artifact2Item = v);
            yield return ("weapon1",   () => c.Weapon1Item,   v => c.Weapon1Item   = v);
            yield return ("weapon2",   () => c.Weapon2Item,   v => c.Weapon2Item   = v);
            yield return ("shield",    () => c.ShieldItem,    v => c.ShieldItem    = v);
        }

        // ── Сохранение: абсолютный путь → копия в _assets + относительный путь ─
        /// <summary>
        /// Копирует все картинки персонажа рядом с JSON и заменяет пути на относительные.
        /// Мутирует переданный объект — передавать нужно DTO для сериализации, не живую модель.
        /// </summary>
        /// <param name="folderNameOverride">
        /// Для автосейвов — общая папка на все снимки, чтобы не плодить по папке на файл.
        /// </param>
        public static void Externalize(Character c, string jsonPath, string? folderNameOverride = null)
        {
            if (c == null || string.IsNullOrWhiteSpace(jsonPath)) return;

            var baseDir    = Path.GetDirectoryName(Path.GetFullPath(jsonPath));
            if (string.IsNullOrEmpty(baseDir)) return;
            var folderName = folderNameOverride ?? AssetsFolderName(jsonPath);
            var assetsDir  = Path.Combine(baseDir, folderName);

            foreach (var (key, get, set) in ImageFields(c))
            {
                var src = get();
                if (string.IsNullOrWhiteSpace(src)) continue;

                // Уже относительный — значит, уже лежит в _assets, ничего не делаем
                if (!Path.IsPathRooted(src)) continue;

                var ext    = Path.GetExtension(src);
                if (string.IsNullOrEmpty(ext)) ext = ".png";
                var target = Path.Combine(assetsDir, key + ext);
                var rel    = folderName + "/" + key + ext;

                try
                {
                    if (File.Exists(src))
                    {
                        Directory.CreateDirectory(assetsDir);
                        // Источник и цель могут совпасть при повторном сохранении
                        if (!PathsEqual(src, target)) File.Copy(src, target, true);
                        set(rel);
                    }
                    else if (File.Exists(target))
                    {
                        // Оригинал удалили, но копия в _assets уцелела — ссылаемся на неё
                        set(rel);
                    }
                    // Иначе оставляем путь как был: вдруг диск просто не подключён
                }
                catch
                {
                    // Не смогли скопировать (нет прав, файл занят) — не ломаем сохранение,
                    // оставляем абсолютный путь. Хуже, чем было, точно не станет.
                }
            }
        }

        // ── Загрузка: относительный путь → абсолютный ────────────────────────
        /// <summary>Разворачивает относительные пути к картинкам в абсолютные.</summary>
        public static void Internalize(Character c, string jsonPath)
        {
            if (c == null || string.IsNullOrWhiteSpace(jsonPath)) return;

            var baseDir = Path.GetDirectoryName(Path.GetFullPath(jsonPath));
            if (string.IsNullOrEmpty(baseDir)) return;

            foreach (var (_, get, set) in ImageFields(c))
            {
                var stored = get();
                if (string.IsNullOrWhiteSpace(stored)) continue;
                if (Path.IsPathRooted(stored)) continue;   // старый формат — оставляем

                try
                {
                    var full = Path.GetFullPath(
                        Path.Combine(baseDir, stored.Replace('/', Path.DirectorySeparatorChar)));
                    if (File.Exists(full)) set(full);
                }
                catch { /* мусор в пути — оставляем как есть */ }
            }
        }

        private static bool PathsEqual(string a, string b)
        {
            try
            {
                return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
                                     StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }
}
