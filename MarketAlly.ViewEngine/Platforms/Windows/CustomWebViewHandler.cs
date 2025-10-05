using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Text.Json;

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
							setTimeout(() => { window.location.assign(href); }, 50);
							return false;
						}
					}
				}, true);
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

					if (!string.IsNullOrEmpty(url) && IsPotentialPdfUrl(url))
					{
						// Don't cancel navigation, just process PDF in parallel
						_ = Task.Run(async () => await HandlePotentialPdfUrl(url));
					}
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

			if (IsPotentialPdfUrl(url))
			{
				args.Handled = true;
				await HandlePotentialPdfUrl(url);
			}
			else if (args.ResultFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
			{
				args.Handled = true;
				await HandlePotentialPdfUrl(url);
			}
			else
			{
				Process.Start(new ProcessStartInfo(args.ResultFilePath) { UseShellExecute = true });
				args.Cancel = false;
			}
		}

		public void SetUserAgent(string userAgent)
		{
			try
			{
				if (PlatformView?.CoreWebView2?.Settings != null)
				{
					PlatformView.CoreWebView2.Settings.UserAgent = userAgent ??
						"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
				}
				else
				{
				}
			}
			catch (Exception ex)
			{
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

	}
}
