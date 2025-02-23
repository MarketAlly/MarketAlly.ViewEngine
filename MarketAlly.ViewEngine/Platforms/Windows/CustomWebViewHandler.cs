using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Text.Json;

namespace MarketAlly.ViewEngine
{
	public partial class CustomWebViewHandler
	{
		protected override async void ConnectHandler(Microsoft.UI.Xaml.Controls.WebView2 platformView)
		{
			base.ConnectHandler(platformView);

			if (platformView.CoreWebView2 == null)
			{
				// Wait for CoreWebView2 Initialization
				await platformView.EnsureCoreWebView2Async();
			}

			// Now it's safe to attach event handlers
			platformView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
			platformView.CoreWebView2.DownloadStarting += OnDownloadStarting;

			// Now that CoreWebView2 is available, set the User-Agent
			SetUserAgent(VirtualView?.UserAgent);
		}

		private void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
		{
			sender.Navigate(args.Uri);
			args.Handled = true;
		}

		private void OnDownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
		{
			Process.Start(new ProcessStartInfo(args.ResultFilePath) { UseShellExecute = true });
			args.Cancel = false;
		}

		public async void SetUserAgent(string userAgent)
		{
			if (PlatformView.CoreWebView2 == null)
			{
				// Wait for initialization
				await PlatformView.EnsureCoreWebView2Async();
			}

			// Now set the User-Agent
			PlatformView.CoreWebView2.Settings.UserAgent = userAgent ??
				"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
		}

		public async partial Task<PageData> ExtractPageDataAsync()
		{
			if (PlatformView?.CoreWebView2 == null)
			{
				await PlatformView.EnsureCoreWebView2Async();
			}

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

			try
			{
				string result = await PlatformView.CoreWebView2.ExecuteScriptAsync(script);

				if (!string.IsNullOrWhiteSpace(result))
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

					return new PageData
					{
						Title = rawData.Title,
						Body = DecodeBase64(rawData.Html)
					};
				}
			}
			catch (JsonException ex)
			{
				return new PageData { Title = "error", Body = $"Failed to parse page content: {ex.Message}" };
			}

			return new PageData { Title = "no content", Body = "Page content could not be retrieved." };
		}

	}
}
