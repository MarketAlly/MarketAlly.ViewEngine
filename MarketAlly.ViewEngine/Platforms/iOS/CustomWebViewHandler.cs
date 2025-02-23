using Foundation;
using System.Text.Json;
using UIKit;
using WebKit;

namespace MarketAlly.ViewEngine
{
	public partial class CustomWebViewHandler
	{
		protected override void ConnectHandler(WKWebView platformView)
		{
			base.ConnectHandler((WKWebView)platformView);

			if (platformView is WKWebView webView)
			{
				webView.NavigationDelegate = new CustomNavigationDelegate();
				((WKWebView)platformView).Configuration.Preferences.JavaScriptEnabled = true;
				((WKWebView)platformView).Configuration.Preferences.JavaScriptCanOpenWindowsAutomatically = true;
			}
		}

		public void SetUserAgent(string userAgent)
		{
			PlatformView.CustomUserAgent = !string.IsNullOrEmpty(userAgent) ? userAgent :
				"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
		}

		public async partial Task<PageData> ExtractPageDataAsync()
		{
			var webView = PlatformView as WKWebView;
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

			var result = await webView.EvaluateJavaScriptAsync(script);
			return ProcessHtmlResult(result?.ToString());
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
	public class CustomNavigationDelegate : WKNavigationDelegate
	{
		public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
		{
			var url = navigationAction.Request.Url.AbsoluteString;

			if (url.StartsWith("blob:") || url.EndsWith(".pdf") || url.EndsWith(".zip"))
			{
				UIApplication.SharedApplication.OpenUrl(new NSUrl(url));
				decisionHandler(WKNavigationActionPolicy.Cancel);
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
}
