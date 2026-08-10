// Helpers/Log.cs — минимальный файловый лог.
//
// Зачем: в коде было ~20 молчаливых catch { }. Когда у кого-то из игроков
// «не сохраняется» или «слетела тема», выяснить причину было нечем.
// Теперь тихие сбои пишутся в %AppData%\CharacterApp\log.txt — можно попросить
// прислать файл вместо игры в угадайку.
//
// Правила использования:
//   Log.Warn("тема", ex)  — сбой, который приложение пережило (тихий catch)
//   Log.Error("тема", ex) — сбой, о котором пользователь уже узнал из уведомления
//   Log.Info("текст")     — заметное событие (миграция настроек и т.п.)
//
// Логгер сам никогда не бросает исключений: падение логгера не должно
// ронять то, что он логирует.

using System;
using System.IO;
using System.Text;

namespace CharacterApp.Helpers
{
    public static class Log
    {
        private const long MaxBytes = 512 * 1024;   // 512 КБ, потом ротация в log.old.txt
        private static readonly object _gate = new();

        public static string FilePath => Path.Combine(App.DataDir, "log.txt");
        private static string OldPath => Path.Combine(App.DataDir, "log.old.txt");

        public static void Info (string message)                 => Write("INFO ", message, null);
        public static void Warn (string message, Exception? ex)  => Write("WARN ", message, ex);
        public static void Error(string message, Exception? ex)  => Write("ERROR", message, ex);

        private static void Write(string level, string message, Exception? ex)
        {
            try
            {
                var sb = new StringBuilder()
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                    .Append("  ").Append(level).Append("  ").Append(message);

                if (ex != null)
                {
                    sb.AppendLine()
                      .Append("        ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
                    // Стек только для неожиданного — по типу исключения не угадаешь,
                    // поэтому пишем всегда, но с отступом, чтобы лог читался
                    if (!string.IsNullOrEmpty(ex.StackTrace))
                        sb.AppendLine().Append("        ").Append(ex.StackTrace.Trim());
                }

                lock (_gate)
                {
                    Directory.CreateDirectory(App.DataDir);
                    Rotate();
                    File.AppendAllText(FilePath, sb.AppendLine().ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Логгер, который роняет приложение, хуже отсутствия логгера
            }
        }

        private static void Rotate()
        {
            try
            {
                var fi = new FileInfo(FilePath);
                if (!fi.Exists || fi.Length < MaxBytes) return;
                File.Move(FilePath, OldPath, true);
            }
            catch { }
        }
    }
}
