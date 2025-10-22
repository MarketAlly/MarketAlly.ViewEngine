using Microsoft.Extensions.Logging;
using UnifiedBrowser.Maui.Controls;
using UnifiedBrowser.Maui.Models;

namespace Slyv.Maui.Views
{
    /// <summary>
    /// Main browser page showcasing the UnifiedBrowser control
    /// </summary>
    public partial class BrowserPage : ContentPage
    {
        private readonly ILogger<BrowserPage>? _logger;
        private readonly UnifiedBrowserControl _browserControl;

        /// <summary>
        /// Constructor with dependency injection support
        /// </summary>
        public BrowserPage(UnifiedBrowserControl browserControl, ILogger<BrowserPage>? logger = null)
        {
            InitializeComponent();
            _logger = logger;
            _browserControl = browserControl;

            // Configure the browser control
            _browserControl.HomeUrl = "https://www.google.com";
            _browserControl.ShowAddressBar = true;
            _browserControl.ShowBottomNavigation = false;
            _browserControl.ShowNavigationInAddressBar = false;
            _browserControl.ShowLinksButton = true;
            _browserControl.ShowMarkdownButton = true;
            _browserControl.ShowShareButton = true;
            _browserControl.ShowSaveButton = true;
            _browserControl.EnableRouteExtraction = true;
            _browserControl.EnableJSONState = true;
            _browserControl.NormalizeRoutes = true;
            _browserControl.ShowRefreshButton = false;
            _browserControl.EnableAISummarization = true;
            _browserControl.EnableThumbnailCapture = true;
            _browserControl.ThumbnailWidth = 1920;
            _browserControl.MaxTokens = 8192;
            _browserControl.MaxContextTokens = 32456;
            _browserControl.UserAgentMode = MarketAlly.Maui.ViewEngine.UserAgentMode.Default;
            _browserControl.ThumbnailHeight = 800;
            _browserControl.AutoGenerateSummary = false;
            _browserControl.GeneratingSummaryMessage = "Analyzing page content...";

            // Configure bookmarks file path
            _browserControl.BookmarksFilePath = System.IO.Path.Combine(
                FileSystem.AppDataDirectory,
                "bookmarks.html"
            );

            // Configure default bookmark (optional - defaults to "MarketAlly" and "https://www.marketally.com")
            // Uncomment the lines below to customize the default bookmark:
            // _browserControl.DefaultBookmarkTitle = "My Company";
            // _browserControl.DefaultBookmarkUrl = "https://mycompany.com";

            // Add the control to the host
            BrowserHost.Content = _browserControl;

            // Subscribe to browser events
            _browserControl.Navigating += OnBrowserNavigating;
            _browserControl.Navigated += OnBrowserNavigated;
            _browserControl.PageLoadComplete += OnPageLoadComplete;
            _browserControl.LinkSelected += OnLinkSelected;
            _browserControl.PageDataChanged += OnPageDataChanged;
            _browserControl.PageTitleChanged += OnPageTitleChanged;
        }

        private UnifiedBrowserControl BrowserControl => _browserControl;

        /// <summary>
        /// Navigate to a specific URL
        /// </summary>
        public void NavigateToUrl(string url)
        {
            try
            {
                _logger?.LogInformation($"Navigating to URL: {url}");
                _browserControl.NavigateTo(url);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to navigate to URL: {url}");
            }
        }

        private void OnBrowserNavigating(object? sender, WebNavigatingEventArgs e)
        {
            _logger?.LogInformation($"Navigating to: {e.Url}");

            // You can cancel navigation here if needed
            // e.Cancel = true;
        }

        private void OnBrowserNavigated(object? sender, WebNavigatedEventArgs e)
        {
            _logger?.LogInformation($"Navigated to: {e.Url} with result: {e.Result}");

            // Update the page title
            Title = $"Slyv Browser - {BrowserControl.PageTitle}";
        }

        private async void OnPageLoadComplete(object? sender, EventArgs e)
        {
            _logger?.LogInformation("Page load complete");

            // Example: Auto-extract links when page loads
            if (false) // Set to true to enable auto-extraction
            {
                var links = await BrowserControl.ExtractLinksAsync();
                _logger?.LogInformation($"Found {links.Count} links on the page");
            }

            // Example: Auto-generate summary if enabled
            if (BrowserControl.AutoGenerateSummary && BrowserControl.EnableAISummarization)
            {
                try
                {
                    var summary = await BrowserControl.GenerateSummaryAsync();
                    _logger?.LogInformation("Summary generated successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to generate summary");
                }
            }
        }

        private void OnLinkSelected(object? sender, LinkSelectedEventArgs e)
        {
            _logger?.LogInformation($"Link selected: {e.SelectedLink.Title} - {e.SelectedLink.Url}");

            // The browser will automatically navigate to the selected link
            // You can add additional handling here if needed
        }

        private void OnPageDataChanged(object? sender, MarketAlly.Maui.ViewEngine.PageData e)
        {
            //_logger?.LogInformation($"Page data changed - Title: {e.Title}");

            //// Update any UI elements that depend on page data
            //Title = string.IsNullOrEmpty(e.Title) ? "Slyv Browser" : $"{e.Title}";
        }

        private void OnPageTitleChanged(object? sender, PageTitleChangedEventArgs e)
        {
            _logger?.LogInformation($"Page title changed - Title: {e.Title}, URL: {e.Url}");

            // Update the navigation page title
            Title = string.IsNullOrEmpty(e.Title) ? "Slyv Browser" : $"{e.Title}";
        }

        private void OnRefreshClicked(object sender, EventArgs e)
        {
            _logger?.LogInformation("Refresh button clicked");
            _browserControl.RefreshCurrentTab();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Force refresh the toolbar to ensure it's visible
            // Use very short delay to ensure control is fully loaded
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(10), () =>
            {
                // Force the browser control to refresh its toolbar
                if (BrowserControl != null)
                {
                    try
                    {
                        // Try to call ForceRefreshToolbar if available
                        var forceRefreshMethod = BrowserControl.GetType().GetMethod("ForceRefreshToolbar");
                        forceRefreshMethod?.Invoke(BrowserControl, null);
                    }
                    catch
                    {
                        // Fallback to InvalidateMeasure
                        BrowserControl.InvalidateMeasure();
                    }
                }
            });

            // Optional: Navigate to a specific URL when the page appears
            // BrowserControl.NavigateTo("https://docs.microsoft.com");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Clean up if needed
            _logger?.LogInformation("Browser page disappearing");
        }

        /// <summary>
        /// Handle hardware back button on Android
        /// </summary>
        protected override bool OnBackButtonPressed()
        {
            // If the browser can go back, do that instead of navigating away from the page
            if (BrowserControl.GetWebView().CanGoBackInHistory)
            {
                _ = BrowserControl.GoBackAsync();
                return true; // Prevent default back navigation
            }

            return base.OnBackButtonPressed();
        }
    }
}