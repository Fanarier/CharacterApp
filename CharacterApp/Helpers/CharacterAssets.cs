// Helpers/CharacterAssets.cs
// Картинки персонажа хранятся внутри его файла.
//
// Как было: рядом с .json создавалась папка "<имя>_assets", туда копировались
// портрет и иконки предметов, а в файл писались относительные пути. Передавать
// персонажа приходилось вместе с папкой — забыл её, и картинок нет.
//
// Как стало: картинка кодируется в текст и лежит прямо в .json. Один файл —
// и всё на месте. Предметы инвентаря так хранились с самого начала, теперь
// то же самое у портрета и экипировки.
//
// Интерфейс по-прежнему работает с путями к файлам, поэтому при загрузке
// картинка распаковывается в кэш приложения и подставляется путь к ней.
// Это позволило не переписывать отрисовку в каждом контроле.
//
// Старые файлы с папкой рядом читаются как раньше: путь на месте — картинка
// подхватится и при следующем сохранении переедет внутрь файла.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using CharacterApp.Models;

namespace CharacterApp.Helpers
{
    public static class CharacterAssets
    {
        /// <summary>Куда распаковываются картинки для показа.</summary>
        public static string CacheDir => Path.Combine(App.DataDir, "imgcache");

        // ── Доступ к полям с картинками ──────────────────────────────────────
        // EquipmentPage кладёт в Character ССЫЛКИ на предметы, которые живут
        // в слотах на экране. Поэтому сеттер не мутирует предмет, а подставляет
        // его копию — иначе правка пути гасила бы картинку прямо на странице.
        private static IEnumerable<ImageField> ImageFields(Character c)
        {
            yield return new ImageField(
                () => c.PhotoPath,   v => c.PhotoPath   = v,
                () => c.PhotoBase64, v => c.PhotoBase64 = v);

            foreach (var (get, set) in EquipmentSlots(c))
            {
                var item = get();
                if (item == null) continue;
                var captured = item;
                yield return new ImageField(
                    () => captured.ImagePath,
                    v => set(WithImage(captured, path: v, data: captured.ImageBase64)),
                    () => captured.ImageBase64,
                    v => set(WithImage(captured, path: captured.ImagePath, data: v)));
            }
        }

        private readonly record struct ImageField(
            Func<string> GetPath, Action<string> SetPath,
            Func<string> GetData, Action<string> SetData);

        private static EquipmentItem WithImage(EquipmentItem src, string path, string data)
            => new EquipmentItem
            {
                Name        = src.Name,
                ImagePath   = path,
                ImageBase64 = data,
                Rarity      = src.Rarity,
                Stats       = src.Stats,
                Effects     = src.Effects,
            };

        private static IEnumerable<(Func<EquipmentItem?> Get, Action<EquipmentItem?> Set)>
            EquipmentSlots(Character c)
        {
            yield return (() => c.HeadItem,      v => c.HeadItem      = v);
            yield return (() => c.BodyItem,      v => c.BodyItem      = v);
            yield return (() => c.HandsItem,     v => c.HandsItem     = v);
            yield return (() => c.BeltItem,      v => c.BeltItem      = v);
            yield return (() => c.LegsItem,      v => c.LegsItem      = v);
            yield return (() => c.Ring1Item,     v => c.Ring1Item     = v);
            yield return (() => c.Ring2Item,     v => c.Ring2Item     = v);
            yield return (() => c.AmuletItem,    v => c.AmuletItem    = v);
            yield return (() => c.Ornament1Item, v => c.Ornament1Item = v);
            yield return (() => c.Ornament2Item, v => c.Ornament2Item = v);
            yield return (() => c.Artifact1Item, v => c.Artifact1Item = v);
            yield return (() => c.Artifact2Item, v => c.Artifact2Item = v);
            yield return (() => c.Weapon1Item,   v => c.Weapon1Item   = v);
            yield return (() => c.Weapon2Item,   v => c.Weapon2Item   = v);
            yield return (() => c.ShieldItem,    v => c.ShieldItem    = v);
        }

        // ── Сохранение: файл с диска → текст внутри персонажа ────────────────
        /// <summary>
        /// Складывает картинки внутрь объекта персонажа перед записью в файл.
        /// Мутирует переданный объект — передавать нужно копию для сохранения,
        /// а не то, что показано на экране.
        /// </summary>
        public static void EmbedImages(Character c)
        {
            if (c == null) return;

            foreach (var f in ImageFields(c))
            {
                var path = f.GetPath();
                var data = f.GetData();

                // Картинка уже внутри и файл не менялся — перечитывать незачем
                if (!string.IsNullOrEmpty(data) && !IsFreshFile(path, data)) continue;
                if (string.IsNullOrWhiteSpace(path)) continue;

                try
                {
                    if (!File.Exists(path)) continue;   // диск отключён — старую копию не теряем
                    f.SetData(Convert.ToBase64String(File.ReadAllBytes(path)));
                }
                catch (Exception ex)
                {
                    Log.Warn($"не удалось вложить картинку {path} в файл персонажа", ex);
                }
            }
        }

        /// <summary>Файл на диске отличается от того, что уже вложено.</summary>
        private static bool IsFreshFile(string path, string data)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
                // Дешёвая проверка по размеру: точное сравнение стоило бы чтения файла
                long onDisk = new FileInfo(path).Length;
                long stored = (long)(data.Length * 3L / 4) - (data.EndsWith("==") ? 2 : data.EndsWith("=") ? 1 : 0);
                return Math.Abs(onDisk - stored) > 2;
            }
            catch { return false; }
        }

        // ── Загрузка: текст внутри персонажа → файл в кэше ───────────────────
        /// <summary>
        /// Раскладывает вложенные картинки в кэш и подставляет пути к ним,
        /// чтобы страницы могли рисовать их как обычные файлы.
        /// </summary>
        public static void ExtractImages(Character c)
        {
            if (c == null) return;

            foreach (var f in ImageFields(c))
            {
                var data = f.GetData();
                if (string.IsNullOrWhiteSpace(data)) continue;

                var path = WriteToCache(data);
                if (!string.IsNullOrEmpty(path)) f.SetPath(path);
            }
        }

        /// <summary>
        /// Пишет картинку в кэш. Имя файла — от содержимого, поэтому одна и та же
        /// картинка не плодит копии, а повторная загрузка персонажа ничего не пишет.
        /// </summary>
        private static string WriteToCache(string base64)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                var name  = Convert.ToHexString(MD5.HashData(bytes))[..16] + GuessExtension(bytes);
                var path  = Path.Combine(CacheDir, name);

                if (File.Exists(path) && new FileInfo(path).Length == bytes.Length) return path;

                Directory.CreateDirectory(CacheDir);
                File.WriteAllBytes(path, bytes);
                return path;
            }
            catch (Exception ex)
            {
                Log.Warn("не удалось распаковать картинку из файла персонажа", ex);
                return "";
            }
        }

        /// <summary>Расширение по сигнатуре — WPF ориентируется на содержимое, но с ним аккуратнее.</summary>
        private static string GuessExtension(byte[] b)
        {
            if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8) return ".jpg";
            if (b.Length > 7 && b[0] == 0x89 && b[1] == 0x50) return ".png";
            if (b.Length > 5 && b[0] == 0x47 && b[1] == 0x49) return ".gif";
            if (b.Length > 1 && b[0] == 0x42 && b[1] == 0x4D) return ".bmp";
            return ".img";
        }

        /// <summary>
        /// Убирает из кэша то, к чему давно не обращались. Вызывается при старте:
        /// иначе картинки персонажей, которых больше не открывают, копились бы вечно.
        /// </summary>
        public static void CleanCache(int keepDays = 30)
        {
            try
            {
                if (!Directory.Exists(CacheDir)) return;
                var edge = DateTime.UtcNow.AddDays(-keepDays);
                foreach (var f in new DirectoryInfo(CacheDir).GetFiles())
                    if (f.LastAccessTimeUtc < edge && f.LastWriteTimeUtc < edge)
                        try { f.Delete(); } catch { }
            }
            catch (Exception ex) { Log.Warn("не удалось почистить кэш картинок", ex); }
        }
    }
}
