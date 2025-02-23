using Android.Webkit;
using Microsoft.Maui.Handlers;

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
			settings.DatabaseEnabled = true;
			settings.SetAppCacheEnabled(true);
			settings.MixedContentMode = MixedContentMode.AlwaysAllow;

			CookieManager.Instance.SetAcceptCookie(true);
			CookieManager.Instance.SetAcceptThirdPartyCookies(platformView, true);
		}

		public void SetUserAgent(string userAgent)
		{
			PlatformView.Settings.UserAgentString = !string.IsNullOrEmpty(userAgent) ? userAgent :
				"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
		}
	}
}
