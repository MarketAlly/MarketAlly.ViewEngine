using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevExpress.Maui.Controls;
using MarketAlly.Maui.ViewEngine;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using UnifiedBrowser.Maui.Models;
using UnifiedBrowser.Maui.ViewModels;
using UnifiedBrowser.Maui.Views;
using UnifiedData.Maui.Core;
using UnifiedRag.Maui.Core;

namespace UnifiedBrowser.Maui.Controls
{
    /// <summary>
    /// A comprehensive browser control with AI-powered summarization and link extraction capabilities
    /// </summary>
    public partial class UnifiedBrowserControl : ContentView
    {
        private readonly BrowserViewModel _viewModel;
        private readonly IUnifiedDataService? _dataService;
        private readonly ILogger? _logger;
        private bool _isNavigatingFromMarkdown = false;
        private bool _hasInitialized = false;

        #region Bindable Properties

        public static readonly BindableProperty HomeUrlProperty = BindableProperty.Create(
            nameof(HomeUrl), typeof(string), typeof(UnifiedBrowserControl), "https://www.google.com",
            propertyChanged: OnHomeUrlChanged);

        public static readonly BindableProperty ShowAddressBarProperty = BindableProperty.Create(
            nameof(ShowAddressBar), typeof(bool), typeof(UnifiedBrowserControl), true);

        public static readonly BindableProperty ShowBottomNavigationProperty = BindableProperty.Create(
            nameof(ShowBottomNavigation), typeof(bool), typeof(UnifiedBrowserControl), true);

        public static readonly BindableProperty ShowNavigationInAddressBarProperty = BindableProperty.Create(
            nameof(ShowNavigationInAddressBar), typeof(bool), typeof(UnifiedBrowserControl), false);

		public static readonly BindableProperty ShowTabsButtonProperty = BindableProperty.Create(
			nameof(ShowTabsButton), typeof(bool), typeof(UnifiedBrowserControl), true);

		public static readonly BindableProperty ShowLinksButtonProperty = BindableProperty.Create(
            nameof(ShowLinksButton), typeof(bool), typeof(UnifiedBrowserControl), true);

        public static readonly BindableProperty ShowMarkdownButtonProperty = BindableProperty.Create(
            nameof(ShowMarkdownButton), typeof(bool), typeof(UnifiedBrowserControl), true);

        public static readonly BindableProperty ShowShareButtonProperty = BindableProperty.Create(
            nameof(ShowShareButton), typeof(bool), typeof(UnifiedBrowserControl), true);

        public static readonly BindableProperty ShowSaveButtonProperty = BindableProperty.Create(
            nameof(ShowSaveButton), typeof(bool), typeof(UnifiedBrowserControl), true);

        public static readonly BindableProperty ShowRefreshButtonProperty = BindableProperty.Create(
            nameof(ShowRefreshButton), typeof(bool), typeof(UnifiedBrowserControl), true);

        public static readonly BindableProperty ShowHomeButtonProperty = BindableProperty.Create(
            nameof(ShowHomeButton), typeof(bool), typeof(UnifiedBrowserControl), false);

        public static readonly BindableProperty ShowSearchButtonProperty = BindableProperty.Create(
            nameof(ShowSearchButton), typeof(bool), typeof(UnifiedBrowserControl), false);

        public static readonly BindableProperty EnableRouteExtractionProperty = BindableProperty.Create(
            nameof(EnableRouteExtraction), typeof(bool), typeof(UnifiedBrowserControl), false,
            propertyChanged: OnRouteExtractionPropertyChanged);

        public static readonly BindableProperty NormalizeRoutesProperty = BindableProperty.Create(
            nameof(NormalizeRoutes), typeof(bool), typeof(UnifiedBrowserControl), true,
            propertyChanged: OnRouteExtractionPropertyChanged);

        public static readonly BindableProperty EnableAdDetectionProperty = BindableProperty.Create(
            nameof(EnableAdDetection), typeof(bool), typeof(UnifiedBrowserControl), true,
            propertyChanged: OnAdDetectionPropertyChanged);

        public static readonly BindableProperty MaxRoutesProperty = BindableProperty.Create(
            nameof(MaxRoutes), typeof(int), typeof(UnifiedBrowserControl), 200,
            propertyChanged: OnMaxRoutesPropertyChanged);

        public static readonly BindableProperty MaxContextTokensProperty = BindableProperty.Create(
            nameof(MaxContextTokens), typeof(int), typeof(UnifiedBrowserControl), 8192,
            propertyChanged: OnMaxContextTokensPropertyChanged);

        public static readonly BindableProperty MaxTokensProperty = BindableProperty.Create(
            nameof(MaxTokens), typeof(int), typeof(UnifiedBrowserControl), 4096,
            propertyChanged: OnMaxTokensPropertyChanged);

        public static readonly BindableProperty UserAgentProperty = BindableProperty.Create(
            nameof(UserAgent), typeof(string), typeof(UnifiedBrowserControl),
            "",
            propertyChanged: OnUserAgentPropertyChanged);

        public static readonly BindableProperty UserAgentModeProperty = BindableProperty.Create(
            nameof(UserAgentMode), typeof(MarketAlly.Maui.ViewEngine.UserAgentMode), typeof(UnifiedBrowserControl),
            MarketAlly.Maui.ViewEngine.UserAgentMode.Default,
            propertyChanged: OnUserAgentModePropertyChanged);

        public static readonly BindableProperty PageTitleProperty = BindableProperty.Create(
            nameof(PageTitle), typeof(string), typeof(UnifiedBrowserControl), string.Empty);

        public static readonly BindableProperty GeneratingSummaryMessageProperty = BindableProperty.Create(
            nameof(GeneratingSummaryMessage), typeof(string), typeof(UnifiedBrowserControl), "Generating summary...");

        public static readonly BindableProperty EnableAISummarizationProperty = BindableProperty.Create(
            nameof(EnableAISummarization), typeof(bool), typeof(UnifiedBrowserControl), true);

        public static readonly BindableProperty AutoGenerateSummaryProperty = BindableProperty.Create(
            nameof(AutoGenerateSummary), typeof(bool), typeof(UnifiedBrowserControl), false);

        public static readonly BindableProperty IconColorLightProperty = BindableProperty.Create(
            nameof(IconColorLight), typeof(Color), typeof(UnifiedBrowserControl), Color.FromArgb("#007AFF"),
            propertyChanged: OnColorPropertyChanged);

        public static readonly BindableProperty IconColorDarkProperty = BindableProperty.Create(
            nameof(IconColorDark), typeof(Color), typeof(UnifiedBrowserControl), Color.FromArgb("#0A84FF"),
            propertyChanged: OnColorPropertyChanged);

        public static readonly BindableProperty ToolbarBackgroundColorLightProperty = BindableProperty.Create(
            nameof(ToolbarBackgroundColorLight), typeof(Color), typeof(UnifiedBrowserControl), Color.FromArgb("#F5F5F5"),
            propertyChanged: OnColorPropertyChanged);

        public static readonly BindableProperty ToolbarBackgroundColorDarkProperty = BindableProperty.Create(
            nameof(ToolbarBackgroundColorDark), typeof(Color), typeof(UnifiedBrowserControl), Color.FromArgb("#1C1C1E"),
            propertyChanged: OnColorPropertyChanged);

        public static readonly BindableProperty AutoLoadHomePageProperty = BindableProperty.Create(
            nameof(AutoLoadHomePage), typeof(bool), typeof(UnifiedBrowserControl), true);

        public static readonly BindableProperty StartWithBlankTabProperty = BindableProperty.Create(
            nameof(StartWithBlankTab), typeof(bool), typeof(UnifiedBrowserControl), false);

        public static readonly BindableProperty EnableJSONStateProperty = BindableProperty.Create(
            nameof(EnableJSONState), typeof(bool), typeof(UnifiedBrowserControl), false,
            propertyChanged: OnEnableJSONStateChanged);

        public static readonly BindableProperty ThumbnailStoragePathProperty = BindableProperty.Create(
            nameof(ThumbnailStoragePath), typeof(string), typeof(UnifiedBrowserControl), null,
            propertyChanged: OnThumbnailStoragePathChanged);

        public static readonly BindableProperty EnableThumbnailCaptureProperty = BindableProperty.Create(
            nameof(EnableThumbnailCapture), typeof(bool), typeof(UnifiedBrowserControl), true);

        public static readonly BindableProperty ThumbnailWidthProperty = BindableProperty.Create(
            nameof(ThumbnailWidth), typeof(int), typeof(UnifiedBrowserControl), 640);

        public static readonly BindableProperty ThumbnailHeightProperty = BindableProperty.Create(
            nameof(ThumbnailHeight), typeof(int), typeof(UnifiedBrowserControl), 360);

        public static readonly BindableProperty CaptureThumbnailOnNavigationProperty = BindableProperty.Create(
            nameof(CaptureThumbnailOnNavigation), typeof(bool), typeof(UnifiedBrowserControl), true);

        public static readonly BindableProperty BookmarksFilePathProperty = BindableProperty.Create(
            nameof(BookmarksFilePath), typeof(string), typeof(UnifiedBrowserControl), null,
            propertyChanged: OnBookmarksFilePathChanged);

        public static readonly BindableProperty DefaultBookmarkTitleProperty = BindableProperty.Create(
            nameof(DefaultBookmarkTitle), typeof(string), typeof(UnifiedBrowserControl), "MarketAlly",
            propertyChanged: OnDefaultBookmarkChanged);

        public static readonly BindableProperty DefaultBookmarkUrlProperty = BindableProperty.Create(
            nameof(DefaultBookmarkUrl), typeof(string), typeof(UnifiedBrowserControl), "https://www.marketally.com",
            propertyChanged: OnDefaultBookmarkChanged);

        public static readonly BindableProperty ShowMarkdownHistoryProperty = BindableProperty.Create(
            nameof(ShowMarkdownHistory), typeof(bool), typeof(UnifiedBrowserControl), true);

        #endregion

        #region Properties

        public string HomeUrl
        {
            get => (string)GetValue(HomeUrlProperty);
            set => SetValue(HomeUrlProperty, value);
        }

        public bool ShowAddressBar
        {
            get => (bool)GetValue(ShowAddressBarProperty);
            set => SetValue(ShowAddressBarProperty, value);
        }

        public bool ShowBottomNavigation
        {
            get => (bool)GetValue(ShowBottomNavigationProperty);
            set => SetValue(ShowBottomNavigationProperty, value);
        }

        public bool ShowNavigationInAddressBar
        {
            get => (bool)GetValue(ShowNavigationInAddressBarProperty);
            set => SetValue(ShowNavigationInAddressBarProperty, value);
        }

        public bool ShowLinksButton
        {
            get => (bool)GetValue(ShowLinksButtonProperty);
            set => SetValue(ShowLinksButtonProperty, value);
		}

		public bool ShowTabsButton
		{
			get => (bool)GetValue(ShowTabsButtonProperty);
			set => SetValue(ShowTabsButtonProperty, value);
		}

		public bool ShowMarkdownButton
        {
            get => (bool)GetValue(ShowMarkdownButtonProperty);
            set => SetValue(ShowMarkdownButtonProperty, value);
        }

        public bool ShowShareButton
        {
            get => (bool)GetValue(ShowShareButtonProperty);
            set => SetValue(ShowShareButtonProperty, value);
        }

        public bool ShowSaveButton
        {
            get => (bool)GetValue(ShowSaveButtonProperty);
            set => SetValue(ShowSaveButtonProperty, value);
        }

        public bool ShowRefreshButton
        {
            get => (bool)GetValue(ShowRefreshButtonProperty);
            set => SetValue(ShowRefreshButtonProperty, value);
        }

        public bool ShowHomeButton
        {
            get => (bool)GetValue(ShowHomeButtonProperty);
            set => SetValue(ShowHomeButtonProperty, value);
        }

        public bool ShowSearchButton
        {
            get => (bool)GetValue(ShowSearchButtonProperty);
            set => SetValue(ShowSearchButtonProperty, value);
        }

        public bool EnableRouteExtraction
        {
            get => (bool)GetValue(EnableRouteExtractionProperty);
            set => SetValue(EnableRouteExtractionProperty, value);
        }

        public bool NormalizeRoutes
        {
            get => (bool)GetValue(NormalizeRoutesProperty);
            set => SetValue(NormalizeRoutesProperty, value);
        }

        public bool EnableAdDetection
        {
            get => (bool)GetValue(EnableAdDetectionProperty);
            set => SetValue(EnableAdDetectionProperty, value);
        }

        public int MaxRoutes
        {
            get => (int)GetValue(MaxRoutesProperty);
            set => SetValue(MaxRoutesProperty, value);
        }

        public int MaxContextTokens
        {
            get => (int)GetValue(MaxContextTokensProperty);
            set => SetValue(MaxContextTokensProperty, value);
        }

        public int MaxTokens
        {
            get => (int)GetValue(MaxTokensProperty);
            set => SetValue(MaxTokensProperty, value);
        }

        public string UserAgent
        {
            get => (string)GetValue(UserAgentProperty);
            set => SetValue(UserAgentProperty, value);
        }

        public MarketAlly.Maui.ViewEngine.UserAgentMode UserAgentMode
        {
            get => (MarketAlly.Maui.ViewEngine.UserAgentMode)GetValue(UserAgentModeProperty);
            set => SetValue(UserAgentModeProperty, value);
        }

        public string PageTitle
        {
            get => (string)GetValue(PageTitleProperty);
            set => SetValue(PageTitleProperty, value);
        }

        public string GeneratingSummaryMessage
        {
            get => (string)GetValue(GeneratingSummaryMessageProperty);
            set => SetValue(GeneratingSummaryMessageProperty, value);
        }

        public bool EnableAISummarization
        {
            get => (bool)GetValue(EnableAISummarizationProperty);
            set => SetValue(EnableAISummarizationProperty, value);
        }

        public bool AutoGenerateSummary
        {
            get => (bool)GetValue(AutoGenerateSummaryProperty);
            set => SetValue(AutoGenerateSummaryProperty, value);
        }

        public Color IconColorLight
        {
            get => (Color)GetValue(IconColorLightProperty);
            set => SetValue(IconColorLightProperty, value);
        }

        public Color IconColorDark
        {
            get => (Color)GetValue(IconColorDarkProperty);
            set => SetValue(IconColorDarkProperty, value);
        }

        public Color ToolbarBackgroundColorLight
        {
            get => (Color)GetValue(ToolbarBackgroundColorLightProperty);
            set => SetValue(ToolbarBackgroundColorLightProperty, value);
        }

        public Color ToolbarBackgroundColorDark
        {
            get => (Color)GetValue(ToolbarBackgroundColorDarkProperty);
            set => SetValue(ToolbarBackgroundColorDarkProperty, value);
        }

        public bool AutoLoadHomePage
        {
            get => (bool)GetValue(AutoLoadHomePageProperty);
            set => SetValue(AutoLoadHomePageProperty, value);
        }

        public bool StartWithBlankTab
        {
            get => (bool)GetValue(StartWithBlankTabProperty);
            set => SetValue(StartWithBlankTabProperty, value);
        }

        public bool EnableJSONState
        {
            get => (bool)GetValue(EnableJSONStateProperty);
            set => SetValue(EnableJSONStateProperty, value);
        }

        public string? ThumbnailStoragePath
        {
            get => (string?)GetValue(ThumbnailStoragePathProperty);
            set => SetValue(ThumbnailStoragePathProperty, value);
        }

        public bool EnableThumbnailCapture
        {
            get => (bool)GetValue(EnableThumbnailCaptureProperty);
            set => SetValue(EnableThumbnailCaptureProperty, value);
        }

        public int ThumbnailWidth
        {
            get => (int)GetValue(ThumbnailWidthProperty);
            set => SetValue(ThumbnailWidthProperty, value);
        }

        public int ThumbnailHeight
        {
            get => (int)GetValue(ThumbnailHeightProperty);
            set => SetValue(ThumbnailHeightProperty, value);
        }

        public bool CaptureThumbnailOnNavigation
        {
            get => (bool)GetValue(CaptureThumbnailOnNavigationProperty);
            set => SetValue(CaptureThumbnailOnNavigationProperty, value);
        }

        public string? BookmarksFilePath
        {
            get => (string?)GetValue(BookmarksFilePathProperty);
            set => SetValue(BookmarksFilePathProperty, value);
        }

        public string? DefaultBookmarkTitle
        {
            get => (string?)GetValue(DefaultBookmarkTitleProperty);
            set => SetValue(DefaultBookmarkTitleProperty, value);
        }

        public string? DefaultBookmarkUrl
        {
            get => (string?)GetValue(DefaultBookmarkUrlProperty);
            set => SetValue(DefaultBookmarkUrlProperty, value);
        }

        public bool ShowMarkdownHistory
        {
            get => (bool)GetValue(ShowMarkdownHistoryProperty);
            set => SetValue(ShowMarkdownHistoryProperty, value);
        }

        #endregion

        #region Public Properties and Methods

        /// <summary>
        /// Gets the current URL of the browser
        /// </summary>
        public string CurrentUrl => _viewModel?.CurrentUrl ?? string.Empty;

        /// <summary>
        /// Gets the current page content
        /// </summary>
        public string PageContent => _viewModel?.PageContent ?? string.Empty;

        /// <summary>
        /// Gets the extracted links from the current page
        /// </summary>
        public ObservableCollection<LinkItem> ExtractedLinks => _viewModel?.BodyRoutes ?? new ObservableCollection<LinkItem>();

        /// <summary>
        /// Gets the underlying WebView control for advanced scenarios
        /// </summary>
        public MarketAlly.Maui.ViewEngine.BrowserView? GetWebView() => _viewModel?.TabManager?.ActiveTab?.WebView;

        /// <summary>
        /// Navigate to a specific URL
        /// </summary>
        public void NavigateTo(string url)
        {
            _viewModel?.NavigateToUrl(url, true);
        }

        /// <summary>
        /// Navigate back in history
        /// </summary>
        public Task GoBackAsync() => _viewModel?.GoBackCommand.ExecuteAsync(null) ?? Task.CompletedTask;

        /// <summary>
        /// Navigate forward in history
        /// </summary>
        public Task GoForwardAsync() => _viewModel?.GoForwardCommand.ExecuteAsync(null) ?? Task.CompletedTask;

        /// <summary>
        /// Refresh the current page
        /// </summary>
        public Task RefreshAsync() => _viewModel?.RefreshCommand.ExecuteAsync(null) ?? Task.CompletedTask;

        /// <summary>
        /// Refresh the current visible tab
        /// </summary>
        public void RefreshCurrentTab()
        {
            _viewModel?.RefreshCommand.Execute(null);
        }

        /// <summary>
        /// Load a URL in the current tab with optional AI summary generation
        /// </summary>
        /// <param name="url">The URL to load</param>
        /// <param name="generateSummary">If true, automatically generates an AI summary and shows the markdown view</param>
        /// <returns>Task that completes when navigation and optional summary generation are done</returns>
        public async Task LoadUrlAsync(string url, bool generateSummary = false)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (_viewModel == null)
                return;

            // Navigate to the URL
            NavigateTo(url);

            if (generateSummary)
            {
                // Wait for the page to load by monitoring the IsPageLoading property
                var maxWaitTime = TimeSpan.FromSeconds(30);
                var startTime = DateTime.Now;

                while (_viewModel.IsPageLoading && (DateTime.Now - startTime) < maxWaitTime)
                {
                    await Task.Delay(100);
                }

                // If page loaded successfully, generate summary
                if (!_viewModel.IsPageLoading)
                {
                    // Show markdown view
                    _viewModel.IsMarkdownViewVisible = true;
                    _viewModel.IsLinksVisible = false;
                    _viewModel.IsWebViewVisible = false;

                    // Generate the summary
                    var summary = await GenerateSummaryAsync();

                    // Fire the SummaryGenerated event
                    SummaryGenerated?.Invoke(this, new SummaryGeneratedEventArgs(
                        summary,
                        _viewModel.CurrentUrl,
                        _viewModel.PageTitle));
                }
            }
        }

        /// <summary>
        /// Navigate to home URL
        /// </summary>
        public void GoHome()
        {
            _viewModel?.GoHomeCommand.Execute(null);
        }

        /// <summary>
        /// Extract links from the current page
        /// </summary>
        public Task<ObservableCollection<LinkItem>> ExtractLinksAsync()
        {
            return _viewModel?.ExtractLinksAsync() ?? Task.FromResult(new ObservableCollection<LinkItem>());
        }

        /// <summary>
        /// Generate AI summary of the current page
        /// </summary>
        public async Task<string> GenerateSummaryAsync()
        {
            if (_viewModel == null)
                return string.Empty;

            var summary = await _viewModel.GenerateSummaryAsync();

            // Fire the SummaryGenerated event
            if (!string.IsNullOrWhiteSpace(summary))
            {
                SummaryGenerated?.Invoke(this, new SummaryGeneratedEventArgs(
                    summary,
                    _viewModel.CurrentUrl,
                    _viewModel.PageTitle));
            }

            return summary;
        }

        /// <summary>
        /// Show the links view
        /// </summary>
        public void ShowLinks()
        {
            _viewModel?.ToggleLinksCommand.Execute(null);
        }

        /// <summary>
        /// Show the markdown view
        /// </summary>
        public void ShowMarkdownView()
        {
            _viewModel?.ToggleMarkdownViewCommand.Execute(null);
        }

        /// <summary>
        /// Manually capture and save a thumbnail for the current tab
        /// </summary>
        public async Task<bool> CaptureTabThumbnailAsync()
        {
            if (_viewModel == null)
                return false;

            try
            {
                await _viewModel.CaptureAndSaveThumbnailAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Manually adjust the address entry width
        /// </summary>
        /// <param name="reservedWidth">The width to reserve for other toolbar items</param>
        public void SetAddressEntryReservedWidth(double reservedWidth)
        {
            if (AddressEntry == null || MainToolbar == null)
                return;

            var toolbarWidth = MainToolbar.Width;
            if (toolbarWidth <= 0 || double.IsNaN(toolbarWidth))
                toolbarWidth = this.Width;

            if (toolbarWidth <= 0 || double.IsNaN(toolbarWidth))
                return;

            AddressEntry.WidthRequest = toolbarWidth - reservedWidth;
            SearchEntry.WidthRequest = toolbarWidth - reservedWidth;
		}

        /// <summary>
        /// Add a bookmark for the current page
        /// </summary>
        public async Task<bool> AddBookmarkAsync()
        {
            if (_viewModel == null)
                return false;

            try
            {
                await _viewModel.AddBookmarkCommand.ExecuteAsync(null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Add a bookmark with specific title and URL
        /// </summary>
        public Task<bool> AddBookmarkAsync(string title, string url)
        {
            return _viewModel?.BookmarkManager.AddBookmarkAsync(title, url) ?? Task.FromResult(false);
        }

        /// <summary>
        /// Delete a bookmark
        /// </summary>
        public Task<bool> DeleteBookmarkAsync(string url)
        {
            return _viewModel?.BookmarkManager.DeleteBookmarkAsync(url) ?? Task.FromResult(false);
        }

        /// <summary>
        /// Import bookmarks from a file
        /// </summary>
        public Task<bool> ImportBookmarksAsync(string filePath)
        {
            return _viewModel?.BookmarkManager.ImportBookmarksAsync(filePath) ?? Task.FromResult(false);
        }

        /// <summary>
        /// Export bookmarks to a file
        /// </summary>
        public Task<bool> ExportBookmarksAsync(string filePath)
        {
            return _viewModel?.BookmarkManager.ExportBookmarksAsync(filePath) ?? Task.FromResult(false);
        }

        /// <summary>
        /// Check if a URL is bookmarked
        /// </summary>
        public bool IsBookmarked(string url)
        {
            return _viewModel?.BookmarkManager.IsBookmarked(url) ?? false;
        }

        /// <summary>
        /// Get the count of bookmarks
        /// </summary>
        public int GetBookmarkCount()
        {
            return _viewModel?.Bookmarks?.Count ?? 0;
        }

        /// <summary>
        /// Get a read-only list of all bookmarks
        /// </summary>
        public IReadOnlyList<BookmarkItemViewModel> GetBookmarks()
        {
            return _viewModel?.Bookmarks?.ToList() ?? new List<BookmarkItemViewModel>();
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when navigation starts
        /// </summary>
        public event EventHandler<WebNavigatingEventArgs> Navigating;

        /// <summary>
        /// Raised when navigation completes
        /// </summary>
        public event EventHandler<WebNavigatedEventArgs> Navigated;

        /// <summary>
        /// Raised when page data changes
        /// </summary>
        public event EventHandler<PageData> PageDataChanged;

        /// <summary>
        /// Raised when page load completes
        /// </summary>
        public event EventHandler PageLoadComplete;

        /// <summary>
        /// Raised when a link is selected from the links view
        /// </summary>
        public event EventHandler<LinkSelectedEventArgs> LinkSelected;

        /// <summary>
        /// Raised when the page title changes
        /// </summary>
        public event EventHandler<PageTitleChangedEventArgs> PageTitleChanged;

        /// <summary>
        /// Raised when a summary is generated for the current page
        /// </summary>
        public event EventHandler<SummaryGeneratedEventArgs> SummaryGenerated;

        #endregion

        #region Constructor

        public UnifiedBrowserControl()
        {
            InitializeComponent();

            // Create a temporary WebView for initial setup - will be replaced by tab's WebView
            var tempWebView = new MarketAlly.Maui.ViewEngine.BrowserView();
            var tempMarkdownWebView = new Microsoft.Maui.Controls.WebView();

            // Create view model with optional services
            _viewModel = new BrowserViewModel(
                tempWebView,
                tempMarkdownWebView,
                null, // IUnifiedDataService - will be injected if available
                null, // IRAGService - will be injected if available
                null, // ILogger - will be injected if available
                UserAgent,
                UserAgentMode
            )
            {
                HomeUrl = HomeUrl,
                EnableAISummarization = EnableAISummarization,
                AutoGenerateSummary = AutoGenerateSummary,
                EnableThumbnailCapture = EnableThumbnailCapture,
                ThumbnailWidth = ThumbnailWidth,
                ThumbnailHeight = ThumbnailHeight,
                CaptureThumbnailOnNavigation = CaptureThumbnailOnNavigation,
                MaxRoutes = MaxRoutes,
                EnableAdDetection = EnableAdDetection
            };

            BindingContext = _viewModel;

            // Set thumbnail storage path if provided
            if (!string.IsNullOrWhiteSpace(ThumbnailStoragePath))
            {
                _viewModel.TabManager.ThumbnailStoragePath = ThumbnailStoragePath;
            }

            // IMPORTANT: Set default bookmark values BEFORE setting the file path
            // This ensures the defaults are available when creating a new bookmarks file
            _viewModel.BookmarkManager.SetDefaultBookmark(
                DefaultBookmarkTitle ?? "MarketAlly",
                DefaultBookmarkUrl ?? "https://www.marketally.com"
            );

            // Set bookmarks file path if provided (this triggers LoadBookmarksAsync)
            if (!string.IsNullOrWhiteSpace(BookmarksFilePath))
            {
                _viewModel.BookmarkManager.BookmarksFilePath = BookmarksFilePath;
            }

            // Wire up events
            SetupEventHandlers();

            // Subscribe to navigation request event to close toolbar page
            _viewModel.OnNavigateRequested = () =>
            {
                if (MainToolbar != null)
                {
                    MainToolbar.SelectedPage = null;
                }
            };

            // Subscribe to tab switcher request
            _viewModel.OnShowTabSwitcherRequested = () =>
            {
                ShowTabSwitcher();
            };

            // Subscribe to history view request
            _viewModel.OnShowHistoryRequested = () =>
            {
                ShowHistoryView();
            };

            // Subscribe to WebView changed event (tab switch)
            _viewModel.OnWebViewChanged = () =>
            {
                UpdateWebViewContent();
            };

            // Force initial toolbar visibility and colors
            if (MainToolbar != null)
            {
                // Set explicit initial colors
                var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
                MainToolbar.BackgroundColor = isDark ? ToolbarBackgroundColorDark : ToolbarBackgroundColorLight;
                MainToolbar.IsVisible = ShowAddressBar;
                MainToolbar.InvalidateMeasure();
            }

            // Apply initial colors immediately without dispatch
            ApplyThemeColors();
        }

        /// <summary>
        /// Constructor with dependency injection support
        /// </summary>
        public UnifiedBrowserControl(
            IUnifiedDataService? dataService,
            IRAGService? ragService,
            ILogger<BrowserViewModel>? logger)
        {
            InitializeComponent();

            // Store service references for use in history view
            _dataService = dataService;
            _logger = logger;

            // Create a temporary WebView for initial setup - will be replaced by tab's WebView
            var tempWebView = new MarketAlly.Maui.ViewEngine.BrowserView();
            var tempMarkdownWebView = new Microsoft.Maui.Controls.WebView();

            _viewModel = new BrowserViewModel(
                tempWebView,
                tempMarkdownWebView,
                dataService,
                ragService,
                logger,
                UserAgent,
                UserAgentMode
            )
            {
                HomeUrl = HomeUrl,
                EnableAISummarization = EnableAISummarization,
                AutoGenerateSummary = AutoGenerateSummary,
                EnableThumbnailCapture = EnableThumbnailCapture,
                ThumbnailWidth = ThumbnailWidth,
                ThumbnailHeight = ThumbnailHeight,
                CaptureThumbnailOnNavigation = CaptureThumbnailOnNavigation,
                MaxRoutes = MaxRoutes,
                EnableAdDetection = EnableAdDetection
            };

            BindingContext = _viewModel;

            // Set thumbnail storage path if provided
            if (!string.IsNullOrWhiteSpace(ThumbnailStoragePath))
            {
                _viewModel.TabManager.ThumbnailStoragePath = ThumbnailStoragePath;
            }

            // IMPORTANT: Set default bookmark values BEFORE setting the file path
            // This ensures the defaults are available when creating a new bookmarks file
            _viewModel.BookmarkManager.SetDefaultBookmark(
                DefaultBookmarkTitle ?? "MarketAlly",
                DefaultBookmarkUrl ?? "https://www.marketally.com"
            );

            // Set bookmarks file path if provided (this triggers LoadBookmarksAsync)
            if (!string.IsNullOrWhiteSpace(BookmarksFilePath))
            {
                _viewModel.BookmarkManager.BookmarksFilePath = BookmarksFilePath;
            }

            // Wire up events
            SetupEventHandlers();

            // Subscribe to navigation request event to close toolbar page
            _viewModel.OnNavigateRequested = () =>
            {
                if (MainToolbar != null)
                {
                    MainToolbar.SelectedPage = null;
                }
            };

            // Subscribe to tab switcher request
            _viewModel.OnShowTabSwitcherRequested = () =>
            {
                ShowTabSwitcher();
            };

            // Subscribe to history view request
            _viewModel.OnShowHistoryRequested = () =>
            {
                ShowHistoryView();
            };

            // Subscribe to WebView changed event (tab switch)
            _viewModel.OnWebViewChanged = () =>
            {
                UpdateWebViewContent();
            };

            // Force initial toolbar visibility and colors
            if (MainToolbar != null)
            {
                // Set explicit initial colors
                var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
                MainToolbar.BackgroundColor = isDark ? ToolbarBackgroundColorDark : ToolbarBackgroundColorLight;
                MainToolbar.IsVisible = ShowAddressBar;
                MainToolbar.InvalidateMeasure();
            }

            // Apply initial colors immediately without dispatch
            ApplyThemeColors();
        }

        #endregion

        #region Private Methods

        private void SetupEventHandlers()
        {
            // Subscribe to view model property changes
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Apply theme colors when control loads
            this.Loaded += OnControlLoaded;

            // Subscribe to toolbar page changes to clear chip selection
            if (MainToolbar != null)
            {
                MainToolbar.PropertyChanged += OnToolbarPropertyChanged;
            }
        }

        private void OnToolbarPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SelectedPage")
            {
                // When OutlookPage is shown, clear chip selection
                if (MainToolbar?.SelectedPage?.Name == "OutlookPage" && markList != null)
                {
                    markList.SelectedItem = null;
                }

                // When leaving NavigatePage without committing navigation, restore the actual current URL
                if (MainToolbar?.SelectedPage?.Name != "NavigatePage" && _viewModel != null)
                {
                    // Restore the actual URL from the active tab to CurrentUrl
                    // This handles the case where user clears the address bar but then navigates away
                    var activeTab = _viewModel.TabManager?.ActiveTab;
                    if (activeTab != null && !string.IsNullOrWhiteSpace(activeTab.Url))
                    {
                        // Only restore if the CurrentUrl doesn't match the actual tab URL
                        if (_viewModel.CurrentUrl != activeTab.Url)
                        {
                            _viewModel.CurrentUrl = activeTab.Url;
                        }
                    }
                }
            }
        }

        private void OnControlLoaded(object sender, EventArgs e)
        {
            if (MainToolbar != null)
            {
                MainToolbar.IsVisible = ShowAddressBar;
                var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
                MainToolbar.BackgroundColor = isDark ? ToolbarBackgroundColorDark : ToolbarBackgroundColorLight;
            }

            // Set up size monitoring for address entry
            AdjustAddressEntryWidth();
            if (MainToolbar != null)
            {
                MainToolbar.SizeChanged += OnToolbarSizeChanged;
            }

            // Only initialize tabs and load home page on first load
            if (!_hasInitialized)
            {
                _hasInitialized = true;

                // If JSON state is enabled, try to restore tabs
                if (EnableJSONState && !StartWithBlankTab)
                {
                    Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), async () =>
                    {
                        var restored = await _viewModel.RestoreTabStateAsync();
                        if (restored)
                        {
                            UpdateWebViewContent();

                            // If AutoLoadHomePage is enabled, check if we need to add a home page tab
                            if (AutoLoadHomePage && !string.IsNullOrWhiteSpace(HomeUrl))
                            {
                                var activeTab = _viewModel.TabManager.ActiveTab;
                                // Only add home page tab if the active tab is NOT already on the home page
                                if (activeTab != null && !IsHomePageUrl(activeTab.Url))
                                {
                                    // Create a new tab with the home page
                                    var homeTab = _viewModel.TabManager.NewTab(HomeUrl);
                                    if (homeTab != null)
                                    {
                                        homeTab.NavigateTo(HomeUrl);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // If no state was restored, initialize with blank tab if requested
                            _viewModel.InitializeFirstTab(startBlank: StartWithBlankTab);

                            // Auto-load home page if enabled and not starting with blank tab
                            if (AutoLoadHomePage && !StartWithBlankTab && !string.IsNullOrWhiteSpace(HomeUrl))
                            {
                                NavigateTo(HomeUrl);
                            }
                        }
                    });
                }
                else
                {
                    // Initialize the first tab when control loads (blank if requested)
                    _viewModel.InitializeFirstTab(startBlank: StartWithBlankTab);

                    // Auto-load home page if enabled and not starting with blank tab
                    if (AutoLoadHomePage && !StartWithBlankTab && !string.IsNullOrWhiteSpace(HomeUrl))
                    {
                        // Small delay to ensure everything is fully loaded
                        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
                        {
                            NavigateTo(HomeUrl);
                        });
                    }
                }
            }
        }

        private void OnToolbarSizeChanged(object? sender, EventArgs e)
        {
            AdjustAddressEntryWidth();
        }

        private bool IsHomePageUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(HomeUrl))
                return false;

            try
            {
                // Normalize URLs for comparison (remove trailing slashes, compare domains)
                var urlUri = new Uri(url.TrimEnd('/'));
                var homeUri = new Uri(HomeUrl.TrimEnd('/'));

                // Compare the full URLs
                return string.Equals(urlUri.AbsoluteUri, homeUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // If URL parsing fails, do simple string comparison
                return string.Equals(url.TrimEnd('/'), HomeUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
            }
        }

        private void AdjustAddressEntryWidth()
        {
            if (AddressEntry == null || MainToolbar == null)
                return;

            try
            {
                // Get the toolbar width
                var toolbarWidth = MainToolbar.Width;

                // If toolbar width is not yet calculated, use the parent width
                if (toolbarWidth <= 0 || double.IsNaN(toolbarWidth))
                {
                    toolbarWidth = this.Width;
                }

                // If still no valid width, try to get the device display width
                if (toolbarWidth <= 0 || double.IsNaN(toolbarWidth))
                {
                    var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
                    toolbarWidth = displayInfo.Width / displayInfo.Density;
                }

                // If still no valid width, use a default
                if (toolbarWidth <= 0 || double.IsNaN(toolbarWidth))
                {
                    toolbarWidth = 800; // Default fallback
                }

                // Use the public method to set the width
                SetAddressEntryReservedWidth(65);

            }
            catch (Exception)
            {
                // Ignore width adjustment errors
            }
        }

        private void ApplyThemeColors()
        {
            // Apply toolbar background color based on theme
            if (MainToolbar != null)
            {
                // First set the background directly
                MainToolbar.BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? ToolbarBackgroundColorDark
                    : ToolbarBackgroundColorLight;

                // Then set app theme color for dynamic switching
                MainToolbar.SetAppThemeColor(BackgroundColorProperty, ToolbarBackgroundColorLight, ToolbarBackgroundColorDark);

                // Ensure toolbar is visible
                if (!MainToolbar.IsVisible && ShowAddressBar)
                {
                    MainToolbar.IsVisible = true;
                }
            }

            // Apply icon colors to all toolbar buttons
            ApplyIconColorsToToolbar();
        }

        private void ApplyIconColorsToToolbar()
        {
            if (MainToolbar == null) return;

            // Apply colors to all toolbar items
            foreach (var item in MainToolbar.Items)
            {
                ApplyColorToToolbarElement(item);
            }

            // Apply colors to items in toolbar pages
            foreach (var page in MainToolbar.Pages)
            {
                foreach (var item in page.Items)
                {
                    ApplyColorToPageItem(item);
                }
            }
        }

        private void ApplyColorToToolbarElement(DevExpress.Maui.Controls.ToolbarElementBase item)
        {
            if (item is ToolbarNavigationButton navButton)
            {
                navButton.SetAppThemeColor(ToolbarNavigationButton.IconColorProperty, IconColorLight, IconColorDark);
            }
            else if (item is ToolbarToggleButton toggleButton)
            {
                toggleButton.SetAppThemeColor(ToolbarToggleButton.IconColorProperty, IconColorLight, IconColorDark);
            }
        }

        private void ApplyColorToPageItem(DevExpress.Maui.Controls.ToolbarItemBase item)
        {
            // Check if it's a toolbar button and apply colors
            if (item is ToolbarButton button)
            {
                // Check if it's the trash button by checking the Icon property
                if (button.Icon?.ToString()?.Contains("trash") == true)
                {
                    button.SetAppThemeColor(ToolbarButton.IconColorProperty, Color.FromArgb("#FF3B30"), Color.FromArgb("#FF453A"));
                }
                else
                {
                    button.SetAppThemeColor(ToolbarButton.IconColorProperty, IconColorLight, IconColorDark);
                }
            }
        }

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BrowserViewModel.PageTitle))
            {
                PageTitle = _viewModel.PageTitle;

                // Fire the PageTitleChanged event
                PageTitleChanged?.Invoke(this, new PageTitleChangedEventArgs(_viewModel.PageTitle, _viewModel.CurrentUrl));
            }
        }

        private void ShowTabSwitcher()
        {
            if (TabSwitcherHost == null)
                return;

            // Create tab switcher view if not already created
            if (TabSwitcherHost.Content == null)
            {
                var tabSwitcher = new Views.TabSwitcherView(_viewModel);
                TabSwitcherHost.Content = tabSwitcher;
            }
        }

        private void ShowHistoryView()
        {
            if (HistoryViewHost == null)
                return;

            // Create history view if not already created
            if (HistoryViewHost.Content == null)
            {
                var historyView = new Views.HistoryView();
                historyView.Initialize(_viewModel, _dataService, _logger, ShowMarkdownHistory);
                HistoryViewHost.Content = historyView;
            }
        }

        private void UpdateWebViewContent()
        {
            var activeTab = _viewModel?.TabManager?.ActiveTab;
            if (activeTab == null)
                return;

            // Update WebView host
            if (WebViewHost != null)
            {
                WebViewHost.Content = activeTab.WebView;
            }

            // Update Markdown WebView host
            if (MarkdownWebViewHost != null)
            {
                MarkdownWebViewHost.Content = activeTab.MarkdownWebView;
            }
        }

        private static void OnHomeUrlChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && control._viewModel != null)
            {
                control._viewModel.HomeUrl = (string)newValue;
            }
        }

        private static void OnRouteExtractionPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control)
            {
                var webView = control.GetWebView();
                if (webView != null)
                {
                    if (bindable.GetType().GetProperty("EnableRouteExtraction") is not null)
                    {
                        webView.EnableRouteExtraction = control.EnableRouteExtraction;
                    }
                    if (bindable.GetType().GetProperty("NormalizeRoutes") is not null)
                    {
                        webView.NormalizeRoutes = control.NormalizeRoutes;
                    }
                }
            }
        }

        private static void OnAdDetectionPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && newValue is bool enableAdDetection)
            {
                // Update all webviews in all tabs
                if (control._viewModel?.TabManager != null)
                {
                    foreach (var tab in control._viewModel.TabManager.Tabs)
                    {
                        if (tab.WebView != null)
                        {
                            tab.WebView.EnableAdDetection = enableAdDetection;
                        }
                    }
                }

                // Update the view model property
                if (control._viewModel != null)
                {
                    control._viewModel.EnableAdDetection = enableAdDetection;
                }
            }
        }

        private static void OnMaxRoutesPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && newValue is int maxRoutes)
            {
                // Update all webviews in all tabs
                if (control._viewModel?.TabManager != null)
                {
                    foreach (var tab in control._viewModel.TabManager.Tabs)
                    {
                        if (tab.WebView != null)
                        {
                            tab.WebView.MaxRoutes = maxRoutes;
                        }
                    }
                }

                // Update the view model property
                if (control._viewModel != null)
                {
                    control._viewModel.MaxRoutes = maxRoutes;
                }
            }
        }

        private static void OnMaxContextTokensPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && newValue is int maxContextTokens)
            {
                // Update the view model property
                if (control._viewModel != null)
                {
                    control._viewModel.MaxContextTokens = maxContextTokens;
                }
            }
        }

        private static void OnMaxTokensPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && newValue is int maxTokens)
            {
                // Update the view model property
                if (control._viewModel != null)
                {
                    control._viewModel.MaxTokens = maxTokens;
                }
            }
        }

        private static void OnUserAgentPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && newValue is string userAgent)
            {
                var webView = control.GetWebView();
                if (webView != null)
                {
                    webView.UserAgent = userAgent;
                }
            }
        }

        private static void OnUserAgentModePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && newValue is MarketAlly.Maui.ViewEngine.UserAgentMode mode)
            {
                var webView = control.GetWebView();
                if (webView != null)
                {
                    webView.UserAgentMode = mode;
                }
            }
        }

        private static void OnColorPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && control.IsLoaded)
            {
                control.ApplyThemeColors();
            }
        }

        private static void OnEnableJSONStateChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && control._viewModel != null && newValue is bool enableState)
            {
                control._viewModel.EnableJSONState = enableState;

                // If enabled and control is loaded, try to restore state
                if (enableState && control.IsLoaded)
                {
                    control.RestoreTabState();
                }
            }
        }

        private static void OnThumbnailStoragePathChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && control._viewModel != null && newValue is string path)
            {
                control._viewModel.TabManager.ThumbnailStoragePath = path;
            }
        }

        private static void OnBookmarksFilePathChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && control._viewModel != null && newValue is string path)
            {
                control._viewModel.BookmarkManager.BookmarksFilePath = path;
            }
        }

        private static void OnDefaultBookmarkChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is UnifiedBrowserControl control && control._viewModel != null)
            {
                control._viewModel.BookmarkManager.SetDefaultBookmark(
                    control.DefaultBookmarkTitle ?? "MarketAlly",
                    control.DefaultBookmarkUrl ?? "https://www.marketally.com"
                );
            }
        }

        private async void RestoreTabState()
        {
            if (!EnableJSONState || _viewModel == null)
                return;

            try
            {
                var restored = await _viewModel.TabManager.LoadTabStateFromFile();
                if (restored)
                {
                    // Update the WebView content to show the active tab
                    UpdateWebViewContent();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring tab state: {ex.Message}");
            }
        }

        private async void SaveTabState()
        {
            if (!EnableJSONState || _viewModel == null)
                return;

            try
            {
                await _viewModel.TabManager.SaveTabStateToFile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving tab state: {ex.Message}");
            }
        }

        // Note: Event handlers for PageDataChanged, navigation etc. are now handled
        // by the BrowserTab instances and the ViewModel manages the active tab

        #endregion

        #region Overrides

        protected override void OnParentChanged()
        {
            base.OnParentChanged();

            // When parent is set, force toolbar visibility
            if (Parent != null && MainToolbar != null)
            {
                Dispatcher.Dispatch(() =>
                {
                    var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
                    MainToolbar.BackgroundColor = isDark ? ToolbarBackgroundColorDark : ToolbarBackgroundColorLight;
                    MainToolbar.IsVisible = ShowAddressBar;
                    ApplyThemeColors();
                });
            }
        }

        #endregion

        #region Cleanup

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

            if (Handler == null)
            {
                // Cleanup when control is removed
                CleanupEventHandlers();
            }
            else
            {
                // Handler is attached, force refresh
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1), () =>
                {
                    if (MainToolbar != null)
                    {
                        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
                        MainToolbar.BackgroundColor = isDark ? ToolbarBackgroundColorDark : ToolbarBackgroundColorLight;
                        MainToolbar.IsVisible = ShowAddressBar;
                        ApplyThemeColors();
                    }
                });
            }
        }

        private void CleanupEventHandlers()
        {
            // Save tab state if enabled before cleanup
            if (EnableJSONState)
            {
                SaveTabState();
            }

            // Unsubscribe from events to prevent memory leaks
            this.Loaded -= OnControlLoaded;

            if (MainToolbar != null)
            {
                MainToolbar.SizeChanged -= OnToolbarSizeChanged;
                MainToolbar.PropertyChanged -= OnToolbarPropertyChanged;
            }

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

                // Dispose the view model to clean up resources
                _viewModel.Dispose();
            }
        }

		#endregion

		private void DXCollectionView_Tap(object sender, DevExpress.Maui.CollectionView.CollectionViewGestureEventArgs e)
		{
			if (e.Item is LinkItem linkItem)
			{
				_viewModel.NavigateToLinkCommand.Execute(linkItem.Url);

				// Raise event
				LinkSelected?.Invoke(this, new LinkSelectedEventArgs(linkItem));
			}
		}
	}

	/// <summary>
	/// Event args for link selection
	/// </summary>
	public class LinkSelectedEventArgs : EventArgs
    {
        public LinkItem SelectedLink { get; }

        public LinkSelectedEventArgs(LinkItem link)
        {
            SelectedLink = link;
        }
    }

    /// <summary>
    /// Event args for page title changes
    /// </summary>
    public class PageTitleChangedEventArgs : EventArgs
    {
        public string Title { get; }
        public string Url { get; }

        public PageTitleChangedEventArgs(string title, string url)
        {
            Title = title;
            Url = url;
        }
    }

    /// <summary>
    /// Event args for summary generation
    /// </summary>
    public class SummaryGeneratedEventArgs : EventArgs
    {
        public string Summary { get; }
        public string Url { get; }
        public string Title { get; }

        public SummaryGeneratedEventArgs(string summary, string url, string title)
        {
            Summary = summary;
            Url = url;
            Title = title;
        }
    }
}