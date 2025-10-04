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

		private void InjectContentMonitoringScript()
		{
			try
			{
				if (PlatformView != null)
				{
					PlatformView.EvaluateJavascript(ContentMonitoringScript, null);
				}
			}
			catch (Exception ex)
			{
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

			public override async void OnPageFinished(Android.Webkit.WebView view, string url)
			{
				base.OnPageFinished(view, url);

				// Re-inject monitoring script after each page load
				_handler.InjectContentMonitoringScript();

				// Trigger page data extraction
				await _handler.OnPageDataChangedAsync();
			}

			public override void DoUpdateVisitedHistory(Android.Webkit.WebView view, string url, bool isReload)
			{
				base.DoUpdateVisitedHistory(view, url, isReload);

				// Trigger page data extraction
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					// Re-inject monitoring script after each page load
					_handler.InjectContentMonitoringScript();
					await _handler.OnPageDataChangedAsync();
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
						await _handler.HandlePotentialPdfUrl(url);
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
			public async void OnContentChanged()
			{
				await _handler.OnPageDataChangedAsync();
			}
		}

		public partial async Task InjectJavaScriptAsync(string script)
		{
			PlatformView.EvaluateJavascript(script, null);
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

		public async void OnDownloadStart(string url, string userAgent, string contentDisposition, string mimetype, long contentLength)
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
	}
}
