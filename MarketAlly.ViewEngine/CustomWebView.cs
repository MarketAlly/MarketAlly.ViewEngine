namespace MarketAlly.Maui.ViewEngine
{
	public class WebView : Microsoft.Maui.Controls.WebView
	{
		// Expose PageDataChanged event at the WebView level
		public event EventHandler<PageData> PageDataChanged;

		// Track navigation behavior for auto-detection
		private string _lastUrl = string.Empty;
		private int _clicksWithoutNavigation = 0;
		private bool _autoDetectNavigationIssues = true;

		public static readonly BindableProperty UserAgentProperty =
			BindableProperty.Create(nameof(UserAgent), typeof(string), typeof(WebView), default(string));

		public static readonly BindableProperty MaxRoutesProperty =
			BindableProperty.Create(nameof(MaxRoutes), typeof(int), typeof(WebView), 100);

		public static readonly BindableProperty EnableRouteExtractionProperty =
			BindableProperty.Create(nameof(EnableRouteExtraction), typeof(bool), typeof(WebView), false);

		public static readonly BindableProperty NormalizeRoutesProperty =
			BindableProperty.Create(nameof(NormalizeRoutes), typeof(bool), typeof(WebView), true);

		public static readonly BindableProperty ExcludeDomainsProperty =
			BindableProperty.Create(nameof(ExcludeDomains), typeof(List<string>), typeof(WebView), null);

		public static readonly BindableProperty EnableAdDetectionProperty =
			BindableProperty.Create(nameof(EnableAdDetection), typeof(bool), typeof(WebView), false);

		public static readonly BindableProperty ForceLinkNavigationProperty =
			BindableProperty.Create(nameof(ForceLinkNavigation), typeof(bool), typeof(WebView), false);

		public static readonly BindableProperty AutoDetectNavigationIssuesProperty =
			BindableProperty.Create(nameof(AutoDetectNavigationIssues), typeof(bool), typeof(WebView), true);

		public WebView()
		{
			// Use both Loaded and HandlerChanged for maximum compatibility
			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
			HandlerChanged += OnHandlerChanged;
			HandlerChanging += OnHandlerChanging;
			PropertyChanged += OnPropertyChanged;

			// Also subscribe to Navigating and Navigated events
			Navigating += OnNavigating;
			Navigated += OnNavigated;
		}

		private void OnNavigating(object sender, WebNavigatingEventArgs e)
		{
		}

		private async void OnNavigated(object sender, WebNavigatedEventArgs e)
		{
			// Check if navigation was successful and URL changed
			if (e.Result == WebNavigationResult.Success)
			{
				// Auto-detection logic
				if (AutoDetectNavigationIssues && !string.IsNullOrEmpty(e.Url))
				{
					if (e.Url != _lastUrl)
					{
						// Navigation succeeded, reset counter
						_clicksWithoutNavigation = 0;
						_lastUrl = e.Url;

						// Disable force navigation for new pages
						if (ForceLinkNavigation)
						{
							ForceLinkNavigation = false;
							System.Diagnostics.Debug.WriteLine($"Auto-disabled ForceLinkNavigation after successful navigation to: {e.Url}");
						}
					}
				}

				// Trigger page data extraction when navigation completes successfully
				if (Handler is WebViewHandler handler)
				{
					await Task.Delay(500); // Small delay to let page load
					await handler.OnPageDataChangedAsync();
				}
			}
		}

		private void OnLoaded(object sender, EventArgs e)
		{
			SubscribeToHandlerEvents();
		}

		private void OnUnloaded(object sender, EventArgs e)
		{
			UnsubscribeFromHandlerEvents();
			PropertyChanged -= OnPropertyChanged;
		}

		private void OnHandlerChanging(object sender, HandlerChangingEventArgs e)
		{

			// Unsubscribe from old handler
			if (e.OldHandler is WebViewHandler oldHandler)
			{
				oldHandler.PageDataChanged -= OnPageDataChanged;
			}
		}

		private void OnHandlerChanged(object sender, EventArgs e)
		{
			SubscribeToHandlerEvents();
		}

		private void SubscribeToHandlerEvents()
		{
			if (Handler is WebViewHandler handler)
			{
				handler.PageDataChanged -= OnPageDataChanged;
				handler.PageDataChanged += OnPageDataChanged;
			}
			else
			{
			}
		}

		private void UnsubscribeFromHandlerEvents()
		{
			if (Handler is WebViewHandler handler)
			{
				handler.PageDataChanged -= OnPageDataChanged;
			}
		}

		private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{

			if (e.PropertyName == nameof(Source))
			{
				var sourceUrl = (Source as UrlWebViewSource)?.Url;
				NormalizeSource();
			}
		}

		private void NormalizeSource()
		{
			if (Source is UrlWebViewSource urlSource && !string.IsNullOrEmpty(urlSource.Url))
			{
				var url = urlSource.Url.Trim();

				// Check if URL needs scheme
				if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
				    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
				    !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
				{
					// Create new source with normalized URL
					Source = new UrlWebViewSource { Url = "https://" + url };
				}
			}
		}

		private void OnPageDataChanged(object sender, PageData pageData)
		{
			// Auto-detection: check if URL hasn't changed (possible click without navigation)
			if (AutoDetectNavigationIssues && !string.IsNullOrEmpty(pageData.Url))
			{
				if (pageData.Url == _lastUrl)
				{
					_clicksWithoutNavigation++;
					System.Diagnostics.Debug.WriteLine($"Detected click without navigation (count: {_clicksWithoutNavigation}) on: {pageData.Url}");

					// After 2 clicks that don't navigate, enable force navigation
					if (_clicksWithoutNavigation >= 2 && !ForceLinkNavigation)
					{
						ForceLinkNavigation = true;
						System.Diagnostics.Debug.WriteLine($"Auto-enabled ForceLinkNavigation after {_clicksWithoutNavigation} non-navigating clicks");
					}
				}
				else
				{
					// URL changed, reset counter
					_clicksWithoutNavigation = 0;
					_lastUrl = pageData.Url;
				}
			}

			PageDataChanged?.Invoke(this, pageData);
		}

		public string UserAgent
		{
			get => (string)GetValue(UserAgentProperty);
			set => SetValue(UserAgentProperty, value);
		}

		/// <summary>
		/// Maximum number of routes to return in Routes and BodyRoutes collections.
		/// Default is 100. Set to -1 for unlimited (not recommended for large pages).
		/// Only applies when EnableRouteExtraction is true or ExtractRoutesAsync() is called.
		/// </summary>
		public int MaxRoutes
		{
			get => (int)GetValue(MaxRoutesProperty);
			set => SetValue(MaxRoutesProperty, value);
		}

		/// <summary>
		/// When false (default), Routes and BodyRoutes will be empty on PageDataChanged.
		/// Call ExtractRoutesAsync() to extract links on-demand for better performance.
		/// Set to true to automatically extract routes on every page load.
		/// </summary>
		public bool EnableRouteExtraction
		{
			get => (bool)GetValue(EnableRouteExtractionProperty);
			set => SetValue(EnableRouteExtractionProperty, value);
		}

		/// <summary>
		/// When true (default), URLs are normalized by removing tracking parameters (zx, ved, utm_*, fbclid, etc.) and fragments.
		/// This groups duplicate URLs with different tracking params into a single route with higher Occurrences count.
		/// Set to false to keep all URLs exactly as they appear (each tracking variation becomes a separate route).
		/// </summary>
		public bool NormalizeRoutes
		{
			get => (bool)GetValue(NormalizeRoutesProperty);
			set => SetValue(NormalizeRoutesProperty, value);
		}

		/// <summary>
		/// List of domains to exclude from URL normalization.
		/// URLs from these domains will keep all query parameters and fragments intact, even when NormalizeRoutes is true.
		/// Example: ExcludeDomains = new List&lt;string&gt; { "example.com", "api.mysite.com" }
		/// </summary>
		public List<string> ExcludeDomains
		{
			get => (List<string>)GetValue(ExcludeDomainsProperty);
			set => SetValue(ExcludeDomainsProperty, value);
		}

		/// <summary>
		/// When true, runs background ad detection on extracted routes.
		/// When false (default), ad detection is skipped for better performance.
		/// Ad detection adds IsPotentialAd and AdReason properties to RouteInfo.
		/// Set to true only if you need to filter advertisements from route lists.
		/// </summary>
		public bool EnableAdDetection
		{
			get => (bool)GetValue(EnableAdDetectionProperty);
			set => SetValue(EnableAdDetectionProperty, value);
		}

		/// <summary>
		/// When true, forces link navigation on sites that prevent it (like some SPAs).
		/// When false (default), uses normal WebView navigation behavior.
		/// Can be set manually or will be auto-enabled when navigation issues are detected.
		/// </summary>
		public bool ForceLinkNavigation
		{
			get => (bool)GetValue(ForceLinkNavigationProperty);
			set => SetValue(ForceLinkNavigationProperty, value);
		}

		/// <summary>
		/// When true (default), automatically detects when sites prevent navigation and enables ForceLinkNavigation.
		/// Detects by counting clicks that don't result in URL changes.
		/// Set to false to disable auto-detection and control ForceLinkNavigation manually.
		/// </summary>
		public bool AutoDetectNavigationIssues
		{
			get => (bool)GetValue(AutoDetectNavigationIssuesProperty);
			set => SetValue(AutoDetectNavigationIssuesProperty, value);
		}

		/// <summary>
		/// Fetches the page's title, body, and meta description.
		/// </summary>
		public async Task<PageData> GetPageDataAsync()
		{
			if (Handler is WebViewHandler customHandler)
			{
				return await customHandler.GetPageDataAsync();
			}

			return new PageData { Title = "Error", Body = "WebView handler is not available." };
		}

		/// <summary>
		/// Extracts routes on-demand from the current page.
		/// Returns the current PageData with Routes and BodyRoutes populated.
		/// Use this when EnableRouteExtraction is false for better performance.
		/// </summary>
		public async Task<PageData> ExtractRoutesAsync()
		{
			if (Handler is WebViewHandler customHandler)
			{
				return await customHandler.ExtractRoutesAsync();
			}

			return new PageData { Title = "Error", Body = "WebView handler is not available." };
		}
	}
}
