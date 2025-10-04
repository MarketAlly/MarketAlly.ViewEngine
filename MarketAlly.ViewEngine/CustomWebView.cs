namespace MarketAlly.Maui.ViewEngine
{
	public class WebView : Microsoft.Maui.Controls.WebView
	{
		// Expose PageDataChanged event at the WebView level
		public event EventHandler<PageData> PageDataChanged;

		public static readonly BindableProperty UserAgentProperty =
			BindableProperty.Create(nameof(UserAgent), typeof(string), typeof(WebView), default(string));

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
			Console.WriteLine($"[CustomWebView] Navigating to: {e.Url}");
		}

		private void OnNavigated(object sender, WebNavigatedEventArgs e)
		{
			Console.WriteLine($"[CustomWebView] Navigated to: {e.Url}, Result: {e.Result}");
		}

		private void OnLoaded(object sender, EventArgs e)
		{
			Console.WriteLine($"[CustomWebView] OnLoaded - Handler type: {Handler?.GetType().FullName}");
			SubscribeToHandlerEvents();
		}

		private void OnUnloaded(object sender, EventArgs e)
		{
			Console.WriteLine($"[CustomWebView] OnUnloaded");
			UnsubscribeFromHandlerEvents();
			PropertyChanged -= OnPropertyChanged;
		}

		private void OnHandlerChanging(object sender, HandlerChangingEventArgs e)
		{
			Console.WriteLine($"[CustomWebView] OnHandlerChanging - Old: {e.OldHandler?.GetType().FullName}, New: {e.NewHandler?.GetType().FullName}");

			// Unsubscribe from old handler
			if (e.OldHandler is WebViewHandler oldHandler)
			{
				Console.WriteLine("[CustomWebView] Unsubscribing from old handler");
				oldHandler.PageDataChanged -= OnPageDataChanged;
			}
		}

		private void OnHandlerChanged(object sender, EventArgs e)
		{
			Console.WriteLine($"[CustomWebView] OnHandlerChanged - Handler type: {Handler?.GetType().FullName}");
			SubscribeToHandlerEvents();
		}

		private void SubscribeToHandlerEvents()
		{
			if (Handler is WebViewHandler handler)
			{
				Console.WriteLine("[CustomWebView] Handler IS WebViewHandler - subscribing to PageDataChanged");
				handler.PageDataChanged -= OnPageDataChanged;
				handler.PageDataChanged += OnPageDataChanged;
			}
			else
			{
				Console.WriteLine($"[CustomWebView] Handler is NOT WebViewHandler! It's: {Handler?.GetType().FullName}");
			}
		}

		private void UnsubscribeFromHandlerEvents()
		{
			if (Handler is WebViewHandler handler)
			{
				Console.WriteLine("[CustomWebView] Unsubscribing from handler events");
				handler.PageDataChanged -= OnPageDataChanged;
			}
		}

		private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			Console.WriteLine($"[CustomWebView] PropertyChanged: {e.PropertyName}");

			if (e.PropertyName == nameof(Source))
			{
				var sourceUrl = (Source as UrlWebViewSource)?.Url;
				Console.WriteLine($"[CustomWebView] Source changed to: {sourceUrl}");
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
					Console.WriteLine($"[CustomWebView] Normalizing URL from '{url}' to 'https://{url}'");
					// Create new source with normalized URL
					Source = new UrlWebViewSource { Url = "https://" + url };
				}
			}
		}

		private void OnPageDataChanged(object sender, PageData pageData)
		{
			Console.WriteLine($"[CustomWebView] OnPageDataChanged called - Title: {pageData?.Title}, URL: {pageData?.Url}");
			Console.WriteLine($"[CustomWebView] PageDataChanged event has {PageDataChanged?.GetInvocationList().Length ?? 0} subscribers");
			PageDataChanged?.Invoke(this, pageData);
		}

		public string UserAgent
		{
			get => (string)GetValue(UserAgentProperty);
			set => SetValue(UserAgentProperty, value);
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
	}
}
