namespace MarketAlly.Maui.ViewEngine
{
	public class WebView : Microsoft.Maui.Controls.WebView
	{
		// Expose PageDataChanged event at the WebView level
		public event EventHandler<PageData> PageDataChanged;

		public static readonly BindableProperty UserAgentProperty =
			BindableProperty.Create(nameof(UserAgent), typeof(string), typeof(WebView), default(string));

		public static readonly BindableProperty MaxRoutesProperty =
			BindableProperty.Create(nameof(MaxRoutes), typeof(int), typeof(WebView), 100);

		public static readonly BindableProperty EnableRouteExtractionProperty =
			BindableProperty.Create(nameof(EnableRouteExtraction), typeof(bool), typeof(WebView), false);

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

			// Trigger page data extraction when navigation completes successfully
			if (e.Result == WebNavigationResult.Success && Handler is WebViewHandler handler)
			{
				await Task.Delay(500); // Small delay to let page load
				await handler.OnPageDataChangedAsync();
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
