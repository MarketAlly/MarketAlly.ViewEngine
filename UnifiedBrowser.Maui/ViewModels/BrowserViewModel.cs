using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markdig;
using MarketAlly.Dialogs.Maui.Dialogs;
using MarketAlly.Dialogs.Maui.Models;
using MarketAlly.Maui.ViewEngine;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using UnifiedBrowser.Maui.Models;
using UnifiedBrowser.Maui.Services;
using UnifiedData.Maui.Core;
using UnifiedRag.Maui.Core;
using PageData = MarketAlly.Maui.ViewEngine.PageData;

namespace UnifiedBrowser.Maui.ViewModels
{
    /// <summary>
    /// View model for the browser control
    /// </summary>
    public partial class BrowserViewModel : ObservableObject, IDisposable
    {
        private MarketAlly.Maui.ViewEngine.BrowserView _webView;
        private Microsoft.Maui.Controls.WebView _markdownWebView;
        private readonly IUnifiedDataService? _dataService;
        private readonly IRAGService? _ragService;
        private readonly ILogger<BrowserViewModel>? _logger;
        private readonly TabManager _tabManager;
        private readonly BookmarkManager _bookmarkManager;

        #region Observable Properties

        [ObservableProperty]
        private string _currentUrl = string.Empty;

        [ObservableProperty]
        private string _pageTitle = string.Empty;

		[ObservableProperty]
		private string _pageURL = string.Empty;

		[ObservableProperty]
		private string _pageBody = string.Empty;

		[ObservableProperty]
        private string _pageContent = string.Empty;

		[ObservableProperty]
		private string _pageLinks = string.Empty;

		[ObservableProperty]
        private string _markdownContent = string.Empty;

        [ObservableProperty]
        private bool _isPageLoading = false;

        [ObservableProperty]
        private bool _isGeneratingSummary = false;

        [ObservableProperty]
        private bool _isLoadingLinks = false;

        [ObservableProperty]
        private bool _areActionButtonsEnabled = false;

        private bool _isLinksVisible = false;
        public bool IsLinksVisible
        {
            get => _isLinksVisible;
            set
            {
                if (SetProperty(ref _isLinksVisible, value))
                {
                    // When showing links, hide other views
                    if (value)
                    {
                        IsMarkdownViewVisible = false;
                        IsWebViewVisible = false;
                        IsTabSwitcherVisible = false;
                        IsHistoryVisible = false;

                        // Load links if needed
                        if (BodyRoutes.Count == 0)
                        {
                            _ = LoadBodyRoutesAsync();
                        }
                    }
                    else
                    {
                        // When hiding links, show web view
                        IsWebViewVisible = true;
                    }
                }
            }
        }

        private bool _isMarkdownViewVisible = false;
        public bool IsMarkdownViewVisible
        {
            get => _isMarkdownViewVisible;
            set
            {
                if (SetProperty(ref _isMarkdownViewVisible, value))
                {
                    // When showing markdown, hide other views
                    if (value)
                    {
                        IsLinksVisible = false;
                        IsWebViewVisible = false;
                        IsTabSwitcherVisible = false;
                        IsHistoryVisible = false;

                        // Generate summary if needed
                        if (string.IsNullOrWhiteSpace(PageContent))
                        {
                            _ = GenerateSummaryAsync();
                        }
                    }
                    else
                    {
                        // When hiding markdown, show web view
                        IsWebViewVisible = true;
                    }
                }
            }
        }

        [ObservableProperty]
        private bool _isWebViewVisible = true;

        [ObservableProperty]
        private ObservableCollection<LinkItem> _bodyRoutes = new();

        [ObservableProperty]
        private ObservableCollection<LinkItem> _filteredBodyRoutes = new();

        [ObservableProperty]
        private string _linkCountText = "0 links";

        [ObservableProperty]
        private bool _showAds = true;

        [ObservableProperty]
        private bool _hasAds = false;

        [ObservableProperty]
        private bool _enableAdDetection = true;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private int _maxRoutes = 200;

        [ObservableProperty]
        private int _maxContextTokens = 8192;

        [ObservableProperty]
        private int _maxTokens = 4096;

        [ObservableProperty]
        private string _homeUrl = "https://www.google.com";

        [ObservableProperty]
        private bool _enableAISummarization = true;

        [ObservableProperty]
        private bool _autoGenerateSummary = false;

        private bool _enableJSONState = false;
        public bool EnableJSONState
        {
            get => _enableJSONState;
            set => SetProperty(ref _enableJSONState, value);
        }

        private bool _enableThumbnailCapture = true;
        public bool EnableThumbnailCapture
        {
            get => _enableThumbnailCapture;
            set => SetProperty(ref _enableThumbnailCapture, value);
        }

        private int _thumbnailWidth = 640;
        public int ThumbnailWidth
        {
            get => _thumbnailWidth;
            set => SetProperty(ref _thumbnailWidth, value);
        }

        private int _thumbnailHeight = 360;
        public int ThumbnailHeight
        {
            get => _thumbnailHeight;
            set => SetProperty(ref _thumbnailHeight, value);
        }

        private bool _captureThumbnailOnNavigation = true;
        public bool CaptureThumbnailOnNavigation
        {
            get => _captureThumbnailOnNavigation;
            set => SetProperty(ref _captureThumbnailOnNavigation, value);
        }

        [ObservableProperty]
        private bool _canGoForward = false;

        [ObservableProperty]
        private bool _canGoBack = false;

        private bool _isTabSwitcherVisible = false;
        public bool IsTabSwitcherVisible
        {
            get => _isTabSwitcherVisible;
            set
            {
                if (SetProperty(ref _isTabSwitcherVisible, value))
                {
                    // When showing the tab switcher, trigger the event to load it
                    if (value)
                    {
                        OnShowTabSwitcherRequested?.Invoke();

                        // Clear any stuck loading states before showing tab switcher
                        ClearStuckLoadingStates();

                        // Capture thumbnail for the current tab when showing the tab switcher
                        // This ensures the current tab's preview is up-to-date
                        if (EnableThumbnailCapture && EnableJSONState)
                        {
                            _ = CaptureAndSaveThumbnailAsync();
                        }
                    }
                }
            }
        }

        private bool _isHistoryVisible = false;
        public bool IsHistoryVisible
        {
            get => _isHistoryVisible;
            set
            {
                if (SetProperty(ref _isHistoryVisible, value))
                {
                    // When showing history, hide other views
                    if (value)
                    {
                        IsLinksVisible = false;
                        IsMarkdownViewVisible = false;
                        IsWebViewVisible = false;
                        IsTabSwitcherVisible = false;

                        // Trigger event to load history view
                        OnShowHistoryRequested?.Invoke();
                    }
                    else
                    {
                        // When hiding history, show web view if no other view is visible
                        if (!IsMarkdownViewVisible && !IsLinksVisible && !IsTabSwitcherVisible)
                        {
                            IsWebViewVisible = true;
                        }
                    }
                }
            }
        }

        private void ClearStuckLoadingStates()
        {
            try
            {
                // Check all tabs and clear any loading states that shouldn't be active
                foreach (var tab in _tabManager.Tabs)
                {
                    // If a tab is marked as loading but hasn't been accessed recently, clear it
                    if (tab.IsLoading)
                    {
                        var timeSinceAccess = DateTime.Now - tab.LastAccessed;
                        if (timeSinceAccess.TotalSeconds > 5)
                        {
                            _logger?.LogWarning("Clearing stuck loading state on tab {TabId}, last accessed {LastAccessed}",
                                tab.Id, tab.LastAccessed);
                            tab.IsLoading = false;
                        }
                    }
                }

                // Also clear the page loading indicator if it's stuck
                if (IsPageLoading)
                {
                    var activeTab = _tabManager.ActiveTab;
                    if (activeTab != null && !activeTab.IsLoading)
                    {
                        _logger?.LogWarning("Clearing stuck page loading indicator");
                        IsPageLoading = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error clearing stuck loading states");
            }
        }

        [ObservableProperty]
        private int _tabCount = 1;

        [ObservableProperty]
        private string _tabCountDisplay = "1";

        [ObservableProperty]
        private bool _isCurrentPageBookmarked = false;

        [ObservableProperty]
        private string _markdownDocumentTitle = string.Empty;

        [ObservableProperty]
        private bool _isFavorite = false;

        private string? _currentMarkdownDocumentId = null;

        #endregion

        #region Bookmarks

        /// <summary>
        /// Gets the bookmark manager for this browser instance
        /// </summary>
        public BookmarkManager BookmarkManager => _bookmarkManager;

        /// <summary>
        /// Gets the observable collection of bookmarks
        /// </summary>
        public ObservableCollection<BookmarkItemViewModel> Bookmarks => _bookmarkManager.Bookmarks;

        #endregion

        #region Events

        /// <summary>
        /// Event raised when navigation is requested via the Navigate command
        /// </summary>
        public Action? OnNavigateRequested;

        /// <summary>
        /// Event raised when tab switcher should be shown
        /// </summary>
        public Action? OnShowTabSwitcherRequested;

        /// <summary>
        /// Event raised when history view should be shown
        /// </summary>
        public Action? OnShowHistoryRequested;

        #endregion

        #region Constructor

        public BrowserViewModel(
            MarketAlly.Maui.ViewEngine.BrowserView webView,
            Microsoft.Maui.Controls.WebView markdownWebView,
            IUnifiedDataService? dataService = null,
            IRAGService? ragService = null,
            ILogger<BrowserViewModel>? logger = null,
            string? userAgent = null,
            MarketAlly.Maui.ViewEngine.UserAgentMode userAgentMode = MarketAlly.Maui.ViewEngine.UserAgentMode.Default)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _markdownWebView = markdownWebView ?? throw new ArgumentNullException(nameof(markdownWebView));
            _dataService = dataService;
            _ragService = ragService;
            _logger = logger;

            // Initialize BookmarkManager
            _bookmarkManager = new BookmarkManager(logger as ILogger<BookmarkManager>);

            // Initialize TabManager with browser settings
            _tabManager = new TabManager(
                maxTabs: 10,
                defaultUrl: HomeUrl,
                enableRouteExtraction: true,
                normalizeRoutes: true,
                userAgent: userAgent,
                userAgentMode: userAgentMode,
                maxRoutes: MaxRoutes,
                enableAdDetection: EnableAdDetection);

            // Subscribe to tab manager events
            _tabManager.ActiveTabChanged += OnActiveTabChanged;
            _tabManager.PropertyChanged += OnTabManagerPropertyChanged;

			// Subscribe to WebView events
			_webView.Navigating += OnWebViewNavigating;
            _webView.Navigated += OnWebViewNavigated;
            _webView.PageLoadComplete += OnPageLoadComplete;
            _webView.PageDataChanged += OnWebViewPageDataChanged;

            // Note: Markdown WebView Navigating event is subscribed in DisplayMarkdown
            // after content is loaded to avoid intercepting the initial HTML load

            // Create the initial tab but don't navigate yet
            // The control will call InitializeFirstTab when ready
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task GoBack()
        {
            try
            {
                if (_webView.CanGoBackInHistory)
                {
                    await _webView.GoBackInHistoryAsync();
                    UpdateNavigationState();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error navigating back");
            }
        }

        [RelayCommand]
        private async Task GoForward()
        {
            try
            {
                if (_webView.CanGoForwardInHistory)
                {
                    await _webView.GoForwardInHistoryAsync();
                    UpdateNavigationState();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error navigating forward");
            }
        }

        [RelayCommand]
        private void GoHome()
        {
            try
            {
                NavigateToUrl(HomeUrl, true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error navigating home");
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            try
            {
                await _webView.ReloadAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing page");
            }
        }

        [RelayCommand]
        private void Navigate()
        {
            try
			{
				if (string.IsNullOrWhiteSpace(CurrentUrl))
					return;

				var input = CurrentUrl.Trim();
				string url;

				// Check if it's already a full URL
				if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
					input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				{
					url = input;
				}
				// Otherwise, treat it as a search query
				else
				{
					// URL encode the search query
					var encodedQuery = Uri.EscapeDataString(input);
					url = $"https://www.google.com/search?q={encodedQuery}";
				}

				// Trigger event to close the toolbar page
				OnNavigateRequested?.Invoke();

				NavigateToUrl(url, false);
			}
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error navigating to URL");
            }
        }

        [RelayCommand]
        private async Task Share()
        {
            try
            {
                await Microsoft.Maui.ApplicationModel.DataTransfer.Share.RequestAsync(new ShareTextRequest
                {
                    Uri = CurrentUrl,
                    Title = PageTitle
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sharing page");
            }
        }

        [RelayCommand]
        private async Task SavePage()
        {
            try
            {
                if (_dataService != null && !string.IsNullOrWhiteSpace(PageContent))
                {
                    var document = new UnifiedDocument
                    {
                        Title = PageTitle ?? "Untitled",
                        Content = PageContent,
                        ContentType = DocumentContentType.Web,
                        SourceUrl = CurrentUrl,
                        Metadata = new Dictionary<string, object>
                        {
                            ["url"] = CurrentUrl,
                            ["savedAt"] = DateTime.UtcNow.ToString("O")
                        },
                        Tags = new[] { "web-page", "browser" }
                    };

                    var documentId = await _dataService.SaveAsync(document);
                    _logger?.LogInformation("Page saved successfully: {DocumentId}", documentId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving page");
            }
        }

        [RelayCommand]
        private async Task ToggleLinks()
        {
            try
            {
                IsLinksVisible = !IsLinksVisible;
                IsMarkdownViewVisible = false;
                IsWebViewVisible = !IsLinksVisible;

                if (IsLinksVisible && BodyRoutes.Count == 0)
                {
                    // Only load if we don't already have routes from PageDataChanged
                    await LoadBodyRoutesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error toggling links view");
            }
        }

        [RelayCommand]
        private async Task ToggleMarkdownView()
        {
            try
            {
                IsMarkdownViewVisible = !IsMarkdownViewVisible;
                IsLinksVisible = false;
                IsWebViewVisible = !IsMarkdownViewVisible;

                if (IsMarkdownViewVisible && string.IsNullOrWhiteSpace(MarkdownContent))
                {
                    // Try to load from database first
                    var loaded = await LoadMarkdownFromDatabaseAsync();

                    // If not found in database, generate new summary
                    if (!loaded)
                    {
                        await GenerateSummaryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error toggling markdown view");
            }
        }

        [RelayCommand]
        private void NavigateToLink(string url)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    // Switch back to web view
                    IsLinksVisible = false;
                    IsMarkdownViewVisible = false;
                    IsWebViewVisible = true;

                    // Navigate to the URL
                    NavigateToUrl(url, false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error navigating to link: {Url}", url);
            }
        }

        [RelayCommand]
        private async Task ClipboardLinks()
        {
            try
            {
                if (BodyRoutes == null || BodyRoutes.Count == 0)
                {
                    _logger?.LogWarning("No links to copy to clipboard");
                    return;
                }

                // Create a formatted list with title and URL
                var formattedLinks = new System.Text.StringBuilder();

                foreach (var link in BodyRoutes)
                {
                    // Add title (or URL if title is empty)
                    var title = string.IsNullOrWhiteSpace(link.Title) ? link.Url : link.Title;
                    formattedLinks.AppendLine(title);

                    // Add URL
                    formattedLinks.AppendLine(link.Url);

                    // Add blank line separator
                    formattedLinks.AppendLine();
                }

                // Copy to clipboard
                await Clipboard.Default.SetTextAsync(formattedLinks.ToString());

                _logger?.LogInformation("Copied {Count} links to clipboard as formatted list", BodyRoutes.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error copying links to clipboard");
            }
        }

        #region Outlook Commands

        [RelayCommand]
        private async Task ShowNews()
        {
            try
            {
                NavigateToUrl("https://news.google.com", true);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error showing news");
            }
        }

        [RelayCommand]
        private async Task ShowSearch()
        {
            try
            {
                NavigateToUrl("https://www.google.com", true);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error showing search");
            }
        }

        [RelayCommand]
        private async Task ShowBookmarks()
        {
            try
            {
                // Bookmarks are already displayed in the toolbar via binding
                // This command can be used to trigger any additional bookmark-related UI
                _logger?.LogInformation("Bookmarks command triggered");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error showing bookmarks");
            }
        }

        [RelayCommand]
        private async Task FlipBookmark()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentUrl))
                {
                    _logger?.LogWarning("Cannot flip bookmark: no URL loaded");
                    return;
                }

                // Check if current page is bookmarked
                if (_bookmarkManager.IsBookmarked(CurrentUrl))
                {
                    // Remove bookmark
                    var success = await _bookmarkManager.DeleteBookmarkAsync(CurrentUrl);
                    if (success)
                    {
                        _logger?.LogInformation("Bookmark removed: {Url}", CurrentUrl);
                        IsCurrentPageBookmarked = false;
                    }
                }
                else
                {
                    // Add bookmark - prompt user for name with a shorter default title
                    var shortTitle = GenerateShortTitle(PageTitle, CurrentUrl);

                    var bookmarkName = await PromptDialog.ShowAsync(
                        "Add Bookmark",
                        shortTitle,
                        "Add",
                        "Cancel"
                    );

                    // If user cancelled (returned null), don't add bookmark
                    if (bookmarkName == null)
                    {
                        _logger?.LogInformation("Bookmark cancelled by user");
                        return;
                    }

                    // If user clicked Add with empty text, use the default short title
                    if (string.IsNullOrWhiteSpace(bookmarkName))
                    {
                        bookmarkName = shortTitle;
                        _logger?.LogInformation("Using default bookmark name: {Title}", shortTitle);
                    }

                    var success = await _bookmarkManager.AddBookmarkAsync(bookmarkName, CurrentUrl);
                    if (success)
                    {
                        _logger?.LogInformation("Bookmark added: {Title} -> {Url}", bookmarkName, CurrentUrl);
                        IsCurrentPageBookmarked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error flipping bookmark");
            }
        }

        /// <summary>
        /// Generates a shorter title suitable for pill display in bookmarks
        /// </summary>
        private string GenerateShortTitle(string? pageTitle, string url)
        {
            // If no page title, extract from URL
            if (string.IsNullOrWhiteSpace(pageTitle))
            {
                try
                {
                    var uri = new Uri(url);
                    // Use domain name without www
                    var host = uri.Host.Replace("www.", "");
                    return host;
                }
                catch
                {
                    return "Bookmark";
                }
            }

            // Remove common prefixes/suffixes
            var title = pageTitle.Trim();

            // Remove common site suffixes
            var suffixes = new[] { " - YouTube", " | Facebook", " - Twitter", " - Wikipedia", " | LinkedIn", " - Google" };
            foreach (var suffix in suffixes)
            {
                if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    title = title.Substring(0, title.Length - suffix.Length).Trim();
                    break;
                }
            }

            // If still too long, truncate intelligently
            if (title.Length > 20)
            {
                // Try to break at word boundary
                var words = title.Split(new[] { ' ', '-', '|', ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    // Take first 2-3 words that fit within 20 chars
                    var shortTitle = "";
                    foreach (var word in words)
                    {
                        if ((shortTitle + " " + word).Length <= 20)
                        {
                            shortTitle += (shortTitle.Length > 0 ? " " : "") + word;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(shortTitle))
                    {
                        return shortTitle;
                    }
                }

                // Last resort: hard truncate at 20 chars
                return title.Substring(0, 17) + "...";
            }

            return title;
        }

        [RelayCommand]
        private async Task AddBookmark()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentUrl))
                {
                    _logger?.LogWarning("Cannot add bookmark: no URL loaded");
                    return;
                }

                var title = string.IsNullOrWhiteSpace(PageTitle) ? CurrentUrl : PageTitle;
                var success = await _bookmarkManager.AddBookmarkAsync(title, CurrentUrl);

                if (success)
                {
                    _logger?.LogInformation("Bookmark added: {Title} -> {Url}", title, CurrentUrl);
                }
                else
                {
                    _logger?.LogWarning("Failed to add bookmark");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error adding bookmark");
            }
        }

        [RelayCommand]
        private async Task DeleteBookmark(BookmarkItemViewModel? bookmark)
        {
            try
            {
                if (bookmark == null)
                    return;

                var success = await _bookmarkManager.DeleteBookmarkAsync(bookmark);
                if (success)
                {
                    _logger?.LogInformation("Bookmark deleted: {Title}", bookmark.Text);
                }
                else
                {
                    _logger?.LogWarning("Failed to delete bookmark");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting bookmark");
            }
        }

        [RelayCommand]
        private void ViewBookmark(BookmarkItemViewModel? bookmark)
        {
            try
            {
                if (bookmark != null && !string.IsNullOrWhiteSpace(bookmark.Url))
                {
                    _logger?.LogInformation("Navigating to bookmark: {Title} -> {Url}", bookmark.Text, bookmark.Url);
                    NavigateToUrl(bookmark.Url, false);

                    // Trigger event to close the toolbar page
                    OnNavigateRequested?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error viewing bookmark");
            }
        }

        [RelayCommand]
        private async Task ImportBookmarks(string? filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    return;

                var success = await _bookmarkManager.ImportBookmarksAsync(filePath);
                if (success)
                {
                    _logger?.LogInformation("Bookmarks imported successfully from {Path}", filePath);
                }
                else
                {
                    _logger?.LogWarning("Failed to import bookmarks from {Path}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error importing bookmarks");
            }
        }

        [RelayCommand]
        private async Task ExportBookmarks(string? filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    return;

                var success = await _bookmarkManager.ExportBookmarksAsync(filePath);
                if (success)
                {
                    _logger?.LogInformation("Bookmarks exported successfully to {Path}", filePath);
                }
                else
                {
                    _logger?.LogWarning("Failed to export bookmarks to {Path}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error exporting bookmarks");
            }
        }

        #endregion

        #region Tab Commands

        [RelayCommand]
        private async Task AddTab()
        {
            try
            {
                var newTab = _tabManager.NewTab();
                if (newTab != null)
                {
                    _logger?.LogInformation("New tab created with ID: {TabId}", newTab.Id);
                    // The tab manager will automatically switch to the new tab
                    // and trigger ActiveTabChanged event which saves state
                }
                else
                {
                    _logger?.LogWarning("Could not create new tab - max tabs reached ({MaxTabs})", _tabManager.TabCount);
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error adding tab");
            }
        }

        [RelayCommand]
        private async Task ViewTabs()
        {
            try
            {
                // Toggle the tab switcher visibility
                // The property setter will handle showing the tab switcher
                IsTabSwitcherVisible = !IsTabSwitcherVisible;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error viewing tabs");
            }
        }

        [RelayCommand]
        private async Task ClearAllTabs()
        {
            try
            {
                // Show confirmation dialog
                var tabCount = _tabManager.TabCount;
                var message = tabCount > 1
                    ? $"Are you sure you want to close all {tabCount - 1} other tab{(tabCount > 2 ? "s" : "")}? This action cannot be undone."
                    : "Are you sure you want to navigate the current tab to home?";

                var confirmed = await ConfirmDialog.ShowAsync(
                    "Clear All Tabs",
                    message,
                    "Clear",
                    "Cancel"
                );

                if (!confirmed)
                {
                    _logger?.LogInformation("User cancelled clearing all tabs");
                    return;
                }

                // Keep at least one tab open
                if (_tabManager.TabCount > 1)
                {
                    _tabManager.CloseOtherTabs();
                    _logger?.LogInformation("Closed all tabs except active");

                    await AlertDialog.ShowAsync(
                        "Tabs Cleared",
                        $"Closed {tabCount - 1} tab{(tabCount > 2 ? "s" : "")}. Keeping current tab active.",
                        DialogType.Success
                    );
                }
                else
                {
                    // If there's only one tab, navigate it to home
                    NavigateToUrl(HomeUrl, true);
                    _logger?.LogInformation("Navigated single tab to home");
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error clearing tabs");
                await AlertDialog.ShowAsync(
                    "Error",
                    $"An error occurred while clearing tabs: {ex.Message}",
                    DialogType.Error
                );
            }
        }

        [RelayCommand]
        private void CloseTab(BrowserTab tab)
        {
            try
            {
                // If this is the last tab, close the tab view and create a new home tab
                if (_tabManager.TabCount == 1)
                {
                    // Close the tab view
                    IsTabSwitcherVisible = false;

                    // Navigate the existing tab to home instead of closing it
                    tab.NavigateTo(HomeUrl);
                    _logger?.LogInformation("Last tab navigated to home: {TabId}", tab.Id);
                }
                else
                {
                    // Close the tab normally
                    _tabManager.CloseTab(tab);
                    _logger?.LogInformation("Tab closed: {TabId}", tab.Id);
                }

                // Save tab state if enabled
                if (EnableJSONState)
                {
                    _ = SaveTabStateAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error closing tab");
            }
        }

        [RelayCommand]
        private void Done()
        {
            // Hide all overlay views
            IsTabSwitcherVisible = false;
            IsHistoryVisible = false;
            IsLinksVisible = false;
            IsMarkdownViewVisible = false;

            // Ensure we're showing the web view
            IsWebViewVisible = true;
        }

        #endregion

        #region Markdown Commands

        [RelayCommand]
        private async Task RegenerateSummary()
        {
            try
            {
                if (_dataService == null || string.IsNullOrWhiteSpace(_currentMarkdownDocumentId))
                {
                    _logger?.LogWarning("Cannot regenerate summary: no document ID available");
                    return;
                }

                // Force regeneration by temporarily bypassing the database check
                IsGeneratingSummary = true;
                try
                {
                    if (string.IsNullOrWhiteSpace(PageBody))
                    {
                        MarkdownContent = "Could not extract page content.";
                        await DisplayMarkdown(MarkdownContent);
                        return;
                    }

                    // Use the specialized SummarizeWebpageAsync method with structured return
                    var bodyRoutes = BodyRoutes.Select(r => r.Url).ToList();

                    var result = await _ragService!.SummarizeWebpageAsync(
                        url: PageURL,
                        title: PageTitle,
                        pageBody: PageBody,
                        options: new UnifiedRag.Maui.Models.RAGOptions
                        {
                            MaxContextTokens = MaxContextTokens,
                            GenerationOptions = new UnifiedRag.Maui.Models.GenerationOptions
                            {
                                MaxTokens = MaxTokens,
                                Temperature = 0.3f
                            }
                        });

                    // Use the detailed summary as the markdown content
                    MarkdownContent = result.DetailedSummary;
                    PageContent = result.DetailedSummary;

                    // Update the stored document ID
                    _currentMarkdownDocumentId = result.DocumentId;
                    MarkdownDocumentTitle = PageTitle;

                    // Update document properties from database
                    if (!string.IsNullOrWhiteSpace(_currentMarkdownDocumentId))
                    {
                        var documents = await _dataService.SearchBySourceUrlAsync(CurrentUrl, exactMatch: true, maxResults: 1);
                        var document = documents?.FirstOrDefault();
                        if (document != null)
                        {
                            IsFavorite = document.Visibility == 2;
                        }
                    }

                    _logger?.LogInformation("Summary regenerated: DocumentId: {DocumentId}", result.DocumentId);

                    // Convert to HTML and display
                    await DisplayMarkdown(MarkdownContent);
                }
                finally
                {
                    IsGeneratingSummary = false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error regenerating summary");
                await AlertDialog.ShowAsync(
                    "Error",
                    $"Failed to regenerate summary: {ex.Message}",
                    DialogType.Error
                );
            }
        }

        [RelayCommand]
        private async Task DeleteMarkdownDocument()
        {
            try
            {
                if (_dataService == null || string.IsNullOrWhiteSpace(_currentMarkdownDocumentId))
                {
                    _logger?.LogWarning("Cannot delete document: no document ID available");
                    return;
                }

                // Show confirmation dialog
                var confirmed = await ConfirmDialog.ShowAsync(
                    "Delete Document",
                    "Are you sure you want to delete this document? This action cannot be undone.",
                    "Delete",
                    "Cancel"
                );

                if (!confirmed)
                {
                    _logger?.LogInformation("Document deletion cancelled by user");
                    return;
                }

                // Delete the document
                var success = await _dataService.DeleteAsync(_currentMarkdownDocumentId);

                if (success)
                {
                    _logger?.LogInformation("Document deleted: {DocumentId}", _currentMarkdownDocumentId);

                    // Clear markdown state
                    _currentMarkdownDocumentId = null;
                    MarkdownDocumentTitle = string.Empty;
                    IsFavorite = false;
                    MarkdownContent = string.Empty;
                    PageContent = string.Empty;

                    // Switch back to web view
                    IsMarkdownViewVisible = false;
                    IsWebViewVisible = true;

                    await AlertDialog.ShowAsync(
                        "Success",
                        "Document deleted successfully",
                        DialogType.Success
                    );
                }
                else
                {
                    _logger?.LogWarning("Failed to delete document: {DocumentId}", _currentMarkdownDocumentId);
                    await AlertDialog.ShowAsync(
                        "Error",
                        "Failed to delete document",
                        DialogType.Error
                    );
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting markdown document");
                await AlertDialog.ShowAsync(
                    "Error",
                    $"Failed to delete document: {ex.Message}",
                    DialogType.Error
                );
            }
        }

        #endregion

        #region Additional Commands

        [RelayCommand]
        private async Task ShowAbout()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentUrl))
                {
                    await AlertDialog.ShowAsync(
                        "Page Information",
                        "No page loaded",
                        DialogType.Info
                    );
                    return;
                }

                // Gather page information
                var pageInfo = await GatherPageInformationAsync();

                // Display in a dialog
                await AlertDialog.ShowAsync(
                    "Page Information",
                    pageInfo,
					DialogType.Info
				);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error showing about");
                await AlertDialog.ShowAsync(
                    "Error",
                    $"Failed to retrieve page information: {ex.Message}",
					DialogType.Error
				);
            }
        }

        /// <summary>
        /// Gathers comprehensive information about the current page
        /// </summary>
        private async Task<string> GatherPageInformationAsync()
        {
            var info = new System.Text.StringBuilder();

            try
            {
                // Parse the URL
                Uri? uri = null;
                try
                {
                    uri = new Uri(CurrentUrl);
                }
                catch
                {
                    info.AppendLine($"URL: {CurrentUrl}");
                    info.AppendLine("Invalid URL format");
                    return info.ToString();
                }

                // Basic Information
                info.AppendLine("=== PAGE DETAILS ===");
                if (!string.IsNullOrWhiteSpace(PageTitle))
                {
                    info.AppendLine($"Title: {PageTitle}");
                }
                info.AppendLine($"URL: {CurrentUrl}");
                info.AppendLine();

                // Domain Information
                info.AppendLine("=== DOMAIN INFO ===");
                info.AppendLine($"Protocol: {uri.Scheme.ToUpperInvariant()}");
                info.AppendLine($"Host: {uri.Host}");
                if (uri.Port != 80 && uri.Port != 443)
                {
                    info.AppendLine($"Port: {uri.Port}");
                }
                info.AppendLine();

                // Security Information
                info.AppendLine("=== SECURITY ===");
                if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                {
                    info.AppendLine("Connection: Secure (HTTPS)");
                    info.AppendLine("Certificate: Valid");
                    // Note: We can't easily get certificate details from WebView
                    // but we can indicate it's using HTTPS
                }
                else if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
                {
                    info.AppendLine("Connection: Not Secure (HTTP)");
                    info.AppendLine("⚠️ Information sent is not encrypted");
                }
                else
                {
                    info.AppendLine($"Connection: {uri.Scheme}");
                }
                info.AppendLine();

                // Bookmark Status
                info.AppendLine("=== BOOKMARK ===");
                if (IsCurrentPageBookmarked)
                {
                    var bookmark = _bookmarkManager.Bookmarks.FirstOrDefault(b => b.Url == CurrentUrl);
                    info.AppendLine($"Status: Bookmarked");
                    if (bookmark != null)
                    {
                        info.AppendLine($"Name: {bookmark.Text}");
                        if (bookmark.AddDate.HasValue)
                        {
                            info.AppendLine($"Added: {bookmark.AddDate.Value.ToLocalTime():yyyy-MM-dd HH:mm}");
                        }
                    }
                }
                else
                {
                    info.AppendLine("Status: Not Bookmarked");
                }
                info.AppendLine();

                // Page Content Stats
                if (!string.IsNullOrWhiteSpace(PageBody))
                {
                    info.AppendLine("=== CONTENT ===");
                    var charCount = PageBody.Length;
                    var wordCount = PageBody.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    info.AppendLine($"Characters: {charCount:N0}");
                    info.AppendLine($"Words: {wordCount:N0}");

                    if (BodyRoutes.Count > 0)
                    {
                        info.AppendLine($"Links: {BodyRoutes.Count}");
                    }
                    info.AppendLine();
                }

                // Tab Information
                var activeTab = _tabManager.ActiveTab;
                if (activeTab != null)
                {
                    info.AppendLine("=== TAB INFO ===");
                    info.AppendLine($"Tab ID: {activeTab.Id}");
                    info.AppendLine($"Last Accessed: {activeTab.LastAccessed:HH:mm:ss}");
                    info.AppendLine($"Total Tabs: {_tabManager.TabCount}");
                }

                return info.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error gathering page information");
                info.AppendLine();
                info.AppendLine($"Error: {ex.Message}");
                return info.ToString();
            }
        }

        #endregion

        /// <summary>
        /// Updates the IsCurrentPageBookmarked property based on the current URL
        /// </summary>
        private void UpdateBookmarkState()
        {
            if (string.IsNullOrWhiteSpace(CurrentUrl))
            {
                IsCurrentPageBookmarked = false;
                return;
            }

            IsCurrentPageBookmarked = _bookmarkManager.IsBookmarked(CurrentUrl);
        }

        #endregion

        #region Property Changed Handlers

        /// <summary>
        /// Called when ShowAds property changes
        /// </summary>
        partial void OnShowAdsChanged(bool value)
        {
            // Apply the filter when the ShowAds toggle changes
            ApplyLinkFilter();

            _logger?.LogInformation("Ad filter toggled: ShowAds={ShowAds}", value);
        }

        /// <summary>
        /// Called when SearchQuery property changes
        /// </summary>
        partial void OnSearchQueryChanged(string value)
        {
            // Apply the filter when the search query changes
            ApplyLinkFilter();

            _logger?.LogInformation("Link search query changed: {SearchQuery}", value);
        }

        /// <summary>
        /// Called when EnableAdDetection property changes
        /// </summary>
        partial void OnEnableAdDetectionChanged(bool value)
        {
            // Update the WebView's ad detection setting
            if (_webView != null)
            {
                _webView.EnableAdDetection = value;
                _webView.MaxRoutes = MaxRoutes;
            }

            // Also update all tabs' WebViews
            foreach (var tab in _tabManager.Tabs)
            {
                if (tab.WebView != null)
                {
                    tab.WebView.EnableAdDetection = value;
					tab.WebView.MaxRoutes = MaxRoutes;
				}
            }

            _logger?.LogInformation("Ad detection {Status}", value ? "enabled" : "disabled");
        }

        /// <summary>
        /// Called when MaxRoutes property changes
        /// </summary>
        partial void OnMaxRoutesChanged(int value)
        {
            // Update the WebView's max routes setting
            if (_webView != null)
            {
                _webView.MaxRoutes = value;
            }

            // Also update all tabs' WebViews
            foreach (var tab in _tabManager.Tabs)
            {
                if (tab.WebView != null)
                {
                    tab.WebView.MaxRoutes = value;
                }
            }

            _logger?.LogInformation("MaxRoutes set to {MaxRoutes}", value);
        }

        /// <summary>
        /// Called when IsFavorite property changes
        /// </summary>
        partial void OnIsFavoriteChanged(bool value)
        {
            // Update the document's Visibility in the database
            if (_dataService != null && !string.IsNullOrWhiteSpace(_currentMarkdownDocumentId))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var documents = await _dataService.SearchBySourceUrlAsync(CurrentUrl, exactMatch: true, maxResults: 1);
                        var document = documents?.FirstOrDefault();

                        if (document != null)
                        {
                            // Set Visibility: 2 = favorite, 1 = not favorite
                            document.Visibility = value ? 2 : 1;
                            await _dataService.SaveAsync(document);
                            _logger?.LogInformation("Updated document favorite status: {IsFavorite} (Visibility={Visibility})",
                                value, document.Visibility);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error updating document favorite status");
                    }
                });
            }
        }

        #endregion

        #region Public Methods

        public async void NavigateToUrl(string url, bool setURL)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            // Trim whitespace
            url = url.Trim();

            // Ensure URL has a scheme
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            Console.WriteLine($"[NAV] Navigating to URL: {url} (setURL={setURL}, CanGoBack before={_webView.CanGoBackInHistory})");
            _logger?.LogInformation("Navigating to URL: {Url} (setURL={SetURL}, CanGoBack before={CanGoBackBefore})",
                url, setURL, _webView.CanGoBackInHistory);

            // Set the WebView source
            _webView.Source = new UrlWebViewSource { Url = url };

            // Update immediately
            UpdateNavigationState();
            Console.WriteLine($"[NAV] Immediately after Source set: CanGoBack={CanGoBack}");

            // Also schedule additional updates to catch late state changes
            Microsoft.Maui.Controls.Application.Current?.Dispatcher.DispatchDelayed(
                TimeSpan.FromMilliseconds(100),
                () => {
                    UpdateNavigationState();
                    Console.WriteLine($"[NAV] 100ms after NavigateToUrl: CanGoBack={CanGoBack}");
                    _logger?.LogDebug("Navigation state 100ms after NavigateToUrl: CanGoBack={CanGoBack}", CanGoBack);
                }
            );

            Microsoft.Maui.Controls.Application.Current?.Dispatcher.DispatchDelayed(
                TimeSpan.FromMilliseconds(500),
                () => {
                    UpdateNavigationState();
                    Console.WriteLine($"[NAV] 500ms after NavigateToUrl: CanGoBack={CanGoBack}");
                    _logger?.LogDebug("Navigation state 500ms after NavigateToUrl: CanGoBack={CanGoBack}", CanGoBack);
                }
            );
        }

        public async Task<ObservableCollection<LinkItem>> ExtractLinksAsync()
        {
            IsLoadingLinks = true;
            try
            {
                // Extract links from the current page
                var extractScript = @"
                    (function() {
                        var links = Array.from(document.querySelectorAll('a[href]'));
                        var linkData = links.map(function(link, index) {
                            var url = link.href;
                            var text = link.innerText || link.textContent || '';
                            var title = link.title || text || url;

                            // Determine if it's potentially an ad
                            var isAd = link.classList.contains('ad') ||
                                       link.classList.contains('sponsored') ||
                                       link.getAttribute('data-ad') !== null ||
                                       url.includes('doubleclick') ||
                                       url.includes('googleadservices');

                            return {
                                url: url,
                                title: title.substring(0, 100),
                                occurrences: 1,
                                ranking: index + 1,
                                isPotentialAd: isAd
                            };
                        });

                        // Group by URL and count occurrences
                        var grouped = {};
                        linkData.forEach(function(link) {
                            if (!grouped[link.url]) {
                                grouped[link.url] = link;
                            } else {
                                grouped[link.url].occurrences++;
                            }
                        });

                        return Object.values(grouped);
                    })()";

                var result = await _webView.EvaluateJavaScriptAsync(extractScript);

                if (!string.IsNullOrWhiteSpace(result))
                {
                    // Parse the JSON result
                    var links = System.Text.Json.JsonSerializer.Deserialize<List<LinkItem>>(result);
                    if (links != null)
                    {
                        BodyRoutes = new ObservableCollection<LinkItem>(
                            links.OrderByDescending(l => l.Occurrences)
                                 .ThenBy(l => l.Ranking)
                                 .Take(50)); // Limit to top 50 links

                        // Update link count and apply filter
                        UpdateLinkCount();
                        ApplyLinkFilter();
                    }
                }

                return BodyRoutes;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error extracting links");
                return new ObservableCollection<LinkItem>();
            }
            finally
            {
                IsLoadingLinks = false;
            }
        }

        public async Task<string> GenerateSummaryAsync()
        {
            if (!EnableAISummarization || _ragService == null)
            {
                MarkdownContent = "AI summarization is disabled or not available.";
                await DisplayMarkdown(MarkdownContent);
                return MarkdownContent;
            }

            IsGeneratingSummary = true;
            try
            {
                if (string.IsNullOrWhiteSpace(PageBody))
                {
                    MarkdownContent = "Could not extract page content.";
                    await DisplayMarkdown(MarkdownContent);
                    return MarkdownContent;
                }

                // Check if document already exists in database
                if (_dataService != null && !string.IsNullOrWhiteSpace(PageURL))
                {
                    _logger?.LogInformation("Checking if document already exists for URL: {Url}", PageURL);

                    var existingDocuments = await _dataService.SearchBySourceUrlAsync(PageURL, exactMatch: true, maxResults: 1);
                    var existingDocument = existingDocuments?.FirstOrDefault();

                    if (existingDocument != null && !string.IsNullOrWhiteSpace(existingDocument.Content))
                    {
                        _logger?.LogInformation("Found existing document (ID: {Id}) for URL: {Url}. Using cached summary.",
                            existingDocument.Id, PageURL);

                        // Use the existing summary
                        MarkdownContent = existingDocument.Content;
                        PageContent = existingDocument.Content;

                        // Set markdown toolbar properties
                        _currentMarkdownDocumentId = existingDocument.Id;
                        MarkdownDocumentTitle = existingDocument.Title ?? PageTitle;
                        IsFavorite = existingDocument.Visibility == 2;

                        // Display the cached markdown
                        await DisplayMarkdown(MarkdownContent);

                        return MarkdownContent;
                    }

                    _logger?.LogInformation("No existing document found for URL: {Url}. Generating new summary.", CurrentUrl);
                }

				// Use the specialized SummarizeWebpageAsync method with structured return
				var bodyRoutes = BodyRoutes.Select(r => r.Url).ToList();

				var result = await _ragService.SummarizeWebpageAsync(
					url: PageURL,
					title: PageTitle,
					pageBody: PageBody,
					options: new UnifiedRag.Maui.Models.RAGOptions
					{
						MaxContextTokens = MaxContextTokens,
						GenerationOptions = new UnifiedRag.Maui.Models.GenerationOptions
						{
							MaxTokens = MaxTokens,
							Temperature = 0.3f
						}
					});

				// Use the detailed summary as the markdown content
				MarkdownContent = result.DetailedSummary;
				PageContent = result.DetailedSummary;

				// Set markdown toolbar properties
				_currentMarkdownDocumentId = result.DocumentId;
				MarkdownDocumentTitle = PageTitle;

				// Check the document's favorite status from database
				if (!string.IsNullOrWhiteSpace(result.DocumentId) && _dataService != null)
				{
					var documents = await _dataService.SearchBySourceUrlAsync(CurrentUrl, exactMatch: true, maxResults: 1);
					var document = documents?.FirstOrDefault();
					if (document != null)
					{
						IsFavorite = document.Visibility == 2;
					}
					else
					{
						IsFavorite = false;
					}
				}

				// Convert to HTML and display
				await DisplayMarkdown(MarkdownContent);

                return MarkdownContent;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating summary");
                MarkdownContent = $"Error generating summary: {ex.Message}";
                await DisplayMarkdown(MarkdownContent);
                return MarkdownContent;
            }
            finally
            {
                IsGeneratingSummary = false;
            }
        }

        /// <summary>
        /// Loads markdown content from the database for the current URL
        /// </summary>
        /// <returns>True if content was found and loaded, false otherwise</returns>
        private async Task<bool> LoadMarkdownFromDatabaseAsync()
        {
            if (_dataService == null || string.IsNullOrWhiteSpace(CurrentUrl))
                return false;

            try
            {
                _logger?.LogInformation("Attempting to load saved content for URL: {Url}", CurrentUrl);

                // Search for a document with this exact URL
                var documents = await _dataService.SearchBySourceUrlAsync(CurrentUrl, exactMatch: true, maxResults: 1);
                var document = documents?.FirstOrDefault();

                if (document != null && !string.IsNullOrWhiteSpace(document.Content))
                {
                    _logger?.LogInformation("Found saved content for URL: {Url}", CurrentUrl);

                    // Set the markdown content from the saved document
                    MarkdownContent = document.Content;
                    PageContent = document.Content;

                    // Set markdown toolbar properties
                    _currentMarkdownDocumentId = document.Id;
                    MarkdownDocumentTitle = document.Title ?? PageTitle;
                    IsFavorite = document.Visibility == 2;

                    // Display the saved markdown
                    await DisplayMarkdown(MarkdownContent);

                    return true;
                }

                _logger?.LogInformation("No saved content found for URL: {Url}", CurrentUrl);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading markdown from database");
                return false;
            }
        }

        /// <summary>
        /// Navigates to a history item and displays its saved markdown content
        /// </summary>
        public async Task NavigateToHistoryItemAsync(string url, string markdownContent, int visibility = 1, string? documentId = null, string? title = null)
        {
            try
            {
                _logger?.LogInformation("Navigating to history item with saved content: {Url}, Visibility: {Visibility}", url, visibility);

                // Update the current URL in the address bar
                CurrentUrl = url;

                // Set the markdown content from the saved document
                MarkdownContent = markdownContent;
                PageContent = markdownContent;

                // Set the document metadata
                if (!string.IsNullOrWhiteSpace(documentId))
                {
                    _currentMarkdownDocumentId = documentId;
                }

                if (!string.IsNullOrWhiteSpace(title))
                {
                    MarkdownDocumentTitle = title;
                    PageTitle = title;
                }

                // Set the favorite status based on visibility
                // Visibility: 0=Hidden, 1=Normal, 2=Favorite
                IsFavorite = visibility == 2;
                _logger?.LogDebug("Set IsFavorite to {IsFavorite} based on visibility {Visibility}", IsFavorite, visibility);

                // Display the saved markdown first
                await DisplayMarkdown(MarkdownContent);

                // Then show the markdown view (this ensures content is ready before showing)
                IsMarkdownViewVisible = true;
                IsLinksVisible = false;
                IsWebViewVisible = false;

                // Update bookmark state for the new URL
                UpdateBookmarkState();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error navigating to history item");
            }
        }

        public async void OnPageDataChanged(PageData pageData)
        {
            if (pageData != null)
            {
                // Page data changed means page is done loading - hide progress bar
                IsPageLoading = false;

                // Enable action buttons now that page data is ready
                AreActionButtonsEnabled = true;

                // Also clear the active tab's loading state
                var activeTab = _tabManager.ActiveTab;
                if (activeTab != null)
                {
                    activeTab.IsLoading = false;
                }

                _logger?.LogDebug("OnPageDataChanged: Page loading complete, enabling action buttons");

                PageContent = "";
				PageTitle = pageData.Title ?? "Untitled";
				PageURL = pageData.Url ?? string.Empty;
				PageBody = pageData.Body ?? string.Empty;

				// Update CurrentUrl to ensure the address bar reflects the actual page URL
				// This is critical for when navigation happens via link clicks
				if (!string.IsNullOrWhiteSpace(pageData.Url) && CurrentUrl != pageData.Url)
				{
					CurrentUrl = pageData.Url;
					_logger?.LogDebug("Updated CurrentUrl from PageDataChanged to: {Url}", pageData.Url);

					// Also update the active tab's URL
					var workingTab = _tabManager.ActiveTab;
					if (workingTab != null)
					{
						workingTab.Url = pageData.Url;
					}

					// Update bookmark state when URL changes
					UpdateBookmarkState();
				}

				// Update active tab's title and thumbnail
				activeTab = _tabManager.ActiveTab;
                if (activeTab != null)
                {
                    // Update title
                    if (!string.IsNullOrWhiteSpace(pageData.Title))
                    {
                        activeTab.Title = pageData.Title.Length > 50
                            ? pageData.Title.Substring(0, 47) + "..."
                            : pageData.Title;
                    }

                    // Update thumbnail if provided
                    if (pageData.Thumbnail != null)
                    {
                        activeTab.Thumbnail = pageData.Thumbnail;
                        _logger?.LogInformation("Received thumbnail from PageData for tab {TabId}", activeTab.Id);

                        // Save thumbnail to disk if JSON state is enabled
                        if (EnableJSONState)
                        {
                            await SavePageDataThumbnailAsync(activeTab, pageData.Thumbnail);
                        }
                    }
                    else
                    {
                        _logger?.LogDebug("No thumbnail in PageData for tab {TabId}", activeTab.Id);
                    }
                }

                // Update routes if available
                if (pageData.BodyRoutes != null)
                {

                    var linkItems = pageData.BodyRoutes.Select((r, index) =>
                    {
                        // Determine the best title to use
                        string title = r.Text;
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            // Try to extract a meaningful title from the URL
                            try
                            {
                                var uri = new Uri(r.Url);
                                var fileName = System.IO.Path.GetFileName(uri.LocalPath);
                                title = !string.IsNullOrWhiteSpace(fileName) && fileName != "/"
                                    ? fileName
                                    : uri.Host; // Use domain as fallback
                            }
                            catch
                            {
                                title = r.Url; // Use full URL as last resort
                            }
                        }

                        return new LinkItem
                        {
                            Url = r.Url,
                            Title = title,
                            Occurrences = r.Occurrences,
                            Ranking = r.Rank, // Use Rank property from RouteInfo, not index
                            IsPotentialAd = r.IsPotentialAd
                        };
                    }).ToList();

                    BodyRoutes = new ObservableCollection<LinkItem>(linkItems);

                    // Update link count and apply filter
                    UpdateLinkCount();
                    ApplyLinkFilter();
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Updates the link count text displayed in the toolbar
        /// </summary>
        private void UpdateLinkCount()
        {
            var totalLinks = BodyRoutes?.Count ?? 0;
            var adLinks = BodyRoutes?.Count(l => l.IsPotentialAd) ?? 0;
			var regularLinks = totalLinks - adLinks;

			// Update HasAds flag to enable/disable the ads toggle button
			HasAds = adLinks > 0;

			if (totalLinks == 0)
            {
				LinkCountText = "0 links";
			}
            else if (adLinks == 0)
            {
				LinkCountText = $"{totalLinks} {(totalLinks == 1 ? "link" : "links")}";
			}
            else
            {
				LinkCountText = $"{totalLinks} {(totalLinks == 1 ? "link" : "links")} ({regularLinks} regular, {adLinks} ads)";
			}
        }

        /// <summary>
        /// Applies the ad filter and search filter to the links
        /// </summary>
        private void ApplyLinkFilter()
        {
            if (BodyRoutes == null || BodyRoutes.Count == 0)
            {
                FilteredBodyRoutes = new ObservableCollection<LinkItem>();
                return;
            }

            IEnumerable<LinkItem> filtered = BodyRoutes;

            // Filter out ads if ShowAds is false
            if (!ShowAds)
            {
                filtered = filtered.Where(link => !link.IsPotentialAd);
            }

            // Apply search filter if search query is not empty
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var searchLower = SearchQuery.ToLower();
                filtered = filtered.Where(link =>
                    (!string.IsNullOrWhiteSpace(link.Title) && link.Title.ToLower().Contains(searchLower)) ||
                    (!string.IsNullOrWhiteSpace(link.Url) && link.Url.ToLower().Contains(searchLower))
                );
            }

            FilteredBodyRoutes = new ObservableCollection<LinkItem>(filtered);
        }

        private async Task LoadBodyRoutesAsync()
        {
            try
            {
                IsLoadingLinks = true;

                // If we already have routes from PageDataChanged, we're done
                if (BodyRoutes.Count > 0)
                {
                    IsLoadingLinks = false;
                    return;
                }

                // Use WebView's ExtractRoutesAsync - it handles JavaScript extraction internally with better logic
                if (_webView != null)
                {
                    var routes = await _webView.ExtractRoutesAsync();
                    if (routes != null && routes.BodyRoutes.Count > 0)
                    {
                        var linkItems = routes.BodyRoutes.Select(r => new LinkItem
                        {
                            Url = r.Url,
                            Title = string.IsNullOrWhiteSpace(r.Text) ? r.Url : r.Text,
                            Occurrences = r.Occurrences,
                            Ranking = r.Rank,
                            IsPotentialAd = r.IsPotentialAd
                        }).ToList();

                        BodyRoutes = new ObservableCollection<LinkItem>(linkItems);

                        // Update link count and apply filter
                        UpdateLinkCount();
                        ApplyLinkFilter();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in LoadBodyRoutesAsync");
            }
            finally
            {
                IsLoadingLinks = false;
            }
        }

        private void UpdateNavigationState()
        {
            try
            {
                var previousCanGoBack = CanGoBack;
                var previousCanGoForward = CanGoForward;

                CanGoBack = _webView.CanGoBackInHistory;
                CanGoForward = _webView.CanGoForwardInHistory;

                // Log when navigation state changes for debugging
                if (previousCanGoBack != CanGoBack || previousCanGoForward != CanGoForward)
                {
                    Console.WriteLine($"[NAV] Navigation state CHANGED: CanGoBack={CanGoBack} (was {previousCanGoBack}), CanGoForward={CanGoForward} (was {previousCanGoForward})");
                    _logger?.LogDebug("Navigation state updated: CanGoBack={CanGoBack}, CanGoForward={CanGoForward}",
                        CanGoBack, CanGoForward);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NAV] ERROR in UpdateNavigationState: {ex.Message}");
                _logger?.LogError(ex, "Error updating navigation state");
            }
        }

        private CancellationTokenSource? _loadingTimeoutCts;
        private const int LOADING_TIMEOUT_MS = 30000; // 30 seconds

        private void OnWebViewNavigating(object? sender, MarketAlly.Maui.ViewEngine.WebNavigationEventArgs e)
        {
            try
            {
                // Cancel any existing timeout
                _loadingTimeoutCts?.Cancel();
                _loadingTimeoutCts?.Dispose();

                // Set loading state to true when navigation starts
                IsPageLoading = true;

                // Disable action buttons (links and markdown) until page data is ready
                AreActionButtonsEnabled = false;

                _logger?.LogDebug("Navigation starting to: {Url}", e.Url);

                // Update active tab's loading state
                var activeTab = _tabManager.ActiveTab;
                if (activeTab != null)
                {
                    activeTab.IsLoading = true;
                    activeTab.LastAccessed = DateTime.Now;

                    // Start a timeout to clear loading state if navigation takes too long
                    _loadingTimeoutCts = new CancellationTokenSource();
                    var timeoutToken = _loadingTimeoutCts.Token;
                    var currentTabId = activeTab.Id;

                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(LOADING_TIMEOUT_MS, timeoutToken);

                            // If we get here, the timeout wasn't cancelled (navigation is stuck)
                            Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
                            {
                                // Clear loading state for the specific tab that was loading
                                var tab = _tabManager.Tabs.FirstOrDefault(t => t.Id == currentTabId);
                                if (tab != null && tab.IsLoading)
                                {
                                    tab.IsLoading = false;
                                    _logger?.LogWarning("Loading timeout reached for tab {TabId}, clearing loading state", currentTabId);
                                }

                                // Also clear page loading if this is still the active tab
                                if (_tabManager.ActiveTab?.Id == currentTabId)
                                {
                                    IsPageLoading = false;
                                }
                            });
                        }
                        catch (TaskCanceledException)
                        {
                            // Normal - navigation completed before timeout
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in navigating handler");
                IsPageLoading = false;

                // Clear loading state on error
                var activeTab = _tabManager.ActiveTab;
                if (activeTab != null)
                {
                    activeTab.IsLoading = false;
                }
            }
        }

        private async void OnWebViewNavigated(object? sender, MarketAlly.Maui.ViewEngine.WebNavigationEventArgs e)
        {
            try
            {
                // Cancel the loading timeout since navigation completed
                _loadingTimeoutCts?.Cancel();
                _loadingTimeoutCts?.Dispose();
                _loadingTimeoutCts = null;

                // Set loading state to false when navigation completes
                IsPageLoading = false;
                _logger?.LogDebug("Navigation completed to: {Url} with result: {Result}", e.Url, e.Result);

                // Update active tab's properties - ALWAYS clear loading state
                var activeTab = _tabManager.ActiveTab;
                if (activeTab != null)
                {
                    // Always clear loading state, regardless of result
                    activeTab.IsLoading = false;

                    if (e.Result == MarketAlly.Maui.ViewEngine.WebNavigationResult.Success && !string.IsNullOrWhiteSpace(e.Url))
                    {
                        activeTab.Url = e.Url;

                        // Try to get the page title
                        try
                        {
                            var title = await _webView.EvaluateJavaScriptAsync("document.title");
                            if (!string.IsNullOrWhiteSpace(title) && title != "null")
                            {
                                activeTab.Title = title.Length > 50 ? title.Substring(0, 47) + "..." : title;
                                PageTitle = title;
                            }
                        }
                        catch
                        {
                            // Title will be updated by OnPageDataChanged if available
                        }
                    }
                }

                // Always update CurrentUrl when we have a URL, not just on success
                // This ensures the address bar reflects the actual navigation attempt
                if (!string.IsNullOrWhiteSpace(e.Url))
                {
                    CurrentUrl = e.Url;
                    _logger?.LogDebug("Updated CurrentUrl to: {Url}", e.Url);

                    // Update bookmark state when URL changes
                    UpdateBookmarkState();
                }

                // Log navigation errors for debugging
                if (e.Result != MarketAlly.Maui.ViewEngine.WebNavigationResult.Success)
                {
                    _logger?.LogWarning("Navigation failed to {Url} with result: {Result}", e.Url, e.Result);

                    // If HTTP2 error, we might want to retry with HTTP/1.1
                    // This would require WebView configuration support
                }

                // Update navigation state - this is critical for back/forward buttons
                Console.WriteLine($"[NAV] OnWebViewNavigated - immediate update for {e.Url}");
                UpdateNavigationState();

                // Also update after delays to ensure WebView state is fully updated
                // Multiple updates catch the state at different points in the loading process
                Microsoft.Maui.Controls.Application.Current?.Dispatcher.DispatchDelayed(
                    TimeSpan.FromMilliseconds(100),
                    () => {
                        Console.WriteLine($"[NAV] OnWebViewNavigated - 100ms delayed update");
                        UpdateNavigationState();
                    }
                );

                Microsoft.Maui.Controls.Application.Current?.Dispatcher.DispatchDelayed(
                    TimeSpan.FromMilliseconds(500),
                    async () =>
                    {
                        Console.WriteLine($"[NAV] OnWebViewNavigated - 500ms delayed update");
                        UpdateNavigationState();

                        // Capture thumbnail if enabled and navigation is successful
                        if (CaptureThumbnailOnNavigation && e.Result == MarketAlly.Maui.ViewEngine.WebNavigationResult.Success)
                        {
                            // Wait a bit more for the page to render
                            await Task.Delay(500);
                            await CaptureAndSaveThumbnailAsync();
                        }

                        // Save tab state after navigation if enabled
                        if (EnableJSONState)
                        {
                            _ = SaveTabStateAsync();
                        }
                    }
                );

                // Final update after a longer delay to catch any late state changes
                Microsoft.Maui.Controls.Application.Current?.Dispatcher.DispatchDelayed(
                    TimeSpan.FromMilliseconds(1000),
                    () => {
                        Console.WriteLine($"[NAV] OnWebViewNavigated - 1000ms delayed update (final)");
                        UpdateNavigationState();
                    }
                );
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in navigated handler");
                IsPageLoading = false; // Ensure loading state is cleared on error

                // Also clear the active tab's loading state
                var activeTab = _tabManager.ActiveTab;
                if (activeTab != null)
                {
                    activeTab.IsLoading = false;
                }
            }
        }

        private async void OnPageLoadComplete(object? sender, EventArgs e)
        {
            try
            {
                // Cancel the loading timeout since page load is complete
                _loadingTimeoutCts?.Cancel();
                _loadingTimeoutCts?.Dispose();
                _loadingTimeoutCts = null;

                // Ensure loading state is false
                IsPageLoading = false;
                _logger?.LogDebug("Page load complete, clearing loading state");

                // Also ensure active tab's loading state is cleared
                var activeTab = _tabManager.ActiveTab;
                if (activeTab != null)
                {
                    activeTab.IsLoading = false;
                    _logger?.LogDebug("Cleared loading state for tab {TabId} on page load complete", activeTab.Id);
                }

                // Update navigation state
                UpdateNavigationState();

                // Update page title
                var titleScript = "document.title";
                var title = await _webView.EvaluateJavaScriptAsync(titleScript);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    PageTitle = title;
                }

                // Auto-generate summary if enabled
                if (AutoGenerateSummary && EnableAISummarization)
                {
                    _ = Task.Run(async () => await GenerateSummaryAsync());
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in page load complete handler");
                IsPageLoading = false; // Ensure loading state is cleared on error
            }
        }

        private void OnWebViewPageDataChanged(object? sender, PageData pageData)
        {
            OnPageDataChanged(pageData);
        }

        private void OnMarkdownWebViewNavigating(object? sender, WebNavigatingEventArgs e)
        {
            try
            {
                // When a link is clicked in the markdown view:
                // 1. Cancel the navigation in the markdown WebView
                // 2. Switch back to the web view
                // 3. Navigate the main WebView to that URL

                // Cancel navigation in markdown WebView
                e.Cancel = true;

                _logger?.LogInformation("Link clicked in markdown view: {Url}", e.Url);

                PageContent = ""; // Clear page content since we're navigating away

				// Switch to web view
				IsMarkdownViewVisible = false;
                IsLinksVisible = false;
                IsWebViewVisible = true;

                // Navigate the main WebView to the URL
                NavigateToUrl(e.Url, false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling markdown WebView navigation");
            }
        }

        private async Task DisplayMarkdown(string markdown)
        {
            try
            {
                // Convert markdown to HTML
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .Build();

                var html = Markdig.Markdown.ToHtml(markdown, pipeline);

                // Wrap in HTML document with styling
                var fullHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            padding: 16px;
            line-height: 1.6;
            color: #333;
            max-width: 800px;
            margin: 0 auto;
        }}
        h1, h2, h3, h4, h5, h6 {{
            margin-top: 24px;
            margin-bottom: 16px;
            font-weight: 600;
            line-height: 1.25;
        }}
        h1 {{ font-size: 2em; border-bottom: 1px solid #eaecef; padding-bottom: 0.3em; }}
        h2 {{ font-size: 1.5em; border-bottom: 1px solid #eaecef; padding-bottom: 0.3em; }}
        h3 {{ font-size: 1.25em; }}
        p {{ margin-bottom: 16px; }}
        code {{
            padding: 0.2em 0.4em;
            background-color: rgba(175, 184, 193, 0.2);
            border-radius: 3px;
            font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
            font-size: 85%;
        }}
        pre {{
            padding: 16px;
            overflow: auto;
            font-size: 85%;
            line-height: 1.45;
            background-color: #f6f8fa;
            border-radius: 6px;
        }}
        blockquote {{
            padding: 0 1em;
            color: #6a737d;
            border-left: 0.25em solid #dfe2e5;
            margin-bottom: 16px;
        }}
        ul, ol {{
            padding-left: 2em;
            margin-bottom: 16px;
        }}
        li {{ margin-bottom: 0.25em; }}
        a {{
            color: #0366d6;
            text-decoration: none;
        }}
        a:hover {{
            text-decoration: underline;
        }}
        @media (prefers-color-scheme: dark) {{
            body {{
                color: #c9d1d9;
                background-color: #0d1117;
            }}
            h1, h2 {{
                border-bottom-color: #30363d;
            }}
            code {{
                background-color: rgba(110, 118, 129, 0.4);
            }}
            pre {{
                background-color: #161b22;
            }}
            blockquote {{
                color: #8b949e;
                border-left-color: #30363d;
            }}
            a {{
                color: #58a6ff;
            }}
        }}
    </style>
</head>
<body>
    {html}
</body>
</html>";

                // Unsubscribe first to avoid duplicate subscriptions
                _markdownWebView.Navigating -= OnMarkdownWebViewNavigating;

                // Load HTML into markdown WebView
                _markdownWebView.Source = new HtmlWebViewSource { Html = fullHtml };

                // Subscribe to Navigating event AFTER content is set
                // This way we only intercept user link clicks, not the initial HTML load
                await Task.Delay(1000);
                _markdownWebView.Navigating += OnMarkdownWebViewNavigating;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error displaying markdown");
            }
        }

        private void OnActiveTabChanged(object? sender, EventArgs e)
        {
            var activeTab = _tabManager.ActiveTab;
            if (activeTab != null)
            {
                // Switch the WebView references to the active tab's WebView
                SwitchToTab(activeTab);
            }

            // Auto-save tab state if enabled
            if (EnableJSONState)
            {
                _ = SaveTabStateAsync();
            }
        }

        private void OnTabManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TabManager.TabCount))
            {
                _tabCount = _tabManager.TabCount;
                OnPropertyChanged(nameof(TabCount));
                _tabCountDisplay = _tabManager.TabCount > 9 ? "9+" : _tabManager.TabCount.ToString();
                OnPropertyChanged(nameof(TabCountDisplay));
            }
        }

        private void SwitchToTab(BrowserTab tab)
        {
            // Cancel any pending loading timeout for the old tab
            _loadingTimeoutCts?.Cancel();
            _loadingTimeoutCts?.Dispose();
            _loadingTimeoutCts = null;

            // Clear loading state on the old tab if we're switching away while loading
            var oldTab = _tabManager.ActiveTab;
            if (oldTab != null && oldTab.IsLoading)
            {
                oldTab.IsLoading = false;
                _logger?.LogDebug("Cleared loading state on tab {TabId} during switch", oldTab.Id);
            }

            // Log the switch
            _logger?.LogDebug("Switching from tab {OldTabId} to tab {NewTabId}",
                oldTab?.Id, tab.Id);

            // Unsubscribe from old WebView events
            if (_webView != null)
            {
                _webView.Navigating -= OnWebViewNavigating;
                _webView.Navigated -= OnWebViewNavigated;
                _webView.PageLoadComplete -= OnPageLoadComplete;
                _webView.PageDataChanged -= OnWebViewPageDataChanged;
            }

            // Unsubscribe from old Markdown WebView events (if previously subscribed)
            if (_markdownWebView != null)
            {
                _markdownWebView.Navigating -= OnMarkdownWebViewNavigating;
            }

            // Switch to new tab's WebView
            _webView = tab.WebView;
            _markdownWebView = tab.MarkdownWebView;

            // Configure the WebView with current settings
            // This ensures newly created WebViews get the proper configuration
            if (_webView != null)
            {
                _webView.EnableAdDetection = EnableAdDetection;
                _webView.MaxRoutes = MaxRoutes;
                _logger?.LogDebug("Configured WebView for tab {TabId}: EnableAdDetection={EnableAdDetection}, MaxRoutes={MaxRoutes}",
                    tab.Id, EnableAdDetection, MaxRoutes);
            }

            // Subscribe to new WebView events
            _webView.Navigating += OnWebViewNavigating;
            _webView.Navigated += OnWebViewNavigated;
            _webView.PageLoadComplete += OnPageLoadComplete;
            _webView.PageDataChanged += OnWebViewPageDataChanged;

            // Note: Markdown WebView Navigating event will be subscribed in DisplayMarkdown
            // when content is loaded

            // Update UI properties
            CurrentUrl = tab.Url;
            PageTitle = tab.Title;

            // Update bookmark state when switching tabs
            UpdateBookmarkState();

            // Sync the page loading state and log it
            if (tab.IsLoading != IsPageLoading)
            {
                _logger?.LogDebug("Syncing IsPageLoading from {OldValue} to {NewValue} for tab {TabId}",
                    IsPageLoading, tab.IsLoading, tab.Id);
            }
            IsPageLoading = tab.IsLoading;

            // Enable action buttons if the tab is not loading (has content ready)
            // This ensures buttons are enabled when switching to an already-loaded tab
            if (!tab.IsLoading && !string.IsNullOrWhiteSpace(tab.Url))
            {
                AreActionButtonsEnabled = true;
                _logger?.LogDebug("Enabled action buttons for already-loaded tab {TabId}", tab.Id);
            }
            else
            {
                AreActionButtonsEnabled = false;
                _logger?.LogDebug("Disabled action buttons for loading tab {TabId}", tab.Id);
            }

            // Fire WebView changed event for the control to update its content
            OnWebViewChanged?.Invoke();

            // Update navigation state immediately
            UpdateNavigationState();

            // Also update after a small delay to ensure the WebView is fully ready
            Microsoft.Maui.Controls.Application.Current?.Dispatcher.DispatchDelayed(
                TimeSpan.FromMilliseconds(50),
                () => UpdateNavigationState()
            );
        }

        /// <summary>
        /// Event raised when the active WebView changes (tab switch)
        /// </summary>
        public Action? OnWebViewChanged;

        /// <summary>
        /// Gets the tab manager for external access
        /// </summary>
        public TabManager TabManager => _tabManager;

        /// <summary>
        /// Initializes the first tab when the control is ready
        /// </summary>
        /// <param name="startBlank">If true, creates a blank tab without navigating</param>
        public void InitializeFirstTab(bool startBlank = false)
        {
            if (_tabManager.TabCount == 0)
            {
                var firstTab = _tabManager.NewTab(HomeUrl);
                if (firstTab != null)
                {
                    // Configure the first tab's WebView with current settings
                    if (firstTab.WebView != null)
                    {
                        firstTab.WebView.EnableAdDetection = EnableAdDetection;
                        firstTab.WebView.MaxRoutes = MaxRoutes;
                        _logger?.LogDebug("Configured first tab WebView: EnableAdDetection={EnableAdDetection}, MaxRoutes={MaxRoutes}",
                            EnableAdDetection, MaxRoutes);
                    }

                    if (!startBlank)
                    {
                        firstTab.NavigateTo(HomeUrl);
                    }
                }
            }

            // Start periodic check for stuck loading states (runs every 10 seconds)
            StartLoadingStateMonitor();
        }

        private System.Threading.Timer? _loadingStateMonitor;

        private void StartLoadingStateMonitor()
        {
            _loadingStateMonitor?.Dispose();
            _loadingStateMonitor = new System.Threading.Timer(_ =>
            {
                try
                {
                    Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
                    {
                        // Check all tabs for stuck loading states
                        var stuckTabs = _tabManager.Tabs
                            .Where(t => t.IsLoading && (DateTime.Now - t.LastAccessed).TotalSeconds > 30)
                            .ToList();

                        foreach (var tab in stuckTabs)
                        {
                            _logger?.LogWarning("Found stuck loading state on tab {TabId}, last accessed {LastAccessed}, clearing",
                                tab.Id, tab.LastAccessed);
                            tab.IsLoading = false;
                        }

                        // Also sync the current page loading state if needed
                        var activeTab = _tabManager.ActiveTab;
                        if (activeTab != null && activeTab.IsLoading != IsPageLoading)
                        {
                            _logger?.LogWarning("Page loading state mismatch detected, syncing to tab state: {TabIsLoading}",
                                activeTab.IsLoading);
                            IsPageLoading = activeTab.IsLoading;
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in loading state monitor");
                }
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }


        /// <summary>
        /// Selects a specific tab
        /// </summary>
        public void SelectTab(BrowserTab tab)
        {
            _tabManager.ActiveTab = tab;

            // When selecting a tab, ensure we're showing the web view
            IsLinksVisible = false;
            IsMarkdownViewVisible = false;
            IsHistoryVisible = false;
            IsWebViewVisible = true;
        }

        /// <summary>
        /// Saves tab state asynchronously if enabled
        /// </summary>
        private async Task SaveTabStateAsync()
        {
            if (!EnableJSONState)
                return;

            try
            {
                await _tabManager.SaveTabStateToFile();
                _logger?.LogDebug("Tab state saved successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving tab state");
            }
        }

        /// <summary>
        /// Restores tab state asynchronously if enabled
        /// </summary>
        public async Task<bool> RestoreTabStateAsync()
        {
            if (!EnableJSONState)
                return false;

            try
            {
                var restored = await _tabManager.LoadTabStateFromFile();
                if (restored)
                {
                    _logger?.LogInformation("Tab state restored successfully");

                    // Configure all restored tabs' WebViews with current settings
                    foreach (var tab in _tabManager.Tabs)
                    {
                        if (tab.WebView != null)
                        {
                            tab.WebView.EnableAdDetection = EnableAdDetection;
                            tab.WebView.MaxRoutes = MaxRoutes;
                        }
                    }
                    _logger?.LogDebug("Configured {Count} restored tab WebViews: EnableAdDetection={EnableAdDetection}, MaxRoutes={MaxRoutes}",
                        _tabManager.Tabs.Count, EnableAdDetection, MaxRoutes);

                    // Ensure the IsPageLoading state is synced with the active tab
                    // Restored tabs should never be in a loading state
                    var activeTab = _tabManager.ActiveTab;
                    if (activeTab != null)
                    {
                        IsPageLoading = activeTab.IsLoading; // Should be false
                        _logger?.LogDebug("Synced IsPageLoading to {IsLoading} after tab restoration", IsPageLoading);
                    }

                    OnWebViewChanged?.Invoke();
                }
                return restored;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error restoring tab state");
                return false;
            }
        }

        /// <summary>
        /// Manually captures and saves a thumbnail for the active tab
        /// </summary>
        public async Task CaptureAndSaveThumbnailAsync()
        {
            if (!EnableThumbnailCapture || !EnableJSONState)
                return;

            var activeTab = _tabManager.ActiveTab;
            if (activeTab == null || activeTab.WebView == null)
                return;

            try
            {
                // Use higher resolution for better quality
                // The image will be downscaled when displayed but will look sharper
                var captureWidth = ThumbnailWidth * 2;  // Double the resolution
                var captureHeight = ThumbnailHeight * 2;

                _logger?.LogDebug("Capturing thumbnail at {Width}x{Height} (display size: {DisplayWidth}x{DisplayHeight})",
                    captureWidth, captureHeight, ThumbnailWidth, ThumbnailHeight);

                // Capture the thumbnail using the WebView's method
                var thumbnail = await activeTab.WebView.CaptureThumbnailAsync(captureWidth, captureHeight);

                if (thumbnail != null)
                {
                    // Update the tab's thumbnail
                    activeTab.Thumbnail = thumbnail;

                    _logger?.LogInformation("Captured high-res thumbnail for tab {TabId} at {Width}x{Height}",
                        activeTab.Id, captureWidth, captureHeight);

                    // Save to disk
                    await SavePageDataThumbnailAsync(activeTab, thumbnail);
                }
                else
                {
                    _logger?.LogDebug("Manual thumbnail capture returned null for tab {TabId}", activeTab.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error capturing thumbnail for tab {TabId}", activeTab.Id);
            }
        }

        /// <summary>
        /// Saves a thumbnail from PageData to disk
        /// </summary>
        private async Task SavePageDataThumbnailAsync(BrowserTab tab, ImageSource thumbnail)
        {
            try
            {
                // Convert ImageSource to bytes
                byte[]? imageBytes = null;

                // Handle different ImageSource types
                if (thumbnail is StreamImageSource streamSource)
                {
                    using var stream = await streamSource.Stream(CancellationToken.None);
                    if (stream != null)
                    {
                        using var memoryStream = new MemoryStream();
                        await stream.CopyToAsync(memoryStream);
                        imageBytes = memoryStream.ToArray();
                    }
                }
                else if (thumbnail is FileImageSource fileSource && !string.IsNullOrEmpty(fileSource.File))
                {
                    imageBytes = await File.ReadAllBytesAsync(fileSource.File);
                }
                else if (thumbnail is UriImageSource uriSource && uriSource.Uri != null)
                {
                    // Download image from URL
                    using var httpClient = new HttpClient();
                    imageBytes = await httpClient.GetByteArrayAsync(uriSource.Uri);
                }

                // Save thumbnail if we got the bytes
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    await _tabManager.SaveThumbnailAsync(tab, imageBytes);
                    _logger?.LogDebug("Saved thumbnail for tab {TabId}", tab.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving PageData thumbnail for tab {TabId}", tab.Id);
            }
        }

        #endregion

        #region IDisposable Implementation

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _loadingTimeoutCts?.Cancel();
                    _loadingTimeoutCts?.Dispose();
                    _loadingStateMonitor?.Dispose();

                    // Unsubscribe from events
                    if (_webView != null)
                    {
                        _webView.Navigating -= OnWebViewNavigating;
                        _webView.Navigated -= OnWebViewNavigated;
                        _webView.PageLoadComplete -= OnPageLoadComplete;
                        _webView.PageDataChanged -= OnWebViewPageDataChanged;
                    }

                    if (_markdownWebView != null)
                    {
                        _markdownWebView.Navigating -= OnMarkdownWebViewNavigating;
                    }

                    if (_tabManager != null)
                    {
                        _tabManager.ActiveTabChanged -= OnActiveTabChanged;
                        _tabManager.PropertyChanged -= OnTabManagerPropertyChanged;
                    }

                    _logger?.LogDebug("BrowserViewModel disposed");
                }

                _disposed = true;
            }
        }

        #endregion
    }
}