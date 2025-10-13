using Microsoft.Maui.Hosting;

namespace MarketAlly.Maui.ViewEngine
{
	/// <summary>
	/// Extension methods for configuring MarketAlly.Maui.ViewEngine in MauiAppBuilder.
	/// </summary>
	public static class MauiAppBuilderExtensions
	{
		/// <summary>
		/// Registers the custom WebView handler for MarketAlly.Maui.ViewEngine.
		/// Call this method in your MauiProgram.cs to enable the custom WebView control.
		/// </summary>
		/// <param name="builder">The MauiAppBuilder instance.</param>
		/// <returns>The MauiAppBuilder for chaining.</returns>
		/// <example>
		/// <code>
		/// var builder = MauiApp.CreateBuilder();
		/// builder
		///     .UseMauiApp&lt;App&gt;()
		///     .UseMarketAllyViewEngine(); // Register the custom WebView handler
		/// </code>
		/// </example>
		public static MauiAppBuilder UseMarketAllyViewEngine(this MauiAppBuilder builder)
		{
			builder.ConfigureMauiHandlers(handlers =>
			{
				handlers.AddHandler(typeof(WebView), typeof(WebViewHandler));
			});

			return builder;
		}
	}
}
