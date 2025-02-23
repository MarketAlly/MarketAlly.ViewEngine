namespace MarketAlly.ViewEngine
{
	public class CustomWebView : WebView
	{
		public static readonly BindableProperty UserAgentProperty =
			BindableProperty.Create(nameof(UserAgent), typeof(string), typeof(CustomWebView), default(string));

		public string UserAgent
		{
			get => (string)GetValue(UserAgentProperty);
			set => SetValue(UserAgentProperty, value);
		}
	}
}
