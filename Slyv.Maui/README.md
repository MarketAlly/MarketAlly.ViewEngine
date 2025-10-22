# Slyv.Maui Browser Application

A sample .NET MAUI application demonstrating the UnifiedBrowser.Maui control with AI-powered features.

## Features

This application showcases the UnifiedBrowser control with:

- **Full Web Browser**: Complete navigation with back, forward, refresh, and home
- **AI-Powered Summarization**: Generate intelligent summaries of web pages
- **Link Extraction**: Automatically extract and analyze all links from pages
- **Markdown Rendering**: View content in clean markdown format
- **DevExpress Toolbar**: Professional UI with toggle groups and context-aware pages
- **Multiple View Modes**: Outlook, Navigate, and Tabs modes

## Project Structure

```
Slyv.Maui/
├── MauiProgram.cs          # Service configuration and DI setup
├── AppShell.xaml           # Navigation structure with tabs
├── BrowserPage.xaml        # Full-featured browser implementation
├── SimpleBrowserPage.xaml  # Minimal browser implementation
├── MainPage.xaml           # Original template page (kept for reference)
└── Resources/
    └── Images/
        ├── globe.svg       # Browser tab icon
        └── home.svg        # Home tab icon
```

## Configuration

The application is configured in `MauiProgram.cs` to use OpenAI by default:

```csharp
builder.AddUnifiedBrowser(
    openAiApiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "sk-your-api-key-here",
    modelName: "gpt-4o-mini",
    configureBrowser: options =>
    {
        options.DefaultHomeUrl = "https://www.google.com";
        options.EnableAISummarization = true;
    }
);
```

### Available Provider Options

You can easily switch between different AI providers:

#### OpenAI (Default)
```csharp
builder.AddUnifiedBrowser(
    openAiApiKey: "your-api-key",
    modelName: "gpt-4o-mini"
);
```

#### Anthropic Claude
```csharp
builder.AddUnifiedBrowserWithClaude(
    anthropicApiKey: "your-api-key",
    modelName: "claude-3-5-sonnet-20241022"
);
```

#### Together.AI
```csharp
builder.AddUnifiedBrowserWithTogetherAI(
    togetherApiKey: "your-api-key",
    modelName: "meta-llama/Meta-Llama-3.1-70B-Instruct-Turbo"
);
```

#### Local Model
```csharp
builder.AddUnifiedBrowserWithLocalModel(
    modelPath: "path/to/model.gguf",
    maxTokens: 512
);
```

#### Basic Browser (No AI)
```csharp
builder.AddUnifiedBrowserBasic();
```

## Setting Up API Keys

### Method 1: Environment Variables (Recommended)

Set your API key as an environment variable:

**Windows (Command Prompt)**
```cmd
set OPENAI_API_KEY=sk-your-actual-api-key
```

**Windows (PowerShell)**
```powershell
$env:OPENAI_API_KEY="sk-your-actual-api-key"
```

**macOS/Linux**
```bash
export OPENAI_API_KEY=sk-your-actual-api-key
```

### Method 2: Direct Configuration

Replace the placeholder in `MauiProgram.cs`:
```csharp
openAiApiKey: "sk-your-actual-api-key"
```

**⚠️ Warning**: Never commit API keys to source control!

## Running the Application

1. **Prerequisites**
   - .NET 9.0 SDK
   - Visual Studio 2022 or VS Code with .NET MAUI workload
   - Android/iOS development tools configured

2. **Build and Run**
   ```bash
   # Restore packages
   dotnet restore

   # Build for Android
   dotnet build -t:Run -f net9.0-android

   # Build for iOS (macOS only)
   dotnet build -t:Run -f net9.0-ios
   ```

3. **Using Visual Studio**
   - Open `Slyv.Maui.csproj`
   - Select target platform (Android/iOS)
   - Press F5 to run with debugging

## Using the Browser

### Navigation
- **Back/Forward**: Navigate through history
- **Refresh**: Reload current page
- **Home**: Return to default home page
- **Address Bar**: Enter URLs or search terms

### AI Features
- **Generate Summary**: Click the markdown button to create an AI summary
- **Extract Links**: Click the links button to extract all page links
- **Auto-Summary**: Enable in settings to automatically summarize pages

### View Modes
- **Outlook Mode**: Quick access to News, Search, and Bookmarks
- **Navigate Mode**: Standard browser navigation controls
- **Tabs Mode**: Manage multiple browser tabs

## Customization

### Browser Properties

You can customize the browser behavior in `BrowserPage.xaml`:

```xml
<browser:UnifiedBrowserControl
    HomeUrl="https://www.example.com"
    EnableAISummarization="True"
    AutoGenerateSummary="True"
    ShowAddressBar="True"
    ShowLinksButton="True"
    ShowMarkdownButton="True"
    ShowShareButton="True"
    ShowSaveButton="True"
    EnableRouteExtraction="True"
    NormalizeRoutes="True"
    GeneratingSummaryMessage="Creating summary..."
/>
```

### Available Properties
- `HomeUrl`: Default home page URL
- `EnableAISummarization`: Enable/disable AI features
- `AutoGenerateSummary`: Auto-generate summaries on page load
- `ShowAddressBar`: Show/hide address bar
- `ShowLinksButton`: Show/hide links extraction button
- `ShowMarkdownButton`: Show/hide markdown view button
- `ShowShareButton`: Show/hide share button
- `ShowSaveButton`: Show/hide save button
- `EnableRouteExtraction`: Enable automatic route extraction
- `NormalizeRoutes`: Normalize extracted routes

## Event Handling

The browser control provides several events you can handle:

```csharp
// In BrowserPage.xaml.cs
BrowserControl.Navigating += (s, e) => {
    // Handle navigation starting
};

BrowserControl.Navigated += (s, e) => {
    // Handle navigation complete
};

BrowserControl.PageLoadComplete += (s, e) => {
    // Handle page fully loaded
};

BrowserControl.LinkSelected += (s, e) => {
    // Handle link selection from links view
};
```

## Troubleshooting

### Build Errors

If you encounter build errors:
1. Ensure all NuGet packages are restored: `dotnet restore`
2. Clean and rebuild: `dotnet clean && dotnet build`
3. Check that DevExpress packages are properly licensed

### AI Features Not Working

1. Verify API key is set correctly
2. Check internet connectivity
3. Review logs for API errors
4. Ensure selected model is available for your API key

### Performance Issues

1. Disable auto-summary for better performance
2. Reduce link extraction limit
3. Use a faster AI model (e.g., gpt-4o-mini)
4. Consider using local models for offline use

## Dependencies

- **UnifiedBrowser.Maui**: The main browser control
- **UnifiedData.Maui**: Data persistence and storage
- **UnifiedRag.Maui**: AI/RAG functionality
- **DevExpress.Maui**: Professional UI controls
- **CommunityToolkit.Maui**: Additional MAUI utilities
- **MarketAlly.ViewEngine**: Enhanced WebView

## License

This is a sample application demonstrating the UnifiedBrowser.Maui control.

## Support

For issues with the UnifiedBrowser control, refer to the UnifiedBrowser.Maui documentation.