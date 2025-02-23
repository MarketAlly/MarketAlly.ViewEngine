using WebKit;

namespace MarketAlly.ViewEngine
{
	public partial class CustomWebViewHandler
	{
		protected override void ConnectHandler(WKWebView platformView)
		{
			base.ConnectHandler(platformView);
			platformView.Configuration.Preferences.JavaScriptEnabled = true;
			platformView.Configuration.Preferences.JavaScriptCanOpenWindowsAutomatically = true;
		}

		public void SetUserAgent(string userAgent)
		{
			PlatformView.CustomUserAgent = !string.IsNullOrEmpty(userAgent) ? userAgent :
				"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
		}
	}
}
