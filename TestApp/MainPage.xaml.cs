using MarketAlly.Maui.ViewEngine;

namespace TestApp
{
	public partial class MainPage : ContentPage
	{
		int count = 0;

		public MainPage()
		{
			InitializeComponent();
		}

		private async void urlEntry_Completed(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(urlEntry.Text))
			{
				string enteredUrl = urlEntry.Text.Trim();

				// Ensure it starts with http/https for WebView
				if (!enteredUrl.StartsWith("http://") && !enteredUrl.StartsWith("https://"))
				{
					enteredUrl = "https://" + enteredUrl;
				}

				webView.Source = enteredUrl;
			}
		}

		private void webView_PageDataChanged(object sender, PageData e)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				titleEntry.Text = e.Title;
				bodyEntry.Text = e.Body;
			});
		}
	}

}
