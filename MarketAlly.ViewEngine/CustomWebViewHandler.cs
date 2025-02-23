using Microsoft.Maui.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
	}
}
