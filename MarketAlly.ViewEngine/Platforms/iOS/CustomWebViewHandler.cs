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
                // Prevent multiple injections
                if (window.__contentMonitorInjected) {
                    console.log('[iOS] Content monitor already injected');
                    return;
                }
                window.__contentMonitorInjected = true;

                function notifyContentChange() {
                    window.webkit.messageHandlers.contentChanged.postMessage('contentChanged');
                }

                // Monitor DOM changes - optimized to watch only specific attributes
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

                    // Monitor clicks
                    document.addEventListener('click', () => {
                        setTimeout(notifyContentChange, 500);
                    }, true);
                }

                console.log('[iOS] Content monitoring script injected');
            })();";

		private async Task InjectContentMonitoringScriptAsync()
		{
			try
			{
				if (PlatformView != null)
				{
					Console.WriteLine("[iOS] Injecting content monitoring script");
					await PlatformView.EvaluateJavaScriptAsync(ContentMonitoringScript);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[iOS] Error injecting content monitoring script: {ex.Message}");
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
				Console.WriteLine($"Navigated to: {webView.Url?.AbsoluteString}");

				// Re-inject monitoring script after each navigation
				await _handler.InjectContentMonitoringScriptAsync();

				// Trigger page data extraction
				await _handler.OnPageDataChangedAsync();
			}

			public async override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
			{
				var url = navigationAction.Request.Url?.AbsoluteString;
				Console.WriteLine($"iOS deciding policy for URL: {url}");

				if (_handler.IsPotentialPdfUrl(url))
				{
					Console.WriteLine("PDF detected, reading content in parallel...");
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

		public async partial Task<PageData> ExtractPageDataAsync()
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
					return ProcessHtmlResult(result?.ToString());
				}
				else
				{
					Console.WriteLine("[iOS] JavaScript execution timed out");
					return new PageData { Title = "Timeout", Body = "Page data extraction timed out." };
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[iOS] Error extracting page data: {ex.Message}");
				return new PageData { Title = "Error", Body = $"Failed to extract page data: {ex.Message}" };
			}
		}

		private PageData ProcessHtmlResult(string result)
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

				var bodyText = !string.IsNullOrEmpty(rawData.Body) ? DecodeBase64(rawData.Body) : string.Empty;
				var routes = PageDataExtractor.ProcessLinks(rawData.Links, rawData.Url);
				var bodyRoutes = PageDataExtractor.ProcessLinks(rawData.BodyLinks, rawData.Url);

				return new PageData
				{
					Title = rawData.Title ?? "Untitled",
					Body = bodyText,
					Url = rawData.Url ?? string.Empty,
					Routes = routes,
					BodyRoutes = bodyRoutes
				};
			}
			catch (JsonException ex)
			{
				Console.WriteLine($"[iOS] JSON parsing error: {ex.Message}");
				return new PageData { Title = "Error", Body = $"Failed to parse JSON: {ex.Message}" };
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[iOS] Error processing HTML result: {ex.Message}");
				return new PageData { Title = "Error", Body = $"Failed to process HTML: {ex.Message}" };
			}
		}

		public partial async Task InjectJavaScriptAsync(string script)
		{
			await PlatformView.EvaluateJavaScriptAsync(script);
		}
	}

	}
