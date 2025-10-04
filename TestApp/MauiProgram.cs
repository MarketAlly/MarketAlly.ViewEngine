using MarketAlly.Maui.ViewEngine;
using Microsoft.Extensions.Logging;

namespace TestApp
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			try
			{
				var builder = MauiApp.CreateBuilder();

				builder
					.UseMauiApp<App>()
					.ConfigureFonts(fonts =>
					{
						fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
						fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
					});

				// Register Custom WebView Handler
				builder.ConfigureMauiHandlers(handlers =>
				{
					handlers.AddHandler(typeof(MarketAlly.Maui.ViewEngine.WebView), typeof(MarketAlly.Maui.ViewEngine.WebViewHandler));
				});

#if DEBUG
				builder.Logging.AddDebug();
#endif

				var app = builder.Build();
				return app;
			}
			catch (Exception ex)
			{
				throw;
			}
		}
	}
}
