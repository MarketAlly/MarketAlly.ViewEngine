using MarketAlly.ViewEngine;

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

		private async void webView_Navigated(object sender, WebNavigatedEventArgs e)
		{
			var pageData = await webView.GetPageDataAsync();

			titleEntry.Text = pageData.Title;
			bodyEntry.Text = pageData.Body;
			Console.WriteLine($"Title: {pageData.Title}");
			Console.WriteLine($"Meta Description: {pageData.MetaDescription}");
			Console.WriteLine($"Body: {pageData.Body}");

			
		}
	}

}
