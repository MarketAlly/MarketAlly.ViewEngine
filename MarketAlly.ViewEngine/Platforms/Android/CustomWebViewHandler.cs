using Android.Content;
using Android.Webkit;
using Microsoft.Maui.Handlers;
using System.Text.Json;
using static Android.Media.MediaRouter;

namespace MarketAlly.ViewEngine
{
	public partial class CustomWebViewHandler
	{
		protected override void ConnectHandler(Android.Webkit.WebView platformView)
		{
			base.ConnectHandler(platformView);

			var settings = platformView.Settings;
			settings.JavaScriptEnabled = true;
			settings.DomStorageEnabled = true;

			CookieManager.Instance.SetAcceptCookie(true);
			CookieManager.Instance.SetAcceptThirdPartyCookies(platformView, true);

			// Track the last URL
			string lastUrl = string.Empty;

			// Create a custom WebViewClient to monitor URL changes
			platformView.SetWebViewClient(new CustomWebViewClient(this, (url) =>
			{
				if (url != lastUrl)
				{
					lastUrl = url;
					MainThread.BeginInvokeOnMainThread(async () =>
					{
						await Task.Delay(500); // Give content time to update
						await OnPageDataChangedAsync();
					});
				}
			}));

			// Handle downloads
			platformView.SetDownloadListener(new CustomDownloadListener(this));

			// Inject the content monitoring script
			platformView.EvaluateJavascript(@"
        (function() {
            function notifyContentChange() {
                if (window.Android) {
                    window.Android.onContentChanged();
                }
            }

            // Monitor DOM changes
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

            observer.observe(document.body, {
                childList: true,
                subtree: true,
                attributes: true
            });

            // Monitor clicks
            document.addEventListener('click', () => {
                setTimeout(notifyContentChange, 500);
            }, true);
        })();
    ", null);

			// Add JavaScript interface for content change notifications
			platformView.AddJavascriptInterface(new WebViewJavaScriptInterface(this), "Android");
		}

		public class CustomWebViewClient : WebViewClient
		{
			private readonly CustomWebViewHandler _handler;
			private readonly Action<string> _onUrlChanged;

			public CustomWebViewClient(CustomWebViewHandler handler, Action<string> onUrlChanged)
			{
				_handler = handler;
				_onUrlChanged = onUrlChanged;
			}

			public override void OnPageFinished(Android.Webkit.WebView view, string url)
			{
				base.OnPageFinished(view, url);
				_onUrlChanged(url);
			}

			public override void DoUpdateVisitedHistory(Android.Webkit.WebView view, string url, bool isReload)
			{
				base.DoUpdateVisitedHistory(view, url, isReload);
				_onUrlChanged(url);
			}
		}

		public class WebViewJavaScriptInterface : Java.Lang.Object
		{
			private readonly CustomWebViewHandler _handler;

			public WebViewJavaScriptInterface(CustomWebViewHandler handler)
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

		public async partial Task<PageData> ExtractPageDataAsync()
		{
			var webView = PlatformView as Android.Webkit.WebView;
			if (webView == null)
				return new PageData { Title = "Error", Body = "WebView not initialized." };

			var script = @"
                (function() {
                    function toBase64(str) {
                        try {
                            return btoa(unescape(encodeURIComponent(str)));
                        } catch (e) {
                            return 'ERROR_ENCODING';
                        }
                    }
                    
                    let pageData = {
                        title: document.title || '',
                        html: toBase64(document.documentElement.outerHTML || '')
                    };

                    console.log('Extracted JSON:', JSON.stringify(pageData));
                    return JSON.stringify(pageData);
                })();";

			var tcs = new TaskCompletionSource<string>();
			webView.EvaluateJavascript(script, new ValueCallback(tcs));
			var result = await tcs.Task;

			return ProcessHtmlResult(result);
		}

		private PageData ProcessHtmlResult(string result)
		{
			if (string.IsNullOrWhiteSpace(result))
				return new PageData { Title = "No Content", Body = "Empty HTML response." };

			try
			{
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

				var bodyText = DecodeBase64(rawData.Html);
				return new PageData
				{
					Title = rawData.Title,
					Body = bodyText,
					Url = rawData.Url // Include URL in the response
				};
			}
			catch
			{
				return new PageData { Title = "Error", Body = "Failed to parse HTML." };
			}
		}
	}

	public class WebViewJavaScriptInterface : Java.Lang.Object
	{
		private readonly CustomWebViewHandler _handler;

		public WebViewJavaScriptInterface(CustomWebViewHandler handler)
		{
			_handler = handler;
		}

		[Android.Webkit.JavascriptInterface]
		public async void OnContentChange()
		{
			await _handler.OnPageDataChangedAsync();
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

	public class CustomWebViewClient : WebViewClient
	{
		private readonly CustomWebViewHandler _handler;

		public CustomWebViewClient(CustomWebViewHandler handler)
		{
			_handler = handler;
		}

		public override async void OnPageFinished(Android.Webkit.WebView view, string url)
		{
			base.OnPageFinished(view, url);
			Console.WriteLine($"Navigated to: {url}");
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

	public class CustomDownloadListener : Java.Lang.Object, IDownloadListener
	{
		private readonly CustomWebViewHandler _handler;

		public CustomDownloadListener(CustomWebViewHandler handler)
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
