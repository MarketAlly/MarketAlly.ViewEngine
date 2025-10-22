using Microsoft.Maui.Controls;
using System.Globalization;

namespace UnifiedBrowser.Maui.Converters
{
    /// <summary>
    /// Performs a logical AND operation on multiple boolean values
    /// </summary>
    public class AndConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0)
                return false;

            foreach (var value in values)
            {
                if (value is bool boolValue && !boolValue)
                    return false;
            }

            return true;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}