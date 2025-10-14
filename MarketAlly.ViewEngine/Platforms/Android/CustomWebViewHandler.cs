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

            let debounceTimer = null;

            function notifyContentChange() {
                if (debounceTimer) {
                    clearTimeout(debounceTimer);
                }

                debounceTimer = setTimeout(() => {
                    if (window.Android) {
                        window.Android.onContentChanged();
                    }
                    debounceTimer = null;
                }, 1000);
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
                    const currentUrl = window.location.href;

                    // Check if it's a real navigation link
                    if (href &&
                        href !== currentUrl &&
                        !href.startsWith('javascript:') &&
                        href !== '#' &&
                        !href.startsWith('about:')) {

                        // Check for SPAs that prevent navigation
                        const isGoogleNews = window.location.hostname.includes('news.google.com');
                        const isExternalLink = new URL(href).hostname !== window.location.hostname;

                        // Force navigation for known SPAs or external links
                        if (isGoogleNews || isExternalLink) {
                            console.log('WebView: Force navigating to:', href);
                            e.preventDefault();
                            e.stopPropagation();

                            // Use location.assign for proper navigation history
                            setTimeout(() => {
                                window.location.assign(href);
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

				document.addEventListener('click', function(e) {
					let target = e.target;
					while (target && target !== document.body) {
						if (target.tagName === 'A') break;
						target = target.parentElement;
					}

					if (target && target.tagName === 'A' && target.href) {
						const href = target.href;
						if (href &&
							href !== window.location.href &&
							!href.startsWith('javascript:') &&
							href !== '#' &&
							!href.startsWith('about:')) {

							console.log('Force navigating to:', href);
							e.preventDefault();
							e.stopPropagation();

							// Try to use the native click first for better history support
							// Create and dispatch a new click event without our handler
							window.__forceLinkNavInjected = false;
							setTimeout(() => {
								// Use location.href for better history tracking
								window.location.href = href;
								window.__forceLinkNavInjected = true;
							}, 50);
							return false;
						}
					}
				}, true);
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

				if (_handler.IsPotentialPdfUrl(url))
				{
					// Process PDF in parallel
					MainThread.BeginInvokeOnMainThread(async () =>
					{
					try
					{
						await _handler.HandlePotentialPdfUrl(url);
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Error in ShouldOverrideUrlLoading: {ex.Message}");
					}
					});
				}

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
		/// Modern way to capture WebView content using PixelCopy API (Android 8.0+)
		/// This is the correct, non-deprecated method that captures ONLY the WebView content, not overlays
		/// </summary>
		private async Task<Android.Graphics.Bitmap> CaptureWithPixelCopyAsync(int width, int height)
		{
			var tcs = new TaskCompletionSource<Android.Graphics.Bitmap>();

			try
			{
				// Create bitmap to receive the pixel data
				var bitmap = Android.Graphics.Bitmap.CreateBitmap(width, height, Android.Graphics.Bitmap.Config.Argb8888);

			// Use PixelCopy to capture directly from the WebView surface
			// This captures ONLY the WebView content, not overlays or other views on top
			Android.Views.PixelCopy.Request(
				PlatformView,
				bitmap,
				new PixelCopyListener(bitmap, tcs),
				new Android.OS.Handler(Android.OS.Looper.MainLooper)
			);

				// Wait for the pixel copy to complete
				var result = await tcs.Task;
				return result;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error in PixelCopy capture: {ex.Message}");
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

		/// <summary>
		/// Listener for PixelCopy operations
		/// </summary>
		private class PixelCopyListener : Java.Lang.Object, Android.Views.PixelCopy.IOnPixelCopyFinishedListener
		{
			private readonly Android.Graphics.Bitmap _bitmap;
			private readonly TaskCompletionSource<Android.Graphics.Bitmap> _tcs;

			public PixelCopyListener(Android.Graphics.Bitmap bitmap, TaskCompletionSource<Android.Graphics.Bitmap> tcs)
			{
				_bitmap = bitmap;
				_tcs = tcs;
			}

			public void OnPixelCopyFinished(int copyResult)
			{
				if (copyResult == (int)Android.Views.PixelCopyResult.Success)
				{
					// Success - the bitmap was filled with pixel data
					_tcs.TrySetResult(_bitmap);
				}
				else
				{
					System.Diagnostics.Debug.WriteLine($"PixelCopy failed with result: {copyResult}");
					_tcs.TrySetResult(null);
				}
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
						await _handler.HandlePotentialPdfUrl(url);
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
	}
}
