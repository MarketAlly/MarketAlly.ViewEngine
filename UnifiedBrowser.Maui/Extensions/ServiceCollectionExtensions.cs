using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnifiedData.Maui;
using UnifiedData.Maui.Core;
using UnifiedData.Maui.Extensions;
using UnifiedRag.Maui.Core;
using UnifiedRag.Maui.Models;
using UnifiedRag.Maui.Extensions;
using UnifiedRag.Maui.Services;
using UnifiedBrowser.Maui.Controls;
using UnifiedBrowser.Maui.ViewModels;
using CommunityToolkit.Maui;
using DevExpress.Maui;

namespace UnifiedBrowser.Maui.Extensions
{
    /// <summary>
    /// Extension methods for configuring UnifiedBrowser services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds UnifiedBrowser with all required services using OpenAI
        /// </summary>
        public static MauiAppBuilder AddUnifiedBrowser(
            this MauiAppBuilder builder,
            string openAiApiKey,
            string modelName = "gpt-4o-mini",
            Action<UnifiedDataOptions>? configureData = null,
            Action<BrowserOptions>? configureBrowser = null)
        {
            // NOTE: DevExpress and CommunityToolkit must be initialized in the main app's MauiProgram.cs
            // immediately after .UseMauiApp<App>()
            // Example:
            // builder.UseMauiApp<App>()
            //        .UseDevExpress()
            //        .UseDevExpressCollectionView()
            //        .UseDevExpressControls()
            //        .UseDevExpressEditors()
            //        .UseMauiCommunityToolkit()

            // Configure UnifiedData
            builder.Services.AddUnifiedData(options =>
            {
                options.DatabaseName = "browser_data.db";
                options.VectorDimensions = 384;
                options.EnableVectorSearch = true;
                options.EnableHybridSearch = true;

                configureData?.Invoke(options);
            });

            // Configure UnifiedRag with OpenAI
            builder.Services.AddUnifiedRagWithOpenAI(
                dataOptions =>
                {
                    dataOptions.DatabaseName = "browser_data.db";
                    dataOptions.VectorDimensions = 384;
                    configureData?.Invoke(dataOptions);
                },
                apiKey: openAiApiKey,
                modelName: modelName
            );

            // Register browser services
            RegisterBrowserServices(builder.Services, configureBrowser);

            return builder;
        }

        /// <summary>
        /// Adds UnifiedBrowser with Anthropic Claude
        /// </summary>
        public static MauiAppBuilder AddUnifiedBrowserWithClaude(
            this MauiAppBuilder builder,
            string anthropicApiKey,
            string modelName = "claude-3-5-sonnet-20241022",
            Action<UnifiedDataOptions>? configureData = null,
            Action<BrowserOptions>? configureBrowser = null)
        {
            // NOTE: DevExpress and CommunityToolkit must be initialized in the main app

            // Configure UnifiedData
            builder.Services.AddUnifiedData(options =>
            {
                options.DatabaseName = "browser_data.db";
                options.VectorDimensions = 384;
                options.EnableVectorSearch = true;
                options.EnableHybridSearch = true;

                configureData?.Invoke(options);
            });

            // Configure UnifiedRag with Anthropic
            builder.Services.AddUnifiedRagWithAnthropic(
                dataOptions =>
                {
                    dataOptions.DatabaseName = "browser_data.db";
                    dataOptions.VectorDimensions = 384;
                    configureData?.Invoke(dataOptions);
                },
                apiKey: anthropicApiKey,
                modelName: modelName
            );

            // Register browser services
            RegisterBrowserServices(builder.Services, configureBrowser);

            return builder;
        }

        /// <summary>
        /// Adds UnifiedBrowser with Together.AI
        /// </summary>
        public static MauiAppBuilder AddUnifiedBrowserWithTogetherAI(
            this MauiAppBuilder builder,
            string togetherApiKey,
            string modelName = "meta-llama/Meta-Llama-3.1-70B-Instruct-Turbo",
            Action<UnifiedDataOptions>? configureData = null,
            Action<BrowserOptions>? configureBrowser = null)
        {
            // NOTE: DevExpress and CommunityToolkit must be initialized in the main app

            // Configure UnifiedData
            builder.Services.AddUnifiedData(options =>
            {
                options.DatabaseName = "browser_data.db";
                options.VectorDimensions = 384;
                options.EnableVectorSearch = true;
                options.EnableHybridSearch = true;

                configureData?.Invoke(options);
            });

            // Configure UnifiedRag with Together.AI
            builder.Services.AddUnifiedRagWithTogetherAI(
                dataOptions =>
                {
                    dataOptions.DatabaseName = "browser_data.db";
                    dataOptions.VectorDimensions = 384;
                    configureData?.Invoke(dataOptions);
                },
                apiKey: togetherApiKey,
                modelName: modelName
            );

            // Register browser services
            RegisterBrowserServices(builder.Services, configureBrowser);

            return builder;
        }

        /// <summary>
        /// Adds UnifiedBrowser with local LLM model
        /// </summary>
        public static MauiAppBuilder AddUnifiedBrowserWithLocalModel(
            this MauiAppBuilder builder,
            string modelPath,
            int maxTokens = 512,
            Action<UnifiedDataOptions>? configureData = null,
            Action<BrowserOptions>? configureBrowser = null)
        {
            // NOTE: DevExpress and CommunityToolkit must be initialized in the main app

            // Configure UnifiedData
            builder.Services.AddUnifiedData(options =>
            {
                options.DatabaseName = "browser_data.db";
                options.VectorDimensions = 384;
                options.EnableVectorSearch = true;
                options.EnableHybridSearch = true;

                configureData?.Invoke(options);
            });

            // Configure UnifiedRag with local model
            builder.Services.AddUnifiedRagWithLlamaAlly(
                dataOptions =>
                {
                    dataOptions.DatabaseName = "browser_data.db";
                    dataOptions.VectorDimensions = 384;
                    configureData?.Invoke(dataOptions);
                },
                modelPath: modelPath,
                maxTokens: maxTokens
            );

            // Register browser services
            RegisterBrowserServices(builder.Services, configureBrowser);

            return builder;
        }

        /// <summary>
        /// Adds UnifiedBrowser without AI features (browser only)
        /// </summary>
        public static MauiAppBuilder AddUnifiedBrowserBasic(
            this MauiAppBuilder builder,
            Action<BrowserOptions>? configureBrowser = null)
        {
            // NOTE: DevExpress and CommunityToolkit must be initialized in the main app

            // Configure minimal UnifiedData (required for save functionality)
            builder.Services.AddUnifiedData(options =>
            {
                options.DatabaseName = "browser_data.db";
                options.EnableVectorSearch = false; // No AI features
            });

            // Register null RAG service for basic browser
            builder.Services.AddSingleton<IRAGService, NullRAGService>();

            // Register browser services
            RegisterBrowserServices(builder.Services, configureBrowser);

            return builder;
        }

        /// <summary>
        /// Adds UnifiedBrowser with custom provider configuration
        /// </summary>
        public static MauiAppBuilder AddUnifiedBrowserCustom(
            this MauiAppBuilder builder,
            Action<IServiceCollection> configureServices,
            Action<BrowserOptions>? configureBrowser = null)
        {
            // NOTE: DevExpress and CommunityToolkit must be initialized in the main app

            // Let consumer configure their own services
            configureServices(builder.Services);

            // Register browser services
            RegisterBrowserServices(builder.Services, configureBrowser);

            return builder;
        }

        /// <summary>
        /// Registers browser-specific services
        /// </summary>
        private static void RegisterBrowserServices(
            IServiceCollection services,
            Action<BrowserOptions>? configureBrowser)
        {
            // Configure browser options
            var browserOptions = new BrowserOptions();
            configureBrowser?.Invoke(browserOptions);
            services.AddSingleton(browserOptions);

            // Register browser view model
            services.AddTransient<BrowserViewModel>();

            // Register browser control with DI support
            services.AddTransient<UnifiedBrowserControl>(serviceProvider =>
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
        }
    }

    /// <summary>
    /// Interface for pages that contain the browser
    /// </summary>
    public interface IBrowserPage
    {
        UnifiedBrowserControl BrowserControl { get; }
    }

    /// <summary>
    /// Null implementation of IRAGService for basic browser without AI
    /// </summary>
    internal class NullRAGService : IRAGService
    {
        public event EventHandler<ConversationUpdatedEventArgs>? ConversationUpdated;
        public event EventHandler<ConversationSummarizedEventArgs>? ConversationSummarized;
        public event EventHandler<ToolExecutedEventArgs>? ToolExecuted;
        public event EventHandler<StatusUpdateEventArgs>? StatusUpdated;

        public Task<RAGResponse> AskAsync(string question, RAGOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RAGResponse
            {
                Answer = "AI features are not configured.",
                Sources = new List<RAGSource>()
            });
        }

        public async IAsyncEnumerable<string> AskStreamAsync(string question, RAGOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return "AI features are not configured.";
        }

        public Task<RAGResponse> AskWithConversationAsync(string question, string conversationId, string? parentId = null, RAGOptions? options = null, CancellationToken cancellationToken = default)
        {
            return AskAsync(question, options, cancellationToken);
        }

        public async IAsyncEnumerable<string> AskStreamWithConversationAsync(string question, string conversationId, string? parentId = null, RAGOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return "AI features are not configured.";
        }

        public void ClearConversation()
        {
            // No-op
        }

        public void ClearConversation(string conversationId)
        {
            // No-op
        }

        public Task<UnifiedDocument?> GetConversation(string conversationId)
        {
            return Task.FromResult<UnifiedDocument?>(null);
        }

        public Task<List<ConversationTurn>> GetConversationHistoryAsync(string conversationId)
        {
            return Task.FromResult(new List<ConversationTurn>());
        }

        public Task<List<UnifiedDocument>> GetConversationTreeAsync(string conversationId, bool includeDeleted = false)
        {
            return Task.FromResult(new List<UnifiedDocument>());
        }

        public Task<List<UnifiedDocument>> GetConversationPathAsync(string rootId, string nodeId, bool includeDeleted = false, DynamicRAGService.SiblingMode siblingMode = DynamicRAGService.SiblingMode.None)
        {
            return Task.FromResult(new List<UnifiedDocument>());
        }

        public Task<string> SummarizeConversationAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("No conversation to summarize.");
        }

        public Task SaveConversationToDocumentAsync(UnifiedDocument conversation, RAGOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<List<UnifiedDocument>> LoadConversationFromDocumentAsync(string documentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<UnifiedDocument>());
        }

        public Task<UnifiedDocument> ForkConversationAsync(string sourceConversationId, int upToTurnIndex, string? newConversationId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UnifiedDocument
            {
                Id = newConversationId ?? Guid.NewGuid().ToString(),
                Title = "Forked Conversation",
                Content = string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task<WebpageSummaryResult> SummarizeWebpageAsync(
            string url,
            string title,
            string pageBody,
            RAGOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WebpageSummaryResult
            {
                Title = title,
                Url = url,
                DetailedSummary = "AI features are not configured.",
                Brief = "AI features are not configured.",
                Topics = new List<WebpageTopic>()
            });
        }
    }
}