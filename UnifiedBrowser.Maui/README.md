# UnifiedBrowser.Maui

A comprehensive browser control library for .NET MAUI applications with AI-powered summarization, multi-tab support, bookmark management, persistent state, browsing history, and link extraction capabilities.

[![NuGet](https://img.shields.io/nuget/v/UnifiedBrowser.Maui.svg)](https://www.nuget.org/packages/UnifiedBrowser.Maui/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

### 🌐 Core Browser Functionality
- Full WebView integration with MarketAlly.ViewEngine
- Navigation controls (back, forward, home, refresh)
- Address bar with URL entry and search
- Page load status indicators with progress bar
- Customizable user agent and user agent modes
- Multiple platform support (iOS, Android, Windows, macOS)

### 📑 Multi-Tab Support
- Create, switch, and close multiple tabs
- Tab state persistence across app sessions
- Tab thumbnails with automatic capture
- Tab switcher with grid/list view
- Configurable maximum tabs limit
- Drag-to-close gesture support

### 🔖 Bookmark Management
- One-click bookmark toggle for current page
- Visual bookmark indicator (filled/unfilled icon)
- Import/export bookmarks in Netscape HTML format
- Compatible with Chrome, Firefox, Edge, Safari
- Persistent bookmark storage
- Quick access from toolbar

### 📜 Browsing History
- Automatic history tracking for visited pages
- Filter history by topic and date
- Search through history
- View saved page summaries offline
- Delete individual items or clear all history
- Topic-based organization

### 🔗 Smart Link Extraction
- Automatic link extraction from pages
- Ad detection and filtering
- Link occurrence counting and ranking
- Configurable maximum links (default: 200)
- Copy all links as JSON array
- Filter view to show/hide ads

### 🤖 AI-Powered Summarization
- AI-generated page summaries using UnifiedRag.Maui
- Markdown rendering with syntax highlighting
- Configurable token limits (MaxContextTokens, MaxTokens)
- Support for multiple LLM providers:
  - OpenAI (GPT-4, GPT-3.5)
  - Anthropic Claude
  - Together.AI (Llama, Mixtral, etc.)
  - Local models via LlamaSharp
- Automatic or on-demand generation
- Summary persistence in database

### 💾 Data Integration
- Save pages to UnifiedData storage
- Full-text search capabilities
- Vector search with embeddings
- Metadata and topic preservation
- Offline access to saved content
- Cloud sync support

### 🎨 Highly Customizable UI
- 30+ bindable properties
- Toggle individual UI components
- Customizable colors for light/dark themes
- Adjustable toolbar layouts
- DevExpress controls integration
- Touch gesture support

## Installation

```bash
dotnet add package UnifiedBrowser.Maui
```

## Quick Start

### 1. Configure in MauiProgram.cs

The UnifiedBrowser.Maui package includes extension methods for easy setup:

```csharp
using UnifiedBrowser.Maui.Extensions;
using DevExpress.Maui;
using CommunityToolkit.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            // REQUIRED: Initialize DevExpress and CommunityToolkit
            .UseDevExpress(useLocalization: false)
            .UseDevExpressCollectionView()
            .UseDevExpressControls()
            .UseDevExpressEditors()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Configure UnifiedBrowser with OpenAI
        builder.AddUnifiedBrowser(
            openAiApiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            modelName: "gpt-4o-mini",
            configureBrowser: options =>
            {
                options.DefaultHomeUrl = "https://www.google.com";
                options.EnableAISummarization = true;
            }
        );

        return builder.Build();
    }
}
```

### 2. Use in XAML

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:browser="clr-namespace:UnifiedBrowser.Maui.Controls;assembly=UnifiedBrowser.Maui">

    <browser:UnifiedBrowserControl
        HomeUrl="https://www.google.com"
        ShowAddressBar="True"
        EnableAISummarization="True"
        EnableJSONState="True"
        BookmarksFilePath="{Binding BookmarksPath}"
        MaxRoutes="200"
        MaxContextTokens="8192"
        MaxTokens="4096" />

</ContentPage>
```

## Configuration Options

### Bindable Properties

#### Navigation & Display
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HomeUrl` | string | "https://www.google.com" | Default home page URL |
| `ShowAddressBar` | bool | true | Show/hide the address bar |
| `ShowBottomNavigation` | bool | true | Show/hide bottom navigation |
| `ShowNavigationInAddressBar` | bool | false | Show back/forward in address bar |
| `AutoLoadHomePage` | bool | true | Auto-load home page on start |
| `StartWithBlankTab` | bool | false | Start with a blank tab |

#### UI Components
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ShowTabsButton` | bool | true | Show/hide tabs button |
| `ShowLinksButton` | bool | true | Show/hide links extraction button |
| `ShowMarkdownButton` | bool | true | Show/hide markdown view button |
| `ShowShareButton` | bool | true | Show/hide share button |
| `ShowSaveButton` | bool | true | Show/hide save button |
| `ShowRefreshButton` | bool | true | Show/hide refresh button |
| `ShowHomeButton` | bool | false | Show/hide home button |
| `ShowSearchButton` | bool | false | Show/hide search button |

#### Link Extraction
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableRouteExtraction` | bool | false | Enable automatic route extraction |
| `NormalizeRoutes` | bool | true | Normalize extracted routes |
| `EnableAdDetection` | bool | true | Detect and flag ads |
| `MaxRoutes` | int | 200 | Maximum links to extract |

#### AI Features
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableAISummarization` | bool | true | Enable AI features |
| `AutoGenerateSummary` | bool | false | Auto-generate summary on load |
| `GeneratingSummaryMessage` | string | "Generating summary..." | Loading message |
| `MaxContextTokens` | int | 8192 | Max tokens for context |
| `MaxTokens` | int | 4096 | Max tokens for generation |

#### State & Storage
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableJSONState` | bool | false | Enable tab state persistence |
| `BookmarksFilePath` | string | null | Path to bookmarks file |
| `DefaultBookmarkTitle` | string | "MarketAlly" | Default bookmark title |
| `DefaultBookmarkUrl` | string | "https://www.marketally.com" | Default bookmark URL |
| `ShowMarkdownHistory` | bool | true | Show markdown in history |

#### Thumbnails
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableThumbnailCapture` | bool | true | Enable thumbnail capture |
| `ThumbnailStoragePath` | string | null | Thumbnail storage location |
| `ThumbnailWidth` | int | 640 | Thumbnail width in pixels |
| `ThumbnailHeight` | int | 360 | Thumbnail height in pixels |
| `CaptureThumbnailOnNavigation` | bool | true | Auto-capture after navigation |

#### Appearance
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IconColorLight` | Color | #007AFF | Icon color for light theme |
| `IconColorDark` | Color | #0A84FF | Icon color for dark theme |
| `ToolbarBackgroundColorLight` | Color | #F5F5F5 | Toolbar color (light) |
| `ToolbarBackgroundColorDark` | Color | #1C1C1E | Toolbar color (dark) |

#### Advanced
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UserAgent` | string | "" | Custom user agent string |
| `UserAgentMode` | enum | Default | User agent mode |

## Public Methods

### Navigation
```csharp
// Navigate to a URL
browser.NavigateTo("https://example.com");

// Navigation controls
await browser.GoBackAsync();
await browser.GoForwardAsync();
await browser.RefreshAsync();
browser.GoHome();

// Get current state
string url = browser.CurrentUrl;
string content = browser.PageContent;
```

### Tabs
```csharp
// Get the tab manager
var tabManager = browser.TabManager;

// Tab operations through view model
await browser._viewModel.AddTabCommand.ExecuteAsync(null);
await browser._viewModel.ViewTabsCommand.ExecuteAsync(null);
```

### Bookmarks
```csharp
// Add bookmark for current page
await browser.AddBookmarkAsync();

// Add bookmark with specific title and URL
await browser.AddBookmarkAsync("Google", "https://www.google.com");

// Delete bookmark
await browser.DeleteBookmarkAsync("https://example.com");

// Check if bookmarked
bool isBookmarked = browser.IsBookmarked("https://example.com");

// Import/Export
await browser.ImportBookmarksAsync("/path/to/bookmarks.html");
await browser.ExportBookmarksAsync("/path/to/bookmarks.html");

// Get bookmark count and list
int count = browser.GetBookmarkCount();
var bookmarks = browser.GetBookmarks();
```

### Links
```csharp
// Extract links from current page
var links = await browser.ExtractLinksAsync();

foreach (var link in links)
{
    Console.WriteLine($"{link.Title}: {link.Url}");
    Console.WriteLine($"  Occurrences: {link.Occurrences}");
    Console.WriteLine($"  Is Ad: {link.IsPotentialAd}");
}

// Show links view
browser.ShowLinks();
```

### AI Summarization
```csharp
// Generate AI summary
string summary = await browser.GenerateSummaryAsync();

// Load URL with auto-summary
await browser.LoadUrlAsync("https://example.com", generateSummary: true);

// Show markdown view
browser.ShowMarkdownView();
```

### Thumbnails
```csharp
// Capture thumbnail for current tab
await browser.CaptureTabThumbnailAsync();

// Configure thumbnail settings
browser.ThumbnailWidth = 1024;
browser.ThumbnailHeight = 576;
browser.CaptureThumbnailOnNavigation = true;
```

## Events

```csharp
// Navigation events
browser.Navigating += (sender, e) =>
{
    Console.WriteLine($"Navigating to: {e.Url}");
    // Can cancel: e.Cancel = true;
};

browser.Navigated += (sender, e) =>
{
    Console.WriteLine($"Navigated: {e.Url} - {e.Result}");
};

browser.PageLoadComplete += (sender, e) =>
{
    Console.WriteLine("Page fully loaded");
};

// Page data changes
browser.PageDataChanged += (sender, pageData) =>
{
    Console.WriteLine($"Title: {pageData.Title}");
    Console.WriteLine($"Links: {pageData.BodyRoutes?.Count ?? 0}");
};

// Link selection
browser.LinkSelected += (sender, e) =>
{
    Console.WriteLine($"Selected: {e.SelectedLink.Url}");
};

// Page title changes
browser.PageTitleChanged += (sender, e) =>
{
    Console.WriteLine($"Title: {e.Title}");
};

// Summary generation
browser.SummaryGenerated += (sender, e) =>
{
    Console.WriteLine($"Summary: {e.Summary}");
};
```

## Tab State Persistence

Enable automatic tab state saving and restoration:

```csharp
browser.EnableJSONState = true;
browser.ThumbnailStoragePath = Path.Combine(
    FileSystem.AppDataDirectory,
    "thumbnails"
);
```

**State includes:**
- All open tabs with URLs and titles
- Active tab selection
- Tab thumbnails
- Last accessed timestamps

**Storage locations:**
- Android: `/data/data/[app]/files/browser_tabs_state.json`
- iOS: `[sandbox]/Documents/browser_tabs_state.json`

## Bookmark Management

Configure bookmark storage:

```csharp
browser.BookmarksFilePath = Path.Combine(
    FileSystem.AppDataDirectory,
    "bookmarks.html"
);

browser.DefaultBookmarkTitle = "MyApp";
browser.DefaultBookmarkUrl = "https://myapp.com";
```

**Features:**
- Netscape Bookmark HTML format (cross-browser compatible)
- Import from Chrome, Firefox, Edge, Safari
- Export for backup or sharing
- Automatic persistence
- Visual bookmark indicator in toolbar

## Browsing History

History is automatically tracked when UnifiedData service is configured. Pages are saved when:
- AI summary is generated
- User explicitly saves the page
- Page content is stored for offline access

**History features:**
- Filter by topic or date
- Search through history
- View saved summaries offline
- Delete individual items
- Clear all history
- Organized by creation date

Access history through the toolbar history button or programmatically through `UnifiedDataService`.

## Provider Setup Options

### OpenAI
```csharp
builder.AddUnifiedBrowser(
    openAiApiKey: "your-api-key",
    modelName: "gpt-4o-mini"
);
```

### Anthropic Claude
```csharp
builder.AddUnifiedBrowserWithClaude(
    anthropicApiKey: "your-api-key",
    modelName: "claude-3-5-sonnet-20241022"
);
```

### Together.AI
```csharp
builder.AddUnifiedBrowserWithTogetherAI(
    togetherApiKey: "your-api-key",
    modelName: "meta-llama/Meta-Llama-3.1-70B-Instruct-Turbo"
);
```

### Local Model
```csharp
builder.AddUnifiedBrowserWithLocalModel(
    modelPath: "/path/to/model.gguf",
    maxTokens: 512
);
```

### Basic (No AI)
```csharp
builder.AddUnifiedBrowserBasic();
```

## Use Cases

### Research Browser with History
```csharp
var browser = new UnifiedBrowserControl
{
    EnableAISummarization = true,
    AutoGenerateSummary = true,
    ShowSaveButton = true,
    EnableJSONState = true,
    MaxContextTokens = 16384,
    MaxTokens = 8192
};
```

### Document Viewer with Bookmarks
```csharp
var browser = new UnifiedBrowserControl
{
    HomeUrl = "https://docs.company.com",
    ShowAddressBar = false,
    ShowMarkdownButton = true,
    EnableAISummarization = true,
    BookmarksFilePath = Path.Combine(
        FileSystem.AppDataDirectory,
        "bookmarks.html"
    )
};
```

### Link Aggregator
```csharp
var browser = new UnifiedBrowserControl
{
    ShowLinksButton = true,
    EnableRouteExtraction = true,
    EnableAdDetection = true,
    MaxRoutes = 500
};
```

## Requirements

- .NET 9.0 or later
- .NET MAUI
- **Required Packages:**
  - UnifiedRag.Maui (for AI features)
  - UnifiedData.Maui (for storage features)
  - MarketAlly.ViewEngine (for enhanced WebView)
  - MarketAlly.Dialogs.Maui (for dialogs)
  - MarketAlly.TouchEffect.Maui (for touch gestures)
  - Markdig (for markdown rendering)
  - DevExpress.Maui.* (UI controls)
  - CommunityToolkit.Maui (MAUI toolkit)
  - CommunityToolkit.Mvvm (MVVM toolkit)

## Platform Support

- ✅ iOS 11.0+
- ✅ Android 24+ (API Level 24)
- ⚠️ Windows 10.0.17763+ (limited testing)
- ⚠️ macOS 13.1+ (limited testing)

## Performance Considerations

1. **AI Summarization**: On-demand to avoid unnecessary API calls
2. **Link Extraction**: Limited to configurable maximum (default 200)
3. **Content Extraction**: Configurable token limits for summarization
4. **Tab State**: Saved asynchronously to avoid UI blocking
5. **Thumbnails**: Captured at 2x resolution for high-DPI displays
6. **History**: Indexed for fast search and filtering

## License

This project is licensed under the MIT License.

## Author

David H Friedel Jr

## Company

MarketAlly

## Support

For issues, questions, or feature requests, please open an issue on the GitHub repository.

## Contributing

Contributions are welcome! Please read our contributing guidelines before submitting PRs.

---

Built with dedication by the MarketAlly team for the .NET MAUI community.
