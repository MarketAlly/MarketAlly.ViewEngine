using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnifiedData.Maui.Embeddings;

namespace Slyv.Maui.Services
{
    /// <summary>
    /// Dynamic embedding provider that switches between OpenAI and local embeddings based on settings
    /// </summary>
    public class DynamicEmbeddingProvider : IEmbeddingProvider
    {
        private readonly IAppSettingsService _settingsService;
        private readonly ILogger<DynamicEmbeddingProvider> _logger;
        private IEmbeddingProvider? _currentProvider;
        private string? _lastProviderType;
        private string? _lastApiKey;

        public DynamicEmbeddingProvider(
            IAppSettingsService settingsService,
            ILogger<DynamicEmbeddingProvider>? logger = null)
        {
            _settingsService = settingsService;
            _logger = logger ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<DynamicEmbeddingProvider>();
        }

        private async Task<IEmbeddingProvider> GetOrCreateProviderAsync()
        {
            // Load current settings
            await _settingsService.LoadSettingsAsync();

            var providerType = _settingsService.EmbeddingProvider?.ToLowerInvariant() ?? "local";
            var openAiKey = _settingsService.OpenAIKey;

            // Check if we need to recreate the provider
            bool needsRecreation = _currentProvider == null ||
                                  _lastProviderType != providerType ||
                                  (providerType == "openai" && _lastApiKey != openAiKey);

            if (needsRecreation)
            {
                _logger.LogInformation($"Creating embedding provider: {providerType}");

                try
                {
                    switch (providerType)
                    {
                        case "openai" when !string.IsNullOrEmpty(openAiKey):
                            _logger.LogInformation($"Creating OpenAI embedding provider with text-embedding-3-small model (key starts with: {openAiKey.Substring(0, Math.Min(10, openAiKey.Length))}...)");
                            _currentProvider = new OpenAIEmbeddingProvider(
                                openAiKey,
                                "text-embedding-3-small",  // Use the small model for efficiency
                                1536,  // Standard dimensions for text-embedding-3-small
                                _logger as ILogger<OpenAIEmbeddingProvider>);
                            _lastProviderType = providerType;
                            _lastApiKey = openAiKey;
                            _logger.LogInformation("OpenAI embedding provider created successfully");
                            break;

                        case "openai":
                            // OpenAI selected but no API key
                            _logger.LogWarning("OpenAI embedding provider selected but no API key configured. Falling back to local.");
                            goto default;

                        default:
                            // Use local ONNX or lightweight provider
                            _logger.LogInformation("Using local embedding provider");

                            // Try to use ONNX if models are available
                            try
                            {
                                var downloadedModels = OnnxModelDownloader.GetDownloadedModels();
                                if (downloadedModels.Count > 0)
                                {
                                    var (model, _, _) = downloadedModels[0];
                                    _logger.LogInformation($"Using ONNX model: {model.Name}");
                                    _currentProvider = await EmbeddingProviderFactory.CreateOnnxProviderAsync(model);
                                }
                                else
                                {
                                    _logger.LogInformation("No ONNX models downloaded, using lightweight provider");
                                    _currentProvider = new LightweightEmbeddingProvider(384);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to create ONNX provider, falling back to lightweight");
                                _currentProvider = new LightweightEmbeddingProvider(384);
                            }

                            _lastProviderType = providerType;
                            _lastApiKey = null;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to create {providerType} provider, falling back to lightweight");
                    _currentProvider = new LightweightEmbeddingProvider(384);
                    _lastProviderType = "lightweight";
                    _lastApiKey = null;
                }
            }

            return _currentProvider!;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            var provider = await GetOrCreateProviderAsync();

            _logger.LogDebug($"Generating embedding using {provider.GetType().Name} for text of length {text?.Length ?? 0}");

            try
            {
                var embedding = await provider.GetEmbeddingAsync(text);

                // Log some diagnostic info
                if (embedding != null && embedding.Length > 0)
                {
                    _logger.LogDebug($"Generated embedding with {embedding.Length} dimensions");

                    // Log first few values for debugging (to verify it's not all zeros or identical)
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        var sample = string.Join(", ", embedding.Take(5).Select(v => v.ToString("F4")));
                        _logger.LogTrace($"Embedding sample (first 5 values): [{sample}]");
                    }
                }
                else
                {
                    _logger.LogWarning("Provider returned null or empty embedding");
                }

                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding, falling back to lightweight provider");

                // Fall back to lightweight provider on error
                if (!(provider is LightweightEmbeddingProvider))
                {
                    _currentProvider = new LightweightEmbeddingProvider(384);
                    _lastProviderType = "lightweight";
                    _lastApiKey = null;
                    return await _currentProvider.GetEmbeddingAsync(text);
                }

                throw;
            }
        }

        public async Task<Dictionary<string, float[]>> GetEmbeddingsBatchAsync(IEnumerable<string> texts)
        {
            var provider = await GetOrCreateProviderAsync();

            var textList = texts.ToList();
            _logger.LogDebug($"Generating batch embeddings using {provider.GetType().Name} for {textList.Count} texts");

            try
            {
                return await provider.GetEmbeddingsBatchAsync(textList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating batch embeddings, falling back to lightweight provider");

                // Fall back to lightweight provider on error
                if (!(provider is LightweightEmbeddingProvider))
                {
                    _currentProvider = new LightweightEmbeddingProvider(384);
                    _lastProviderType = "lightweight";
                    _lastApiKey = null;
                    return await _currentProvider.GetEmbeddingsBatchAsync(textList);
                }

                throw;
            }
        }

        public int GetDimensions()
        {
            // Return dimensions based on current provider type
            // This is called before embeddings are generated, so check settings
            Task.Run(async () => await _settingsService.LoadSettingsAsync()).Wait();

            var providerType = _settingsService.EmbeddingProvider?.ToLowerInvariant() ?? "local";

            switch (providerType)
            {
                case "openai":
                    return 1536; // text-embedding-3-small dimensions
                default:
                    // Check if ONNX models are available
                    var downloadedModels = OnnxModelDownloader.GetDownloadedModels();
                    if (downloadedModels.Count > 0)
                    {
                        return downloadedModels[0].Item1.VectorDimensions;
                    }
                    return 384; // Lightweight provider dimensions
            }
        }

        public int GetMaxTextLength()
        {
            // OpenAI supports much longer text than local models
            Task.Run(async () => await _settingsService.LoadSettingsAsync()).Wait();
            var providerType = _settingsService.EmbeddingProvider?.ToLowerInvariant() ?? "local";

            return providerType == "openai" ? 32000 : 8192;
        }

        public bool IsAvailable()
        {
            // Check if the current provider configuration is available
            Task.Run(async () => await _settingsService.LoadSettingsAsync()).Wait();

            var providerType = _settingsService.EmbeddingProvider?.ToLowerInvariant() ?? "local";

            switch (providerType)
            {
                case "openai":
                    return !string.IsNullOrEmpty(_settingsService.OpenAIKey);
                default:
                    return true; // Local providers are always available
            }
        }

        public EmbeddingProviderInfo GetInfo()
        {
            Task.Run(async () => await _settingsService.LoadSettingsAsync()).Wait();
            var providerType = _settingsService.EmbeddingProvider?.ToLowerInvariant() ?? "local";

            switch (providerType)
            {
                case "openai" when !string.IsNullOrEmpty(_settingsService.OpenAIKey):
                    return new EmbeddingProviderInfo
                    {
                        Name = "OpenAI (Dynamic)",
                        Model = "text-embedding-3-small",
                        Dimensions = 1536,
                        MaxTextLength = 32000,
                        SupportsAsync = true,
                        SupportsBatch = true,
                        IsLocal = false,
                        CostPer1000Tokens = 0.00002m
                    };

                default:
                    var downloadedModels = OnnxModelDownloader.GetDownloadedModels();
                    if (downloadedModels.Count > 0)
                    {
                        var model = downloadedModels[0].Item1;
                        return new EmbeddingProviderInfo
                        {
                            Name = "ONNX (Dynamic)",
                            Model = model.Name,
                            Dimensions = model.VectorDimensions,
                            MaxTextLength = 8192,
                            SupportsAsync = true,
                            SupportsBatch = true,
                            IsLocal = true
                        };
                    }

                    return new EmbeddingProviderInfo
                    {
                        Name = "Lightweight (Dynamic)",
                        Model = "Hash-based",
                        Dimensions = 384,
                        MaxTextLength = 8192,
                        SupportsAsync = true,
                        SupportsBatch = true,
                        IsLocal = true
                    };
            }
        }

        /// <summary>
        /// Force the provider to recreate on next use (useful after settings change)
        /// </summary>
        public void InvalidateCache()
        {
            _logger.LogInformation("Invalidating embedding provider cache");
            _currentProvider = null;
            _lastProviderType = null;
            _lastApiKey = null;
        }
    }
}