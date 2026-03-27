using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace CharacterApp.Converters
{
    public class RatingIconMultiConverter : IMultiValueConverter
    {
        // values[0] = index (int)
        // values[1] = current value (int)
        // values[2] = active icon path (string)
        // values[3] = inactive icon path (string)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values.Length < 4) return null;
                if (!int.TryParse(values[0]?.ToString(), out int index)) return null;
                if (!int.TryParse(values[1]?.ToString(), out int current)) return null;
                var active = values[2] as string;
                var inactive = values[3] as string;
                var path = index <= current ? active : inactive;
                if (string.IsNullOrEmpty(path)) return null;
                return new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute));
            }
            catch { return null; }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
