using Microsoft.Maui.Controls.Internals;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace MarketAlly.Maui.ViewEngine
{
	/// <summary>
	/// Navigation history item containing URL, title, and timestamp
	/// </summary>
	public class NavigationHistoryItem
	{
		public string Url { get; set; }
		public string Title { get; set; }
		public DateTime Timestamp { get; set; }
		public string FaviconUrl { get; set; }
		public Microsoft.Maui.Controls.ImageSource Thumbnail { get; set; }
	}

	/// <summary>
	/// Hybrid control that seamlessly displays web pages and PDF files with custom navigation history
	/// </summary>
	[Preserve]
	public class BrowserView : ContentView
	{
		private readonly Grid _container;
		private readonly WebView _webView;
		private readonly PdfView _pdfView;
		private byte[] _currentPdfData;
		private int _currentHistoryIndex = -1;
		private bool _isNavigatingFromHistory = false;
		private bool _isShowingPdf = false;
		private System.Threading.CancellationTokenSource _historyNavigationCts;
	private readonly HashSet<string> _failedPdfUrls = new HashSet<string>();

		// Expose PageDataChanged event
		public event EventHandler<PageData> PageDataChanged;
		public event EventHandler<EventArgs> PageLoadComplete;
		public event EventHandler<NavigationHistoryItem> NavigatingToHistoryItem;

		#region Bindable Properties

		public static readonly BindableProperty SourceProperty =
			BindableProperty.Create(nameof(Source), typeof(WebViewSource), typeof(BrowserView), null,
				propertyChanged: OnSourceChanged);

		public static readonly BindableProperty UserAgentProperty =
			BindableProperty.Create(nameof(UserAgent), typeof(string), typeof(BrowserView), default(string));

		public static readonly BindableProperty UserAgentModeProperty =
			BindableProperty.Create(nameof(UserAgentMode), typeof(UserAgentMode), typeof(BrowserView), UserAgentMode.Default);

		public static readonly BindableProperty MaxRoutesProperty =
			BindableProperty.Create(nameof(MaxRoutes), typeof(int), typeof(BrowserView), 100);

		public static readonly BindableProperty EnableRouteExtractionProperty =
			BindableProperty.Create(nameof(EnableRouteExtraction), typeof(bool), typeof(BrowserView), false);

		public static readonly BindableProperty NormalizeRoutesProperty =
			BindableProperty.Create(nameof(NormalizeRoutes), typeof(bool), typeof(BrowserView), true);

		public static readonly BindableProperty ExcludeDomainsProperty =
			BindableProperty.Create(nameof(ExcludeDomains), typeof(List<string>), typeof(BrowserView), null);

		public static readonly BindableProperty EnableAdDetectionProperty =
			BindableProperty.Create(nameof(EnableAdDetection), typeof(bool), typeof(BrowserView), false);

		public static readonly BindableProperty ForceLinkNavigationProperty =
			BindableProperty.Create(nameof(ForceLinkNavigation), typeof(bool), typeof(BrowserView), false);

		public static readonly BindableProperty AutoDetectNavigationIssuesProperty =
			BindableProperty.Create(nameof(AutoDetectNavigationIssues), typeof(bool), typeof(BrowserView), true);

		public static readonly BindableProperty IsLoadingProperty =
			BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(BrowserView), false,
				propertyChanged: (bindable, oldValue, newValue) =>
				{
					var view = (BrowserView)bindable;
					if (newValue is bool isLoading && !isLoading)
					{
						view.PageLoadComplete?.Invoke(view, EventArgs.Empty);
					}
				});

		public static readonly BindableProperty EnableThumbnailCaptureProperty =
			BindableProperty.Create(nameof(EnableThumbnailCapture), typeof(bool), typeof(BrowserView), false);

		public static readonly BindableProperty MaxHistoryItemsProperty =
			BindableProperty.Create(nameof(MaxHistoryItems), typeof(int), typeof(BrowserView), 50);

		public static readonly BindableProperty ShowInlinePdfProperty =
			BindableProperty.Create(nameof(ShowInlinePdf), typeof(bool), typeof(BrowserView), true);

		public static readonly BindableProperty EnableZoomProperty =
			BindableProperty.Create(nameof(EnableZoom), typeof(bool), typeof(BrowserView), true);

		#endregion

		#region Properties

		public WebViewSource Source
		{
			get => (WebViewSource)GetValue(SourceProperty);
			set => SetValue(SourceProperty, value);
		}

		public string UserAgent
		{
			get => (string)GetValue(UserAgentProperty);
			set => SetValue(UserAgentProperty, value);
		}

		public UserAgentMode UserAgentMode
		{
			get => (UserAgentMode)GetValue(UserAgentModeProperty);
			set => SetValue(UserAgentModeProperty, value);
		}

		public int MaxRoutes
		{
			get => (int)GetValue(MaxRoutesProperty);
			set => SetValue(MaxRoutesProperty, value);
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

		public List<string> ExcludeDomains
		{
			get => (List<string>)GetValue(ExcludeDomainsProperty);
			set => SetValue(ExcludeDomainsProperty, value);
		}

		public bool EnableAdDetection
		{
			get => (bool)GetValue(EnableAdDetectionProperty);
			set => SetValue(EnableAdDetectionProperty, value);
		}

		public bool ForceLinkNavigation
		{
			get => (bool)GetValue(ForceLinkNavigationProperty);
			set => SetValue(ForceLinkNavigationProperty, value);
		}

		public bool AutoDetectNavigationIssues
		{
			get => (bool)GetValue(AutoDetectNavigationIssuesProperty);
			set => SetValue(AutoDetectNavigationIssuesProperty, value);
		}

		public bool IsLoading
		{
			get => (bool)GetValue(IsLoadingProperty);
			set => SetValue(IsLoadingProperty, value);
		}

		public bool EnableThumbnailCapture
		{
			get => (bool)GetValue(EnableThumbnailCaptureProperty);
			set => SetValue(EnableThumbnailCaptureProperty, value);
		}

		public int MaxHistoryItems
		{
			get => (int)GetValue(MaxHistoryItemsProperty);
			set => SetValue(MaxHistoryItemsProperty, value);
		}

		public bool ShowInlinePdf
		{
			get => (bool)GetValue(ShowInlinePdfProperty);
			set => SetValue(ShowInlinePdfProperty, value);
		}

		public bool EnableZoom
		{
			get => (bool)GetValue(EnableZoomProperty);
			set => SetValue(EnableZoomProperty, value);
		}

		/// <summary>
		/// Observable collection of navigation history with titles
		/// </summary>
		public ObservableCollection<NavigationHistoryItem> NavigationHistory { get; } = new ObservableCollection<NavigationHistoryItem>();

		/// <summary>
		/// Current position in navigation history (0-based)
		/// </summary>
		public int CurrentHistoryIndex
		{
			get => _currentHistoryIndex;
			private set
			{
				if (_currentHistoryIndex != value)
				{
					_currentHistoryIndex = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CanGoBackInHistory));
					OnPropertyChanged(nameof(CanGoForwardInHistory));
				}
			}
		}

		public bool CanGoBackInHistory => CurrentHistoryIndex > 0;
		public bool CanGoForwardInHistory => CurrentHistoryIndex < NavigationHistory.Count - 1;

		#endregion

		public BrowserView()
		{
			_container = new Grid();
			_webView = new WebView();
		_webView.ParentBrowserView = this;
			_pdfView = new PdfView();

			// Setup grid
			_container.Children.Add(_webView);
			_container.Children.Add(_pdfView);

			// Initially show WebView
			_webView.IsVisible = true;
			_pdfView.IsVisible = false;

			Content = _container;

			// Wire up events
			_webView.PageDataChanged += OnWebViewPageDataChanged;
			_webView.PageLoadComplete += (s, e) => PageLoadComplete?.Invoke(this, e);
			_webView.PropertyChanged += OnWebViewPropertyChanged;

			// Sync properties from BrowserView to WebView
			this.PropertyChanged += OnBrowserViewPropertyChanged;
		}

		private static void OnSourceChanged(BindableObject bindable, object oldValue, object newValue)
		{
			var view = (BrowserView)bindable;
			if (newValue is WebViewSource source)
			{
				view._webView.Source = source;
			}
		}

		private void OnBrowserViewPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			// Sync relevant properties to WebView
			switch (e.PropertyName)
			{
				case nameof(UserAgent):
					_webView.UserAgent = UserAgent;
					break;
				case nameof(UserAgentMode):
					_webView.UserAgentMode = UserAgentMode;
					break;
				case nameof(MaxRoutes):
					_webView.MaxRoutes = MaxRoutes;
					break;
				case nameof(EnableRouteExtraction):
					_webView.EnableRouteExtraction = EnableRouteExtraction;
					break;
				case nameof(NormalizeRoutes):
					_webView.NormalizeRoutes = NormalizeRoutes;
					break;
				case nameof(ExcludeDomains):
					_webView.ExcludeDomains = ExcludeDomains;
					break;
				case nameof(EnableAdDetection):
					_webView.EnableAdDetection = EnableAdDetection;
					break;
				case nameof(ForceLinkNavigation):
					_webView.ForceLinkNavigation = ForceLinkNavigation;
					break;
				case nameof(AutoDetectNavigationIssues):
					_webView.AutoDetectNavigationIssues = AutoDetectNavigationIssues;
					break;
				case nameof(EnableThumbnailCapture):
					_webView.EnableThumbnailCapture = EnableThumbnailCapture;
					break;
				case nameof(EnableZoom):
					_webView.EnableZoom = EnableZoom;
					_pdfView.MaxZoom = EnableZoom ? 4.0f : 1.0f;
					break;
			}
		}

		private void OnWebViewPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			// Sync IsLoading from WebView to BrowserView
			if (e.PropertyName == nameof(WebView.IsLoading))
			{
				IsLoading = _webView.IsLoading;
			}
		}

		private async void OnWebViewPageDataChanged(object sender, PageData pageData)
		{
			// Check if this is a PDF URL
			if (_webView.Handler is WebViewHandler handler && handler.IsPotentialPdfUrl(pageData.Url))
			{
			// Skip if we've already tried and failed to download this PDF
			if (_failedPdfUrls.Contains(pageData.Url))
			{
				System.Diagnostics.Debug.WriteLine($"PDF URL previously failed, letting WebView handle it: {pageData.Url}");
			}
			else if (ShowInlinePdf && await handler.ConfirmPdfContent(pageData.Url))
			{
				System.Diagnostics.Debug.WriteLine($"Confirmed PDF content, calling ShowPdfAsync");
				await ShowPdfAsync(pageData.Url);
				return;
			}
		}

		// Not a PDF, ensure WebView is visible
		_webView.IsVisible = true;
		_pdfView.IsVisible = false;
		_currentPdfData = null;

		// Add to navigation history (only if not navigating from history)
		if (!_isNavigatingFromHistory && !string.IsNullOrEmpty(pageData.Url) && !string.IsNullOrEmpty(pageData.Title))
		{
			AddToNavigationHistory(pageData);
		}
		else if (_isNavigatingFromHistory)
		{
			System.Diagnostics.Debug.WriteLine($"Skipping history add - navigating from history");
		}

		// Note: Don't reset _isNavigatingFromHistory here - it's managed by NavigateToHistoryItemInternalAsync

		// Forward event
		PageDataChanged?.Invoke(this, pageData);
	}

		internal async Task ShowPdfAsync(string url)
		{
			try
			{
				IsLoading = true;

				// Download PDF with proper headers
				using var httpClient = new HttpClient(new HttpClientHandler
				{
					AllowAutoRedirect = true,
					MaxAutomaticRedirections = 10
				});
				httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
				httpClient.DefaultRequestHeaders.Add("Accept", "application/pdf,application/octet-stream,*/*");

				_currentPdfData = await httpClient.GetByteArrayAsync(url);

				System.Diagnostics.Debug.WriteLine($"Downloaded {_currentPdfData?.Length ?? 0} bytes from {url}");
				if (_currentPdfData != null && _currentPdfData.Length >= 20)
				{
					var headerPreview = System.Text.Encoding.ASCII.GetString(_currentPdfData, 0, Math.Min(20, _currentPdfData.Length));
					System.Diagnostics.Debug.WriteLine($"Content starts with: {headerPreview}");
				}

				// Validate it's actually a PDF (check for PDF header: %PDF)
				if (_currentPdfData == null || _currentPdfData.Length < 5 ||
					!(_currentPdfData[0] == 0x25 && _currentPdfData[1] == 0x50 &&
					  _currentPdfData[2] == 0x44 && _currentPdfData[3] == 0x46))
				{
					// Not a valid PDF, let WebView handle it instead
					System.Diagnostics.Debug.WriteLine($"Downloaded content is not a valid PDF, falling back to WebView");
				_failedPdfUrls.Add(url);
				_webView.IsVisible = true;
				_pdfView.IsVisible = false;
					IsLoading = false;
					return;
				}

				System.Diagnostics.Debug.WriteLine($"PDF header validated successfully");

				// Save to temp file for PdfView
				var tempFile = DataSources.PdfTempFileHelper.CreateTempPdfFilePath();
				await File.WriteAllBytesAsync(tempFile, _currentPdfData);

				// Show PDF
				_pdfView.Uri = tempFile;
				_webView.IsVisible = false;
				_pdfView.IsVisible = true;

				// Extract text for PageData (best effort - ignore errors)
				string extractedText = string.Empty;
				if (_webView.Handler is WebViewHandler handler)
				{
					try
					{
						await handler.HandlePdfDownload(_currentPdfData, url);
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Failed to extract PDF text: {ex.Message}");
					}
				}

				// Create PageData for PDF
				var pdfTitle = System.IO.Path.GetFileName(url);
				if (string.IsNullOrWhiteSpace(pdfTitle))
				{
					// Extract title from URL or use a default
					var uri = new Uri(url);
					pdfTitle = uri.Segments.Length > 0 ? uri.Segments[^1] : "PDF Document";
				}

				var pageData = new PageData
				{
					Title = pdfTitle,
					Body = extractedText,
					Url = url,
					MetaDescription = "PDF Document"
				};

				// Add to history
				System.Diagnostics.Debug.WriteLine($"ShowPdfAsync: _isNavigatingFromHistory={_isNavigatingFromHistory}, URL={url}, Title={pdfTitle}");
				if (!_isNavigatingFromHistory)
				{
					AddToNavigationHistory(pageData);
				}
				else
				{
					System.Diagnostics.Debug.WriteLine("PDF NOT added to history - navigating from history");
				}

				_isNavigatingFromHistory = false;
				IsLoading = false;

				// Fire event
				PageDataChanged?.Invoke(this, pageData);
			}
			catch (Exception ex)
			{
				IsLoading = false;
				System.Diagnostics.Debug.WriteLine($"Error loading PDF: {ex.Message}");
			}
		}

		private void AddToNavigationHistory(PageData pageData)
		{
			System.Diagnostics.Debug.WriteLine($"AddToNavigationHistory: URL={pageData.Url}, Title={pageData.Title}, CurrentIndex={CurrentHistoryIndex}, HistoryCount={NavigationHistory.Count}");

			// Remove forward history when navigating to new page
			if (CurrentHistoryIndex < NavigationHistory.Count - 1)
			{
				while (NavigationHistory.Count > CurrentHistoryIndex + 1)
				{
					NavigationHistory.RemoveAt(NavigationHistory.Count - 1);
				}
			}

			// Don't add duplicate consecutive entries
			if (NavigationHistory.Count == 0 ||
				NavigationHistory[NavigationHistory.Count - 1].Url != pageData.Url)
			{
				var historyItem = new NavigationHistoryItem
				{
					Url = pageData.Url,
					Title = pageData.Title ?? "Untitled",
					Timestamp = DateTime.Now,
					FaviconUrl = pageData.FaviconUrl,
					Thumbnail = EnableThumbnailCapture ? pageData.Thumbnail : null
				};

				NavigationHistory.Add(historyItem);
				CurrentHistoryIndex = NavigationHistory.Count - 1;

				// Debug: Show current history
				System.Diagnostics.Debug.WriteLine($"  History now has {NavigationHistory.Count} items:");
				for (int i = 0; i < NavigationHistory.Count; i++)
				{
					var marker = i == CurrentHistoryIndex ? " <--" : "";
					System.Diagnostics.Debug.WriteLine($"    [{i}] {NavigationHistory[i].Title} ({NavigationHistory[i].Url}){marker}");
				}

				// Enforce max history limit
				while (NavigationHistory.Count > MaxHistoryItems)
				{
					NavigationHistory.RemoveAt(0);
					CurrentHistoryIndex--;
				}
			}
		}

		#region Navigation Methods

		/// <summary>
		/// Navigate back in custom history
		/// </summary>
		public async Task GoBackInHistoryAsync()
		{
			if (!CanGoBackInHistory)
			{
				System.Diagnostics.Debug.WriteLine($"Cannot go back: CurrentIndex={CurrentHistoryIndex}");
				return;
			}

			System.Diagnostics.Debug.WriteLine($"Going back: CurrentIndex={CurrentHistoryIndex} -> {CurrentHistoryIndex - 1}");
			CurrentHistoryIndex--;
			await NavigateToHistoryItemInternalAsync(CurrentHistoryIndex);
		}

		/// <summary>
		/// Navigate forward in custom history
		/// </summary>
		public async Task GoForwardInHistoryAsync()
		{
			if (!CanGoForwardInHistory)
			{
				System.Diagnostics.Debug.WriteLine($"Cannot go forward: CurrentIndex={CurrentHistoryIndex}, HistoryCount={NavigationHistory.Count}");
				return;
			}

			System.Diagnostics.Debug.WriteLine($"Going forward: CurrentIndex={CurrentHistoryIndex} -> {CurrentHistoryIndex + 1}");
			System.Diagnostics.Debug.WriteLine($"  Target: {NavigationHistory[CurrentHistoryIndex + 1].Title} ({NavigationHistory[CurrentHistoryIndex + 1].Url})");
			CurrentHistoryIndex++;
			await NavigateToHistoryItemInternalAsync(CurrentHistoryIndex);
		}

		/// <summary>
		/// Navigate to specific history item by index
		/// </summary>
		public async Task NavigateToHistoryItemAsync(int index)
		{
			if (index < 0 || index >= NavigationHistory.Count) return;

			CurrentHistoryIndex = index;
			await NavigateToHistoryItemInternalAsync(index);
		}

		private async Task NavigateToHistoryItemInternalAsync(int index)
		{
			var item = NavigationHistory[index];
			_isNavigatingFromHistory = true;

			// Cancel any previous navigation flag reset
			_historyNavigationCts?.Cancel();
			_historyNavigationCts = new System.Threading.CancellationTokenSource();

			NavigatingToHistoryItem?.Invoke(this, item);

			// Fire PageDataChanged immediately for instant UI update
			var historyPageData = new PageData
			{
				Url = item.Url,
				Title = item.Title,
				FaviconUrl = item.FaviconUrl,
				Thumbnail = item.Thumbnail
			};
			PageDataChanged?.Invoke(this, historyPageData);

			// Check if it's a PDF
			if (_webView.Handler is WebViewHandler handler && handler.IsPotentialPdfUrl(item.Url))
			{
				// Load PDF (will fire PageDataChanged again when complete)
				await ShowPdfAsync(item.Url);
			}
			else
			{
				// Navigate to web page (will fire PageDataChanged again when loaded)
				Source = new UrlWebViewSource { Url = item.Url };
			}

			// Reset flag after a delay to allow all PageDataChanged events to fire
			// Use a local reference to detect if a new navigation has started
			var localCts = _historyNavigationCts;
			_ = Task.Delay(2000).ContinueWith(t =>
			{
				// Only reset if this is still the current navigation (not cancelled)
				if (!localCts.IsCancellationRequested && _historyNavigationCts == localCts)
				{
					_isNavigatingFromHistory = false;
					System.Diagnostics.Debug.WriteLine("Reset _isNavigatingFromHistory flag");
				}
				else
				{
					System.Diagnostics.Debug.WriteLine("Navigation flag reset skipped (new navigation started)");
				}
			}, TaskScheduler.Default);
		}

		/// <summary>
		/// Clear navigation history
		/// </summary>
		public void ClearNavigationHistory()
		{
			NavigationHistory.Clear();
			CurrentHistoryIndex = -1;
		}

		#endregion

		#region History Management

		/// <summary>
		/// Export navigation history as JSON
		/// </summary>
		public string ExportHistoryJson()
		{
			return JsonSerializer.Serialize(NavigationHistory);
		}

		/// <summary>
		/// Import navigation history from JSON
		/// </summary>
		public void ImportHistoryJson(string json)
		{
			try
			{
				var items = JsonSerializer.Deserialize<List<NavigationHistoryItem>>(json);
				if (items != null)
				{
					NavigationHistory.Clear();
					foreach (var item in items)
					{
						NavigationHistory.Add(item);
					}
					CurrentHistoryIndex = NavigationHistory.Count - 1;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error importing history: {ex.Message}");
			}
		}

		/// <summary>
		/// Search navigation history by title or URL
		/// </summary>
		public List<NavigationHistoryItem> SearchHistory(string query)
		{
			if (string.IsNullOrWhiteSpace(query))
				return NavigationHistory.ToList();

			return NavigationHistory
				.Where(h => h.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
							h.Url.Contains(query, StringComparison.OrdinalIgnoreCase))
				.ToList();
		}

		#endregion

		#region Public Methods (delegates to WebView)

		public async Task<PageData> GetPageDataAsync()
		{
			return await _webView.GetPageDataAsync();
		}

		public async Task<PageData> ExtractRoutesAsync()
		{
			return await _webView.ExtractRoutesAsync();
		}

		public async Task<Microsoft.Maui.Controls.ImageSource> CaptureThumbnailAsync(int width = 320, int height = 180)
		{
			// If showing a PDF, capture thumbnail from the PDF data
			if (_pdfView.IsVisible && _currentPdfData != null)
			{
				return await CapturePdfThumbnailAsync(width, height);
			}

			return await _webView.CaptureThumbnailAsync(width, height);
		}

		private async Task<Microsoft.Maui.Controls.ImageSource> CapturePdfThumbnailAsync(int width, int height)
		{
			try
			{
				// Use iText7 to render first page of PDF as thumbnail
				using var ms = new System.IO.MemoryStream(_currentPdfData);
				using var pdfReader = new iText.Kernel.Pdf.PdfReader(ms);
				using var pdfDocument = new iText.Kernel.Pdf.PdfDocument(pdfReader);

				if (pdfDocument.GetNumberOfPages() == 0)
					return null;

				var page = pdfDocument.GetPage(1);
				var pageSize = page.GetPageSize();

				// Calculate aspect ratio
				float aspectRatio = pageSize.GetWidth() / pageSize.GetHeight();
				int targetWidth = width;
				int targetHeight = height;

				if (aspectRatio > (width / (float)height))
				{
					targetHeight = (int)(width / aspectRatio);
				}
				else
				{
					targetWidth = (int)(height * aspectRatio);
				}

				// Note: Actually rendering PDF to image requires platform-specific code
				// For now, return null - this would need platform-specific implementation
				// using the same approach as PdfViewHandler
				return null;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error capturing PDF thumbnail: {ex.Message}");
				return null;
			}
		}

		public async Task ScrollToTopAsync()
		{
			await _webView.ScrollToTopAsync();
		}

		public async Task<string> EvaluateJavaScriptAsync(string script)
		{
			return await _webView.EvaluateJavaScriptAsync(script);
		}

		#endregion
	}
}
