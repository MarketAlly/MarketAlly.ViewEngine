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
