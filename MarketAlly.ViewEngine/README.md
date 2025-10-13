# MarketAlly.Maui.ViewEngine

## 📢 Overview
`MarketAlly.Maui.ViewEngine` is a powerful `.NET MAUI` WebView control that mimics a **real browser**, enabling full JavaScript support, cookies, WebRTC, and custom User-Agent overrides. It works seamlessly across **Android, iOS, and Windows** with advanced content monitoring and automatic PDF extraction capabilities.

## 🚀 Features
✅ Supports **custom User-Agent**
✅ Enables **cookies, storage, WebRTC, and WebGL**
✅ **Bypasses WebView detection techniques**
✅ **PageDataChanged event** - Automatically triggered on navigation and dynamic content changes
✅ **Intelligent content monitoring** with debounced DOM change detection
✅ **On-demand route extraction** - Zero performance impact by default, extract links only when needed
✅ **Thumbnail capture** - Generate page screenshots on-demand or automatically
✅ **Automatic PDF extraction** - Detects and extracts text from PDF URLs
✅ **Ad detection and filtering** - Identifies potential ads with scoring system
✅ **Cross-platform JavaScript injection** for custom behaviors
✅ **Fully compatible with .NET MAUI**
✅ Works on **Android, iOS, and Windows**

---

## **📌 Installation**
To install the package, run the following command in your .NET MAUI app:
```sh
dotnet add package MarketAlly.Maui.ViewEngine
```
Or, in **Visual Studio**:
1. Open **Package Manager Console**.
2. Run:
   ```powershell
   Install-Package MarketAlly.Maui.ViewEngine
   ```

---

## **📌 Setup in `MauiProgram.cs`**
After installing, you **must register the custom WebView handler** inside your `.NET MAUI` app.

### **Modify `MauiProgram.cs` to add the handler:**

**Option 1: Using the Extension Method (Recommended)**
```csharp
using MarketAlly.Maui.ViewEngine;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { /* ... */ })
            .UseMarketAllyViewEngine(); // ✅ One line registration!

        return builder.Build();
    }
}
```

**Option 2: Manual Registration**
```csharp
using MarketAlly.Maui.ViewEngine;

var builder = MauiApp.CreateBuilder();
builder.UseMauiApp<App>();

// Register the custom WebView handler
builder.ConfigureMauiHandlers(handlers =>
{
    handlers.AddHandler(typeof(MarketAlly.Maui.ViewEngine.WebView),
                       typeof(MarketAlly.Maui.ViewEngine.WebViewHandler));
});

return builder.Build();
```

✅ **This ensures your app correctly loads the `WebView` with platform-specific optimizations.**

---

## **📌 How to Use `WebView`**
Once registered, you can use `WebView` in **XAML** or **C#**.

### **🔹 Using in XAML**
```xml
<ContentPage xmlns:viewengine="clr-namespace:MarketAlly.Maui.ViewEngine;assembly=MarketAlly.Maui.ViewEngine">
    <viewengine:WebView
        Source="https://example.com"
        UserAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        EnableRouteExtraction="false"
        NormalizeRoutes="true"
        MaxRoutes="100"
        PageDataChanged="WebView_PageDataChanged"/>
</ContentPage>
```

### **🔹 Handling Page Data Changes**
```csharp
private void WebView_PageDataChanged(object sender, PageData pageData)
{
    // Access extracted page data
    string title = pageData.Title;
    string bodyText = pageData.Body;
    string url = pageData.Url;
    string metaDescription = pageData.MetaDescription;

    // Access all links found on the page (navigation, header, footer, etc.)
    var allLinks = pageData.Routes;
    foreach (var route in allLinks)
    {
        Console.WriteLine($"Link: {route.Text} -> {route.Url} (Absolute: {route.IsAbsolute})");
    }

    // Access links found specifically in the body content
    var contentLinks = pageData.BodyRoutes;
    foreach (var route in contentLinks)
    {
        Console.WriteLine($"Body Link: {route.Text} -> {route.Url}");
    }
}
```

### **🔹 Using in C#**
```csharp
using MarketAlly.Maui.ViewEngine;

var webView = new WebView
{
    Source = "https://example.com",
    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
};

webView.PageDataChanged += (sender, pageData) =>
{
    // Handle page data extraction
    Console.WriteLine($"Page Title: {pageData.Title}");
};

Content = new StackLayout
{
    Children = { webView }
};
```

### **🔹 Manually Extract Page Data**
```csharp
var pageData = await webView.GetPageDataAsync();
Console.WriteLine($"Title: {pageData.Title}");
Console.WriteLine($"Body: {pageData.Body}");
Console.WriteLine($"URL: {pageData.Url}");
```

---

## **📌 Using WebView in Custom Controls (Nested Usage)**

### **🔹 Handler Registration is Required**

⚠️ **IMPORTANT:** Any app using this control **must** register the handler in `MauiProgram.cs` (see setup section above). Without this registration, the control will fail with "WebView handler is not available" errors.

### **🔹 Creating a Custom Control that Wraps WebView**

If you're building a custom control that includes this WebView, follow these patterns:

**Option A: Using XAML (Recommended)**

```xml
<!-- MyBrowserControl.xaml -->
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:viewengine="clr-namespace:MarketAlly.Maui.ViewEngine;assembly=MarketAlly.Maui.ViewEngine"
             xmlns:local="clr-namespace:MyApp.Controls"
             x:Class="MyApp.Controls.MyBrowserControl"
             x:Name="this">

    <Grid>
        <!-- Bind to the control's properties using RelativeSource -->
        <viewengine:WebView x:Name="BrowserView"
                           Source="{Binding Url, Source={RelativeSource AncestorType={x:Type local:MyBrowserControl}}}"
                           PageDataChanged="OnPageDataChanged" />
    </Grid>
</ContentView>
```

```csharp
// MyBrowserControl.xaml.cs
public partial class MyBrowserControl : ContentView
{
    public static readonly BindableProperty UrlProperty =
        BindableProperty.Create(nameof(Url), typeof(string), typeof(MyBrowserControl), default(string));

    public string Url
    {
        get => (string)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public MyBrowserControl()
    {
        InitializeComponent();
    }

    private void OnPageDataChanged(object sender, PageData pageData)
    {
        // Handle page data from the nested WebView
        // Optionally expose your own event or callback
    }
}
```

**Option B: Creating in Code-Behind**

```csharp
public partial class MyBrowserControl : ContentView
{
    private MarketAlly.Maui.ViewEngine.WebView _webView;

    public static readonly BindableProperty UrlProperty =
        BindableProperty.Create(nameof(Url), typeof(string), typeof(MyBrowserControl),
            default(string), propertyChanged: OnUrlChanged);

    public string Url
    {
        get => (string)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public MyBrowserControl()
    {
        InitializeComponent();

        _webView = new MarketAlly.Maui.ViewEngine.WebView();
        _webView.PageDataChanged += OnPageDataChanged;

        Content = _webView;
    }

    private static void OnUrlChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (MyBrowserControl)bindable;
        if (control._webView != null && newValue is string url)
        {
            control._webView.Source = url;
        }
    }

    private void OnPageDataChanged(object sender, PageData pageData)
    {
        // Handle page data
    }
}
```

### **🔹 Common Mistakes When Nesting**

❌ **Don't do this:**
```csharp
// Setting BindingContext = this blocks parent bindings
public MyBrowserControl()
{
    InitializeComponent();
    BindingContext = this; // ❌ WRONG - breaks nested control bindings
}
```

❌ **Don't reference the wrong WebView type:**
```csharp
// This uses MAUI's built-in WebView, not the custom one
Microsoft.Maui.Controls.WebView webView = new(); // ❌ WRONG
```

✅ **Do this:**
```csharp
// Always use the fully qualified type
MarketAlly.Maui.ViewEngine.WebView webView = new(); // ✅ CORRECT
```

✅ **Use RelativeSource binding in XAML:**
```xml
<!-- Bind to the parent control's properties -->
<viewengine:WebView
    Source="{Binding Url, Source={RelativeSource AncestorType={x:Type local:MyBrowserControl}}}" />
```

### **🔹 Handler Timing and Lifecycle**

The WebView handler is attached **asynchronously** after the control is added to the visual tree. The control automatically handles this:

```csharp
// The handler might not be immediately available
var webView = new MarketAlly.Maui.ViewEngine.WebView();
Debug.WriteLine($"Handler on creation: {webView.Handler}"); // null

// Add to visual tree
Content = webView;

// Handler will be attached after visual tree construction
// The control waits up to 5 seconds for handler attachment automatically
```

If you call `GetPageDataAsync()` or `ExtractRoutesAsync()` before the handler is attached, the control will:
1. Wait up to 5 seconds for the handler to attach
2. Return an error message if the handler never attaches
3. Include the handler type in error messages for debugging

### **🔹 Using in DataTemplate or ControlTemplate**

When using inside templates, bind to the item or templated parent:

```xml
<!-- Inside a DataTemplate (context is the data item) -->
<DataTemplate>
    <viewengine:WebView Source="{Binding ItemUrl}" />
</DataTemplate>

<!-- Inside a ControlTemplate (use TemplateBinding) -->
<ControlTemplate>
    <viewengine:WebView Source="{TemplateBinding Url}" />
</ControlTemplate>

<!-- Accessing page-level properties from inside a template -->
<viewengine:WebView>
    <viewengine:WebView.GestureRecognizers>
        <TapGestureRecognizer
            Command="{Binding Source={RelativeSource AncestorType={x:Type ContentPage}},
                             Path=BindingContext.NavigateCommand}" />
    </viewengine:WebView.GestureRecognizers>
</viewengine:WebView>
```

---

## **📌 Advanced Features**

### **🔹 PageData Object**
The `PageData` object provides comprehensive information about the loaded page:

```csharp
public class PageData
{
    public string Title { get; set; }                      // Page title
    public string Body { get; set; }                       // Extracted text content
    public string Url { get; set; }                        // Current URL
    public string MetaDescription { get; set; }            // Meta description tag
    public List<Route> Routes { get; set; }                // All links on the page
    public List<Route> BodyRoutes { get; set; }            // Links within the body content
    public Microsoft.Maui.Controls.ImageSource Thumbnail { get; set; }  // Optional thumbnail screenshot
}

public class Route
{
    public string Url { get; set; }      // Link URL
    public string Text { get; set; }     // Link text
    public bool IsAbsolute { get; set; } // Whether URL is absolute
}
```

### **🔹 On-Demand Route Extraction (Recommended)**

**NEW in v1.1.1:** Routes are **not extracted by default** for better performance. Extract them on-demand when needed:

```csharp
// Default behavior - Routes and BodyRoutes are empty (zero performance impact)
webView.PageDataChanged += (sender, pageData) =>
{
    Console.WriteLine($"Title: {pageData.Title}");
    Console.WriteLine($"Body: {pageData.Body}");
    // Routes are empty here for performance
};

// Extract routes on-demand (e.g., when user clicks "Show Links" button)
private async void ShowLinksButton_Clicked(object sender, EventArgs e)
{
    var pageData = await webView.ExtractRoutesAsync();

    Console.WriteLine($"Total links: {pageData.Routes.Count}");
    Console.WriteLine($"Content links: {pageData.BodyRoutes.Count}");

    // Display links to user
    foreach (var route in pageData.Routes)
    {
        Console.WriteLine($"{route.Rank}. {route.Text} -> {route.Url}");

        if (route.IsPotentialAd)
        {
            Console.WriteLine($"   [AD DETECTED: {route.AdReason}]");
        }
    }
}
```

### **🔹 Auto-Extract Routes (Original Behavior)**

If you want routes extracted automatically on every page load:

```xml
<viewengine:WebView
    EnableRouteExtraction="true"
    MaxRoutes="100"
    PageDataChanged="WebView_PageDataChanged"/>
```

```csharp
webView.PageDataChanged += (sender, pageData) =>
{
    // Routes are automatically populated
    Console.WriteLine($"Total links: {pageData.Routes.Count}");
};
```

### **🔹 Understanding Routes vs BodyRoutes**

The WebView extracts links from the page in two different collections:

**Routes** - All links found anywhere on the page:
- Navigation menus (header, sidebar, footer)
- Buttons and action links
- Body content links
- Useful for discovering all possible navigation paths

**BodyRoutes** - Links found specifically within the main content area:
- Article links
- Content references
- In-text citations
- Useful for content-focused link analysis

**RouteInfo** properties:
```csharp
public class RouteInfo
{
    public string Url { get; set; }              // Link URL
    public string Text { get; set; }             // Link text
    public int Rank { get; set; }                // Ranking (non-ads first)
    public int Occurrences { get; set; }         // Times this URL appears
    public bool IsPotentialAd { get; set; }      // Ad detection flag
    public string AdReason { get; set; }         // Why flagged as ad
    public List<string> AllTexts { get; set; }   // All text variations
}
```

### **🔹 Ad Detection**

Routes are automatically analyzed for potential ads using:
- `rel="sponsored"` attributes
- Ad-related CSS classes/IDs
- Known ad network domains
- Tracking/affiliate parameters

```csharp
var pageData = await webView.ExtractRoutesAsync();

// Filter out ads
var regularLinks = pageData.Routes
    .Where(r => !r.IsPotentialAd)
    .ToList();

// Get only ads
var adLinks = pageData.Routes
    .Where(r => r.IsPotentialAd)
    .ToList();

foreach (var ad in adLinks)
{
    Console.WriteLine($"Ad detected: {ad.Url}");
    Console.WriteLine($"Reason: {ad.AdReason}");
}
```

### **🔹 URL Normalization and Exclude Domains**

**NormalizeRoutes** (default: true) removes tracking parameters to group duplicate URLs:

```xml
<!-- Default: URLs are normalized (tracking params removed) -->
<viewengine:WebView NormalizeRoutes="true" />

<!-- Keep all URLs exactly as they appear -->
<viewengine:WebView NormalizeRoutes="false" />
```

**ExcludeDomains** allows specific domains to bypass normalization:

```xml
<!-- C# code -->
webView.ExcludeDomains = new List<string> { "example.com", "api.mysite.com" };
```

When a domain is excluded:
- All query parameters are preserved
- Fragments are preserved
- Matches exact domain and subdomains (e.g., "example.com" matches "api.example.com")
- Useful for APIs or sites where query params are essential

**Example:**
```csharp
// Normalize Google URLs but preserve API parameters
webView.NormalizeRoutes = true;
webView.ExcludeDomains = new List<string> { "api.myapp.com", "auth.provider.com" };

var pageData = await webView.ExtractRoutesAsync();

// Google URLs grouped:
//   https://google.com/search?q=test&zx=123 → https://google.com/search?q=test
//   https://google.com/search?q=test&zx=456 → https://google.com/search?q=test
//   (Both become same URL, Occurrences = 2)

// API URLs preserved exactly:
//   https://api.myapp.com/data?key=abc&session=xyz (kept as-is)
```

### **🔹 Performance Optimization**

**MaxRoutes** limits the number of links processed (default: 100):

```xml
<!-- Limit to 50 links for faster processing -->
<viewengine:WebView MaxRoutes="50" />

<!-- Unlimited (not recommended for pages with 1000+ links) -->
<viewengine:WebView MaxRoutes="-1" />
```

**Best Practices:**
- Keep `EnableRouteExtraction="false"` (default) for best performance
- Call `ExtractRoutesAsync()` only when user needs links
- Use `MaxRoutes` to limit processing on link-heavy pages
- Use `ExcludeDomains` for domains where query parameters matter
- Ad detection runs asynchronously in the background

### **🔹 Automatic PDF Detection and Extraction**
The WebView automatically detects PDF URLs (by extension, content-type, or URL patterns) and extracts text content:

```csharp
webView.PageDataChanged += (sender, pageData) =>
{
    if (pageData.Title.Contains("PDF"))
    {
        // PDF was detected and extracted
        Console.WriteLine($"PDF Pages: {pageData.MetaDescription}");
        Console.WriteLine($"PDF Text: {pageData.Body}");
    }
};
```

Supported PDF URL patterns:
- Direct `.pdf` file extensions
- arXiv PDF URLs
- URLs containing `/pdf/` in the path
- Query parameters: `content-type=pdf`, `type=pdf`, `format=pdf`

### **🔹 Thumbnail Capture**

The WebView can capture screenshots of the currently loaded webpage, similar to how route extraction works - on-demand by default for zero performance impact.

**On-Demand Capture (Recommended):**

```csharp
// Capture thumbnail when user clicks a button
private async void CaptureThumbnailButton_Clicked(object sender, EventArgs e)
{
    var thumbnail = await webView.CaptureThumbnailAsync(320, 180);

    if (thumbnail != null)
    {
        // Use the thumbnail (e.g., display in UI)
        ThumbnailImage.Source = thumbnail;
    }
}

// With custom dimensions
var largeThumbnail = await webView.CaptureThumbnailAsync(640, 360);
```

**Automatic Capture:**

```xml
<!-- Enable automatic thumbnail capture on every page load -->
<viewengine:WebView
    EnableThumbnailCapture="true"
    PageDataChanged="WebView_PageDataChanged"/>
```

```csharp
webView.PageDataChanged += (sender, pageData) =>
{
    if (pageData.Thumbnail != null)
    {
        // Thumbnail was automatically captured
        ThumbnailImage.Source = pageData.Thumbnail;
        Console.WriteLine("Thumbnail captured automatically");
    }
};
```

**Use Cases:**
- **Tab Switchers**: Show previews of open tabs (use `EnableThumbnailCapture="true"`)
- **History/Bookmarks**: Capture thumbnails for visual browsing history
- **Share Functionality**: Capture screenshot to share with other apps
- **User-Triggered**: Capture only when user clicks "Take Screenshot" button (use `CaptureThumbnailAsync()`)

**Platform Implementation:**
- **Windows**: Uses `CoreWebView2.CapturePreviewAsync()` for high-quality screenshots
- **Android**: Uses `View.Draw()` to render WebView content to bitmap
- **iOS**: Uses `WKWebView.TakeSnapshotAsync()` for native screenshot capture

**Performance Notes:**
- Thumbnails are NOT generated by default for performance
- `CaptureThumbnailAsync()` returns `null` if capture fails or handler is unavailable
- Images are automatically resized with aspect ratio preservation
- Default size is 320x180 (16:9 aspect ratio)
- Temporary files are created and cleaned up automatically

**Example: Tab Switcher with Thumbnails**

```csharp
public class BrowserTab
{
    public string Url { get; set; }
    public string Title { get; set; }
    public ImageSource Thumbnail { get; set; }
}

// When switching tabs, capture current tab's thumbnail
private async void OnTabSwitching()
{
    if (currentTab != null)
    {
        currentTab.Thumbnail = await webView.CaptureThumbnailAsync();
    }
}

// Display tabs with thumbnails
<CollectionView ItemsSource="{Binding Tabs}">
    <CollectionView.ItemTemplate>
        <DataTemplate>
            <Grid>
                <Image Source="{Binding Thumbnail}"
                       WidthRequest="320"
                       HeightRequest="180"/>
                <Label Text="{Binding Title}"
                       VerticalOptions="End"/>
            </Grid>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

### **🔹 Content Monitoring**
The WebView includes intelligent DOM monitoring that detects:
- Page navigations
- JavaScript-based content updates
- Dynamic content loading (AJAX, Single Page Apps)
- User interactions (clicks that modify content)

**Debouncing:** Content change notifications are automatically debounced (1 second) to prevent excessive event firing from rapid DOM mutations.

### **🔹 Custom JavaScript Injection**
```csharp
// Inject custom JavaScript
await webView.Handler.InjectJavaScriptAsync("alert('Hello from MAUI!');");
```

---

## **📌 Platform-Specific Details**

### **Android**
- Uses native `Android.Webkit.WebView`
- Custom `WebViewClient` for navigation interception
- JavaScript interface for content change notifications
- Supports all Chrome WebView features

### **iOS**
- Uses `WKWebView` with full JavaScript support
- Custom `WKNavigationDelegate` for navigation handling
- Script message handlers for content monitoring
- Full cookie and storage support

### **Windows**
- Uses `WebView2` (Chromium-based)
- Navigation event handlers for content tracking
- Web message API for content change notifications
- Native Windows integration

---

## **📌 FAQ**

### ❓ **How does this WebView differ from the default .NET MAUI WebView?**
- `MarketAlly.Maui.ViewEngine.WebView` provides:
  - **PageDataChanged event** for automatic content extraction
  - **PDF detection and text extraction**
  - **Intelligent content monitoring** with DOM change detection
  - **Custom User-Agent** overrides
  - **Enhanced cookie and storage support**
  - **WebRTC and WebGL** capabilities
- The default `.NET MAUI WebView` lacks these advanced features.

### ❓ **When does the PageDataChanged event fire?**
The event fires automatically:
1. When navigation completes successfully
2. When JavaScript modifies page content (DOM changes)
3. After user interactions that change content (clicks)
4. When PDFs are detected and extracted

All events are debounced to prevent excessive firing.

### ❓ **Does this work with authentication-based websites?**
Yes! **Cookies and session data persist** between navigations, making it suitable for login-based websites.

### ❓ **Does this work on iOS, Android, and Windows?**
Yes! It uses:
- **Android:** Native WebView (with Chrome-like behavior)
- **iOS:** `WKWebView` with full JavaScript and cookie support
- **Windows:** `WebView2` (Chromium-based)

### ❓ **Can I use this with OAuth authentication?**
Yes! The WebView can be used for **OAuth authentication flows**, but we recommend opening authentication pages in the **system browser** (e.g., `SFSafariViewController` for iOS or `Custom Tabs` for Android) for better security.

### ❓ **How do I disable content monitoring?**
Content monitoring is built-in and optimized with debouncing. If you only need navigation events, simply don't subscribe to content change notifications from the monitoring script.

### ❓ **Can I customize the PDF extraction behavior?**
The PDF extraction happens automatically. You can identify PDF content in the `PageDataChanged` event by checking if the title contains "PDF" or by examining the `MetaDescription` which includes page count information.

### ❓ **I'm getting "WebView handler is not available" errors. What's wrong?**

This error means the custom handler is not registered. **Solution:**

1. **Check `MauiProgram.cs`** - Ensure you have this code:
   ```csharp
   // Option 1: Extension method (easiest)
   builder.UseMarketAllyViewEngine();

   // Option 2: Manual registration
   builder.ConfigureMauiHandlers(handlers =>
   {
       handlers.AddHandler(typeof(MarketAlly.Maui.ViewEngine.WebView),
                          typeof(MarketAlly.Maui.ViewEngine.WebViewHandler));
   });
   ```

2. **Verify the namespace** - Make sure you're using `MarketAlly.Maui.ViewEngine.WebView`, not `Microsoft.Maui.Controls.WebView`

3. **Check for timing issues** - If the error includes "timeout waiting for handler", the control is taking too long to attach to the visual tree. This can happen if:
   - The control is deeply nested
   - The parent control has complex initialization
   - Platform-specific handlers are slow to initialize

### ❓ **Can I use this control inside my own custom control?**

Yes! See the **"Using WebView in Custom Controls (Nested Usage)"** section above for complete examples and best practices.

**Key requirements:**
- ✅ Handler must be registered in `MauiProgram.cs`
- ✅ Don't set `BindingContext = this` in your custom control
- ✅ Use `RelativeSource` binding to connect parent properties
- ✅ Always reference `MarketAlly.Maui.ViewEngine.WebView`, not the built-in WebView

---

## **📌 Performance Considerations**

- **On-Demand Routes (v1.1.1)**: Routes are NOT extracted by default - zero performance impact unless you call `ExtractRoutesAsync()`
- **Route Limiting**: `MaxRoutes` property limits processing (default: 100 links)
- **Async Ad Detection**: Ad detection runs in background batches without blocking UI
- **Debouncing**: All content change events are debounced with a 1-second delay to prevent performance issues from rapid DOM mutations
- **Selective Monitoring**: The DOM observer only watches specific attributes and significant changes
- **Async Operations**: All page data extraction is asynchronous and timeout-protected (10-second timeout)
- **PDF Handling**: PDFs are processed in parallel to avoid blocking the UI thread
- **Memory Management**: Raw link data is cached for instant on-demand extraction, freed after ad processing

---

## **📌 Contributing**
Want to improve this library? Feel free to submit **issues** and **pull requests** on https://github.com/MarketAlly/MarketAlly.ViewEngine

---

## **📌 License**
This project is licensed under the **MIT License**.

---

## **📌 Support**
💬 **Need help?**
Open an issue on GitHub or contact us via email at `support@marketally.com`. 🚀
