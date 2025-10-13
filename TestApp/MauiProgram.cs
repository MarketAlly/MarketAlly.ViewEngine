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
					})
					.UseMarketAllyViewEngine(); // Register Custom WebView Handler

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
