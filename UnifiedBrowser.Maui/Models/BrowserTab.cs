using System;
using Microsoft.Maui.Controls;
using MarketAlly.Maui.ViewEngine;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UnifiedBrowser.Maui.Models
{
    /// <summary>
    /// Represents a single browser tab with its own WebView instance and metadata
    /// </summary>
    public sealed class BrowserTab : ObservableObject
    {
        private string _title = "New Tab";
        private string _url = string.Empty;
        private ImageSource? _favicon;
        private ImageSource? _thumbnail;
        private bool _isLoading;
        private DateTime _lastAccessed = DateTime.Now;
        private DateTime _createdAt = DateTime.Now;

        /// <summary>
        /// Unique identifier for this tab
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// The title of the current page in this tab
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// The current URL of this tab
        /// </summary>
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        /// <summary>
        /// The favicon of the current page
        /// </summary>
        public ImageSource? Favicon
        {
            get => _favicon;
            set => SetProperty(ref _favicon, value);
        }

        /// <summary>
        /// A thumbnail preview of the tab content
        /// </summary>
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set => SetProperty(ref _thumbnail, value);
        }

        /// <summary>
        /// Indicates if the tab is currently loading
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// When this tab was last accessed
        /// </summary>
        public DateTime LastAccessed
        {
            get => _lastAccessed;
            set => SetProperty(ref _lastAccessed, value);
        }

        /// <summary>
        /// When this tab was created
        /// </summary>
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        /// <summary>
        /// The WebView instance for this tab (preserves history/state)
        /// </summary>
        public MarketAlly.Maui.ViewEngine.BrowserView WebView { get; }

        /// <summary>
        /// The standard MAUI WebView for markdown content display
        /// </summary>
        public Microsoft.Maui.Controls.WebView MarkdownWebView { get; }

        /// <summary>
        /// Creates a new browser tab
        /// </summary>
        /// <param name="url">Initial URL to navigate to</param>
        /// <param name="enableRouteExtraction">Enable route extraction for this tab</param>
        /// <param name="normalizeRoutes">Normalize routes for this tab</param>
        /// <param name="userAgent">Custom user agent for this tab</param>
        /// <param name="userAgentMode">User agent mode (Default, Desktop, or Mobile)</param>
        /// <param name="maxRoutes">Maximum number of routes to extract</param>
        /// <param name="enableAdDetection">Enable ad detection for this tab</param>
        public BrowserTab(
            string? url = null,
            bool enableRouteExtraction = true,
            bool normalizeRoutes = true,
            string? userAgent = null,
            MarketAlly.Maui.ViewEngine.UserAgentMode userAgentMode = MarketAlly.Maui.ViewEngine.UserAgentMode.Default,
            int maxRoutes = 200,
            bool enableAdDetection = true)
        {
            _url = url ?? "https://www.google.com";

            // Create the ViewEngine WebView for this tab
            WebView = new MarketAlly.Maui.ViewEngine.BrowserView
            {
                Source = _url,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                EnableRouteExtraction = enableRouteExtraction,
                NormalizeRoutes = normalizeRoutes,
                EnableThumbnailCapture = false, // Using manual capture instead
                UserAgent = userAgent ?? "",
                UserAgentMode = userAgentMode,
                ForceLinkNavigation = true,
                MaxRoutes = maxRoutes,
                EnableZoom = true,
                ShowInlinePdf = true,
                ShowLoadingProgress = true,
                ShowPdfActions = false,
				EnableAdDetection = enableAdDetection
			};

            // Create the markdown WebView for this tab
            MarkdownWebView = new Microsoft.Maui.Controls.WebView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                BackgroundColor = Microsoft.Maui.Graphics.Colors.White
            };

            // Note: Navigation events are handled by BrowserViewModel which updates
            // this tab's properties and manages navigation state
        }

        private string GetDomainFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return "New Tab";
            }
        }

        /// <summary>
        /// Navigates this tab to a new URL
        /// </summary>
        public void NavigateTo(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            // Ensure URL has a scheme
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            Url = url;
            WebView.Source = new UrlWebViewSource { Url = url };
        }

        /// <summary>
        /// Cleanup when tab is closed
        /// </summary>
        public void Dispose()
        {
            // Event handlers are managed by BrowserViewModel
        }
    }
}