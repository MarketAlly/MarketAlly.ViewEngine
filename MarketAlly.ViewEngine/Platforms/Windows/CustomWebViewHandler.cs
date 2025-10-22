using Microsoft.Maui.Controls.Internals;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Text.Json;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace MarketAlly.Maui.ViewEngine
{
	public partial class WebViewHandler
	{
		private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2NewWindowRequestedEventArgs> _newWindowHandler;
		private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2DownloadStartingEventArgs> _downloadHandler;
		private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2SourceChangedEventArgs> _sourceChangedHandler;
		private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2WebMessageReceivedEventArgs> _messageReceivedHandler;
		private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2NavigationStartingEventArgs> _navigationStartingHandler;
		private Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2NavigationCompletedEventArgs> _navigationCompletedHandler;

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
                        chrome.webview.postMessage('contentChanged');
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

		private async Task InjectContentMonitoringScriptAsync()
		{
			try
			{
				if (PlatformView?.CoreWebView2 != null)
				{
					await PlatformView.CoreWebView2.ExecuteScriptAsync(ContentMonitoringScript);

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
				if (PlatformView?.CoreWebView2 != null)
				{
					await PlatformView.CoreWebView2.ExecuteScriptAsync(forceLinkScript);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error injecting force link script: {ex.Message}");
			}
		}

		protected override async void ConnectHandler(Microsoft.UI.Xaml.Controls.WebView2 platformView)
		{
			base.ConnectHandler(platformView);

			try
			{
				if (platformView.CoreWebView2 == null)
				{
					await platformView.EnsureCoreWebView2Async();
				}

				// Create event handlers
				_newWindowHandler = OnNewWindowRequested;
				_downloadHandler = OnDownloadStarting;

				// Now it's safe to attach event handlers
				platformView.CoreWebView2.NewWindowRequested += _newWindowHandler;
				platformView.CoreWebView2.DownloadStarting += _downloadHandler;

				// Track the last URL to detect changes
				string lastUrl = string.Empty;

				// Monitor source URL changes
				_sourceChangedHandler = async (sender, args) =>
				{
					var currentUrl = platformView.CoreWebView2?.Source;

					if (currentUrl != lastUrl)
					{
						lastUrl = currentUrl;

						// Give the page a moment to update its content
						await Task.Delay(500);
						await OnPageDataChangedAsync();
					}
				};
				platformView.CoreWebView2.SourceChanged += _sourceChangedHandler;

				// Setup message handler for content changes
				_messageReceivedHandler = async (sender, args) =>
				{
					await OnPageDataChangedAsync();
				};
				platformView.CoreWebView2.WebMessageReceived += _messageReceivedHandler;

				// Add pre-navigation check
				_navigationStartingHandler = async (sender, args) =>
				{
					var url = args?.Uri;

					// PDF handling is now done by BrowserView.ShowPdfAsync
					// No need for legacy HandlePotentialPdfUrl call here
				};
				platformView.CoreWebView2.NavigationStarting += _navigationStartingHandler;

				// Inject monitoring script after each navigation completes
				// Track if UserAgent has been set
				bool userAgentSet = false;

				_navigationCompletedHandler = async (sender, args) =>
				{
					var webView = sender as CoreWebView2;

					if (args.IsSuccess)
					{
						// Re-inject the content monitoring script after each navigation
						// Set user agent on first successful navigation (when CoreWebView2 is fully ready)
						if (!userAgentSet)
						{
							SetUserAgent(VirtualView?.UserAgent);
							userAgentSet = true;
						}

						await InjectContentMonitoringScriptAsync();

						// Trigger page data extraction
						await OnPageDataChangedAsync();
					}
				};
				platformView.CoreWebView2.NavigationCompleted += _navigationCompletedHandler;


				// Set the User-Agent
				// SetUserAgent(VirtualView?.UserAgent); // Commented out - may cause crash if CoreWebView2 not fully ready
			}
			catch (Exception ex)
			{
			}
		}

		protected override void DisconnectHandler(Microsoft.UI.Xaml.Controls.WebView2 platformView)
		{
			if (platformView?.CoreWebView2 != null)
			{
				if (_newWindowHandler != null)
					platformView.CoreWebView2.NewWindowRequested -= _newWindowHandler;

				if (_downloadHandler != null)
					platformView.CoreWebView2.DownloadStarting -= _downloadHandler;

				if (_sourceChangedHandler != null)
					platformView.CoreWebView2.SourceChanged -= _sourceChangedHandler;

				if (_messageReceivedHandler != null)
					platformView.CoreWebView2.WebMessageReceived -= _messageReceivedHandler;

				if (_navigationStartingHandler != null)
					platformView.CoreWebView2.NavigationStarting -= _navigationStartingHandler;

				if (_navigationCompletedHandler != null)
					platformView.CoreWebView2.NavigationCompleted -= _navigationCompletedHandler;

				_newWindowHandler = null;
				_downloadHandler = null;
				_sourceChangedHandler = null;
				_messageReceivedHandler = null;
				_navigationStartingHandler = null;
				_navigationCompletedHandler = null;
			}

			base.DisconnectHandler(platformView);
		}

		public async Task ForcePageDataUpdate()
		{
			try
			{
				if (PlatformView?.CoreWebView2 != null)
				{
					await PlatformView.CoreWebView2.ExecuteScriptAsync(@"
                console.log('Manual update triggered');
                chrome.webview.postMessage(JSON.stringify({
                    type: 'contentChanged',
                    url: window.location.href,
                    trigger: 'manual'
                }));
            ");
				}
				else
				{
				}
			}
			catch (Exception ex)
			{
			}
		}

		public partial async Task InjectJavaScriptAsync(string script)
		{
			await PlatformView.CoreWebView2.ExecuteScriptAsync(script);
		}

		private void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
		{
			sender.Navigate(args.Uri);
			args.Handled = true;
		}

		private async void OnDownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
		{
			var url = args.DownloadOperation.Uri;

			// PDF handling is now done by BrowserView.ShowPdfAsync
			// For non-PDF downloads, open with default application
			if (!IsPotentialPdfUrl(url) && !args.ResultFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
			{
				Process.Start(new ProcessStartInfo(args.ResultFilePath) { UseShellExecute = true });
				args.Cancel = false;
			}
			else
			{
				// Cancel the download - BrowserView will handle PDF display
				args.Handled = true;
			}
		}

		public void SetUserAgent(string userAgent)
		{
			try
			{
				if (PlatformView?.CoreWebView2?.Settings != null)
				{
					// Get the WebView to access UserAgentMode
					var webView = VirtualView as WebView;
					var customUserAgent = userAgent;
					var userAgentMode = webView?.UserAgentMode ?? UserAgentMode.Default;

					if (!string.IsNullOrEmpty(customUserAgent))
					{
						// Custom user agent provided - use it
						PlatformView.CoreWebView2.Settings.UserAgent = customUserAgent;
					}
					else
					{
						// Determine user agent based on mode
						// Default for Windows is Desktop
						bool useDesktop = userAgentMode == UserAgentMode.Desktop ||
										  (userAgentMode == UserAgentMode.Default);

						if (useDesktop)
						{
							// Desktop: Windows Chrome
							PlatformView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
						}
						else
						{
							// Mobile: Android Chrome
							PlatformView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.144 Mobile Safari/537.36";
						}
					}
				}
				else
				{
				}
			}
			catch (Exception ex)
			{
			}
		}

		public partial void SetEnableZoom(bool enable)
		{
			try
			{
				if (PlatformView?.CoreWebView2?.Settings != null)
				{
					PlatformView.CoreWebView2.Settings.IsPinchZoomEnabled = enable;
					PlatformView.CoreWebView2.Settings.IsZoomControlEnabled = enable;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error setting zoom: {ex.Message}");
			}
		}

		public async partial Task<PageData> ExtractPageDataAsync(bool forceRouteExtraction = false)
		{
			try
			{
				if (PlatformView?.CoreWebView2 == null)
				{
					if (PlatformView == null)
						return new PageData { Title = "Error", Body = "WebView not initialized." };

					await PlatformView.EnsureCoreWebView2Async();
				}

				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
				var scriptTask = PlatformView.CoreWebView2.ExecuteScriptAsync(PageDataExtractor.ExtractScript).AsTask();
				var completedTask = await Task.WhenAny(scriptTask, Task.Delay(Timeout.Infinite, cts.Token));

				if (completedTask != scriptTask)
				{
					return new PageData { Title = "Timeout", Body = "Page data extraction timed out." };
				}

				string result = await scriptTask;

				if (!string.IsNullOrWhiteSpace(result))
				{
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

					// Cache raw data for on-demand route extraction
					_cachedRawData = rawData;

					var bodyText = DecodeBase64(rawData.Body);

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
						Title = rawData.Title,
						Body = bodyText,
						Url = rawData.Url,
						Routes = routes,
						BodyRoutes = bodyRoutes
					};

					return pageData;
				}

				return new PageData { Title = "No Content", Body = "Page content could not be retrieved." };
			}
			catch (JsonException ex)
			{
				return new PageData { Title = "Error", Body = $"Failed to parse page content: {ex.Message}" };
			}
			catch (Exception ex)
			{
				return new PageData { Title = "Error", Body = $"Failed to extract page data: {ex.Message}" };
			}
		}

		public partial void EnsureCustomWebViewClient()
		{
			// Not needed on Windows - WebView2 uses event handlers instead
		}

		public partial async Task<Microsoft.Maui.Controls.ImageSource> CaptureThumbnailAsync(int width = 320, int height = 180)
		{
			try
			{
				if (PlatformView?.CoreWebView2 == null)
				{
					return null;
				}

				// Create a temporary file to save the screenshot
				var tempPath = Path.Combine(Path.GetTempPath(), $"webview_thumbnail_{Guid.NewGuid()}.png");

				// Capture the webpage screenshot
				using (var stream = System.IO.File.Create(tempPath))
				{
					await PlatformView.CoreWebView2.CapturePreviewAsync(
						CoreWebView2CapturePreviewImageFormat.Png,
						stream.AsRandomAccessStream());
				}

				// Note: Image resizing is not implemented on Windows platform
				// Return the full-size screenshot - the Image control will handle scaling
				return Microsoft.Maui.Controls.ImageSource.FromFile(tempPath);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error capturing thumbnail: {ex.Message}");
				return null;
			}
		}

		public partial async Task<Microsoft.Maui.Controls.ImageSource> RenderPdfThumbnailAsync(byte[] pdfData, int width, int height)
		{
			try
			{
				// Create in-memory random access stream from PDF data
				using var stream = new InMemoryRandomAccessStream();
				using var writer = new DataWriter(stream);
				writer.WriteBytes(pdfData);
				await writer.StoreAsync();
				await writer.FlushAsync();
				stream.Seek(0);

				// Load PDF document
				var pdfDocument = await PdfDocument.LoadFromStreamAsync(stream);

				if (pdfDocument.PageCount == 0)
					return null;

				// Get first page
				using var page = pdfDocument.GetPage(0);

				// Calculate dimensions maintaining aspect ratio
				var pageSize = page.Size;
				double aspectRatio = pageSize.Width / pageSize.Height;
				double targetWidth = width;
				double targetHeight = height;

				if (aspectRatio > (width / (double)height))
				{
					targetHeight = width / aspectRatio;
				}
				else
				{
					targetWidth = height * aspectRatio;
				}

				// Create render target stream
				using var renderStream = new InMemoryRandomAccessStream();

				// Render page to stream
				var renderOptions = new PdfPageRenderOptions
				{
					DestinationWidth = (uint)targetWidth,
					DestinationHeight = (uint)targetHeight,
					BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255) // White background
				};

				await page.RenderToStreamAsync(renderStream, renderOptions);
				renderStream.Seek(0);

				// Convert stream to byte array
				var bytes = new byte[renderStream.Size];
				await renderStream.ReadAsync(bytes.AsBuffer(), (uint)renderStream.Size, InputStreamOptions.None);

				// Return as ImageSource
				return Microsoft.Maui.Controls.ImageSource.FromStream(() => new System.IO.MemoryStream(bytes));
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Windows: Error rendering PDF thumbnail: {ex.Message}");
				return null;
			}
		}
	}
}
