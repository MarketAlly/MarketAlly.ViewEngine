using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using UnifiedData.Maui.Core;
using UnifiedData.Maui.Extensions;
using UnifiedData.Maui.Services;
using UnifiedRag.Maui.Core;
using UnifiedRag.Maui.Models;

namespace Slyv.Maui.Services
{
    /// <summary>
    /// Adapter that implements both IDataServiceSettings and IProviderSettings from IAppSettingsService
    /// </summary>
    public class SettingsAdapter : IDataServiceSettings, IProviderSettings
    {
        private readonly IAppSettingsService _appSettings;

        public SettingsAdapter(IAppSettingsService appSettings)
        {
            _appSettings = appSettings;
        }

        // IDataServiceSettings properties
        public bool EnableVectorSearch => _appSettings.EnableVectorSearch;
        public string? EmbeddingProvider => _appSettings.EmbeddingProvider;
        public string? EmbeddingModel => _appSettings.EmbeddingModel;
        public bool AutoDownloadEmbeddings => _appSettings.AutoDownloadEmbeddings;
        public bool UseChunking => _appSettings.UseChunking;
        public int ChunkSize => _appSettings.ChunkSize;
        public int ChunkOverlap => _appSettings.ChunkOverlap;

        // IProviderSettings properties
        public string? LLMProvider => _appSettings.LLMProvider;
        public string? OpenAIKey => _appSettings.OpenAIKey;
        public string? AnthropicKey => _appSettings.AnthropicKey;
        public string? TogetherAIKey => _appSettings.TogetherAIKey;
        public string? OpenAIModel => _appSettings.OpenAIModel;
        public string? AnthropicModel => _appSettings.AnthropicModel;
        public string? TogetherAIModel => _appSettings.TogetherAIModel;
        public string? LocalModelPath => _appSettings.LocalModelPath;
        public int MaxTokens => _appSettings.MaxTokens;
        public float Temperature => _appSettings.Temperature;

        public async Task LoadSettingsAsync()
        {
            await _appSettings.LoadSettingsAsync();
        }

        // IProviderSettings methods
        public async Task<ProviderSettings?> GetCurrentProviderSettingsAsync(CancellationToken cancellationToken = default)
        {
            await LoadSettingsAsync();

            return new ProviderSettings
            {
                LLMProvider = LLMProvider,
                OpenAIKey = OpenAIKey,
                AnthropicKey = AnthropicKey,
                TogetherAIKey = TogetherAIKey,
                OpenAIModel = OpenAIModel,
                AnthropicModel = AnthropicModel,
                TogetherAIModel = TogetherAIModel,
                LocalModelPath = LocalModelPath,
                MaxTokens = MaxTokens,
                Temperature = Temperature,
                EmbeddingProvider = EmbeddingProvider,
                EmbeddingModel = EmbeddingModel
            };
        }

        public async Task UpdateProviderSettingsAsync(ProviderSettings settings, CancellationToken cancellationToken = default)
        {
            // Update the underlying app settings
            _appSettings.LLMProvider = settings.LLMProvider;
            _appSettings.OpenAIKey = settings.OpenAIKey;
            _appSettings.AnthropicKey = settings.AnthropicKey;
            _appSettings.TogetherAIKey = settings.TogetherAIKey;
            _appSettings.OpenAIModel = settings.OpenAIModel;
            _appSettings.AnthropicModel = settings.AnthropicModel;
            _appSettings.TogetherAIModel = settings.TogetherAIModel;
            _appSettings.LocalModelPath = settings.LocalModelPath;
            _appSettings.MaxTokens = settings.MaxTokens;
            _appSettings.Temperature = settings.Temperature;
            _appSettings.EmbeddingProvider = settings.EmbeddingProvider;
            _appSettings.EmbeddingModel = settings.EmbeddingModel;

            // Save settings
            await _appSettings.SaveSettingsAsync();
        }
    }

    /// <summary>
    /// Factory that creates UnifiedDataService with dynamic configuration
    /// </summary>
    public class DynamicDataServiceFactory
    {
        private readonly IDataServiceSettings _settingsService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DynamicDataServiceFactory> _logger;
        private IUnifiedDataService? _currentService;
        private string? _currentConfigKey;
        private readonly string _dbPath;

        public DynamicDataServiceFactory(
            IDataServiceSettings settingsService,
            IServiceProvider serviceProvider,
            ILogger<DynamicDataServiceFactory> logger)
        {
            _settingsService = settingsService;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _dbPath = Path.Combine(FileSystem.AppDataDirectory, "chatai.db");
        }

        public async Task<IUnifiedDataService> GetServiceAsync()
        {
            await _settingsService.LoadSettingsAsync();

            // Create a key to detect configuration changes
            var configKey = $"{_settingsService.EnableVectorSearch}|{_settingsService.EmbeddingModel}|{_settingsService.UseChunking}";

            // Return existing service if configuration hasn't changed
            if (_currentService != null && _currentConfigKey == configKey)
            {
                return _currentService;
            }

            _logger.LogInformation("Creating new UnifiedDataService with updated configuration");

            try
            {
                // Dispose old service if it implements IDisposable
                if (_currentService is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                // Create a new service collection for this specific configuration
                var services = new ServiceCollection();

                // Copy logging from main container
                services.AddSingleton(_serviceProvider.GetRequiredService<ILoggerFactory>());
                services.AddLogging();

                // Add UnifiedData with specific configuration
                services.AddUnifiedData(options =>
                {
                    options.DatabaseName = _dbPath;
                    options.EnableVectorSearch = _settingsService.EnableVectorSearch;
                    options.EnableHybridSearch = _settingsService.EnableVectorSearch;
                    options.UseChunking = _settingsService.UseChunking;
                    options.ChunkingOptions.ChunkSize = _settingsService.ChunkSize;
                    options.ChunkingOptions.ChunkOverlap = _settingsService.ChunkOverlap;
                    options.EnableDebugLogging = true;
                    options.AutoDownloadModels = _settingsService.AutoDownloadEmbeddings;

                    // Select embedding provider based on EmbeddingProvider setting
                    // This is independent of whether vector search is enabled
                    var embeddingProvider = _settingsService.EmbeddingProvider ?? "local";

                    switch (embeddingProvider.ToLowerInvariant())
                    {
                        case "local":
                        case "local (onnx)":
                        case "onnx":
                            options.EmbeddingProviderType = "onnx";
                            options.OnnxModelName = _settingsService.EmbeddingModel ?? "all-MiniLM-L6-v2";
                            options.VectorDimensions = GetVectorDimensions(_settingsService.EmbeddingModel);
                            break;

                        case "openai":
                            options.EmbeddingProviderType = "openai";
                            // OpenAI embeddings use different dimensions
                            options.OpenAIModel = "text-embedding-3-small";
                            options.VectorDimensions = 1536; // OpenAI embedding dimensions
                            break;

                        default:
                            // Fall back to lightweight provider
                            options.EmbeddingProviderType = "lightweight";
                            options.VectorDimensions = 384;
                            break;
                    }
                });

                // Build service provider and get the service
                var provider = services.BuildServiceProvider();
                _currentService = provider.GetRequiredService<IUnifiedDataService>();

                _currentConfigKey = configKey;
                _logger.LogInformation("UnifiedDataService created successfully");

                return _currentService;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create UnifiedDataService, using fallback");

                // Fall back to minimal configuration
                var services = new ServiceCollection();
                services.AddSingleton(_serviceProvider.GetRequiredService<ILoggerFactory>());
                services.AddLogging();

                services.AddUnifiedData(options =>
                {
                    options.DatabaseName = _dbPath;
                    options.EnableVectorSearch = true;
                    options.EnableHybridSearch = false;
                    options.UseChunking = false;
                    options.EnableDebugLogging = true;
                    options.AutoDownloadModels = false;
                    options.EmbeddingProviderType = "lightweight";
                    options.VectorDimensions = 384;
                });

                var provider = services.BuildServiceProvider();
                _currentService = provider.GetRequiredService<IUnifiedDataService>();
                _currentConfigKey = configKey;

                return _currentService;
            }
        }

        private int GetVectorDimensions(string? embeddingModel)
        {
            return embeddingModel?.ToLower() switch
            {
                "all-minilm-l6-v2" => 384,
                "all-minilm-l12-v2" => 384,
                "bge-small-en-v1.5" => 384,
                "bge-base-en-v1.5" => 768,
                "all-mpnet-base-v2" => 768,
                _ => 384 // Default
            };
        }

        public void InvalidateConfiguration()
        {
            _currentConfigKey = null;
        }
    }
}