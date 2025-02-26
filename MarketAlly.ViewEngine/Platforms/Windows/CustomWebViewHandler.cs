using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Text.Json;

namespace MarketAlly.Maui.ViewEngine
{
	public partial class WebViewHandler
	{
		protected override async void ConnectHandler(Microsoft.UI.Xaml.Controls.WebView2 platformView)
		{
			base.ConnectHandler(platformView);

			try
			{
				if (platformView.CoreWebView2 == null)
				{
					await platformView.EnsureCoreWebView2Async();
				}
				// Now it's safe to attach event handlers
				platformView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
				platformView.CoreWebView2.DownloadStarting += OnDownloadStarting;

				// Track the last URL to detect changes
				string lastUrl = string.Empty;

				// Monitor source URL changes
				platformView.CoreWebView2.SourceChanged += async (sender, args) =>
				{
					var currentUrl = platformView.CoreWebView2.Source;
					Console.WriteLine($"Source changed to: {currentUrl}");

					if (currentUrl != lastUrl)
					{
						lastUrl = currentUrl;
						Console.WriteLine("URL changed, updating page data...");

						// Give the page a moment to update its content
						await Task.Delay(500);
						await OnPageDataChangedAsync();
					}
				};

				// Setup message handler for content changes
				platformView.CoreWebView2.WebMessageReceived += async (sender, args) =>
				{
					Console.WriteLine($"Received content change message");
					await OnPageDataChangedAsync();
				};

				// Inject the content monitoring script
				await platformView.CoreWebView2.ExecuteScriptAsync(@"
            (function() {
                function notifyContentChange() {
                    chrome.webview.postMessage('contentChanged');
                }

                // Monitor DOM changes
                const observer = new MutationObserver((mutations) => {
                    // Look for significant changes
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


				// Monitor source URL changes
				platformView.CoreWebView2.SourceChanged += async (sender, args) =>
				{
					var currentUrl = platformView.CoreWebView2.Source;
					Console.WriteLine($"Source changed to: {currentUrl}");

					if (currentUrl != lastUrl)
					{
						lastUrl = currentUrl;
						Console.WriteLine("URL changed, updating page data...");
						await Task.Delay(500);
						await OnPageDataChangedAsync();
					}
				};

				// Add pre-navigation check
				platformView.CoreWebView2.NavigationStarting += async (sender, args) =>
				{
					var url = args.Uri;
					Console.WriteLine($"Navigation Starting to: {url}");

					if (IsPotentialPdfUrl(url))
					{
						Console.WriteLine("PDF detected, reading content in parallel...");
						// Don't cancel navigation, just process PDF in parallel
						_ = Task.Run(async () => await HandlePotentialPdfUrl(url));
					}
				};

				// Still keep navigation events for regular page loads
				platformView.CoreWebView2.NavigationCompleted += async (sender, args) =>
				{
					Console.WriteLine($"Navigation Completed: {sender.Source}");
					await OnPageDataChangedAsync();
				};

				// Set the User-Agent
				SetUserAgent(VirtualView?.UserAgent);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in ConnectHandler: {ex}");
			}
		}

		public async Task ForcePageDataUpdate()
		{
			try
			{
				Console.WriteLine("Manually triggering page data update...");
				if (PlatformView?.CoreWebView2 != null)
				{
					await PlatformView.CoreWebView2.ExecuteScriptAsync(@"
                console.log('Manual update triggered');
                chrome.webview.postMessage(JSON.stringify({
                    type: 'contentChanged',
                    url: window.location.href,
                    trigger: 'manual'
                }));
            ");
				}
				else
				{
					Console.WriteLine("CoreWebView2 not available");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in ForcePageDataUpdate: {ex}");
			}
		}

		public partial async Task InjectJavaScriptAsync(string script)
		{
			await PlatformView.CoreWebView2.ExecuteScriptAsync(script);
		}

		private void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
		{
			sender.Navigate(args.Uri);
			args.Handled = true;
		}

		private async void OnDownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
		{
			Console.WriteLine($"Download starting: {args.DownloadOperation.Uri}");  // Add this line
			var url = args.DownloadOperation.Uri;

			if (IsPotentialPdfUrl(url))
			{
				Console.WriteLine("PDF download detected");  // Add this line
				args.Handled = true;
				await HandlePotentialPdfUrl(url);
			}
			else if (args.ResultFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("PDF file extension detected");  // Add this line
				args.Handled = true;
				await HandlePotentialPdfUrl(url);
			}
			else
			{
				Console.WriteLine($"Non-PDF download: {args.ResultFilePath}");  // Add this line
				Process.Start(new ProcessStartInfo(args.ResultFilePath) { UseShellExecute = true });
				args.Cancel = false;
			}
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

			try
			{
				string result = await PlatformView.CoreWebView2.ExecuteScriptAsync(PageDataExtractor.ExtractScript);

				if (!string.IsNullOrWhiteSpace(result))
				{
					result = result.Trim('"')
							   .Replace("\\\"", "\"")
							   .Replace("\\n", " ")
							   .Replace("\\r", " ")
							   .Replace("\\t", " ")
							   .Replace("\\\\", "\\");

					var rawData = JsonSerializer.Deserialize<PageRawData>(result, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});

					return new PageData
					{
						Title = rawData.Title,
						Body = DecodeBase64(rawData.Html),
						Url = rawData.Url // Include URL in the response
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
