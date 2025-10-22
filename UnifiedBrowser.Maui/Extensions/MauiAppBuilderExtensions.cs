using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;
using UnifiedBrowser.Maui.Controls;
using UnifiedBrowser.Maui.ViewModels;
using UnifiedData.Maui.Core;
using UnifiedRag.Maui.Core;
using MarketAlly.Maui.ViewEngine;
using Maui.TouchEffect.Hosting;

namespace UnifiedBrowser.Maui.Extensions
{
    /// <summary>
    /// Extension methods for configuring UnifiedBrowser in a MAUI application
    /// </summary>
    public static class MauiAppBuilderExtensions
    {
        /// <summary>
        /// Registers UnifiedBrowser services and handlers with the MAUI application
        /// </summary>
        /// <param name="builder">The MAUI app builder</param>
        /// <param name="configureOptions">Optional action to configure browser options</param>
        /// <returns>The MAUI app builder for chaining</returns>
        public static MauiAppBuilder UseUnifiedBrowser(
            this MauiAppBuilder builder,
            Action<BrowserOptions>? configureOptions = null)
        {
            // Register the ViewEngine only if not already registered (idempotent)
            if (!builder.Services.Any(d => d.ImplementationType?.FullName?.Contains("ViewEngine") == true))
            {
                builder.UseMarketAllyViewEngine();
            }

            // Register TouchEffect only once across all control libraries
            // Uses builder.Services as shared state to prevent duplicate registration
            builder.RegisterTouchEffectOnce();

			// Configure browser options
			var browserOptions = new BrowserOptions();
            configureOptions?.Invoke(browserOptions);
            builder.Services.AddSingleton(browserOptions);

            // Register browser view model
            builder.Services.AddSingleton<BrowserViewModel>();

            // Register browser control as Singleton to persist state
            builder.Services.AddSingleton<UnifiedBrowserControl>(serviceProvider =>
            {
                var dataService = serviceProvider.GetService<IUnifiedDataService>();
                var ragService = serviceProvider.GetService<IRAGService>();
                var logger = serviceProvider.GetService<ILogger<BrowserViewModel>>();

                return new UnifiedBrowserControl(dataService, ragService, logger)
                {
                    HomeUrl = browserOptions.DefaultHomeUrl,
                    EnableAISummarization = browserOptions.EnableAISummarization,
                    AutoGenerateSummary = browserOptions.AutoGenerateSummary,
                    ShowAddressBar = browserOptions.ShowAddressBar,
                    ShowLinksButton = browserOptions.ShowLinksButton,
                    ShowMarkdownButton = browserOptions.ShowMarkdownButton,
                    ShowShareButton = browserOptions.ShowShareButton,
                    ShowSaveButton = browserOptions.ShowSaveButton,
                    EnableRouteExtraction = browserOptions.EnableRouteExtraction,
                    NormalizeRoutes = browserOptions.NormalizeRoutes,
                    AutoLoadHomePage = browserOptions.AutoLoadHomePage,
                    UserAgent = browserOptions.UserAgent
                };
            });

            return builder;
        }

        /// <summary>
        /// Registers UnifiedBrowser services and handlers with custom service configuration
        /// </summary>
        /// <param name="builder">The MAUI app builder</param>
        /// <param name="configureOptions">Optional action to configure browser options</param>
        /// <param name="configureServices">Action to configure additional services</param>
        /// <returns>The MAUI app builder for chaining</returns>
        public static MauiAppBuilder UseUnifiedBrowser(
            this MauiAppBuilder builder,
            Action<BrowserOptions>? configureOptions,
            Action<IServiceCollection> configureServices)
        {
            // Use the base registration
            builder.UseUnifiedBrowser(configureOptions);

			// Allow additional service configuration
			configureServices?.Invoke(builder.Services);

            return builder;
        }
    }

    /// <summary>
    /// Configuration options for the UnifiedBrowser
    /// </summary>
    public class BrowserOptions
    {
        /// <summary>
        /// The default home URL for the browser
        /// </summary>
        public string DefaultHomeUrl { get; set; } = "https://www.google.com";

        /// <summary>
        /// Whether to enable AI summarization features
        /// </summary>
        public bool EnableAISummarization { get; set; } = true;

        /// <summary>
        /// Whether to automatically generate summaries when pages load
        /// </summary>
        public bool AutoGenerateSummary { get; set; } = false;

        /// <summary>
        /// Whether to show the address bar
        /// </summary>
        public bool ShowAddressBar { get; set; } = true;

        /// <summary>
        /// Whether to show the links extraction button
        /// </summary>
        public bool ShowLinksButton { get; set; } = true;

        /// <summary>
        /// Whether to show the markdown view button
        /// </summary>
        public bool ShowMarkdownButton { get; set; } = true;

        /// <summary>
        /// Whether to show the share button
        /// </summary>
        public bool ShowShareButton { get; set; } = true;

        /// <summary>
        /// Whether to show the save button
        /// </summary>
        public bool ShowSaveButton { get; set; } = true;

        /// <summary>
        /// Whether to enable route extraction on page load
        /// </summary>
        public bool EnableRouteExtraction { get; set; } = true;

        /// <summary>
        /// Whether to normalize routes (remove tracking parameters)
        /// </summary>
        public bool NormalizeRoutes { get; set; } = true;

        /// <summary>
        /// Whether to automatically load the home page on startup
        /// </summary>
        public bool AutoLoadHomePage { get; set; } = true;

        /// <summary>
        /// Custom user agent string
        /// </summary>
        public string? UserAgent { get; set; }
    }
}