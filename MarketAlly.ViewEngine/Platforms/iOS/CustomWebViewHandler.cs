using Foundation;
using System.Text.Json;
using UIKit;
using WebKit;

namespace MarketAlly.Maui.ViewEngine
{
	public partial class WebViewHandler
	{
		private CustomNavigationDelegate _navigationDelegate;
		private CustomScriptMessageHandler _scriptMessageHandler;

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
                        window.webkit.messageHandlers.contentChanged.postMessage('contentChanged');
                        debounceTimer = null;
                    }, 1000);
                }

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

                    document.addEventListener('click', () => {
                        notifyContentChange();
                    }, true);
                }
            })();";

		private async Task InjectContentMonitoringScriptAsync()
		{
			try
			{
				if (PlatformView != null)
				{
					await PlatformView.EvaluateJavaScriptAsync(ContentMonitoringScript);

					// Check if we need to inject link forcing script
					var webView = VirtualView as WebView;
					if (webView?.ForceLinkNavigation == true)
					{
						await InjectForceLinkNavigationScriptAsync();
					}
				}
			}
			catch (Exception ex)
			{
			}
		}

		private async Task InjectForceLinkNavigationScriptAsync()
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

							// Temporarily disable handler and use location.href for better history
							window.__forceLinkNavInjected = false;
							setTimeout(() => {
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
				if (PlatformView != null)
				{
					await PlatformView.EvaluateJavaScriptAsync(forceLinkScript);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error injecting force link script: {ex.Message}");
			}
		}

		protected override void ConnectHandler(WKWebView platformView)
		{
			base.ConnectHandler(platformView);

			if (platformView is WKWebView webView)
			{
				// Create and set custom navigation delegate
				_navigationDelegate = new CustomNavigationDelegate(this);
				webView.NavigationDelegate = _navigationDelegate;

				// Configure WebView
				webView.Configuration.Preferences.JavaScriptEnabled = true;
				webView.Configuration.Preferences.JavaScriptCanOpenWindowsAutomatically = true;

				// Set user agent based on UserAgentMode property
				var customWebView = VirtualView as WebView;
				var customUserAgent = customWebView?.UserAgent;
				var userAgentMode = customWebView?.UserAgentMode ?? UserAgentMode.Default;

				if (!string.IsNullOrEmpty(customUserAgent))
				{
					// Custom user agent provided - use it
					webView.CustomUserAgent = customUserAgent;
				}
				else
				{
					// Determine user agent based on mode
					// Default for iOS is Mobile
					bool useMobile = userAgentMode == UserAgentMode.Mobile ||
									 (userAgentMode == UserAgentMode.Default);

					if (useMobile)
					{
						// Mobile: iOS Safari
						webView.CustomUserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";
					}
					else
					{
						// Desktop: Mac Safari
						webView.CustomUserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
					}
				}

				// Add script message handler for content changes
				_scriptMessageHandler = new CustomScriptMessageHandler(this);
				webView.Configuration.UserContentController.AddScriptMessageHandler(
					_scriptMessageHandler, "contentChanged");
			}
		}

		protected override void DisconnectHandler(WKWebView platformView)
		{
			if (platformView != null)
			{
				platformView.NavigationDelegate = null;

				if (_scriptMessageHandler != null)
				{
					platformView.Configuration?.UserContentController?.RemoveScriptMessageHandler("contentChanged");
					_scriptMessageHandler?.Dispose();
					_scriptMessageHandler = null;
				}

				_navigationDelegate?.Dispose();
				_navigationDelegate = null;
			}

			base.DisconnectHandler(platformView);
		}

		public class CustomScriptMessageHandler : NSObject, IWKScriptMessageHandler
		{
			private readonly WebViewHandler _handler;

			public CustomScriptMessageHandler(WebViewHandler handler)
			{
				_handler = handler;
			}

			public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
			{
				if (message.Name == "contentChanged")
				{
					MainThread.BeginInvokeOnMainThread(async () =>
					{
						await _handler.OnPageDataChangedAsync();
					});
				}
			}
		}

		public class CustomNavigationDelegate : WKNavigationDelegate
		{
			private readonly WebViewHandler _handler;

			public CustomNavigationDelegate(WebViewHandler handler)
			{
				_handler = handler;
			}

			public override async void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
			{

				// Re-inject monitoring script after each navigation
				await _handler.InjectContentMonitoringScriptAsync();

				// Trigger page data extraction
				await _handler.OnPageDataChangedAsync();
			}

			public async override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
			{
				var url = navigationAction.Request.Url?.AbsoluteString;

				if (_handler.IsPotentialPdfUrl(url))
				{
					// Allow navigation to continue
					decisionHandler(WKNavigationActionPolicy.Allow);

					// Process PDF in parallel
					MainThread.BeginInvokeOnMainThread(async () =>
					{
						await _handler.HandlePotentialPdfUrl(url);
					});
					return;
				}

				if (navigationAction.TargetFrame == null || navigationAction.TargetFrame.MainFrame == false)
				{
					webView.LoadRequest(navigationAction.Request); // Load in the same WebView
					decisionHandler(WKNavigationActionPolicy.Cancel);
				}
				else
				{
					decisionHandler(WKNavigationActionPolicy.Allow);
				}
			}
		}

		public void SetUserAgent(string userAgent)
		{
			PlatformView.CustomUserAgent = !string.IsNullOrEmpty(userAgent) ? userAgent :
				"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
		}

		public async partial Task<PageData> ExtractPageDataAsync(bool forceRouteExtraction = false)
		{
			try
			{
				var webView = PlatformView as WKWebView;
				if (webView == null)
					return new PageData { Title = "Error", Body = "WebView not initialized." };

				var script = PageDataExtractor.ExtractScript;

				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
				var resultTask = webView.EvaluateJavaScriptAsync(script);
				var completedTask = await Task.WhenAny(resultTask, Task.Delay(Timeout.Infinite, cts.Token));

				if (completedTask == resultTask)
				{
					var result = await resultTask;
					return ProcessHtmlResult(result?.ToString(), forceRouteExtraction);
				}
				else
				{
					return new PageData { Title = "Timeout", Body = "Page data extraction timed out." };
				}
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

		public partial async Task InjectJavaScriptAsync(string script)
		{
			await PlatformView.EvaluateJavaScriptAsync(script);
		}

		public partial void EnsureCustomWebViewClient()
		{
			// Not needed on iOS - WKWebView uses navigation delegate
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
						// Create snapshot configuration for visible viewport only
						var config = new WKSnapshotConfiguration
						{
							// Use null Rect to capture the visible viewport (default behavior)
							Rect = CoreGraphics.CGRect.Null
						};

						// Take the snapshot of the visible viewport
						var image = await PlatformView.TakeSnapshotAsync(config);

						if (image == null)
						{
							return null;
						}

						// Calculate aspect-preserving dimensions
						float aspectRatio = (float)(image.Size.Width / image.Size.Height);
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

						// Resize the image
						UIGraphics.BeginImageContextWithOptions(new CoreGraphics.CGSize(targetWidth, targetHeight), false, 0);
						image.Draw(new CoreGraphics.CGRect(0, 0, targetWidth, targetHeight));
						var resizedImage = UIGraphics.GetImageFromCurrentImageContext();
						UIGraphics.EndImageContext();

						if (resizedImage == null)
						{
							return null;
						}

						// Convert to PNG data
						var pngData = resizedImage.AsPNG();

						// Save to temporary file
						var tempPath = Path.Combine(Path.GetTempPath(), $"webview_thumbnail_{Guid.NewGuid()}.png");
						pngData.Save(tempPath, true);

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
	}
}
