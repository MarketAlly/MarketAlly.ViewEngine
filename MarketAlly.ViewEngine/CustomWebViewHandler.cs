using Microsoft.Maui.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MarketAlly.ViewEngine
{
	public partial class CustomWebViewHandler : WebViewHandler
	{
		public CustomWebViewHandler() : base(Mapper) { }

		public event EventHandler<PageData> PageDataChanged;

		// Method to trigger the event
		public async Task OnPageDataChangedAsync()
		{
			var pageData = await GetPageDataAsync();
			PageDataChanged?.Invoke(this, pageData);
		}

		public static new IPropertyMapper<CustomWebView, CustomWebViewHandler> Mapper =
			new PropertyMapper<CustomWebView, CustomWebViewHandler>(WebViewHandler.Mapper)
			{
				[nameof(CustomWebView.UserAgent)] = MapUserAgent
			};

		public static void MapUserAgent(CustomWebViewHandler handler, CustomWebView view)
		{
			handler.SetUserAgent(view.UserAgent);
		}

		/// <summary>
		/// Extracts key webpage details (title, body text, metadata) from the currently loaded WebView.
		/// </summary>
		public async Task<PageData> GetPageDataAsync()
		{
			return await ExtractPageDataAsync();
		}

		/// <summary>
		/// Platform-specific method to execute JavaScript and extract page data.
		/// </summary>
		public partial Task<PageData> ExtractPageDataAsync();

		public string DecodeBase64(string base64String)
		{
			try
			{
				byte[] bytes = Convert.FromBase64String(base64String);
				return Encoding.UTF8.GetString(bytes);
			}
			catch
			{
				return "Failed to decode HTML content.";
			}
		}

		// Method to inject JavaScript into WebView
		public partial Task InjectJavaScriptAsync(string script);

		// Method to inject JavaScript that listens for URL changes
		public async Task InjectLocationChangeListener()
		{
			var script = @"
                (function() {
                    window.addEventListener('locationchange', function() {
                        window.external.notify('LocationChanged');
                    });
                })();
            ";
			await InjectJavaScriptAsync(script);
		}

	}

	/// <summary>
	/// Represents extracted webpage details.
	/// </summary>
	public class PageData
	{
		public string Title { get; set; }
		public string Body { get; set; }
		public string MetaDescription { get; set; }
	}
	public class PageRawData
	{
		public string Title { get; set; }
		public string Html { get; set; }
	}
}
