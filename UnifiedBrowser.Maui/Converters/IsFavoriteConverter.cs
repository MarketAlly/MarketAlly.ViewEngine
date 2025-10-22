using Microsoft.Maui.Controls;
using System.Globalization;

namespace UnifiedBrowser.Maui.Converters
{
    /// <summary>
    /// Converter that returns true if Visibility == 2 (favorite)
    /// </summary>
    public class IsFavoriteConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int visibility)
            {
                return visibility == 2;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
