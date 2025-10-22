using Slyv.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketAlly.Dialogs.Maui.Dialogs;
using MarketAlly.Dialogs.Maui.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using UnifiedData.Maui.Core;
using UnifiedData.Maui.Embeddings;
using UnifiedRag.Maui.Core;
using UnifiedRag.Maui.Models;
using UnifiedRag.Maui.Services;

namespace Slyv.Maui.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settingsService;
    private readonly IUnifiedDataService? _dataService;

    [ObservableProperty]
    private string selectedProvider = "OpenAI";

    [ObservableProperty]
    private string selectedEmbeddingProvider = "Local (ONNX)";

    [ObservableProperty]
    private string? openAIKey;

    [ObservableProperty]
    private string? anthropicKey;

    [ObservableProperty]
    private string? togetherAIKey;

    [ObservableProperty]
    private string openAIModel = "gpt-4o-mini";

    [ObservableProperty]
    private string anthropicModel = "claude-sonnet-4-20250514";

    [ObservableProperty]
    private string togetherAIModel = "Qwen/Qwen3-Coder-480B-A35B-Instruct-FP8";

    [ObservableProperty]
    private string localModelPath = "models/llama-3.2-1b-q4.gguf";

    [ObservableProperty]
    private int maxTokens = 1024;

    [ObservableProperty]
    private double temperature = 0.7;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = "Configure your AI settings";

    // LLM Models (for text generation)
    [ObservableProperty]
    private ObservableCollection<LLMModelItem> availableLLMModels = new();

    [ObservableProperty]
    private LLMModelItem? selectedLLMModel;

    [ObservableProperty]
    private bool isLLMModelDownloaded;

    [ObservableProperty]
    private bool isLLMModelDownloading;

    [ObservableProperty]
    private int llmModelDownloadProgress;

    [ObservableProperty]
    private string llmModelStatusMessage = string.Empty;

    // Embedding Models (for vector search/RAG retrieval)
    [ObservableProperty]
    private ObservableCollection<EmbeddingModelItem> availableEmbeddingModels = new();

    [ObservableProperty]
    private EmbeddingModelItem? selectedEmbeddingModel;

    [ObservableProperty]
    private bool isEmbeddingModelDownloaded;

    [ObservableProperty]
    private bool isEmbeddingModelDownloading;

    [ObservableProperty]
    private int embeddingModelDownloadProgress;

    [ObservableProperty]
    private string embeddingModelStatusMessage = string.Empty;

    [ObservableProperty]
    private string embeddingHealthMessage = "Checking...";

    // Document Classification
    [ObservableProperty]
    private string classificationStatusMessage = "Ready";

    [ObservableProperty]
    private bool forceReclassify = false;

    // Utility Plugin Settings
    [ObservableProperty]
    private bool enableUtilityPlugins = true;

    [ObservableProperty]
    private bool enableGetDateTimePlugin = true;

    [ObservableProperty]
    private bool enableSystemInfoPlugin = true;

    [ObservableProperty]
    private bool enableUrlValidatorPlugin = true;

    [ObservableProperty]
    private bool enableWebSearchPlugin = false;

    [ObservableProperty]
    private string? braveApiKey;

    // File Plugin Settings
    [ObservableProperty]
    private bool enableFilePlugins = false;

    [ObservableProperty]
    private bool enableFileInfoPlugin = true;

    [ObservableProperty]
    private bool enableFileOperationsPlugin = false;

    [ObservableProperty]
    private bool enableFileWorkflowPlugin = false;

    [ObservableProperty]
    private bool enableReadFilePlugin = true;

    [ObservableProperty]
    private bool enableSecureFileOperationsPlugin = false;

    [ObservableProperty]
    private bool enableSecureReadFilePlugin = true;

    [ObservableProperty]
    private bool enableStringManipulatorPlugin = true;

    // Computed properties for visibility
    public bool ShowOpenAISettings => SelectedProvider == "OpenAI" || SelectedEmbeddingProvider == "OpenAI";
    public bool ShowAnthropicSettings => SelectedProvider == "Anthropic";
    public bool ShowTogetherAISettings => SelectedProvider == "Together.AI";

    public List<string> Providers { get; } = new() { "OpenAI", "Anthropic", "Together.AI", "Local (LlamaSharp.Ally)" };

    public List<string> EmbeddingProviders { get; } = new() { "Local (ONNX)", "OpenAI" };

	public List<string> OpenAIModels { get; } = new()
    {
	    "gpt-5",                // current flagship multimodal + reasoning model :contentReference[oaicite:1]{index=1}
        "gpt-4.1",              // smart non-reasoning / general model :contentReference[oaicite:2]{index=2}
        "gpt-4.1-mini",         // lighter / cheaper variant :contentReference[oaicite:3]{index=3}
        "gpt-4o",
		"gpt-4o-mini",
		"gpt-3.5-turbo-16k",     // extended context version :contentReference[oaicite:6]{index=6}
        "o3",                    // reasoning (o-series) models :contentReference[oaicite:7]{index=7}
        "o4-mini",               // lighter reasoning variant :contentReference[oaicite:8]{index=8}
    };

	public List<string> AnthropicModels { get; } = new()
    {
        // Claude 4 family (latest)
        "claude-sonnet-4-5-20250929",      // Claude Sonnet 4.5 - Best for coding and agents
        "claude-sonnet-4-20250514",        // Claude Sonnet 4
        "claude-opus-4-1-20250805",        // Claude Opus 4.1 - Most powerful
        "claude-opus-4-20250514",          // Claude Opus 4
    
        // Claude 3.7
        "claude-3-7-sonnet-20250219",      // Claude 3.7 Sonnet - Hybrid reasoning model
    
        // Claude 3.5
        "claude-3-5-sonnet-20241022",      // Claude 3.5 Sonnet (Oct 2024)
    };

	public List<string> TogetherAIModels { get; } = new()
    {
		//"meta-llama/Llama-4-Maverick-17B-128E-Instruct-FP8-no",
		"meta-llama/Meta-Llama-3.1-405B-Instruct-Turbo",
		"openai/gpt-oss-120b",
		//"google/gemma-3-270m-it-no",
		"Qwen/Qwen3-Coder-480B-A35B-Instruct-FP8",
		//"Qwen/Qwen3-235B-A22B-Instruct-2507-tput-no",
		"zai-org/GLM-4.5-Air-FP8"
	};

    public SettingsViewModel(IAppSettingsService settingsService, IUnifiedDataService? dataService = null)
    {
        _settingsService = settingsService;
        _dataService = dataService;

        // Load settings asynchronously on startup
        Task.Run(async () =>
        {
            await _settingsService.LoadSettingsAsync();
            LoadSettings();
            await UpdateEmbeddingHealthAsync();
        });

        // Initialize available models
        LoadAvailableLLMModels();
        LoadAvailableEmbeddingModels();
    }

    private void LoadAvailableLLMModels()
    {
        // Load LLM models for text generation
        foreach (var model in LLMModelDownloader.Models.AllModels)
        {
            AvailableLLMModels.Add(new LLMModelItem
            {
                ModelInfo = model,
                IsDownloaded = LLMModelDownloader.IsModelDownloaded(model),
                DownloadProgress = 0,
                IsDownloading = false,
                StatusMessage = LLMModelDownloader.IsModelDownloaded(model) ? "Downloaded" : "Not downloaded"
            });
        }

        // Select first model by default
        if (AvailableLLMModels.Count > 0)
        {
            SelectedLLMModel = AvailableLLMModels[0];
            UpdateLLMModelStatus();
        }
    }

    private void LoadAvailableEmbeddingModels()
    {
        // Load embedding models for vector search/RAG retrieval
        foreach (var model in OnnxModelDownloader.Models.AllModels)
        {
            AvailableEmbeddingModels.Add(new EmbeddingModelItem
            {
                ModelInfo = model,
                IsDownloaded = OnnxModelDownloader.IsModelDownloaded(model),
                DownloadProgress = 0,
                IsDownloading = false,
                StatusMessage = OnnxModelDownloader.IsModelDownloaded(model) ? "Downloaded" : "Not downloaded"
            });
        }

        // Select first model by default
        if (AvailableEmbeddingModels.Count > 0)
        {
            SelectedEmbeddingModel = AvailableEmbeddingModels[0];
            UpdateEmbeddingModelStatus();
        }
    }

    partial void OnSelectedProviderChanged(string value)
    {
        OnPropertyChanged(nameof(ShowOpenAISettings));
        OnPropertyChanged(nameof(ShowAnthropicSettings));
        OnPropertyChanged(nameof(ShowTogetherAISettings));
    }

    partial void OnSelectedEmbeddingProviderChanged(string value)
    {
        OnPropertyChanged(nameof(ShowOpenAISettings));
        _ = UpdateEmbeddingHealthAsync();
    }

    partial void OnSelectedLLMModelChanged(LLMModelItem? value)
    {
        UpdateLLMModelStatus();
    }

    partial void OnSelectedEmbeddingModelChanged(EmbeddingModelItem? value)
    {
        UpdateEmbeddingModelStatus();
    }

    private void UpdateLLMModelStatus()
    {
        if (SelectedLLMModel == null)
        {
            IsLLMModelDownloaded = false;
            LlmModelStatusMessage = string.Empty;
            return;
        }

        IsLLMModelDownloaded = LLMModelDownloader.IsModelDownloaded(SelectedLLMModel.ModelInfo);
        var toolSupport = SelectedLLMModel.ModelInfo.SupportsToolCalling
            ? $" • {SelectedLLMModel.ModelInfo.ToolCallingQuality} tool calling"
            : "";
        LlmModelStatusMessage = IsLLMModelDownloaded
            ? $"Downloaded • {SelectedLLMModel.ModelInfo.Parameters} params{toolSupport}"
            : $"Not downloaded • {SelectedLLMModel.ModelInfo.SizeMB} MB • {SelectedLLMModel.ModelInfo.Parameters} params{toolSupport}";
    }

    private void UpdateEmbeddingModelStatus()
    {
        if (SelectedEmbeddingModel == null)
        {
            IsEmbeddingModelDownloaded = false;
            EmbeddingModelStatusMessage = string.Empty;
            return;
        }

        IsEmbeddingModelDownloaded = OnnxModelDownloader.IsModelDownloaded(SelectedEmbeddingModel.ModelInfo);
        EmbeddingModelStatusMessage = IsEmbeddingModelDownloaded
            ? $"Downloaded • {SelectedEmbeddingModel.ModelInfo.VectorDimensions}D embeddings"
            : $"Not downloaded • {SelectedEmbeddingModel.ModelInfo.SizeMB} MB • {SelectedEmbeddingModel.ModelInfo.VectorDimensions}D embeddings";
    }

    private void LoadSettings()
    {
        SelectedProvider = _settingsService.LLMProvider switch
        {
            "openai" => "OpenAI",
            "anthropic" => "Anthropic",
            "togetherai" or "together" => "Together.AI",
            _ => "Local (LlamaSharp.Ally)"
        };

        SelectedEmbeddingProvider = _settingsService.EmbeddingProvider switch
        {
            "openai" => "OpenAI",
            _ => "Local (ONNX)"
        };

        OpenAIKey = _settingsService.OpenAIKey;
        AnthropicKey = _settingsService.AnthropicKey;
        TogetherAIKey = _settingsService.TogetherAIKey;
        OpenAIModel = _settingsService.OpenAIModel;
        AnthropicModel = _settingsService.AnthropicModel;
        TogetherAIModel = _settingsService.TogetherAIModel;
        LocalModelPath = _settingsService.LocalModelPath;

        // Debug logging to see what values were loaded
        System.Diagnostics.Debug.WriteLine($"Loaded settings:");
        System.Diagnostics.Debug.WriteLine($"  LLMProvider: {_settingsService.LLMProvider}");
        System.Diagnostics.Debug.WriteLine($"  OpenAIKey: {OpenAIKey?.Substring(0, Math.Min(10, OpenAIKey?.Length ?? 0))}...");
        System.Diagnostics.Debug.WriteLine($"  LocalModelPath: {LocalModelPath}");
        MaxTokens = _settingsService.MaxTokens;
        Temperature = _settingsService.Temperature;

        // Load utility plugin settings
        EnableUtilityPlugins = _settingsService.EnableUtilityPlugins;
        EnableGetDateTimePlugin = _settingsService.EnableGetDateTimePlugin;
        EnableSystemInfoPlugin = _settingsService.EnableSystemInfoPlugin;
        EnableUrlValidatorPlugin = _settingsService.EnableUrlValidatorPlugin;
        EnableWebSearchPlugin = _settingsService.EnableWebSearchPlugin;
        BraveApiKey = _settingsService.BraveApiKey;

        // Load file plugin settings
        EnableFilePlugins = _settingsService.EnableFilePlugins;
        EnableFileInfoPlugin = _settingsService.EnableFileInfoPlugin;
        EnableFileOperationsPlugin = _settingsService.EnableFileOperationsPlugin;
        EnableFileWorkflowPlugin = _settingsService.EnableFileWorkflowPlugin;
        EnableReadFilePlugin = _settingsService.EnableReadFilePlugin;
        EnableSecureFileOperationsPlugin = _settingsService.EnableSecureFileOperationsPlugin;
        EnableSecureReadFilePlugin = _settingsService.EnableSecureReadFilePlugin;
        EnableStringManipulatorPlugin = _settingsService.EnableStringManipulatorPlugin;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        IsLoading = true;
        StatusMessage = "Saving settings...";

        try
        {
            // Map provider selection
            _settingsService.LLMProvider = SelectedProvider switch
            {
                "OpenAI" => "openai",
                "Anthropic" => "anthropic",
                "Together.AI" => "togetherai",
                _ => "llamaally"
            };

            _settingsService.EmbeddingProvider = SelectedEmbeddingProvider switch
            {
                "OpenAI" => "openai",
                _ => "local"
            };

            // Debug logging to see what values are being set
            System.Diagnostics.Debug.WriteLine($"Saving settings:");
            System.Diagnostics.Debug.WriteLine($"  LLMProvider: {_settingsService.LLMProvider}");
            System.Diagnostics.Debug.WriteLine($"  OpenAIKey: {OpenAIKey?.Substring(0, Math.Min(10, OpenAIKey?.Length ?? 0))}...");
            System.Diagnostics.Debug.WriteLine($"  LocalModelPath: {LocalModelPath}");

            _settingsService.OpenAIKey = OpenAIKey;
            _settingsService.AnthropicKey = AnthropicKey;
            _settingsService.TogetherAIKey = TogetherAIKey;
            _settingsService.OpenAIModel = OpenAIModel;
            _settingsService.AnthropicModel = AnthropicModel;
            _settingsService.TogetherAIModel = TogetherAIModel;
            _settingsService.LocalModelPath = LocalModelPath;
            _settingsService.MaxTokens = MaxTokens;
            _settingsService.Temperature = (float)Temperature;

            // Save utility plugin settings
            _settingsService.EnableUtilityPlugins = EnableUtilityPlugins;
            _settingsService.EnableGetDateTimePlugin = EnableGetDateTimePlugin;
            _settingsService.EnableSystemInfoPlugin = EnableSystemInfoPlugin;
            _settingsService.EnableUrlValidatorPlugin = EnableUrlValidatorPlugin;
            _settingsService.EnableWebSearchPlugin = EnableWebSearchPlugin;
            _settingsService.BraveApiKey = BraveApiKey;

            // Save file plugin settings
            _settingsService.EnableFilePlugins = EnableFilePlugins;
            _settingsService.EnableFileInfoPlugin = EnableFileInfoPlugin;
            _settingsService.EnableFileOperationsPlugin = EnableFileOperationsPlugin;
            _settingsService.EnableFileWorkflowPlugin = EnableFileWorkflowPlugin;
            _settingsService.EnableReadFilePlugin = EnableReadFilePlugin;
            _settingsService.EnableSecureFileOperationsPlugin = EnableSecureFileOperationsPlugin;
            _settingsService.EnableSecureReadFilePlugin = EnableSecureReadFilePlugin;
            _settingsService.EnableStringManipulatorPlugin = EnableStringManipulatorPlugin;

            await _settingsService.SaveSettingsAsync();

            // Invalidate the provider cache so the new settings take effect immediately
            try
            {
                var serviceProvider = Application.Current?.Handler?.MauiContext?.Services;

                // Invalidate the dynamic embedding provider cache when embedding provider changes
                if (_settingsService.EmbeddingProvider != null)
                {
                    var dynamicEmbedding = serviceProvider?.GetService<DynamicEmbeddingProvider>();
                    if (dynamicEmbedding != null)
                    {
                        dynamicEmbedding.InvalidateCache();
                        System.Diagnostics.Debug.WriteLine($"Invalidated embedding provider cache - will use {_settingsService.EmbeddingProvider} on next use");
                    }
                }

                var providerFactory = serviceProvider?.GetService<IDynamicProviderFactory>();
                if (providerFactory != null)
                {
                    providerFactory.InvalidateCache();
                    await providerFactory.UpdateProviderAsync();
                }

                // Rebuild the plugin registry with new plugin settings
                var dynamicRegistry = serviceProvider?.GetService<DynamicPluginRegistry>();
                if (dynamicRegistry != null)
                {
                    dynamicRegistry.RebuildRegistry();
                    System.Diagnostics.Debug.WriteLine("Plugin registry rebuilt with new settings");
                }
            }
            catch (Exception updateEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update provider or plugins: {updateEx.Message}");
                // Non-critical error - the provider/plugins will update on next use anyway
            }

            StatusMessage = "Settings saved successfully!";
            await AlertDialog.ShowAsync("Success", "Settings have been saved and will take effect immediately.", DialogType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            await AlertDialog.ShowAsync("Error", $"Failed to save settings: {ex.Message}", DialogType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PickLocalModelAsync()
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.data" } },
                    { DevicePlatform.Android, new[] { "*/*" } },
                    { DevicePlatform.WinUI, new[] { ".gguf", ".bin" } },
                    { DevicePlatform.macOS, new[] { "gguf", "bin" } }
                });

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select GGUF model file",
                FileTypes = customFileType
            });

            if (result != null)
            {
                // Copy to app data directory
                var targetPath = Path.Combine(FileSystem.AppDataDirectory, "models", result.FileName);
                var targetDir = Path.GetDirectoryName(targetPath)!;

                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                using var sourceStream = await result.OpenReadAsync();
                using var targetStream = File.Create(targetPath);
                await sourceStream.CopyToAsync(targetStream);

                LocalModelPath = Path.Combine("models", result.FileName);
                StatusMessage = $"Model copied: {result.FileName}";
            }
        }
        catch (Exception ex)
        {
            await AlertDialog.ShowAsync("Error", $"Failed to pick model: {ex.Message}", DialogType.Error);
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsLoading = true;
        StatusMessage = "Testing connection...";

        try
        {
            // First save the current settings
            await SaveSettingsAsync();

            // Get the dynamic RAG service from DI to test with current settings
            var serviceProvider = Application.Current?.Handler?.MauiContext?.Services;
            if (serviceProvider == null)
            {
                throw new InvalidOperationException("Service provider not available");
            }

            // Get the dynamic provider factory to force provider update
            var providerFactory = serviceProvider.GetService<IDynamicProviderFactory>();
            if (providerFactory != null)
            {
                // Force factory to update with new settings
                providerFactory.InvalidateCache();
                await providerFactory.UpdateProviderAsync();
            }

            // Get the RAG service to test
            var ragService = serviceProvider.GetService<IRAGService>();
            if (ragService == null)
            {
                StatusMessage = "RAG service not configured";
                throw new InvalidOperationException("RAG service not available. Please check your configuration.");
            }

            // Test with a simple prompt
            StatusMessage = $"Testing {SelectedProvider} connection...";
            var testPrompt = "Hello, please respond with 'Connection successful' to confirm the service is working.";

            var options = new RAGOptions
            {
                TopK = 0, // Don't search documents
                GenerationOptions = new GenerationOptions
                {
                    MaxTokens = 50,
                    Temperature = 0.1f,
                    Stream = false
                }
            };

            // Try to get a response with timeout
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var responseTask = ragService.AskAsync(testPrompt, options);

            if (await Task.WhenAny(responseTask, Task.Delay(10000, cts.Token)) == responseTask)
            {
                var response = await responseTask;
                if (response != null && !string.IsNullOrEmpty(response.Answer))
                {
                    StatusMessage = $"{SelectedProvider} connection successful!";
                    await AlertDialog.ShowAsync("Success",
                        $"Connection test successful!\n\nProvider: {SelectedProvider}\nResponse: {response.Answer.Substring(0, Math.Min(100, response.Answer.Length))}",
                        DialogType.Success);
                }
                else
                {
                    throw new InvalidOperationException("No response received from provider");
                }
            }
            else
            {
                throw new TimeoutException("Connection test timed out after 10 seconds");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Test failed: {ex.Message}";
            await AlertDialog.ShowAsync("Error",
                $"Connection test failed.\n\nProvider: {SelectedProvider}\nError: {ex.Message}",
                DialogType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            await _settingsService.LoadSettingsAsync();
            LoadSettings();
            StatusMessage = "Settings loaded";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task DownloadLLMModelAsync()
    {
        if (SelectedLLMModel == null || IsLLMModelDownloading || IsLLMModelDownloaded)
            return;

        try
        {
            IsLLMModelDownloading = true;
            LlmModelStatusMessage = "Starting download...";

            var progress = new Progress<LLMModelDownloader.DownloadProgress>(p =>
            {
                LlmModelDownloadProgress = p.ProgressPercentage;
                LlmModelStatusMessage = p.Message;
            });

            var path = await LLMModelDownloader.DownloadModelAsync(SelectedLLMModel.ModelInfo, null, progress, false);

            IsLLMModelDownloaded = true;
            LocalModelPath = path;
            UpdateLLMModelStatus();
            StatusMessage = $"LLM model downloaded: {SelectedLLMModel.ModelInfo.Name}";
        }
        catch (Exception ex)
        {
            LlmModelStatusMessage = $"Error: {ex.Message}";
            await AlertDialog.ShowAsync("Download Failed", ex.Message, DialogType.Error);
        }
        finally
        {
            IsLLMModelDownloading = false;
        }
    }

    [RelayCommand]
    private async Task DownloadEmbeddingModelAsync()
    {
        if (SelectedEmbeddingModel == null || IsEmbeddingModelDownloading || IsEmbeddingModelDownloaded)
            return;

        try
        {
            IsEmbeddingModelDownloading = true;
            EmbeddingModelStatusMessage = "Starting download...";

            var progress = new Progress<OnnxModelDownloader.DownloadProgress>(p =>
            {
                EmbeddingModelDownloadProgress = p.ProgressPercentage;
                EmbeddingModelStatusMessage = p.Message;
            });

            await OnnxModelDownloader.DownloadModelAsync(SelectedEmbeddingModel.ModelInfo, null, progress, false);

            IsEmbeddingModelDownloaded = true;
            UpdateEmbeddingModelStatus();
            StatusMessage = $"Embedding model downloaded: {SelectedEmbeddingModel.ModelInfo.Name}";
        }
        catch (Exception ex)
        {
            EmbeddingModelStatusMessage = $"Error: {ex.Message}";
            await AlertDialog.ShowAsync("Download Failed", ex.Message, DialogType.Error);
        }
        finally
        {
            IsEmbeddingModelDownloading = false;
        }
    }

    [RelayCommand]
    private void UseDownloadedLLMModel()
    {
        if (SelectedLLMModel == null || !IsLLMModelDownloaded)
            return;

        LocalModelPath = LLMModelDownloader.GetModelPath(SelectedLLMModel.ModelInfo);
        StatusMessage = $"Using LLM model: {SelectedLLMModel.ModelInfo.Name}";
    }

    [RelayCommand]
    private void UseDownloadedEmbeddingModel()
    {
        if (SelectedEmbeddingModel == null || !IsEmbeddingModelDownloaded)
            return;

        // Embedding models are used automatically by UnifiedData
        StatusMessage = $"Using embedding model: {SelectedEmbeddingModel.ModelInfo.Name}";
    }

    private async Task UpdateEmbeddingHealthAsync()
    {
        if (_dataService == null)
        {
            EmbeddingHealthMessage = "Data service unavailable";
            return;
        }

        try
        {
            var embeddingHealth = await _dataService.GetEmbeddingHealthAsync();
            EmbeddingHealthMessage = embeddingHealth.StatusMessage;
        }
        catch (Exception ex)
        {
            EmbeddingHealthMessage = $"⚠️ Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RebuildEmbeddingsAsync()
    {
        if (_dataService == null)
        {
            await AlertDialog.ShowAsync("Error", "Data service is not available", DialogType.Error);
            return;
        }

        if (IsLoading) return;

        // Show which provider will be used
        var providerName = SelectedEmbeddingProvider == "OpenAI" && !string.IsNullOrEmpty(OpenAIKey)
            ? "OpenAI (text-embedding-3-small)"
            : "Local (ONNX or Lightweight)";

        var confirm = await ConfirmDialog.ShowAsync(
            "Rebuild All Embeddings",
            $"This will regenerate embeddings for all documents using the {providerName} provider. This may take several minutes depending on the number of documents. Continue?",
            "Rebuild",
            "Cancel",
            DialogType.Warning);

        if (confirm != true) return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Rebuilding embeddings with {providerName}...";
            EmbeddingHealthMessage = $"⏳ Rebuilding embeddings with {providerName}...";

            // Make sure we're using the latest settings
            await SaveSettingsAsync();

            // Invalidate the dynamic embedding provider cache to ensure it uses current settings
            var serviceProvider = Application.Current?.Handler?.MauiContext?.Services;
            var dynamicEmbedding = serviceProvider?.GetService<DynamicEmbeddingProvider>();
            if (dynamicEmbedding != null)
            {
                dynamicEmbedding.InvalidateCache();
                System.Diagnostics.Debug.WriteLine($"Forced embedding provider to refresh - using {providerName}");
            }

            await _dataService.RebuildIndicesAsync();

            StatusMessage = "Embeddings rebuilt successfully";
            await UpdateEmbeddingHealthAsync();

            await AlertDialog.ShowAsync(
                "Success",
                $"All embeddings have been rebuilt successfully using {providerName}. Vector search will now use these embeddings for better search results.",
                DialogType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error rebuilding: {ex.Message}";
            EmbeddingHealthMessage = $"⚠️ Error: {ex.Message}";
            await AlertDialog.ShowAsync("Error", $"Failed to rebuild embeddings: {ex.Message}", DialogType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RebuildFtsIndexAsync()
    {
        if (_dataService == null)
        {
            await AlertDialog.ShowAsync("Error", "Data service is not available", DialogType.Error);
            return;
        }

        if (IsLoading) return;

        var confirm = await ConfirmDialog.ShowAsync(
            "Rebuild Search Index",
            "This will rebuild the full-text search (FTS5) index from scratch. This can fix issues with phantom search results. Continue?",
            "Rebuild",
            "Cancel",
            DialogType.Warning);

        if (confirm != true) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Rebuilding FTS5 search index...";

            // Check if we can access the storage provider directly
            var storageProvider = _dataService.GetType()
                .GetProperty("_textStorage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .GetValue(_dataService) as UnifiedData.Maui.Storage.UnifiedStorageProvider;

            if (storageProvider != null)
            {
                await storageProvider.RebuildTextIndexAsync();
                StatusMessage = "FTS5 index rebuilt successfully";

                await AlertDialog.ShowAsync(
                    "Success",
                    "The full-text search index has been rebuilt successfully. This should resolve any issues with incorrect search results.",
                    DialogType.Success);
            }
            else
            {
                // Fallback to general index rebuild
                await _dataService.RebuildIndicesAsync();
                StatusMessage = "All indices rebuilt successfully";

                await AlertDialog.ShowAsync(
                    "Success",
                    "All search indices have been rebuilt successfully.",
                    DialogType.Success);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error rebuilding: {ex.Message}";
            await AlertDialog.ShowAsync("Error", $"Failed to rebuild search index: {ex.Message}", DialogType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClassifyAllDocumentsAsync()
    {
        if (IsLoading) return;

        try
        {
            // Get the RAG service from DI
            var serviceProvider = Application.Current?.Handler?.MauiContext?.Services;
            if (serviceProvider == null)
            {
                await AlertDialog.ShowAsync("Error", "Service provider not available", DialogType.Error);
                return;
            }

            var ragService = serviceProvider.GetService<IRAGService>();
            if (ragService == null)
            {
                await AlertDialog.ShowAsync("Error", "RAG service not available", DialogType.Error);
                return;
            }

            // Check if the service also implements IRAGClassificationService
            var classificationService = ragService as UnifiedRag.Maui.Core.IRAGClassificationService;
            if (classificationService == null)
            {
                await AlertDialog.ShowAsync("Error", "Classification is not supported by the current AI provider.", DialogType.Error);
                return;
            }

            var confirmMessage = ForceReclassify
                ? "This will classify ALL documents, including those already classified. This will regenerate topics and summaries. This may take several minutes depending on the number of documents and AI provider speed. Continue?"
                : "This will classify documents that haven't been classified yet. This will extract topics and generate summaries. This may take several minutes depending on the number of documents and AI provider speed. Continue?";

            var confirm = await ConfirmDialog.ShowAsync(
                "Classify All Documents",
                confirmMessage,
                "Classify",
                "Cancel",
                DialogType.Info);

            if (confirm != true) return;

            IsLoading = true;
            StatusMessage = "Classifying documents...";
            ClassificationStatusMessage = "⏳ Classification in progress...";

            // Start classification (it will skip already classified if forceReclassify is false)
            await classificationService.ClassifyAllDocumentsAsync(
                forceReclassify: ForceReclassify,
                contentType: 0,
                maxTopics: 5,
                minConfidence: 0.3f);

            StatusMessage = "Classification complete";
            ClassificationStatusMessage = "✅ Classification complete";

            await AlertDialog.ShowAsync(
                "Success",
                "Documents have been classified successfully. Topics and summaries have been generated.",
                DialogType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error classifying: {ex.Message}";
            ClassificationStatusMessage = $"⚠️ Error: {ex.Message}";
            await AlertDialog.ShowAsync("Error", $"Failed to classify documents: {ex.Message}", DialogType.Error);
        }
        finally
        {
            IsLoading = false;
            ClassificationStatusMessage = "Ready";
        }
    }
}

public partial class LLMModelItem : ObservableObject
{
    public LLMModelDownloader.LLMModelInfo ModelInfo { get; set; } = null!;

    [ObservableProperty]
    private bool isDownloaded;

    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private int downloadProgress;

    [ObservableProperty]
    private string statusMessage = string.Empty;
}

public partial class EmbeddingModelItem : ObservableObject
{
    public OnnxModelDownloader.ModelInfo ModelInfo { get; set; } = null!;

    [ObservableProperty]
    private bool isDownloaded;

    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private int downloadProgress;

    [ObservableProperty]
    private string statusMessage = string.Empty;
}