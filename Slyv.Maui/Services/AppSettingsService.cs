using Microsoft.Extensions.Configuration;

namespace Slyv.Maui.Services;

public interface IAppSettingsService
{
    // LLM Settings
    string LLMProvider { get; set; }
    string EmbeddingProvider { get; set; }
    string? OpenAIKey { get; set; }
    string? AnthropicKey { get; set; }
    string? TogetherAIKey { get; set; }
    string OpenAIModel { get; set; }
    string AnthropicModel { get; set; }
    string TogetherAIModel { get; set; }
    string LocalModelPath { get; set; }
    int MaxTokens { get; set; }
    float Temperature { get; set; }

    // Embedding & Vector Search Settings
    bool EnableVectorSearch { get; set; }
    string EmbeddingModel { get; set; }
    bool AutoDownloadEmbeddings { get; set; }
    bool UseChunking { get; set; }
    int ChunkSize { get; set; }
    int ChunkOverlap { get; set; }

    // Utility Plugin Settings
    bool EnableUtilityPlugins { get; set; }
    bool EnableGetDateTimePlugin { get; set; }
    bool EnableSystemInfoPlugin { get; set; }
    bool EnableUrlValidatorPlugin { get; set; }
    bool EnableWebSearchPlugin { get; set; }
    string? BraveApiKey { get; set; }

    // File Plugin Settings
    bool EnableFilePlugins { get; set; }
    bool EnableFileInfoPlugin { get; set; }
    bool EnableFileOperationsPlugin { get; set; }
    bool EnableFileWorkflowPlugin { get; set; }
    bool EnableReadFilePlugin { get; set; }
    bool EnableSecureFileOperationsPlugin { get; set; }
    bool EnableSecureReadFilePlugin { get; set; }
    bool EnableStringManipulatorPlugin { get; set; }

    Task SaveSettingsAsync();
    Task LoadSettingsAsync();
}

public class AppSettingsService : IAppSettingsService
{
    private readonly IConfiguration _configuration;

    public AppSettingsService(IConfiguration configuration)
    {
        _configuration = configuration;
        // Don't load settings in constructor - let it be loaded on first access
    }

    // LLM Settings
    public string LLMProvider { get; set; } = "OpenAI";
    public string EmbeddingProvider { get; set; } = "local";
    public string? OpenAIKey { get; set; }
    public string? AnthropicKey { get; set; }
    public string? TogetherAIKey { get; set; }
    public string OpenAIModel { get; set; } = "gpt-4o-mini";
    public string AnthropicModel { get; set; } = "claude-3-5-sonnet-20241022";
    public string TogetherAIModel { get; set; } = "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo";
    public string LocalModelPath { get; set; } = "models/llama-3.2-1b-q4.gguf";
    public int MaxTokens { get; set; } = 1024;
    public float Temperature { get; set; } = 0.7f;

    // Embedding & Vector Search Settings
    public bool EnableVectorSearch { get; set; } = true;
    public string EmbeddingModel { get; set; } = "all-MiniLM-L6-v2";
    public bool AutoDownloadEmbeddings { get; set; } = false;
    public bool UseChunking { get; set; } = false;
    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 50;

    // Utility Plugin Settings
    public bool EnableUtilityPlugins { get; set; } = true;
    public bool EnableGetDateTimePlugin { get; set; } = true;
    public bool EnableSystemInfoPlugin { get; set; } = true;
    public bool EnableUrlValidatorPlugin { get; set; } = true;
    public bool EnableWebSearchPlugin { get; set; } = false;
    public string? BraveApiKey { get; set; }

    // File Plugin Settings
    public bool EnableFilePlugins { get; set; } = false;
    public bool EnableFileInfoPlugin { get; set; } = true;
    public bool EnableFileOperationsPlugin { get; set; } = false;
    public bool EnableFileWorkflowPlugin { get; set; } = false;
    public bool EnableReadFilePlugin { get; set; } = true;
    public bool EnableSecureFileOperationsPlugin { get; set; } = false;
    public bool EnableSecureReadFilePlugin { get; set; } = true;
    public bool EnableStringManipulatorPlugin { get; set; } = true;

    public async Task SaveSettingsAsync()
    {
        // LLM Settings
        await SecureStorage.SetAsync("llm_provider", LLMProvider);
        Preferences.Set("embedding_provider", EmbeddingProvider);

        if (!string.IsNullOrEmpty(OpenAIKey))
            await SecureStorage.SetAsync("openai_key", OpenAIKey);

        if (!string.IsNullOrEmpty(AnthropicKey))
            await SecureStorage.SetAsync("anthropic_key", AnthropicKey);

        if (!string.IsNullOrEmpty(TogetherAIKey))
            await SecureStorage.SetAsync("togetherai_key", TogetherAIKey);

        Preferences.Set("openai_model", OpenAIModel);
        Preferences.Set("anthropic_model", AnthropicModel);
        Preferences.Set("togetherai_model", TogetherAIModel);
        Preferences.Set("local_model_path", LocalModelPath);
        Preferences.Set("max_tokens", MaxTokens);
        Preferences.Set("temperature", Temperature);

        // Embedding & Vector Search Settings
        Preferences.Set("enable_vector_search", EnableVectorSearch);
        Preferences.Set("embedding_model", EmbeddingModel);
        Preferences.Set("auto_download_embeddings", AutoDownloadEmbeddings);
        Preferences.Set("use_chunking", UseChunking);
        Preferences.Set("chunk_size", ChunkSize);
        Preferences.Set("chunk_overlap", ChunkOverlap);

        // Utility Plugin Settings
        Preferences.Set("enable_utility_plugins", EnableUtilityPlugins);
        Preferences.Set("enable_datetime_plugin", EnableGetDateTimePlugin);
        Preferences.Set("enable_systeminfo_plugin", EnableSystemInfoPlugin);
        Preferences.Set("enable_urlvalidator_plugin", EnableUrlValidatorPlugin);
        Preferences.Set("enable_websearch_plugin", EnableWebSearchPlugin);

        if (!string.IsNullOrEmpty(BraveApiKey))
            await SecureStorage.SetAsync("brave_api_key", BraveApiKey);

        // File Plugin Settings
        Preferences.Set("enable_file_plugins", EnableFilePlugins);
        Preferences.Set("enable_fileinfo_plugin", EnableFileInfoPlugin);
        Preferences.Set("enable_fileoperations_plugin", EnableFileOperationsPlugin);
        Preferences.Set("enable_fileworkflow_plugin", EnableFileWorkflowPlugin);
        Preferences.Set("enable_readfile_plugin", EnableReadFilePlugin);
        Preferences.Set("enable_securefileoperations_plugin", EnableSecureFileOperationsPlugin);
        Preferences.Set("enable_securereadfile_plugin", EnableSecureReadFilePlugin);
        Preferences.Set("enable_stringmanipulator_plugin", EnableStringManipulatorPlugin);
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            // Load LLM settings from secure storage
            LLMProvider = await SecureStorage.GetAsync("llm_provider") ?? _configuration["LLMProvider"] ?? "anthropic";
            EmbeddingProvider = Preferences.Get("embedding_provider", _configuration["EmbeddingProvider"] ?? "local");
            OpenAIKey = await SecureStorage.GetAsync("openai_key") ?? _configuration["ApiKeys:OpenAI"];
            AnthropicKey = await SecureStorage.GetAsync("anthropic_key") ?? _configuration["ApiKeys:Anthropic"];
            TogetherAIKey = await SecureStorage.GetAsync("togetherai_key") ?? _configuration["ApiKeys:TogetherAI"];

            // Debug logging to verify loaded keys
            System.Diagnostics.Debug.WriteLine($"AppSettingsService.LoadSettingsAsync:");
            System.Diagnostics.Debug.WriteLine($"  OpenAIKey from storage: {OpenAIKey?.Substring(0, Math.Min(20, OpenAIKey?.Length ?? 0))}...");
            System.Diagnostics.Debug.WriteLine($"  LocalModelPath from prefs: {LocalModelPath}");

            // Load LLM preferences
            OpenAIModel = Preferences.Get("openai_model", _configuration["Models:OpenAI"] ?? "gpt-4o-mini");
            AnthropicModel = Preferences.Get("anthropic_model", _configuration["Models:Anthropic"] ?? "claude-3-5-sonnet-20241022");
            TogetherAIModel = Preferences.Get("togetherai_model", _configuration["Models:TogetherAI"] ?? "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo");
            LocalModelPath = Preferences.Get("local_model_path", _configuration["LocalModel:Path"] ?? "models/llama-3.2-1b-q4.gguf");
            MaxTokens = Preferences.Get("max_tokens", int.Parse(_configuration["RAG:MaxTokens"] ?? "1024"));
            Temperature = Preferences.Get("temperature", float.Parse(_configuration["RAG:Temperature"] ?? "0.7"));

            // Load Embedding & Vector Search settings
            EnableVectorSearch = Preferences.Get("enable_vector_search", true);
            EmbeddingModel = Preferences.Get("embedding_model", "all-MiniLM-L6-v2");
            AutoDownloadEmbeddings = Preferences.Get("auto_download_embeddings", false);
            UseChunking = Preferences.Get("use_chunking", false);
            ChunkSize = Preferences.Get("chunk_size", 512);
            ChunkOverlap = Preferences.Get("chunk_overlap", 50);

            // Load Utility Plugin settings
            EnableUtilityPlugins = Preferences.Get("enable_utility_plugins", true);
            EnableGetDateTimePlugin = Preferences.Get("enable_datetime_plugin", true);
            EnableSystemInfoPlugin = Preferences.Get("enable_systeminfo_plugin", true);
            EnableUrlValidatorPlugin = Preferences.Get("enable_urlvalidator_plugin", true);
            EnableWebSearchPlugin = Preferences.Get("enable_websearch_plugin", false);
            BraveApiKey = await SecureStorage.GetAsync("brave_api_key") ?? _configuration["ApiKeys:Brave"];

            // Load File Plugin settings
            EnableFilePlugins = Preferences.Get("enable_file_plugins", false);
            EnableFileInfoPlugin = Preferences.Get("enable_fileinfo_plugin", true);
            EnableFileOperationsPlugin = Preferences.Get("enable_fileoperations_plugin", false);
            EnableFileWorkflowPlugin = Preferences.Get("enable_fileworkflow_plugin", false);
            EnableReadFilePlugin = Preferences.Get("enable_readfile_plugin", true);
            EnableSecureFileOperationsPlugin = Preferences.Get("enable_securefileoperations_plugin", false);
            EnableSecureReadFilePlugin = Preferences.Get("enable_securereadfile_plugin", true);
            EnableStringManipulatorPlugin = Preferences.Get("enable_stringmanipulator_plugin", true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
    }
}