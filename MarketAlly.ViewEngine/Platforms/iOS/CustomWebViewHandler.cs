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
			base.ConnectHandler(platformView);

			if (platformView is WKWebView webView)
			{
				string lastUrl = string.Empty;

				// Create and set custom navigation delegate
				var navigationDelegate = new CustomNavigationDelegate(this, (url) =>
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
				});

				webView.NavigationDelegate = navigationDelegate;

				// Configure WebView
				webView.Configuration.Preferences.JavaScriptEnabled = true;
				webView.Configuration.Preferences.JavaScriptCanOpenWindowsAutomatically = true;

				// Add script message handler for content changes
				webView.Configuration.UserContentController.AddScriptMessageHandler(
					new CustomScriptMessageHandler(this), "contentObserver");

				// Inject the content monitoring script
				webView.EvaluateJavaScriptAsync(@"
            (function() {
                function notifyContentChange() {
                    window.webkit.messageHandlers.contentObserver.postMessage('contentChanged');
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
        ");
			}
		}

		public class CustomNavigationDelegate : WKNavigationDelegate
		{
			private readonly CustomWebViewHandler _handler;
			private readonly Action<string> _onUrlChanged;

			public CustomNavigationDelegate(CustomWebViewHandler handler, Action<string> onUrlChanged)
			{
				_handler = handler;
				_onUrlChanged = onUrlChanged;
			}

			public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
			{
				_onUrlChanged(webView.Url?.AbsoluteString ?? string.Empty);
			}

			public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
			{
				var url = navigationAction.Request.Url.AbsoluteString;

				if (url.StartsWith("blob:") || url.EndsWith(".pdf") || url.EndsWith(".zip"))
				{
					UIApplication.SharedApplication.OpenUrl(new NSUrl(url));
					decisionHandler(WKNavigationActionPolicy.Cancel);
					return;
				}

				_onUrlChanged(url);
				decisionHandler(WKNavigationActionPolicy.Allow);
			}
		}

		public class CustomScriptMessageHandler : NSObject, IWKScriptMessageHandler
		{
			private readonly CustomWebViewHandler _handler;

			public CustomScriptMessageHandler(CustomWebViewHandler handler)
			{
				_handler = handler;
			}

			public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
			{
				if (message.Body.ToString() == "contentChanged")
				{
					MainThread.BeginInvokeOnMainThread(async () =>
					{
						await _handler.OnPageDataChangedAsync();
					});
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

		public partial async Task InjectJavaScriptAsync(string script)
		{
			await PlatformView.EvaluateJavaScriptAsync(script);
		}
	}

	public class CustomScriptMessageHandler : NSObject, IWKScriptMessageHandler
	{
		private readonly CustomWebViewHandler _handler;

		public CustomScriptMessageHandler(CustomWebViewHandler handler)
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
		private readonly CustomWebViewHandler _handler;

		public CustomNavigationDelegate(CustomWebViewHandler handler)
		{
			_handler = handler;
		}

		public override async void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
		{
			Console.WriteLine($"Navigated to: {webView.Url?.AbsoluteString}");
			await _handler.OnPageDataChangedAsync();
		}

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
