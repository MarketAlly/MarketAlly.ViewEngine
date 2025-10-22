# MarketAlly.ViewEngine v2.0.0 - Upgrade Guide

## 🎉 Major Features Added

### 1. **BrowserView Control** - Hybrid WebView + PDF Viewer
A new `BrowserView` control that seamlessly switches between web content and PDF viewing without requiring external apps.

### 2. **Custom Navigation History**
Complete navigation history management with:
- Full URL and title tracking
- Favicon extraction
- Timestamps
- Optional thumbnails
- Searchable history
- Persistence (export/import as JSON)

### 3. **Inline PDF Viewing**
Embedded PDF renderer that automatically detects and displays PDFs inline using the integrated Vitvov.Maui.PDFView source code.

---

## 📦 What's New

### New Files Added
- **BrowserView.cs** - Main hybrid control (recommended over WebView)
- **PdfView.cs** - Internal PDF viewer component
- **IPdfView.cs** - PDF viewer interface
- **PageAppearance.cs** - PDF appearance configuration
- **DataSources/** - PDF data source abstractions
- **Events/** - PDF-related events
- **Helpers/** - PDF helper utilities
- **Platforms/*/PdfView/** - Platform-specific PDF handlers

### Modified Files
- **CustomWebViewHandler.cs** - Added FaviconUrl extraction
- **CustomWebView.cs** - Unchanged (backward compatible)
- **MauiAppBuilderExtensions.cs** - Now registers PdfView handlers
- **MarketAlly.Maui.ViewEngine.csproj** - Version 2.0.0, updated description

### New Classes

#### NavigationHistoryItem
```csharp
public class NavigationHistoryItem
{
    public string Url { get; set; }
    public string Title { get; set; }
    public DateTime Timestamp { get; set; }
    public string FaviconUrl { get; set; }
    public ImageSource Thumbnail { get; set; }
}
```

#### PageData (Updated)
```csharp
public class PageData
{
    public string Title { get; set; }
    public string Body { get; set; }
    public string MetaDescription { get; set; }
    public string Url { get; set; }
    public string FaviconUrl { get; set; }  // NEW!
    public List<RouteInfo> Routes { get; set; }
    public List<RouteInfo> BodyRoutes { get; set; }
    public ImageSource Thumbnail { get; set; }
}
```

---

## 🚀 Usage Examples

### Basic Usage with BrowserView

```xaml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:marketally="clr-namespace:MarketAlly.Maui.ViewEngine;assembly=MarketAlly.Maui.ViewEngine"
             x:Class="YourApp.MainPage">

    <Grid>
        <!-- The new BrowserView control -->
        <marketally:BrowserView x:Name="browserView"
                                Source="https://example.com"
                                PageDataChanged="OnPageDataChanged"
                                MaxHistoryItems="100"
                                ShowInlinePdf="True"
                                EnableRouteExtraction="True"
                                EnableThumbnailCapture="False" />
    </Grid>
</ContentPage>
```

### Navigation History UI

```xaml
<Grid>
    <!-- History drawer -->
    <CollectionView ItemsSource="{Binding Source={x:Reference browserView}, Path=NavigationHistory}"
                    IsVisible="{Binding ShowHistory}">
        <CollectionView.ItemTemplate>
            <DataTemplate>
                <Grid Padding="10">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>

                    <Grid.GestureRecognizers>
                        <TapGestureRecognizer Command="{Binding NavigateCommand}"
                                             CommandParameter="{Binding .}" />
                    </Grid.GestureRecognizers>

                    <!-- Favicon -->
                    <Image Source="{Binding FaviconUrl}"
                           WidthRequest="16"
                           HeightRequest="16"
                           Grid.Column="0" />

                    <!-- Title and URL -->
                    <StackLayout Grid.Column="1" Padding="10,0">
                        <Label Text="{Binding Title}" FontAttributes="Bold" />
                        <Label Text="{Binding Url}" FontSize="12" TextColor="Gray" />
                    </StackLayout>

                    <!-- Timestamp -->
                    <Label Text="{Binding Timestamp, StringFormat='{0:HH:mm}'}"
                           Grid.Column="2"
                           VerticalOptions="Center" />
                </Grid>
            </DataTemplate>
        </CollectionView.ItemTemplate>
    </CollectionView>

    <!-- BrowserView -->
    <marketally:BrowserView x:Name="browserView" />

    <!-- Navigation buttons -->
    <HorizontalStackLayout VerticalOptions="End" Padding="10">
        <Button Text="◀ Back"
                Command="{Binding BackCommand}"
                IsEnabled="{Binding Source={x:Reference browserView}, Path=CanGoBackInHistory}" />
        <Button Text="Forward ▶"
                Command="{Binding ForwardCommand}"
                IsEnabled="{Binding Source={x:Reference browserView}, Path=CanGoForwardInHistory}" />
        <Button Text="📜 History"
                Command="{Binding ShowHistoryCommand}" />
    </HorizontalStackLayout>
</Grid>
```

### Code-Behind

```csharp
public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = new MainViewModel(browserView);
    }

    private void OnPageDataChanged(object sender, PageData pageData)
    {
        // Access favicon
        var favicon = pageData.FaviconUrl;

        // The history is automatically managed!
        Debug.WriteLine($"History count: {browserView.NavigationHistory.Count}");
    }
}

public class MainViewModel : INotifyPropertyChanged
{
    private readonly BrowserView _browserView;
    private bool _showHistory;

    public MainViewModel(BrowserView browserView)
    {
        _browserView = browserView;

        BackCommand = new Command(async () => await _browserView.GoBackInHistoryAsync());
        ForwardCommand = new Command(async () => await _browserView.GoForwardInHistoryAsync());
        ShowHistoryCommand = new Command(() => ShowHistory = !ShowHistory);
        NavigateCommand = new Command<NavigationHistoryItem>(async (item) =>
        {
            var index = _browserView.NavigationHistory.IndexOf(item);
            await _browserView.NavigateToHistoryItemAsync(index);
            ShowHistory = false;
        });
    }

    public bool ShowHistory
    {
        get => _showHistory;
        set { _showHistory = value; OnPropertyChanged(); }
    }

    public ICommand BackCommand { get; }
    public ICommand ForwardCommand { get; }
    public ICommand ShowHistoryCommand { get; }
    public ICommand NavigateCommand { get; }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
```

### History Persistence

```csharp
// Save history when app is closing
protected override void OnDisappearing()
{
    base.OnDisappearing();

    // Export navigation history
    var historyJson = browserView.ExportHistoryJson();
    Preferences.Set("BrowserHistory", historyJson);
}

// Restore history when app starts
protected override void OnAppearing()
{
    base.OnAppearing();

    // Import navigation history
    var historyJson = Preferences.Get("BrowserHistory", string.Empty);
    if (!string.IsNullOrEmpty(historyJson))
    {
        browserView.ImportHistoryJson(historyJson);
    }
}
```

### Search History

```csharp
// Search for pages containing "maui"
var results = browserView.SearchHistory("maui");
foreach (var item in results)
{
    Debug.WriteLine($"{item.Title} - {item.Url}");
}
```

### Programmatic Navigation

```csharp
// Navigate to URL
browserView.Source = new UrlWebViewSource { Url = "https://example.com" };

// Go back in history
await browserView.GoBackInHistoryAsync();

// Go forward in history
await browserView.GoForwardInHistoryAsync();

// Navigate to specific history item
await browserView.NavigateToHistoryItemAsync(5);

// Clear all history
browserView.ClearNavigationHistory();
```

### PDF Handling

```csharp
// PDFs are automatically detected and displayed inline
// No code changes needed!

// To disable inline PDF viewing:
browserView.ShowInlinePdf = false;  // PDFs will be downloaded/extracted as text instead
```

---

## 🔧 New Properties

### BrowserView Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `NavigationHistory` | `ObservableCollection<NavigationHistoryItem>` | Empty | Observable collection of navigation history |
| `CurrentHistoryIndex` | `int` | -1 | Current position in history (0-based) |
| `CanGoBackInHistory` | `bool` | false | True if can navigate backward |
| `CanGoForwardInHistory` | `bool` | false | True if can navigate forward |
| `MaxHistoryItems` | `int` | 50 | Maximum number of history items to keep |
| `ShowInlinePdf` | `bool` | true | Whether to display PDFs inline |

All existing WebView properties are also supported.

---

## 📋 New Methods

### Navigation Methods

```csharp
// Navigate in history
Task GoBackInHistoryAsync()
Task GoForwardInHistoryAsync()
Task NavigateToHistoryItemAsync(int index)

// History management
void ClearNavigationHistory()
string ExportHistoryJson()
void ImportHistoryJson(string json)
List<NavigationHistoryItem> SearchHistory(string query)

// Existing methods (still available)
Task<PageData> GetPageDataAsync()
Task<PageData> ExtractRoutesAsync()
Task<ImageSource> CaptureThumbnailAsync(int width = 320, int height = 180)
Task ScrollToTopAsync()
Task<string> EvaluateJavaScriptAsync(string script)
```

---

## 🔔 New Events

```csharp
// Fires when navigating to a history item
public event EventHandler<NavigationHistoryItem> NavigatingToHistoryItem;

// Existing events (still available)
public event EventHandler<PageData> PageDataChanged;
public event EventHandler<EventArgs> PageLoadComplete;
```

---

## ⚠️ Breaking Changes

**NONE!** This is a fully backward-compatible release.

- Existing `WebView` control continues to work exactly as before
- All existing properties, methods, and events are preserved
- New `BrowserView` control is opt-in

---

## 🔄 Migration Path

### Option 1: Keep Using WebView (No Changes Needed)
Your existing code continues to work without any modifications.

### Option 2: Upgrade to BrowserView (Recommended)

**Before:**
```xaml
<marketally:WebView x:Name="webView"
                    Source="https://example.com"
                    PageDataChanged="OnPageDataChanged" />
```

**After:**
```xaml
<marketally:BrowserView x:Name="browserView"
                        Source="https://example.com"
                        PageDataChanged="OnPageDataChanged" />
```

That's it! All existing properties and methods work the same way, plus you get:
- Inline PDF viewing
- Custom navigation history
- Favicon extraction
- History persistence
- Search functionality

---

## 🏗️ Architecture Changes

### PDF Viewing
- Integrated **Vitvov.Maui.PDFView** source code directly into the project
- No external NuGet dependency required
- All PDF classes are `internal` - not exposed in public API
- Automatic switching between WebView and PdfView based on content type

### Navigation History
- Completely replaces built-in MAUI WebView navigation
- Custom history tracking with full metadata
- Observable collection for real-time UI binding
- Smart duplicate detection
- Configurable history limits

### Favicon Extraction
- JavaScript-based extraction from page `<link>` tags
- Prefers apple-touch-icon and largest size
- Falls back to `/favicon.ico`
- Added to PageData.FaviconUrl

---

## 📝 Notes

1. **PDF files are automatically detected** by URL pattern and Content-Type
2. **Navigation history is managed automatically** - no manual tracking needed
3. **Favicons are extracted automatically** from every page load
4. **All existing WebView functionality remains unchanged**
5. **BrowserView is the recommended control** for new projects

---

## 🐛 Known Limitations

1. PDF viewing requires native platform support (Android API 24+, iOS 11+, Windows 10.0.17763+)
2. Cross-origin iframes cannot access favicons due to browser security
3. History is stored in memory - must call `ExportHistoryJson()` to persist across app restarts

---

## 💡 Tips

1. **Use BrowserView for new projects** to get all the new features
2. **Enable thumbnails sparingly** - they consume memory
3. **Set MaxHistoryItems** based on your app's needs (default 50 is reasonable)
4. **Search history** is case-insensitive and searches both titles and URLs
5. **Persist history** using ExportHistoryJson/ImportHistoryJson with Preferences or SecureStorage

---

## 🎯 Quick Start Checklist

- [ ] Update NuGet package to v2.0.0
- [ ] Replace `WebView` with `BrowserView` in XAML (optional but recommended)
- [ ] Add history UI (CollectionView) if desired
- [ ] Implement back/forward buttons using new navigation methods
- [ ] Add history persistence if needed
- [ ] Test PDF viewing with your PDF URLs
- [ ] Enjoy the new features!

---

## 📞 Support

For issues, questions, or feature requests:
- GitHub: https://github.com/MarketAlly/MarketAlly.ViewEngine
- Issues: https://github.com/MarketAlly/MarketAlly.ViewEngine/issues

---

**Version:** 2.0.0
**Date:** October 2025
**License:** MIT
