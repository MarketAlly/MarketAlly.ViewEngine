using CommunityToolkit.Mvvm.ComponentModel;

namespace UnifiedBrowser.Maui.Models
{
    /// <summary>
    /// View model for a single bookmark item, suitable for binding to UI controls
    /// </summary>
    public partial class BookmarkItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _text = string.Empty;

        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private string? _icon;

        [ObservableProperty]
        private string? _iconUri;

        [ObservableProperty]
        private DateTimeOffset? _addDate;

        [ObservableProperty]
        private DateTimeOffset? _lastModified;

        public BookmarkItemViewModel()
        {
        }

        public BookmarkItemViewModel(string text, string url)
        {
            Text = text;
            Url = url;
            AddDate = DateTimeOffset.UtcNow;
        }

        public BookmarkItemViewModel(string text, string url, string? icon, string? iconUri, DateTimeOffset? addDate, DateTimeOffset? lastModified)
        {
            Text = text;
            Url = url;
            Icon = icon;
            IconUri = iconUri;
            AddDate = addDate;
            LastModified = lastModified;
        }
    }
}
