using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using UnifiedBrowser.Maui.Extensions;
using UnifiedBrowser.Maui.Controls;
using UnifiedBrowser.Maui.ViewModels;
using UnifiedChat.Maui.Extensions;
using UnifiedChat.Maui.Controls;
using UnifiedChat.Maui.ViewModels;
using UnifiedData.Maui;
using UnifiedData.Maui.Core;
using UnifiedData.Maui.Extensions;
using UnifiedData.Maui.Embeddings;
using UnifiedData.Maui.Services;
using UnifiedData.Maui.Storage;
using UnifiedRag.Maui.Core;
using UnifiedRag.Maui.Extensions;
using UnifiedRag.Maui.Services;
using DevExpress.Maui;
using CommunityToolkit.Maui;
using MarketAlly.Maui.ViewEngine;
using Microsoft.Extensions.Options;
using Slyv.Maui.Services;

namespace Slyv.Maui
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.UseDevExpress()
				.UseDevExpressCollectionView()
				.UseDevExpressControls()
				.UseDevExpressEditors()
				// CommunityToolkit must also be initialized here
				.UseMauiCommunityToolkit()
				.UseUnifiedBrowser(options =>
				{
					options.DefaultHomeUrl = "https://www.google.com";
					options.EnableAISummarization = true;
					options.AutoGenerateSummary = false;
					options.ShowAddressBar = true;
					options.ShowLinksButton = true;
					options.ShowMarkdownButton = true;
					options.ShowShareButton = true;
					options.ShowSaveButton = true;
					options.EnableRouteExtraction = true;
					options.NormalizeRoutes = true;
					options.AutoLoadHomePage = true;
				})
				.UseUnifiedChat(options =>
				{
					options.EnableStreaming = true;
					options.DefaultSystemPrompt = "You are a helpful AI assistant.";
					options.MaxTokens = 4096;
					options.Temperature = 0.6;

					options.ShowMetrics = false;
					options.ShowUsage = false;
					options.ShowStreaming = false;
					options.IsTab = false;
				})
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				});

			// Configure additional services (UnifiedData, UnifiedRag, etc.)
			ConfigureAdditionalServices(builder);

#if DEBUG
			builder.Logging.AddDebug();
#endif

			return builder.Build();
		}

		private static void ConfigureAdditionalServices(MauiAppBuilder builder)
		{
			var dbPath = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "slyv_browser.db");

			// Add configuration for RAG services
			builder.Services.AddSingleton<IConfiguration>(sp =>
			{
				var config = new ConfigurationBuilder()
					.AddInMemoryCollection(new Dictionary<string, string>
					{
						["LLMProvider"] = "openai",
						["Models:OpenAI"] = "gpt-4o-mini",
						["Models:Anthropic"] = "claude-3-5-sonnet-20241022",
						["LocalModel:Path"] = "models/llama.gguf",
						["RAG:MaxTokens"] = "1024",
						["RAG:Temperature"] = "0.7"
					})
					.Build();
				return config;
			});

			// Configure UnifiedData with dynamic embedding support
			builder.Services.AddUnifiedData(options =>
			{
				options.DatabaseName = dbPath;
				options.EnableVectorSearch = true;
				options.EnableHybridSearch = true;
				options.UseChunking = false;
				options.EnableDebugLogging = true;
				options.AutoDownloadModels = false;
				options.EmbeddingProviderType = "custom"; // Will be overridden by DynamicEmbeddingProvider
				options.VectorDimensions = 1536; // Max dimensions to support OpenAI
			});

			// Replace the IEmbeddingProvider with DynamicEmbeddingProvider for settings-based switching
			var embeddingDescriptor = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(IEmbeddingProvider));
			if (embeddingDescriptor != null)
			{
				builder.Services.Remove(embeddingDescriptor);
			}
			builder.Services.AddSingleton<DynamicEmbeddingProvider>();
			builder.Services.AddSingleton<IEmbeddingProvider>(sp => sp.GetRequiredService<DynamicEmbeddingProvider>());

			// Replace IUnifiedDataService with SettingsAwareDataService wrapper
			var originalDescriptor = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(IUnifiedDataService));
			if (originalDescriptor != null)
			{
				// Remove ALL existing registrations that depend on IUnifiedDataService
				var trendAnalyzerDesc = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(UnifiedData.Maui.Core.ITopicTrendAnalyzer));
				if (trendAnalyzerDesc != null)
					builder.Services.Remove(trendAnalyzerDesc);

				builder.Services.Remove(originalDescriptor);

				// Register the real service implementation (not exposed as interface)
				builder.Services.AddSingleton<UnifiedDataService>(sp =>
					new UnifiedDataService(
						sp.GetRequiredService<IVectorStorageProvider>(),
						sp.GetRequiredService<ITextStorageProvider>(),
						sp.GetRequiredService<IEmbeddingProvider>(),
						sp.GetRequiredService<ITextChunker>(),
						sp.GetRequiredService<IOptions<UnifiedDataOptions>>(),
						sp.GetRequiredService<ILogger<UnifiedDataService>>()
					));

				// Register the wrapper as IUnifiedDataService
				// Note: SettingsAwareDataService requires IAppSettingsService which we'll add below
				builder.Services.AddSingleton<IUnifiedDataService>(sp =>
					new SettingsAwareDataService(
						sp.GetRequiredService<UnifiedDataService>(),
						sp.GetRequiredService<IAppSettingsService>(),
						sp.GetRequiredService<IOptions<UnifiedDataOptions>>(),
						sp.GetRequiredService<IEmbeddingProvider>()));

				// Re-register ITopicTrendAnalyzer with the wrapped service
				builder.Services.AddTransient<UnifiedData.Maui.Core.ITopicTrendAnalyzer>(sp =>
					new UnifiedData.Maui.Services.TopicTrendAnalyzer(
						sp.GetRequiredService<IUnifiedDataService>()));
			}

			// Add settings services (similar to ChatAI.Maui)
			builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
			builder.Services.AddSingleton<SettingsAdapter>();
			builder.Services.AddSingleton<UnifiedRag.Maui.Core.IProviderSettings>(sp => sp.GetRequiredService<SettingsAdapter>());

			// Add core RAG services
			builder.Services.AddSingleton<UnifiedRag.Maui.Context.IContextManager, UnifiedRag.Maui.Context.ContextManager>();
			builder.Services.AddSingleton<UnifiedRag.Maui.Services.IDynamicProviderFactory, UnifiedRag.Maui.Services.DynamicProviderFactory>();

			// Add AIPlugin framework services for tool calling
			builder.Services.AddSingleton<MarketAlly.AIPlugin.Security.SecurityConfiguration>(sp =>
				MarketAlly.AIPlugin.Security.SecurityConfiguration.CreateDefault());
			builder.Services.AddSingleton<MarketAlly.AIPlugin.Monitoring.AuditLogger>();

			// Register Dynamic AIPluginRegistry that updates based on settings
			builder.Services.AddSingleton<DynamicPluginRegistry>();
			builder.Services.AddSingleton<MarketAlly.AIPlugin.IAIPluginRegistry>(sp =>
				sp.GetRequiredService<DynamicPluginRegistry>());

			// Add metrics collector for monitoring
			builder.Services.AddSingleton<UnifiedRag.Maui.Monitoring.IRAGMetricsCollector,
				UnifiedRag.Maui.Monitoring.InMemoryRAGMetricsCollector>();

			// Add usage tracker (persistent storage)
			builder.Services.AddSingleton<UnifiedRag.Maui.Core.IUsageTracker>(sp =>
			{
				var dataService = sp.GetRequiredService<UnifiedData.Maui.Core.IUnifiedDataService>();
				var logger = sp.GetService<ILogger<UnifiedRag.Maui.Services.PersistentUsageTracker>>();
				var tracker = new UnifiedRag.Maui.Services.PersistentUsageTracker(dataService, logger);
				tracker.SetMonthlyLimit(0); // 0 = unlimited
				return tracker;
			});

			// Register DynamicRAGService directly (with tool support via IAIPluginRegistry)
			builder.Services.AddSingleton<UnifiedRag.Maui.Services.DynamicRAGService>(sp =>
			{
				var pluginRegistry = sp.GetRequiredService<MarketAlly.AIPlugin.IAIPluginRegistry>();
				System.Diagnostics.Debug.WriteLine($"DynamicRAGService: Plugin registry is {(pluginRegistry == null ? "NULL" : "available")}");

				return new UnifiedRag.Maui.Services.DynamicRAGService(
					sp.GetRequiredService<UnifiedData.Maui.Core.IUnifiedDataService>(),
					sp.GetRequiredService<UnifiedRag.Maui.Services.IDynamicProviderFactory>(),
					sp.GetRequiredService<UnifiedRag.Maui.Context.IContextManager>(),
					sp.GetRequiredService<ILogger<UnifiedRag.Maui.Services.DynamicRAGService>>(),
					sp.GetService<UnifiedRag.Maui.Core.IUsageTracker>(),
					sp.GetService<UnifiedRag.Maui.Core.ISummarizationService>(),
					pluginRegistry); // Required plugin registry
			});

			// Register as IRAGService (delegates to DynamicRAGService)
			builder.Services.AddSingleton<UnifiedRag.Maui.Core.IRAGService>(sp =>
				sp.GetRequiredService<UnifiedRag.Maui.Services.DynamicRAGService>());

			// Register as IRAGClassificationService (delegates to DynamicRAGService)
			builder.Services.AddSingleton<UnifiedRag.Maui.Core.IRAGClassificationService>(sp =>
				sp.GetRequiredService<UnifiedRag.Maui.Services.DynamicRAGService>());

			// Register UnifiedBrowserControl with dependencies
			builder.Services.AddTransient<UnifiedBrowser.Maui.Controls.UnifiedBrowserControl>(sp =>
				new UnifiedBrowser.Maui.Controls.UnifiedBrowserControl(
					sp.GetService<IUnifiedDataService>(),
					sp.GetService<UnifiedRag.Maui.Core.IRAGService>(),
					sp.GetService<ILogger<UnifiedBrowser.Maui.ViewModels.BrowserViewModel>>()));

			// Register UnifiedChatControl with dependencies
			builder.Services.AddTransient<UnifiedChat.Maui.Controls.UnifiedChatControl>(sp =>
				new UnifiedChat.Maui.Controls.UnifiedChatControl(
					sp.GetService<IUnifiedDataService>(),
					sp.GetService<UnifiedRag.Maui.Core.IRAGService>(),
					sp));

			// Register AppShell and views as Singletons to persist state across navigation
			builder.Services.AddSingleton<AppShell>();
			builder.Services.AddSingleton<Views.Overview>();
			builder.Services.AddSingleton<Views.BrowserPage>();
			builder.Services.AddSingleton<Views.ChatPage>();
			builder.Services.AddSingleton<Views.SettingsPage>();
			builder.Services.AddTransient<ViewModels.SettingsViewModel>();
		}
	}
}
