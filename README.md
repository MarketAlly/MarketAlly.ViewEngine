# MarketAlly.Maui.ViewEngine

## 📢 Overview
`MarketAlly.Maui.ViewEngine` is a powerful `.NET MAUI` WebView control that mimics a **real browser**, enabling full JavaScript support, cookies, WebRTC, and custom User-Agent overrides. It works seamlessly across **Android, iOS, and Windows** with advanced content monitoring and automatic PDF extraction capabilities.

## 🚀 Features
✅ Supports **custom User-Agent**
✅ Enables **cookies, storage, WebRTC, and WebGL**
✅ **Bypasses WebView detection techniques**
✅ **PageDataChanged event** - Automatically triggered on navigation and dynamic content changes
✅ **Intelligent content monitoring** with debounced DOM change detection
✅ **Automatic PDF extraction** - Detects and extracts text from PDF URLs
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

## **📌 Advanced Features**

### **🔹 PageData Object**
The `PageData` object provides comprehensive information about the loaded page:

```csharp
public class PageData
{
    public string Title { get; set; }              // Page title
    public string Body { get; set; }               // Extracted text content
    public string Url { get; set; }                // Current URL
    public string MetaDescription { get; set; }    // Meta description tag
    public List<Route> Routes { get; set; }        // All links on the page
    public List<Route> BodyRoutes { get; set; }    // Links within the body content
}

public class Route
{
    public string Url { get; set; }      // Link URL
    public string Text { get; set; }     // Link text
    public bool IsAbsolute { get; set; } // Whether URL is absolute
}
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

```csharp
webView.PageDataChanged += (sender, pageData) =>
{
    // Get all navigation and content links
    Console.WriteLine($"Total links found: {pageData.Routes.Count}");

    // Get only links within the main content
    Console.WriteLine($"Content links: {pageData.BodyRoutes.Count}");

    // Example: Find external links in the content
    var externalContentLinks = pageData.BodyRoutes
        .Where(r => r.IsAbsolute && !r.Url.Contains(pageData.Url))
        .ToList();

    // Example: Get navigation menu items (links not in body)
    var navigationLinks = pageData.Routes
        .Except(pageData.BodyRoutes)
        .ToList();
};
```

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

---

## **📌 Performance Considerations**

- **Debouncing**: All content change events are debounced with a 1-second delay to prevent performance issues from rapid DOM mutations
- **Selective Monitoring**: The DOM observer only watches specific attributes and significant changes
- **Async Operations**: All page data extraction is asynchronous and timeout-protected (10-second timeout)
- **PDF Handling**: PDFs are processed in parallel to avoid blocking the UI thread

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
