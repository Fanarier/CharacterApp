using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace CharacterApp
{
    public class RatingIconMultiConverter : IMultiValueConverter
    {
        // values[0] - порядковый номер (int)
        // values[1] - текущее значение рейтинга (int)
        // values[2] - активная иконка (string)
        // values[3] - неактивная иконка (string)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 4 &&
                int.TryParse(values[0]?.ToString(), out int index) &&
                int.TryParse(values[1]?.ToString(), out int currentValue) &&
                values[2] is string active &&
                values[3] is string inactive)
            {
                string path = (index <= currentValue) ? active : inactive;
                return new BitmapImage(new Uri(path, UriKind.Relative));
            }
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}
