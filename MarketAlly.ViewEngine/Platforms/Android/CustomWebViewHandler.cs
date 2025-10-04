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
            // Prevent multiple injections
            if (window.__contentMonitorInjected) {
                console.log('[Android] Content monitor already injected');
                return;
            }
            window.__contentMonitorInjected = true;

            function notifyContentChange() {
                if (window.Android) {
                    window.Android.onContentChanged();
                }
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

            console.log('[Android] Content monitoring script injected');
        })();";

		private void InjectContentMonitoringScript()
		{
			try
			{
				if (PlatformView != null)
				{
					Console.WriteLine("[Android] Injecting content monitoring script");
					PlatformView.EvaluateJavascript(ContentMonitoringScript, null);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[Android] Error injecting content monitoring script: {ex.Message}");
			}
		}

		protected override void ConnectHandler(Android.Webkit.WebView platformView)
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
				Console.WriteLine($"Navigated to: {url}");

				// Re-inject monitoring script after each page load
				_handler.InjectContentMonitoringScript();

				// Trigger page data extraction
				await _handler.OnPageDataChangedAsync();
			}

			public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView view, IWebResourceRequest request)
			{
				var url = request.Url?.ToString();
				Console.WriteLine($"Android checking URL: {url}");

				if (_handler.IsPotentialPdfUrl(url))
				{
					Console.WriteLine("PDF detected, reading content in parallel...");
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
				Console.WriteLine("[Android] OnContentChanged called from JavaScript");
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

		public async partial Task<PageData> ExtractPageDataAsync()
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

				return ProcessHtmlResult(result);
			}
			catch (OperationCanceledException)
			{
				Console.WriteLine("[Android] JavaScript execution timed out");
				return new PageData { Title = "Timeout", Body = "Page data extraction timed out." };
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[Android] Error extracting page data: {ex.Message}");
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
				Console.WriteLine($"[Android] JSON parsing error: {ex.Message}");
				return new PageData { Title = "Error", Body = $"Failed to parse JSON: {ex.Message}" };
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[Android] Error processing HTML result: {ex.Message}");
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
