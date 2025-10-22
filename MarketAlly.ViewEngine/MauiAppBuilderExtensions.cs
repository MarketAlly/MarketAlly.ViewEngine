using Microsoft.Maui.Hosting;

namespace MarketAlly.Maui.ViewEngine
{
	/// <summary>
	/// Extension methods for configuring MarketAlly.Maui.ViewEngine in MauiAppBuilder.
	/// </summary>
	public static class MauiAppBuilderExtensions
	{
		/// <summary>
		/// Registers the custom WebView and PdfView handlers for MarketAlly.Maui.ViewEngine.
		/// Call this method in your MauiProgram.cs to enable the custom WebView and BrowserView controls.
		/// </summary>
		/// <param name="builder">The MauiAppBuilder instance.</param>
		/// <returns>The MauiAppBuilder for chaining.</returns>
		/// <example>
		/// <code>
		/// var builder = MauiApp.CreateBuilder();
		/// builder
		///     .UseMauiApp&lt;App&gt;()
		///     .UseMarketAllyViewEngine(); // Register the custom WebView and PdfView handlers
		/// </code>
		/// </example>
		public static MauiAppBuilder UseMarketAllyViewEngine(this MauiAppBuilder builder)
		{
			builder.ConfigureMauiHandlers(handlers =>
			{
				handlers.AddHandler(typeof(WebView), typeof(WebViewHandler));
#if ANDROID
				handlers.AddHandler(typeof(PdfView), typeof(Platforms.Android.PdfViewHandler));
#elif IOS
				handlers.AddHandler(typeof(PdfView), typeof(Platforms.iOS.PdfViewHandler));
#elif MACCATALYST
				handlers.AddHandler(typeof(PdfView), typeof(Platforms.MacCatalyst.PdfViewHandler));
#elif WINDOWS
				handlers.AddHandler(typeof(PdfView), typeof(Platforms.Windows.PdfViewHandler));
#endif
			});

			return builder;
		}

		// Keep UseMauiPdfView for backward compatibility if needed
		internal static MauiAppBuilder UseMauiPdfView(this MauiAppBuilder builder)
		{
			builder.ConfigureMauiHandlers((handlers) =>
			{
#if ANDROID
				handlers.AddHandler(typeof(PdfView), typeof(Platforms.Android.PdfViewHandler));
#elif IOS
				handlers.AddHandler(typeof(PdfView), typeof(Platforms.iOS.PdfViewHandler));
#elif MACCATALYST
				handlers.AddHandler(typeof(PdfView), typeof(Platforms.MacCatalyst.PdfViewHandler));
#elif WINDOWS
				handlers.AddHandler(typeof(PdfView), typeof(Platforms.Windows.PdfViewHandler));
#endif
			});
			return builder;
		}
	}
}
