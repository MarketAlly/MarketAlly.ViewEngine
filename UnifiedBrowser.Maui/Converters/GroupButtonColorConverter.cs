using System.Globalization;

namespace UnifiedBrowser.Maui.Converters
{
    /// <summary>
    /// Converts a boolean group selection state to a background color
    /// </summary>
    public class GroupButtonBackgroundColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                // Return blue background if selected, transparent if not
                return isSelected
                    ? Color.FromArgb("#007AFF")
                    : Colors.Transparent;
            }

            return Colors.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean group selection state to a text color for light theme
    /// </summary>
    public class GroupButtonTextColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                // Return white text if selected, black if not (light theme)
                return isSelected
                    ? Colors.White
                    : Colors.Black;
            }

            return Colors.Black;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a boolean group selection state to a text color for dark theme
    /// </summary>
    public class GroupButtonTextColorDarkConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                // Return white text if selected, light gray if not (dark theme)
                return isSelected
                    ? Colors.White
                    : Color.FromArgb("#CCCCCC");
            }

            return Color.FromArgb("#CCCCCC");
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
