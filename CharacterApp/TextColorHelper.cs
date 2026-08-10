// TextColorHelper.cs — right-click "Цвет текста" context menu for any TextBox
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CharacterApp.Models;

namespace CharacterApp
{
    public static class TextColorHelper
    {
        /// <summary>
        /// Отметка «в это поле меню цвета уже добавлено».
        ///
        /// Страницы вызывают Register из обработчика Loaded, а Loaded в WPF
        /// срабатывает при каждом показе страницы, а не один раз. Без этой
        /// отметки при возврате на страницу пункты «Цвет текста…» и
        /// «Сбросить цвет» добавлялись в меню повторно, и их становилось
        /// две пары, потом три.
        /// </summary>
        private static readonly DependencyProperty MenuAddedProperty =
            DependencyProperty.RegisterAttached(
                "ColorMenuAdded", typeof(bool), typeof(TextColorHelper),
                new PropertyMetadata(false));

        /// <summary>
        /// Register a TextBox so right-click gives "Цвет текста…" option.
        /// fieldName is used as key in Character.FieldColors.
        /// </summary>
        public static void Register(TextBox tb, string fieldName,
            Func<Character?> getChar, Action markUnsaved)
        {
            if (tb == null) return;

            // Уже добавляли — второй раз не надо
            if (tb.GetValue(MenuAddedProperty) is true) return;
            tb.SetValue(MenuAddedProperty, true);

            // Build context menu (or append to existing)
            var menu = tb.ContextMenu ?? new ContextMenu();
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());

            var mi = new MenuItem
            {
                Header = "🎨  Цвет текста…",
                FontWeight = FontWeights.SemiBold
            };
            mi.Click += (_, _) =>
            {
                var dlg = new ColorPickerWindow
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                // Pre-select current color
                var c = getChar();
                if (c != null && c.FieldColors.TryGetValue(fieldName, out var hex))
                    dlg.PresetHex = hex;

                if (dlg.ShowDialog() == true)
                {
                    var color = dlg.SelectedColor;
                    tb.Foreground = new SolidColorBrush(color);

                    var ch = getChar();
                    if (ch != null)
                    {
                        ch.FieldColors[fieldName] = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                        markUnsaved();
                    }
                }
            };

            // "Reset color" option
            var miReset = new MenuItem { Header = "↩  Сбросить цвет" };
            miReset.Click += (_, _) =>
            {
                tb.ClearValue(TextBox.ForegroundProperty);
                var ch = getChar();
                if (ch != null) { ch.FieldColors.Remove(fieldName); markUnsaved(); }
            };

            menu.Items.Add(mi);
            menu.Items.Add(miReset);
            tb.ContextMenu = menu;
        }

        /// <summary>Apply saved colors to registered TextBoxes.</summary>
        public static void Apply(Dictionary<string, TextBox> fields, Character c)
        {
            foreach (var (name, tb) in fields)
            {
                if (c.FieldColors.TryGetValue(name, out var hex))
                {
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(hex);
                        tb.Foreground = new SolidColorBrush(color);
                    }
                    catch { tb.ClearValue(TextBox.ForegroundProperty); }
                }
                else
                {
                    tb.ClearValue(TextBox.ForegroundProperty);
                }
            }
        }
    }
}
