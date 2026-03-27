using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CharacterApp.Converters
{
    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        // true -> Visible when NOT null/empty, false -> Collapsed when NOT null/empty
        public bool Invert { get; set; } = false;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool notEmpty = !(value == null || string.IsNullOrEmpty(value.ToString()));
            if (Invert) notEmpty = !notEmpty;
            return notEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
