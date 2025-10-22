using Microsoft.Maui.Controls;
using System.Globalization;

namespace UnifiedBrowser.Maui.Converters
{
    /// <summary>
    /// Converts a boolean indicating if a link is an ad to a background color
    /// </summary>
    public class AdBackgroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAd && isAd)
            {
                // Return a subtle warning color for ads
                return Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#1AFF6B00") // Semi-transparent orange for dark theme
                    : Color.FromArgb("#1AFFF3E0"); // Semi-transparent light orange for light theme
            }

            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}