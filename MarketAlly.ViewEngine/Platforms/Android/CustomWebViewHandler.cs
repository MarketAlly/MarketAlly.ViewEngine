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

			// Handle new tab navigation and downloads
			platformView.SetWebViewClient(new CustomWebViewClient());
			platformView.SetDownloadListener(new CustomDownloadListener());
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
				result = result.Trim('"')  // Remove surrounding quotes
							   .Replace("\\\"", "\"")  // Fix escaped quotes
							   .Replace("\\n", " ")  // Normalize line breaks
							   .Replace("\\r", " ")  // Remove carriage returns
							   .Replace("\\t", " ")  // Remove tabs
							   .Replace("\\\\", "\\"); // Fix double backslashes

				Console.WriteLine("Raw JSON Output: " + result); // Debugging Log

				var rawData = JsonSerializer.Deserialize<PageRawData>(result, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				var bodyText = DecodeBase64(rawData.Html);
				return new PageData { Title = rawData.Title, Body = bodyText };
			}
			catch
			{
				return new PageData { Title = "Error", Body = "Failed to parse HTML." };
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

	public class CustomWebViewClient : WebViewClient
	{
		public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView view, IWebResourceRequest request)
		{
			view.LoadUrl(request.Url.ToString());
			return true;
		}
	}

	public class CustomDownloadListener : Java.Lang.Object, IDownloadListener
	{
		public void OnDownloadStart(string url, string userAgent, string contentDisposition, string mimetype, long contentLength)
		{
			var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
			intent.AddFlags(ActivityFlags.NewTask);
			Platform.CurrentActivity.StartActivity(intent);
		}
	}
}
