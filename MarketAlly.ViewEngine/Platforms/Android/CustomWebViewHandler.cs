using Android.Content;
using Android.Webkit;
using Microsoft.Maui.Handlers;
using System.Text.Json;
using static Android.Media.MediaRouter;

namespace MarketAlly.Maui.ViewEngine
{
	public partial class WebViewHandler
	{
		private WebViewClient _webViewClient;
		private CustomDownloadListener _downloadListener;
		private WebViewJavaScriptInterface _jsInterface;

		private const string ContentMonitoringScript = @"
        (function() {
            if (window.__contentMonitorInjected) {
                return;
            }
            window.__contentMonitorInjected = true;
            console.log('✅ ViewEngine v2.0 - Navigation Fix Active');

            let debounceTimer = null;

            function notifyContentChange() {
                if (debounceTimer) {
                    clearTimeout(debounceTimer);
                }

                debounceTimer = setTimeout(() => {
                    if (window.Android && window.Android.onContentChanged) {
                        window.Android.onContentChanged();
                    }
                    debounceTimer = null;
                }, 1000);
            }

            // Normalize URLs for proper comparison (strip hash fragments)
            function normalizeUrl(url) {
                try {
                    const urlObj = new URL(url);
                    // Return URL without hash
                    return urlObj.origin + urlObj.pathname + urlObj.search;
                } catch (e) {
                    return url;
                }
            }

            // Check if link is navigable
            function isNavigableLink(href) {
                if (!href || href.trim() === '') return false;

                const trimmed = href.trim();

                // Filter out non-navigable patterns
                if (trimmed === '#' ||
                    trimmed.startsWith('javascript:') ||
                    trimmed.startsWith('about:') ||
                    trimmed.startsWith('mailto:') ||
                    trimmed.startsWith('tel:')) {
                    return false;
                }

                // Compare normalized URLs (without hash)
                const normalizedHref = normalizeUrl(href);
                const normalizedCurrent = normalizeUrl(window.location.href);

                // Check if this is actually a different page (not just a hash change on same page)
                return normalizedHref !== normalizedCurrent;
            }

            // Fix navigation for SPAs like Google News
            document.addEventListener('click', function(e) {
                let target = e.target;

                // Walk up the DOM to find an anchor element
                while (target && target !== document.body) {
                    if (target.tagName === 'A') break;
                    target = target.parentElement;
                }

                if (target && target.tagName === 'A' && target.href) {
                    const href = target.href;

                    // Check if it's a real navigation link
                    if (isNavigableLink(href)) {
                        // Check for SPAs that prevent navigation
                        const isGoogleNews = window.location.hostname.includes('news.google.com');

                        let isExternalLink = false;
                        try {
                            isExternalLink = new URL(href).hostname !== window.location.hostname;
                        } catch (e) {
                            // If URL parsing fails, assume it's not external
                            isExternalLink = false;
                        }

                        // Force navigation for known SPAs or external links
                        if (isGoogleNews || isExternalLink) {
                            console.log('WebView: Force navigating to:', href);
                            e.preventDefault();
                            e.stopPropagation();
                            e.stopImmediatePropagation();

                            // Use location.assign for proper navigation history
                            setTimeout(() => {
                                try {
                                    window.location.assign(href);
                                } catch (err) {
                                    console.error('Navigation failed:', err);
                                    // Fallback
                                    window.location.href = href;
                                }
                            }, 50);

                            return false;
                        }
                    }
                }

                // Still notify content change for other clicks
                notifyContentChange();
            }, true); // Use capture phase to intercept before SPA handlers

            const observer = new MutationObserver((mutations) => {
                const hasSignificantChanges = mutations.some(mutation =>
                    mutation.addedNodes.length > 0 ||
                    mutation.removedNodes.length > 0 ||
                    (mutation.target.id && (
                        mutation.target.id.includes('content') ||
                        mutation.target.id.includes('main') ||
                        mutation.target.id.includes('root')
                    ))
                );

                if (hasSignificantChanges) {
                    notifyContentChange();
                }
            });

            if (document.body) {
                observer.observe(document.body, {
                    childList: true,
                    subtree: true,
                    attributeFilter: ['class', 'style', 'data-loaded']
                });
            }
        })();";

		private void InjectContentMonitoringScript()
		{
			try
			{
				if (PlatformView != null)
				{
					PlatformView.EvaluateJavascript(ContentMonitoringScript, null);

					// Check if we need to inject link forcing script
					var webView = VirtualView as WebView;
					if (webView?.ForceLinkNavigation == true)
					{
						InjectForceLinkNavigationScript();
					}
				}
			}
			catch (Exception ex)
			{
			}
		}

		private void InjectForceLinkNavigationScript()
		{
			const string forceLinkScript = @"
			(function() {
				if (window.__forceLinkNavInjected) return;
				window.__forceLinkNavInjected = true;

				// Track pending navigations to prevent duplicates
				let pendingNavigation = null;
				const NAVIGATION_TIMEOUT = 5000; // 5 second timeout

				// Normalize URLs for proper comparison (strip hash fragments)
				function normalizeUrl(url) {
					try {
						const urlObj = new URL(url);
						// Return URL without hash
						return urlObj.origin + urlObj.pathname + urlObj.search;
					} catch (e) {
						return url;
					}
				}

				// Check if link is navigable
				function isNavigableLink(href) {
					if (!href || href.trim() === '') return false;

					const trimmed = href.trim();

					// Filter out non-navigable patterns
					if (trimmed === '#' ||
						trimmed.startsWith('javascript:') ||
						trimmed.startsWith('about:') ||
						trimmed.startsWith('mailto:') ||
						trimmed.startsWith('tel:')) {
						return false;
					}

					// Compare normalized URLs (without hash)
					const normalizedHref = normalizeUrl(href);
					const normalizedCurrent = normalizeUrl(window.location.href);

					// Check if this is actually a different page (not just a hash change on same page)
					return normalizedHref !== normalizedCurrent;
				}

				// Force navigation with timeout and retry logic
				function forceNavigate(href) {
					// Cancel any pending navigation
					if (pendingNavigation) {
						clearTimeout(pendingNavigation);
						pendingNavigation = null;
					}

					console.log('Force navigating to:', href);

					// Set a timeout to detect if navigation failed
					pendingNavigation = setTimeout(() => {
						console.warn('Navigation timeout - attempting fallback navigation to:', href);
						// Fallback: try window.open as last resort
						try {
							window.location.replace(href);
						} catch (e) {
							console.error('Navigation failed:', e);
						}
						pendingNavigation = null;
					}, NAVIGATION_TIMEOUT);

					// Primary navigation method
					try {
						window.location.href = href;
					} catch (e) {
						console.error('Primary navigation failed:', e);
						// Immediate fallback
						try {
							window.location.assign(href);
						} catch (e2) {
							console.error('Fallback navigation failed:', e2);
						}
					}
				}

				// Clear pending navigation on successful page unload
				window.addEventListener('beforeunload', function() {
					if (pendingNavigation) {
						clearTimeout(pendingNavigation);
						pendingNavigation = null;
					}
				});

				document.addEventListener('click', function(e) {
					let target = e.target;

					// Walk up the DOM to find an anchor element (max 10 levels)
					let levels = 0;
					while (target && target !== document.body && levels < 10) {
						if (target.tagName === 'A') break;
						target = target.parentElement;
						levels++;
					}

					if (target && target.tagName === 'A' && target.href) {
						const href = target.href;

						// Check if it's a real navigation link
						if (isNavigableLink(href)) {
							console.log('Intercepted link click:', href);

							// ONLY prevent default and force navigate
							// This allows the click to bubble normally to the WebView
							// which preserves back/forward history
							e.preventDefault();
							e.stopPropagation();

							// Let the native WebView handle the navigation
							// This preserves browser history properly
							window.location.assign(href);

							return false;
						}
					}
				}, true); // Use capture phase to intercept before other handlers
			})();";

			try
			{
				PlatformView?.EvaluateJavascript(forceLinkScript, null);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error injecting force link script: {ex.Message}");
			}
		}

		protected override void ConnectHandler(Android.Webkit.WebView platformView)
		{
			try
			{
				base.ConnectHandler(platformView);

				var settings = platformView.Settings;
				settings.JavaScriptEnabled = true;
				settings.DomStorageEnabled = true;

			// Set user agent based on UserAgentMode property
			var webView = VirtualView as WebView;
			var customUserAgent = webView?.UserAgent;
			var userAgentMode = webView?.UserAgentMode ?? UserAgentMode.Default;

			if (!string.IsNullOrEmpty(customUserAgent))
			{
				// Custom user agent provided - use it
				settings.UserAgentString = customUserAgent;
			}
			else
			{
				// Determine user agent based on mode
				// Default for Android is Mobile
				bool useMobile = userAgentMode == UserAgentMode.Mobile ||
				                 (userAgentMode == UserAgentMode.Default);

				if (useMobile)
				{
					// Mobile: Modern Chrome on Android 13
					settings.UserAgentString = "Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.144 Mobile Safari/537.36";
				}
				else
				{
					// Desktop: Windows Chrome
					settings.UserAgentString = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
				}
			}

				CookieManager.Instance.SetAcceptCookie(true);
				CookieManager.Instance.SetAcceptThirdPartyCookies(platformView, true);

				// Create a custom WebViewClient
				_webViewClient = new WebViewClient(this);
				platformView.SetWebViewClient(_webViewClient);

				// Handle downloads
				_downloadListener = new CustomDownloadListener(this);
				platformView.SetDownloadListener(_downloadListener);

				// Add JavaScript interface for content change notifications
				_jsInterface = new WebViewJavaScriptInterface(this);
				platformView.AddJavascriptInterface(_jsInterface, "Android");

			}
			catch (Exception ex)
			{
				throw;
			}
		}

		public partial void EnsureCustomWebViewClient()
		{
			try
			{
				if (PlatformView != null && _webViewClient != null)
				{
					PlatformView.SetWebViewClient(_webViewClient);
				}
				else
				{
				}
			}
			catch (Exception ex)
			{
			}
		}

		protected override void DisconnectHandler(Android.Webkit.WebView platformView)
		{
			if (platformView != null)
			{
				platformView.SetWebViewClient(null);
				platformView.SetDownloadListener(null);
				platformView.RemoveJavascriptInterface("Android");

				_webViewClient?.Dispose();
				_webViewClient = null;

				_downloadListener?.Dispose();
				_downloadListener = null;

				_jsInterface?.Dispose();
				_jsInterface = null;
			}

			base.DisconnectHandler(platformView);
		}

		public class WebViewClient : Android.Webkit.WebViewClient
		{
			private readonly WebViewHandler _handler;

			public WebViewClient(WebViewHandler handler)
			{
				_handler = handler;
			}

			public override void OnPageFinished(Android.Webkit.WebView view, string url)
			{
				base.OnPageFinished(view, url);

				// Must not be async void - unhandled exceptions crash the app in Release mode
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					try
					{
						// Re-inject monitoring script after each page load
						_handler.InjectContentMonitoringScript();

						// Trigger page data extraction
						await _handler.OnPageDataChangedAsync();
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Error in OnPageFinished: {ex.Message}");
					}
				});
			}

			public override void DoUpdateVisitedHistory(Android.Webkit.WebView view, string url, bool isReload)
			{
				base.DoUpdateVisitedHistory(view, url, isReload);

				// Trigger page data extraction
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					try
					{
					// Re-inject monitoring script after each page load
					_handler.InjectContentMonitoringScript();
					await _handler.OnPageDataChangedAsync();
				}
					catch (Exception ex)
					{
					System.Diagnostics.Debug.WriteLine($"Error in DoUpdateVisitedHistory: {ex.Message}");
					}
				});
			}

			public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView view, IWebResourceRequest request)
			{
				var url = request.Url?.ToString();


				return false; // Allow default navigation
			}
		}

		public class WebViewJavaScriptInterface : Java.Lang.Object
		{
			private readonly WebViewHandler _handler;

			public WebViewJavaScriptInterface(WebViewHandler handler)
			{
				_handler = handler;
			}

			[Android.Webkit.JavascriptInterface]
			public void OnContentChanged()
			{
				// Must not be async void - unhandled exceptions in async void crash the app in Release mode
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					try
					{
						await _handler.OnPageDataChangedAsync();
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Error in OnContentChanged: {ex.Message}");
						System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
					}
				});
			}
		}

		public partial async Task InjectJavaScriptAsync(string script)
		{
			PlatformView.EvaluateJavascript(script, null);
			await Task.CompletedTask;
		}

		public void SetUserAgent(string userAgent)
		{
			PlatformView.Settings.UserAgentString = !string.IsNullOrEmpty(userAgent) ? userAgent :
				"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
		}

		public partial void SetEnableZoom(bool enable)
		{
			if (PlatformView?.Settings != null)
			{
				PlatformView.Settings.BuiltInZoomControls = enable;
				PlatformView.Settings.DisplayZoomControls = false; // Hide zoom buttons, keep pinch-to-zoom
				PlatformView.Settings.SetSupportZoom(enable);
			}
		}

		public async partial Task<PageData> ExtractPageDataAsync(bool forceRouteExtraction = false)
		{
			try
			{
				var webView = PlatformView as Android.Webkit.WebView;
				if (webView == null)
					return new PageData { Title = "Error", Body = "WebView not initialized." };

				var script = PageDataExtractor.ExtractScript;

				var tcs = new TaskCompletionSource<string>();
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
				cts.Token.Register(() => tcs.TrySetCanceled());

				webView.EvaluateJavascript(script, new ValueCallback(tcs));
				var result = await tcs.Task;

				return ProcessHtmlResult(result, forceRouteExtraction);
			}
			catch (OperationCanceledException)
			{
				return new PageData { Title = "Timeout", Body = "Page data extraction timed out." };
			}
			catch (Exception ex)
			{
				return new PageData { Title = "Error", Body = $"Failed to extract page data: {ex.Message}" };
			}
		}

		private PageData ProcessHtmlResult(string result, bool forceRouteExtraction = false)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(result))
					return new PageData { Title = "No Content", Body = "Empty HTML response." };
				// Trim invalid characters
				result = result.Trim('"')
						   .Replace("\\\"", "\"")
						   .Replace("\\n", " ")
						   .Replace("\\r", " ")
						   .Replace("\\t", " ")
						   .Replace("\\\\", "\\");

				var rawData = JsonSerializer.Deserialize<PageRawData>(result, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				if (rawData == null)
					return new PageData { Title = "Error", Body = "Failed to deserialize page data." };

				// Cache raw data for on-demand route extraction
				_cachedRawData = rawData;

				var bodyText = !string.IsNullOrEmpty(rawData.Body) ? DecodeBase64(rawData.Body) : string.Empty;

				// Check if route extraction is enabled or forced
				var webView = VirtualView as WebView;
				bool shouldExtractRoutes = forceRouteExtraction || (webView?.EnableRouteExtraction ?? false);

				List<RouteInfo> routes = new List<RouteInfo>();
				List<RouteInfo> bodyRoutes = new List<RouteInfo>();

				if (shouldExtractRoutes)
				{
					int maxRoutes = webView?.MaxRoutes ?? 100;
					bool normalizeRoutes = webView?.NormalizeRoutes ?? true;
					var excludeDomains = webView?.ExcludeDomains;
					bool enableAdDetection = webView?.EnableAdDetection ?? false;

					// Use lazy processing: create basic routes immediately
					routes = PageDataExtractor.CreateBasicRoutes(rawData.Links, maxRoutes, normalizeRoutes, excludeDomains);
					bodyRoutes = PageDataExtractor.CreateBasicRoutes(rawData.BodyLinks, maxRoutes, normalizeRoutes, excludeDomains);

					// Process ad detection asynchronously in background
					if (enableAdDetection)
					{
						_ = Task.Run(async () =>
						{
							await PageDataExtractor.ProcessAdsAsync(routes, rawData.Url);
							await PageDataExtractor.ProcessAdsAsync(bodyRoutes, rawData.Url);
						});
					}
				}

				var pageData = new PageData
				{
					Title = rawData.Title ?? "Untitled",
					Body = bodyText,
					Url = rawData.Url ?? string.Empty,
					Routes = routes,
					BodyRoutes = bodyRoutes
				};

				return pageData;
			}
			catch (JsonException ex)
			{
				return new PageData { Title = "Error", Body = $"Failed to parse JSON: {ex.Message}" };
			}
			catch (Exception ex)
			{
				return new PageData { Title = "Error", Body = $"Failed to process HTML: {ex.Message}" };
			}
		}

		public partial async Task<Microsoft.Maui.Controls.ImageSource> CaptureThumbnailAsync(int width = 320, int height = 180)
		{
			try
			{
				if (PlatformView == null)
				{
					return null;
				}

				// Ensure we're on the main thread
				return await MainThread.InvokeOnMainThreadAsync(async () =>
				{
					try
					{
						// Get the visible viewport dimensions (what user actually sees)
						int viewportWidth = PlatformView.Width;
						int viewportHeight = PlatformView.Height;

						if (viewportWidth <= 0 || viewportHeight <= 0)
						{
							return null;
						}

						Android.Graphics.Bitmap bitmap;

						// Use PixelCopy API for Android 8.0+ (API 26+) - the modern, correct way
						if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
						{
							bitmap = await CaptureWithPixelCopyAsync(viewportWidth, viewportHeight);
						}
						else
						{
							// Fallback for older Android versions (below API 26)
							bitmap = CaptureWithDrawingCache(viewportWidth, viewportHeight);
						}

						if (bitmap == null)
						{
							return null;
						}

						// Calculate aspect-preserving dimensions
						float aspectRatio = viewportWidth / (float)viewportHeight;
						int targetWidth = width;
						int targetHeight = height;

						if (aspectRatio > (width / (float)height))
						{
							// Image is wider - fit to width
							targetHeight = (int)(width / aspectRatio);
						}
						else
						{
							// Image is taller - fit to height
							targetWidth = (int)(height * aspectRatio);
						}

						// Resize the bitmap
						var resizedBitmap = Android.Graphics.Bitmap.CreateScaledBitmap(bitmap, targetWidth, targetHeight, true);

						// Save to temporary file
						var tempPath = Path.Combine(Path.GetTempPath(), $"webview_thumbnail_{Guid.NewGuid()}.png");
						using (var stream = System.IO.File.Create(tempPath))
						{
							await resizedBitmap.CompressAsync(Android.Graphics.Bitmap.CompressFormat.Png, 90, stream);
						}

						// Clean up bitmaps
						bitmap.Dispose();
						resizedBitmap.Dispose();

						// Return as ImageSource
						return Microsoft.Maui.Controls.ImageSource.FromFile(tempPath);
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Error capturing thumbnail: {ex.Message}");
						return null;
					}
				});
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error capturing thumbnail (outer): {ex.Message}");
				return null;
			}
		}

	/// <summary>
	/// Capture WebView content by drawing to Canvas
	/// This captures ONLY the WebView content, not overlays or other views on top
	/// Properly handles scrolled content
	/// </summary>
	private async Task<Android.Graphics.Bitmap> CaptureWithPixelCopyAsync(int width, int height)
	{
		try
		{
			// Create bitmap and canvas
			var bitmap = Android.Graphics.Bitmap.CreateBitmap(width, height, Android.Graphics.Bitmap.Config.Argb8888);
			var canvas = new Android.Graphics.Canvas(bitmap);

			// Save current scroll position
			int scrollX = PlatformView.ScrollX;
			int scrollY = PlatformView.ScrollY;

			// Translate canvas to match current scroll position
			// This ensures we capture what's currently visible, not the top of the page
			canvas.Translate(-scrollX, -scrollY);

			// Draw the WebView content directly to canvas at the scrolled position
			// This captures ONLY the WebView, not any overlays or other UI elements
			PlatformView.Draw(canvas);

			// Return on next frame to ensure drawing is complete
			await Task.Delay(1);
			return bitmap;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error in canvas capture: {ex.Message}");
			return null;
		}
	}


		/// <summary>
		/// Fallback method for Android versions below 8.0 using DrawingCache
		/// This is deprecated but necessary for older devices
		/// </summary>
		private Android.Graphics.Bitmap CaptureWithDrawingCache(int width, int height)
		{
			try
			{
#pragma warning disable CS0618 // Type or member is obsolete
				// Enable drawing cache temporarily
				PlatformView.DrawingCacheEnabled = true;
				PlatformView.BuildDrawingCache(true);

				// Get the drawing cache bitmap
				var cacheBitmap = PlatformView.GetDrawingCache(true);

				// Create a mutable copy since the cache bitmap is immutable
				Android.Graphics.Bitmap bitmap;
				if (cacheBitmap != null)
				{
					bitmap = cacheBitmap.Copy(Android.Graphics.Bitmap.Config.Argb8888, false);
				}
				else
				{
					// Last resort fallback: Create bitmap and draw manually
					bitmap = Android.Graphics.Bitmap.CreateBitmap(width, height, Android.Graphics.Bitmap.Config.Argb8888);
					var canvas = new Android.Graphics.Canvas(bitmap);
					canvas.DrawColor(Android.Graphics.Color.White);
					PlatformView.Draw(canvas);
				}

				// Disable drawing cache
				PlatformView.DrawingCacheEnabled = false;
#pragma warning restore CS0618 // Type or member is obsolete

				return bitmap;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error in DrawingCache capture: {ex.Message}");
				return null;
			}
		}

	}

	class ValueCallback : Java.Lang.Object, IValueCallback
	{
		private TaskCompletionSource<string> _tcs;

		public ValueCallback(TaskCompletionSource<string> tcs)
		{
			_tcs = tcs;
		}

		public void OnReceiveValue(Java.Lang.Object value)
		{
			_tcs.TrySetResult(value?.ToString() ?? "{}");
		}
	}

	public class CustomDownloadListener : Java.Lang.Object, IDownloadListener
	{
		private readonly WebViewHandler _handler;

		public CustomDownloadListener(WebViewHandler handler)
		{
			_handler = handler;
		}

		public void OnDownloadStart(string url, string userAgent, string contentDisposition, string mimetype, long contentLength)
		{
			// Must not be async void - unhandled exceptions crash the app in Release mode
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try
				{
					if (_handler.IsPotentialPdfUrl(url) ||
						mimetype?.Contains("pdf", StringComparison.OrdinalIgnoreCase) == true)
					{
						// WebView has authenticated - download with its cookies
						var browserView = (_handler.VirtualView as WebView)?.ParentBrowserView;
						if (browserView != null && browserView.ShowInlinePdf)
						{
							// Get cookies from WebView's session
							var cookieManager = Android.Webkit.CookieManager.Instance;
							var cookies = cookieManager?.GetCookie(url);

							// Download and display PDF with authentication
							await DownloadPdfWithCookies(url, cookies, browserView);
						}
						else
						{
							// Open with external app
							var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
							intent.AddFlags(ActivityFlags.NewTask);
							Platform.CurrentActivity.StartActivity(intent);
						}
					}
					else
					{
						var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
						intent.AddFlags(ActivityFlags.NewTask);
						Platform.CurrentActivity.StartActivity(intent);
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Error in OnDownloadStart: {ex.Message}");
				}
			});
		}

		private async Task DownloadPdfWithCookies(string url, string cookies, BrowserView browserView)
		{
		try
		{
			browserView.IsLoading = true;

			using var httpClient = new HttpClient(new HttpClientHandler
			{
				AllowAutoRedirect = true,
				MaxAutomaticRedirections = 10
			});

			// Add cookies - THE KEY to bypassing arxiv bot check
			if (!string.IsNullOrEmpty(cookies))
			{
				httpClient.DefaultRequestHeaders.Add("Cookie", cookies);
			}

			httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 10) AppleWebKit/537.36");
			httpClient.DefaultRequestHeaders.Add("Accept", "application/pdf,application/octet-stream,*/*");

			// Download WITH cookies
			var pdfData = await httpClient.GetByteArrayAsync(url);

			System.Diagnostics.Debug.WriteLine($"Android: Downloaded {pdfData?.Length ?? 0} bytes WITH COOKIES");

			// Validate
			if (pdfData != null && pdfData.Length >= 5 &&
				pdfData[0] == 0x25 && pdfData[1] == 0x50 && pdfData[2] == 0x44 && pdfData[3] == 0x46)
			{
				var tempFile = DataSources.PdfTempFileHelper.CreateTempPdfFilePath();
				await System.IO.File.WriteAllBytesAsync(tempFile, pdfData);

				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					var pdfView = browserView.GetType().GetField("_pdfView",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
						?.GetValue(browserView) as PdfView;
					var webView = browserView.GetType().GetField("_webView",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
						?.GetValue(browserView) as WebView;

					if (pdfView != null && webView != null)
					{
						pdfView.Uri = tempFile;
						webView.IsVisible = false;
						pdfView.IsVisible = true;
					}
				});
			}

			browserView.IsLoading = false;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"DownloadPdfWithCookies failed: {ex.Message}");
			browserView.IsLoading = false;
		}
	}
}

}
