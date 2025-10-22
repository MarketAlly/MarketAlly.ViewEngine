using MarketAlly.AIPlugin;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnifiedRag.Maui.Plugins.Search;

namespace Slyv.Maui.Services
{
    /// <summary>
    /// Dynamic plugin registry that rebuilds based on current settings
    /// </summary>
    public class DynamicPluginRegistry : IAIPluginRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DynamicPluginRegistry> _logger;
        private AIPluginRegistry _currentRegistry;
        private readonly object _lock = new object();
        private bool _initialized = false;

        public DynamicPluginRegistry(IServiceProvider serviceProvider, ILogger<DynamicPluginRegistry> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            // Don't build registry in constructor - this can cause circular dependencies
            // Registry will be built lazily on first access
            _logger?.LogInformation("DynamicPluginRegistry created - will build on first access");
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                lock (_lock)
                {
                    if (!_initialized)
                    {
                        RebuildRegistryInternal();
                        _initialized = true;
                    }
                }
            }
        }

        public void RebuildRegistry()
        {
            lock (_lock)
            {
                RebuildRegistryInternal();
                _initialized = true;
            }
        }

        private void RebuildRegistryInternal()
        {
            try
            {
                _logger?.LogInformation("Rebuilding plugin registry based on current settings");

                // Get the correct logger type for AIPluginRegistry
                var registryLogger = _serviceProvider.GetService(typeof(ILogger<AIPluginRegistry>)) as ILogger<AIPluginRegistry>;
                var registry = new AIPluginRegistry(registryLogger);
                var settingsService = _serviceProvider.GetService(typeof(IAppSettingsService)) as IAppSettingsService;
                var securityConfig = _serviceProvider.GetService(typeof(MarketAlly.AIPlugin.Security.SecurityConfiguration))
                    as MarketAlly.AIPlugin.Security.SecurityConfiguration;
                var auditLogger = _serviceProvider.GetService(typeof(MarketAlly.AIPlugin.Monitoring.AuditLogger))
                    as MarketAlly.AIPlugin.Monitoring.AuditLogger;

                // Core RAG plugins (always enabled)
                var searchPlugin = new UnifiedRag.Maui.Plugins.Search.RAGSearchPlugin(
                    _serviceProvider.GetService(typeof(UnifiedData.Maui.Core.IUnifiedDataService)) as UnifiedData.Maui.Core.IUnifiedDataService,
                    _serviceProvider.GetService(typeof(ILogger<UnifiedRag.Maui.Plugins.Search.RAGSearchPlugin>)) as ILogger<UnifiedRag.Maui.Plugins.Search.RAGSearchPlugin>,
                    securityConfig,
                    auditLogger);
                registry.RegisterPlugin(searchPlugin);

                var queryEnhancerPlugin = new UnifiedRag.Maui.Plugins.Query.QueryEnhancerPlugin(
                    _serviceProvider.GetService(typeof(UnifiedRag.Maui.Services.IDynamicProviderFactory)) as UnifiedRag.Maui.Services.IDynamicProviderFactory,
                    _serviceProvider.GetService(typeof(ILogger<UnifiedRag.Maui.Plugins.Query.QueryEnhancerPlugin>)) as ILogger<UnifiedRag.Maui.Plugins.Query.QueryEnhancerPlugin>,
                    securityConfig,
                    auditLogger);
                registry.RegisterPlugin(queryEnhancerPlugin);

                var documentChunkerPlugin = new UnifiedRag.Maui.Plugins.Processing.DocumentChunkerPlugin(
                    _serviceProvider.GetService(typeof(UnifiedData.Maui.Core.IUnifiedDataService)) as UnifiedData.Maui.Core.IUnifiedDataService,
                    _serviceProvider.GetService(typeof(ILogger<UnifiedRag.Maui.Plugins.Processing.DocumentChunkerPlugin>)) as ILogger<UnifiedRag.Maui.Plugins.Processing.DocumentChunkerPlugin>,
                    securityConfig,
                    auditLogger);
                registry.RegisterPlugin(documentChunkerPlugin);

                var citationGeneratorPlugin = new UnifiedRag.Maui.Plugins.Citations.CitationGeneratorPlugin(
                    _serviceProvider.GetService(typeof(UnifiedData.Maui.Core.IUnifiedDataService)) as UnifiedData.Maui.Core.IUnifiedDataService,
                    _serviceProvider.GetService(typeof(ILogger<UnifiedRag.Maui.Plugins.Citations.CitationGeneratorPlugin>)) as ILogger<UnifiedRag.Maui.Plugins.Citations.CitationGeneratorPlugin>,
                    securityConfig,
                    auditLogger);
                registry.RegisterPlugin(citationGeneratorPlugin);

                var documentClassifierPlugin = new UnifiedRag.Maui.Plugins.Processing.DocumentClassifierPlugin(
                    _serviceProvider.GetService(typeof(UnifiedData.Maui.Core.IUnifiedDataService)) as UnifiedData.Maui.Core.IUnifiedDataService,
                    _serviceProvider.GetService(typeof(ILogger<UnifiedRag.Maui.Plugins.Processing.DocumentClassifierPlugin>)) as ILogger<UnifiedRag.Maui.Plugins.Processing.DocumentClassifierPlugin>,
                    securityConfig,
                    auditLogger);
                registry.RegisterPlugin(documentClassifierPlugin);

                var webpageSummarizePlugin = new UnifiedRag.Maui.Plugins.Processing.WebpageSummarizePlugin(
                    _serviceProvider.GetService(typeof(UnifiedData.Maui.Core.IUnifiedDataService)) as UnifiedData.Maui.Core.IUnifiedDataService,
                    _serviceProvider.GetService(typeof(ILogger<UnifiedRag.Maui.Plugins.Processing.WebpageSummarizePlugin>)) as ILogger<UnifiedRag.Maui.Plugins.Processing.WebpageSummarizePlugin>,
                    securityConfig,
                    auditLogger);
                registry.RegisterPlugin(webpageSummarizePlugin);

                // Utility Plugins (controlled by settings)
                if (settingsService?.EnableUtilityPlugins == true)
                {
                    if (settingsService.EnableGetDateTimePlugin)
                    {
                        var dateTimePlugin = new MarketAlly.AIPlugin.Plugins.GetDateTimePlugin();
                        registry.RegisterPlugin(dateTimePlugin);
                    }

                    if (settingsService.EnableSystemInfoPlugin)
                    {
                        var systemInfoPlugin = new MarketAlly.AIPlugin.Plugins.SystemInfoPlugin();
                        registry.RegisterPlugin(systemInfoPlugin);
                    }

                    if (settingsService.EnableUrlValidatorPlugin)
                    {
                        var urlValidatorPlugin = new MarketAlly.AIPlugin.Plugins.UrlValidatorPlugin();
                        registry.RegisterPlugin(urlValidatorPlugin);
                    }

                    _logger?.LogInformation($"EnableWebSearchPlugin setting: {settingsService.EnableWebSearchPlugin}");
                    if (settingsService.EnableWebSearchPlugin)
                    {
                        try
                        {
                            var braveApiKey = settingsService.BraveApiKey ?? Environment.GetEnvironmentVariable("BRAVE_API_KEY") ?? "demo-key";
                            _logger?.LogInformation($"Creating BraveSearchPlugin with Brave API key: {(string.IsNullOrEmpty(braveApiKey) ? "empty" : "set")}");

                            var braveSearchPlugin = new BraveSearchPlugin(
                                braveApiKey,
                                httpClient: null,
                                _serviceProvider.GetService(typeof(ILogger<BraveSearchPlugin>)) as ILogger<BraveSearchPlugin>,
                                securityConfig,
                                auditLogger);
                            registry.RegisterPlugin(braveSearchPlugin);
                            _logger?.LogInformation("BraveSearchPlugin registered successfully");
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to register BraveSearchPlugin");
                        }
                    }
                    else
                    {
                        _logger?.LogInformation("BraveSearchPlugin is disabled in settings");
                    }
                }

                // File Plugins (controlled by settings)
                if (settingsService?.EnableFilePlugins == true)
                {
                    if (settingsService.EnableFileInfoPlugin)
                    {
                        var fileInfoPlugin = new MarketAlly.AIPlugin.Plugins.FileInfoPlugin();
                        registry.RegisterPlugin(fileInfoPlugin);
                    }

                    if (settingsService.EnableReadFilePlugin)
                    {
                        var readFilePlugin = new MarketAlly.AIPlugin.Plugins.ReadFilePlugin();
                        registry.RegisterPlugin(readFilePlugin);
                    }

                    if (settingsService.EnableSecureReadFilePlugin)
                    {
                        var secureReadFilePlugin = new MarketAlly.AIPlugin.Plugins.SecureReadFilePlugin(
                            securityConfig,
                            _serviceProvider.GetService(typeof(ILogger<MarketAlly.AIPlugin.Plugins.SecureReadFilePlugin>)) as ILogger<MarketAlly.AIPlugin.Plugins.SecureReadFilePlugin>);
                        registry.RegisterPlugin(secureReadFilePlugin);
                    }

                    if (settingsService.EnableStringManipulatorPlugin)
                    {
                        var stringManipulatorPlugin = new MarketAlly.AIPlugin.Plugins.StringManipulatorPlugin();
                        registry.RegisterPlugin(stringManipulatorPlugin);
                    }

                    if (settingsService.EnableFileOperationsPlugin)
                    {
                        var fileOperationsPlugin = new MarketAlly.AIPlugin.Plugins.FileOperationsPlugin(
                            securityConfig,
                            _serviceProvider.GetService(typeof(ILogger<MarketAlly.AIPlugin.Plugins.FileOperationsPlugin>)) as ILogger<MarketAlly.AIPlugin.Plugins.FileOperationsPlugin>);
                        registry.RegisterPlugin(fileOperationsPlugin);
                    }

                    if (settingsService.EnableSecureFileOperationsPlugin)
                    {
                        var secureFileOperationsPlugin = new MarketAlly.AIPlugin.Plugins.SecureFileOperationsPlugin(
                            securityConfig,
                            _serviceProvider.GetService(typeof(ILogger<MarketAlly.AIPlugin.Plugins.SecureFileOperationsPlugin>)) as ILogger<MarketAlly.AIPlugin.Plugins.SecureFileOperationsPlugin>);
                        registry.RegisterPlugin(secureFileOperationsPlugin);
                    }

                    if (settingsService.EnableFileWorkflowPlugin)
                    {
                        var fileWorkflowPlugin = new MarketAlly.AIPlugin.Plugins.FileWorkflowPlugin();
                        registry.RegisterPlugin(fileWorkflowPlugin);
                    }
                }

                _logger?.LogInformation($"Registered {registry.GetAllPlugins().Count} plugins with dynamic registry");

                _currentRegistry = registry;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to rebuild plugin registry");
                // Keep the old registry if rebuild fails
            }
        }

        public void RegisterPlugin(IAIPlugin plugin)
        {
            EnsureInitialized();
            lock (_lock)
            {
                _currentRegistry?.RegisterPlugin(plugin);
            }
        }

        public void RegisterPlugin(string functionName, IAIPlugin plugin)
        {
            EnsureInitialized();
            lock (_lock)
            {
                _currentRegistry?.RegisterPlugin(functionName, plugin);
            }
        }

        public bool IsPluginRegistered(string pluginName)
        {
            EnsureInitialized();
            lock (_lock)
            {
                return _currentRegistry?.IsPluginRegistered(pluginName) ?? false;
            }
        }

        public List<IAIPlugin> GetAllPlugins()
        {
            EnsureInitialized();
            lock (_lock)
            {
                return _currentRegistry?.GetAllPlugins() ?? new List<IAIPlugin>();
            }
        }

        public IReadOnlyDictionary<string, AIPluginRegistry.PluginInfo> GetAvailableFunctions()
        {
            EnsureInitialized();
            lock (_lock)
            {
                return _currentRegistry?.GetAvailableFunctions() ?? new Dictionary<string, AIPluginRegistry.PluginInfo>();
            }
        }

        public List<FunctionDefinition> GetAllPluginSchemas()
        {
            EnsureInitialized();
            lock (_lock)
            {
                return _currentRegistry?.GetAllPluginSchemas() ?? new List<FunctionDefinition>();
            }
        }

        public async Task<AIPluginResult> CallFunctionAsync(string functionName, Dictionary<string, object> parameters)
        {
            EnsureInitialized();
            AIPluginRegistry registry;
            lock (_lock)
            {
                registry = _currentRegistry;
            }

            if (registry == null)
            {
                return new AIPluginResult(
                    new InvalidOperationException("Plugin registry not initialized"),
                    "Registry not initialized");
            }

            return await registry.CallFunctionAsync(functionName, parameters);
        }
    }
}
