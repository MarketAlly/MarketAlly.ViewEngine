using Microsoft.UI.Xaml.Controls;

namespace MarketAlly.ViewEngine
{
	public partial class CustomWebViewHandler
	{
		protected override void ConnectHandler(WebView2 platformView)
		{
			base.ConnectHandler(platformView);
			platformView.CoreWebView2.Settings.IsScriptEnabled = true;
		}

		public void SetUserAgent(string userAgent)
		{
			PlatformView.CoreWebView2.Settings.UserAgent = userAgent ??
				"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
		}
	}
}
