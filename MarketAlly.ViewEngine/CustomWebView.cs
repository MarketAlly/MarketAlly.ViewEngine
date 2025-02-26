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
			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
		}

		private void OnLoaded(object sender, EventArgs e)
		{
			if (Handler is WebViewHandler handler)
			{
				// Ensure no duplicate subscriptions
				handler.PageDataChanged -= OnPageDataChanged;
				handler.PageDataChanged += OnPageDataChanged;
			}
		}

		private void OnUnloaded(object sender, EventArgs e)
		{
			if (Handler is WebViewHandler handler)
			{
				handler.PageDataChanged -= OnPageDataChanged; // Unsubscribe to prevent leaks
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
