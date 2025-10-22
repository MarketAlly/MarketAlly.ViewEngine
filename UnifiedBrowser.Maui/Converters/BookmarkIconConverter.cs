using System.Globalization;

namespace UnifiedBrowser.Maui.Converters
{
    /// <summary>
    /// Converts a boolean bookmark state to an icon name
    /// </summary>
    public class BookmarkIconConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isBookmarked)
            {
                // Return filled bookmark icon if bookmarked, outline if not
                return isBookmarked ? "bookmarkfill" : "bookmark";
            }

            return "bookmark";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
