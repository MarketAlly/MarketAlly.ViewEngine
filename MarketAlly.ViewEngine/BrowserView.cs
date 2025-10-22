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
	/// Event args for navigation events (Navigating/Navigated)
	/// </summary>
	public class WebNavigationEventArgs : EventArgs
	{
		public string Url { get; set; }
		public WebNavigationEvent NavigationEvent { get; set; }
		public WebNavigationResult Result { get; set; }
	}

	/// <summary>
	/// Navigation event type
	/// </summary>
	public enum WebNavigationEvent
	{
		NewPage,
		Back,
		Forward,
		Refresh
	}

	/// <summary>
	/// Navigation result
	/// </summary>
	public enum WebNavigationResult
	{
		Success,
		Failure,
		Timeout,
		Cancel
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
		private readonly ProgressBar _downloadProgressBar;
		private readonly Grid _pdfActionsPanel;
		private byte[] _currentPdfData;
		private string _currentPdfUrl;
		private int _currentHistoryIndex = -1;
		private bool _isNavigatingFromHistory = false;
		private System.Threading.CancellationTokenSource _historyNavigationCts;
		private System.Threading.CancellationTokenSource _pdfDownloadCts;
	private readonly HashSet<string> _failedPdfUrls = new HashSet<string>();

		// PDF cache: URL -> (PDF data, temp file path, extracted text)
		private readonly Dictionary<string, (byte[] data, string tempFilePath, string extractedText)> _pdfCache = new Dictionary<string, (byte[], string, string)>();

		// Expose navigation events
		public event EventHandler<PageData> PageDataChanged;
		public event EventHandler<EventArgs> PageLoadComplete;
		public event EventHandler<NavigationHistoryItem> NavigatingToHistoryItem;
		public event EventHandler<WebNavigationEventArgs> Navigating;
		public event EventHandler<WebNavigationEventArgs> Navigated;

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
			BindableProperty.Create(nameof(ExcludeDomains), typeof(List<string>), typeof(BrowserView), default(List<string>));

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

		public static readonly BindableProperty ShowPdfActionsProperty =
			BindableProperty.Create(nameof(ShowPdfActions), typeof(bool), typeof(BrowserView), false);

		public static readonly BindableProperty DownloadProgressProperty =
			BindableProperty.Create(nameof(DownloadProgress), typeof(double), typeof(BrowserView), 0.0);

		public static readonly BindableProperty ShowLoadingProgressProperty =
			BindableProperty.Create(nameof(ShowLoadingProgress), typeof(bool), typeof(BrowserView), true);

		public static readonly BindableProperty PdfUrlPatternsProperty =
			BindableProperty.Create(nameof(PdfUrlPatterns), typeof(string[]), typeof(BrowserView), default(string[]));

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

		public bool ShowPdfActions
		{
			get => (bool)GetValue(ShowPdfActionsProperty);
			set => SetValue(ShowPdfActionsProperty, value);
		}

		public double DownloadProgress
		{
			get => (double)GetValue(DownloadProgressProperty);
			set => SetValue(DownloadProgressProperty, value);
		}

		public bool ShowLoadingProgress
		{
			get => (bool)GetValue(ShowLoadingProgressProperty);
			set => SetValue(ShowLoadingProgressProperty, value);
		}

		/// <summary>
		/// Regular expression patterns used to detect PDF URLs.
		/// If null, uses default patterns. Customize for specific needs.
		/// </summary>
		public string[] PdfUrlPatterns
		{
			get => (string[])GetValue(PdfUrlPatternsProperty);
			set => SetValue(PdfUrlPatternsProperty, value);
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
			// Set default PDF URL patterns
			PdfUrlPatterns = new[]
			{
				@"\.pdf$",                           // Ends with .pdf
				@"arxiv\.org/pdf/\d{4}\.\d{4,5}",   // arXiv PDF pattern
				@"/pdf/",                            // Contains /pdf/ in path
				@"content-type=pdf",                 // Query parameter indicating PDF
				@"type=pdf",                         // Alternative query parameter
				@"format=pdf"                        // Another common parameter
			};

			// Setup grid with rows: main content and actions panel
			// Progress bar will overlay at the top
			_container = new Grid
			{
				RowDefinitions =
				{
					new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // Main content
					new RowDefinition { Height = GridLength.Auto }  // Actions panel
				}
			};

			_webView = new WebView();
			_webView.ParentBrowserView = this;
			_pdfView = new PdfView();

			// Create thin progress bar at top (modern browser style)
			_downloadProgressBar = new ProgressBar
			{
				IsVisible = false,
				HeightRequest = 3,
				ProgressColor = Microsoft.Maui.Graphics.Colors.Blue,
				VerticalOptions = LayoutOptions.Start,
				HorizontalOptions = LayoutOptions.Fill
			};

			// Create PDF actions panel with download button
			_pdfActionsPanel = new Grid
			{
				IsVisible = false,
				Padding = new Thickness(10),
				BackgroundColor = Microsoft.Maui.Graphics.Colors.LightGray,
				ColumnDefinitions =
				{
					new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
					new ColumnDefinition { Width = GridLength.Auto }
				}
			};

			var downloadButton = new Button
			{
				Text = "Download",
				Command = new Command(async () => await DownloadCurrentPdfAsync()),
				Margin = new Thickness(5, 0)
			};
			Grid.SetColumn(downloadButton, 1);

			_pdfActionsPanel.Children.Add(downloadButton);

			// Add views to grid
			// WebView and PdfView occupy row 0
			Grid.SetRow(_webView, 0);
			Grid.SetRow(_pdfView, 0);
			Grid.SetRow(_pdfActionsPanel, 1);

			_container.Children.Add(_webView);
			_container.Children.Add(_pdfView);
			_container.Children.Add(_pdfActionsPanel);

			// Progress bar overlays at top of row 0 (spans all rows for z-index)
			Grid.SetRow(_downloadProgressBar, 0);
			Grid.SetRowSpan(_downloadProgressBar, 2);
			_container.Children.Add(_downloadProgressBar);

			// Initially show WebView
			_webView.IsVisible = true;
			_pdfView.IsVisible = false;

			Content = _container;

			// Ensure touch events are not blocked when nested in ContentViews
			// This is critical for proper scrolling on Android
			InputTransparent = false;
			_container.InputTransparent = false;
			_pdfView.InputTransparent = false;
			_webView.InputTransparent = false;

			// Wire up events
			_webView.PageDataChanged += OnWebViewPageDataChanged;
			_webView.PageLoadComplete += (s, e) => PageLoadComplete?.Invoke(this, e);
			_webView.PropertyChanged += OnWebViewPropertyChanged;
			_webView.Navigating += OnWebViewNavigating;
			_webView.Navigated += OnWebViewNavigated;

			// Sync properties from BrowserView to WebView
			this.PropertyChanged += OnBrowserViewPropertyChanged;
		}

		private void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
		{
			// Forward WebView's Navigating event to BrowserView's Navigating event
			var navigationEvent = e.NavigationEvent switch
			{
				Microsoft.Maui.WebNavigationEvent.NewPage => WebNavigationEvent.NewPage,
				Microsoft.Maui.WebNavigationEvent.Back => WebNavigationEvent.Back,
				Microsoft.Maui.WebNavigationEvent.Forward => WebNavigationEvent.Forward,
				Microsoft.Maui.WebNavigationEvent.Refresh => WebNavigationEvent.Refresh,
				_ => WebNavigationEvent.NewPage
			};

			var args = new WebNavigationEventArgs
			{
				Url = e.Url,
				NavigationEvent = navigationEvent,
				Result = WebNavigationResult.Success // Navigating event hasn't completed yet, so assume success
			};

			Navigating?.Invoke(this, args);
		}

		private void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
		{
			// Forward WebView's Navigated event to BrowserView's Navigated event
			var navigationEvent = e.NavigationEvent switch
			{
				Microsoft.Maui.WebNavigationEvent.NewPage => WebNavigationEvent.NewPage,
				Microsoft.Maui.WebNavigationEvent.Back => WebNavigationEvent.Back,
				Microsoft.Maui.WebNavigationEvent.Forward => WebNavigationEvent.Forward,
				Microsoft.Maui.WebNavigationEvent.Refresh => WebNavigationEvent.Refresh,
				_ => WebNavigationEvent.NewPage
			};

			var navigationResult = e.Result switch
			{
				Microsoft.Maui.WebNavigationResult.Success => WebNavigationResult.Success,
				Microsoft.Maui.WebNavigationResult.Failure => WebNavigationResult.Failure,
				Microsoft.Maui.WebNavigationResult.Timeout => WebNavigationResult.Timeout,
				Microsoft.Maui.WebNavigationResult.Cancel => WebNavigationResult.Cancel,
				_ => WebNavigationResult.Failure
			};

			var args = new WebNavigationEventArgs
			{
				Url = e.Url,
				NavigationEvent = navigationEvent,
				Result = navigationResult
			};

			Navigated?.Invoke(this, args);
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
				case nameof(DownloadProgress):
					UpdateProgressBarVisibility();
					break;
				case nameof(ShowPdfActions):
					UpdatePdfActionsPanelVisibility();
					break;
				case nameof(ShowLoadingProgress):
					UpdateProgressBarVisibility();
					break;
				case nameof(IsLoading):
					UpdateProgressBarVisibility();
					break;
				case nameof(PdfUrlPatterns):
					_webView.PdfUrlPatterns = PdfUrlPatterns;
					break;
			}
		}

		private void UpdateProgressBarVisibility()
		{
			if (!ShowLoadingProgress)
			{
				_downloadProgressBar.IsVisible = false;
				return;
			}

			// Show progress bar when:
			// 1. Downloading a PDF (DownloadProgress between 0 and 1)
			// 2. OR loading a regular page (IsLoading = true)
			bool isDownloadingPdf = DownloadProgress > 0 && DownloadProgress < 1.0;
			bool isLoadingPage = IsLoading && DownloadProgress == 0;

			_downloadProgressBar.IsVisible = isDownloadingPdf || isLoadingPage;

			// Update progress: use DownloadProgress for PDFs, show indeterminate for page loads
			if (isDownloadingPdf)
			{
				_downloadProgressBar.Progress = DownloadProgress;
			}
			else if (isLoadingPage)
			{
				// For regular page loads, show indeterminate progress (pulse animation)
				// MAUI ProgressBar doesn't have built-in indeterminate mode, so we'll show 0.5
				_downloadProgressBar.Progress = 0.5;
			}
		}

		private void UpdatePdfActionsPanelVisibility()
		{
			// Show panel only when ShowPdfActions is true AND we have a PDF loaded
			_pdfActionsPanel.IsVisible = ShowPdfActions && _pdfView.IsVisible && !string.IsNullOrEmpty(_currentPdfUrl);
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
		_currentPdfUrl = null;
		UpdatePdfActionsPanelVisibility();

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
		// Fire Navigating event
		Navigating?.Invoke(this, new WebNavigationEventArgs
		{
			Url = url,
			NavigationEvent = WebNavigationEvent.NewPage,
			Result = WebNavigationResult.Success
		});

		// Cancel any existing PDF download
		_pdfDownloadCts?.Cancel();
		_pdfDownloadCts = new System.Threading.CancellationTokenSource();
		var cancellationToken = _pdfDownloadCts.Token;

		try
		{
			IsLoading = true;
			DownloadProgress = 0.0;
			_currentPdfUrl = url;

			// Check cache first
			string extractedText = string.Empty;
			string tempFile = null;

			if (_pdfCache.ContainsKey(url))
			{
				System.Diagnostics.Debug.WriteLine($"Using cached PDF for {url}");
				var cached = _pdfCache[url];
				_currentPdfData = cached.data;
				tempFile = cached.tempFilePath;
				extractedText = cached.extractedText;
				DownloadProgress = 1.0;
			}
			else
			{
				// Download PDF with proper headers and progress reporting
				using var httpClient = new HttpClient(new HttpClientHandler
				{
					AllowAutoRedirect = true,
					MaxAutomaticRedirections = 10
				});
				httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
				httpClient.DefaultRequestHeaders.Add("Accept", "application/pdf,application/octet-stream,*/*");

				// Get response to read content length
				using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
				response.EnsureSuccessStatusCode();

				var totalBytes = response.Content.Headers.ContentLength ?? -1;
				var canReportProgress = totalBytes != -1;

				using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
				using var memoryStream = new System.IO.MemoryStream();

				var buffer = new byte[8192];
				long totalRead = 0;
				int bytesRead;

				while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
				{
					await memoryStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
					totalRead += bytesRead;

					if (canReportProgress)
					{
						DownloadProgress = (double)totalRead / totalBytes;
					}
				}

				_currentPdfData = memoryStream.ToArray();
				DownloadProgress = 1.0;

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
					DownloadProgress = 0.0;
					_currentPdfUrl = null;
					UpdatePdfActionsPanelVisibility();
					return;
				}

				System.Diagnostics.Debug.WriteLine($"PDF header validated successfully");

				// Save to temp file for PdfView
				tempFile = DataSources.PdfTempFileHelper.CreateTempPdfFilePath();
				await File.WriteAllBytesAsync(tempFile, _currentPdfData);

				// Extract text for PageData (best effort - ignore errors)
				if (_webView.Handler is WebViewHandler handler)
				{
					try
					{
						extractedText = await handler.HandlePdfDownload(_currentPdfData, url);
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Failed to extract PDF text: {ex.Message}");
					}
				}

				// Cache the PDF for future navigation
				_pdfCache[url] = (_currentPdfData, tempFile, extractedText);
				System.Diagnostics.Debug.WriteLine($"Cached PDF: {url}");
			}

			// Show PDF (whether from cache or freshly downloaded)
			_pdfView.Uri = tempFile;
			_webView.IsVisible = false;
			_pdfView.IsVisible = true;
			UpdatePdfActionsPanelVisibility();

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

			// Fire Navigated event (success)
			Navigated?.Invoke(this, new WebNavigationEventArgs
			{
				Url = url,
				NavigationEvent = WebNavigationEvent.NewPage,
				Result = WebNavigationResult.Success
			});
		}
		catch (OperationCanceledException)
		{
			// Download was cancelled by user
			IsLoading = false;
			DownloadProgress = 0.0;
			_currentPdfUrl = null;
			UpdatePdfActionsPanelVisibility();
			System.Diagnostics.Debug.WriteLine($"PDF download cancelled: {url}");

			// Fire Navigated event (cancelled)
			Navigated?.Invoke(this, new WebNavigationEventArgs
			{
				Url = url,
				NavigationEvent = WebNavigationEvent.NewPage,
				Result = WebNavigationResult.Cancel
			});
		}
		catch (Exception ex)
		{
			IsLoading = false;
			DownloadProgress = 0.0;
			_currentPdfUrl = null;
			UpdatePdfActionsPanelVisibility();
			System.Diagnostics.Debug.WriteLine($"Error loading PDF: {ex.Message}");

			// Fire Navigated event (failure)
			Navigated?.Invoke(this, new WebNavigationEventArgs
			{
				Url = url,
				NavigationEvent = WebNavigationEvent.NewPage,
				Result = WebNavigationResult.Failure
			});
		}
	}
		internal void AddToNavigationHistory(PageData pageData)
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

		internal void RaisePageDataChanged(PageData pageData)
		{
			PageDataChanged?.Invoke(this, pageData);
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
			var targetIndex = CurrentHistoryIndex - 1;
			var targetUrl = NavigationHistory[targetIndex].Url;

			// Fire Navigating event for Back navigation
			Navigating?.Invoke(this, new WebNavigationEventArgs
			{
				Url = targetUrl,
				NavigationEvent = WebNavigationEvent.Back,
				Result = WebNavigationResult.Success
			});

			CurrentHistoryIndex--;
			await NavigateToHistoryItemInternalAsync(CurrentHistoryIndex);

			// Fire Navigated event for Back navigation
			Navigated?.Invoke(this, new WebNavigationEventArgs
			{
				Url = targetUrl,
				NavigationEvent = WebNavigationEvent.Back,
				Result = WebNavigationResult.Success
			});
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
			var targetIndex = CurrentHistoryIndex + 1;
			var targetUrl = NavigationHistory[targetIndex].Url;

			// Fire Navigating event for Forward navigation
			Navigating?.Invoke(this, new WebNavigationEventArgs
			{
				Url = targetUrl,
				NavigationEvent = WebNavigationEvent.Forward,
				Result = WebNavigationResult.Success
			});

			CurrentHistoryIndex++;
			await NavigateToHistoryItemInternalAsync(CurrentHistoryIndex);

			// Fire Navigated event for Forward navigation
			Navigated?.Invoke(this, new WebNavigationEventArgs
			{
				Url = targetUrl,
				NavigationEvent = WebNavigationEvent.Forward,
				Result = WebNavigationResult.Success
			});
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

				// Use platform-specific PDF rendering
				// Each platform (Android, iOS, Windows) has its own partial implementation
				if (_webView.Handler is WebViewHandler handler)
				{
					return await handler.RenderPdfThumbnailAsync(_currentPdfData, width, height);
				}

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

		/// <summary>
		/// Reload/refresh the current page or PDF
		/// </summary>
		/// <param name="bypassCache">If true, forces re-download even if cached</param>
		public async Task ReloadAsync(bool bypassCache = false)
		{
			string currentUrl = null;

			if (_pdfView.IsVisible && !string.IsNullOrEmpty(_currentPdfUrl))
			{
				currentUrl = _currentPdfUrl;

				// Fire Navigating event for Refresh
				Navigating?.Invoke(this, new WebNavigationEventArgs
				{
					Url = currentUrl,
					NavigationEvent = WebNavigationEvent.Refresh,
					Result = WebNavigationResult.Success
				});

				// Reloading a PDF
				if (bypassCache && _pdfCache.ContainsKey(_currentPdfUrl))
				{
					System.Diagnostics.Debug.WriteLine($"Reload: Clearing cache for {_currentPdfUrl}");
					_pdfCache.Remove(_currentPdfUrl);
				}

				// On Android, we need to navigate through the WebView to get cookies
				// This will trigger OnDownloadStart which has access to session cookies
				if (DeviceInfo.Platform == DevicePlatform.Android)
				{
					System.Diagnostics.Debug.WriteLine($"Reload: Navigating via WebView to get cookies for {_currentPdfUrl}");
					_webView.Source = new UrlWebViewSource { Url = _currentPdfUrl };
				}
				else
				{
					// iOS/Windows: use direct download
					await ShowPdfAsync(_currentPdfUrl);
				}

				// Fire Navigated event for Refresh
				Navigated?.Invoke(this, new WebNavigationEventArgs
				{
					Url = currentUrl,
					NavigationEvent = WebNavigationEvent.Refresh,
					Result = WebNavigationResult.Success
				});
			}
			else if (_webView.Source is UrlWebViewSource urlSource && !string.IsNullOrEmpty(urlSource.Url))
			{
				currentUrl = urlSource.Url;

				// Fire Navigating event for Refresh
				Navigating?.Invoke(this, new WebNavigationEventArgs
				{
					Url = currentUrl,
					NavigationEvent = WebNavigationEvent.Refresh,
					Result = WebNavigationResult.Success
				});

				// Reloading a web page - the WebView's Reload() will also trigger its own Navigating/Navigated events
				if (bypassCache)
				{
					// Force WebView to reload from network
					_webView.Reload();
				}
				else
				{
					// Regular reload (may use browser cache)
					_webView.Reload();
				}

				// Fire Navigated event for Refresh
				Navigated?.Invoke(this, new WebNavigationEventArgs
				{
					Url = currentUrl,
					NavigationEvent = WebNavigationEvent.Refresh,
					Result = WebNavigationResult.Success
				});
			}
		}

		/// <summary>
		/// Download/share the currently displayed PDF using the system share dialog
		/// </summary>
		public async Task DownloadCurrentPdfAsync()
		{
			if (_currentPdfData == null || string.IsNullOrEmpty(_currentPdfUrl))
			{
				System.Diagnostics.Debug.WriteLine("No PDF currently loaded to download");
				return;
			}

			try
			{
				// Get filename from URL
				var fileName = System.IO.Path.GetFileName(_currentPdfUrl);
				if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
				{
					// Extract from URL or use default
					var uri = new Uri(_currentPdfUrl);
					fileName = uri.Segments.Length > 0 ? uri.Segments[^1] : "document.pdf";
					if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
					{
						fileName += ".pdf";
					}
				}

				// Save to temp file for sharing
				var tempFilePath = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
				await File.WriteAllBytesAsync(tempFilePath, _currentPdfData);

				// Use MAUI Essentials Share API
				await Share.Default.RequestAsync(new ShareFileRequest
				{
					Title = "Download PDF",
					File = new ShareFile(tempFilePath)
				});

				System.Diagnostics.Debug.WriteLine($"PDF shared successfully: {fileName}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error downloading PDF: {ex.Message}");
			}
		}

		/// <summary>
		/// Cancel the current PDF download if one is in progress
		/// </summary>
		public void CancelPdfDownload()
		{
			if (_pdfDownloadCts != null && !_pdfDownloadCts.IsCancellationRequested)
			{
				System.Diagnostics.Debug.WriteLine("Cancelling PDF download");
				_pdfDownloadCts.Cancel();
			}
		}

		#endregion
	}
}
