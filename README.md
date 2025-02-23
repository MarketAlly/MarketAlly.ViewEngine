# MarketAlly.ViewEngine

## 📢 Overview
`MarketAlly.ViewEngine` is a powerful `.NET MAUI` WebView control that provides advanced browser capabilities, content monitoring, and PDF handling. It works seamlessly across **Android, iOS, and Windows**, offering features beyond the standard WebView implementation.

## 🚀 Features
✅ **Browser Capabilities**
- Custom User-Agent configuration
- Full cookie and storage support
- WebRTC and WebGL support
- Browser detection bypass
- Seamless navigation handling

✅ **Content Monitoring**
- Real-time DOM change detection
- Automatic content updates
- Click event monitoring
- Navigation tracking
- Source change notifications

✅ **PDF Integration**
- Automatic PDF download handling
- PDF text extraction
- Integrated PDF processing
- PDF content analysis

✅ **Data Extraction**
- Page title extraction
- HTML content capture
- URL tracking
- Meta description parsing
- Base64 content handling

## **📌 Installation**
Install the package in your .NET MAUI app using:
```sh
dotnet add package MarketAlly.ViewEngine
```

Or via **Visual Studio** Package Manager Console:
```powershell
Install-Package MarketAlly.ViewEngine
```

## **📌 Setup**

### **1. Register in `MauiProgram.cs`**
```csharp
using MarketAlly.ViewEngine;

var builder = MauiApp.CreateBuilder();
builder.UseMauiApp<App>();

builder.ConfigureMauiHandlers(handlers =>
{
    handlers.AddHandler(typeof(CustomWebView), typeof(CustomWebViewHandler));
});

return builder.Build();
```

### **2. XAML Implementation**
```xml
<ContentPage xmlns:controls="clr-namespace:MarketAlly.ViewEngine;assembly=MarketAlly.ViewEngine">
    <controls:CustomWebView 
        x:Name="webView"
        Source="https://example.com"
        UserAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        PageDataChanged="OnPageDataChanged"/>
</ContentPage>
```

### **3. C# Implementation**
```csharp
using MarketAlly.ViewEngine;

var webView = new CustomWebView
{
    Source = "https://example.com",
    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
};

webView.PageDataChanged += async (sender, pageData) =>
{
    Console.WriteLine($"Page Title: {pageData.Title}");
    Console.WriteLine($"Page URL: {pageData.Url}");
    Console.WriteLine($"Content Length: {pageData.Body?.Length ?? 0}");
};
```

## **📌 Advanced Features**

### **Content Monitoring**
The WebView automatically monitors and notifies of content changes:
```csharp
webView.PageDataChanged += async (sender, pageData) =>
{
    // Handle page content updates
    var title = pageData.Title;
    var content = pageData.Body;
    var url = pageData.Url;
    var description = pageData.MetaDescription;
};
```

### **PDF Handling**
PDF files are automatically processed:
```csharp
webView.PageDataChanged += async (sender, pageData) =>
{
    if (pageData.Url.EndsWith(".pdf"))
    {
        // Access extracted PDF content
        var pdfContent = pageData.Body;
        var pageCount = pageData.MetaDescription;
    }
};
```

### **JavaScript Injection**
Inject custom JavaScript into the page:
```csharp
await webView.Handler?.InvokeAsync("InjectJavaScriptAsync", "console.log('Hello from JS!');");
```

## **📌 Platform-Specific Features**

### **Windows (WebView2)**
- Chromium-based engine
- Full DOM access
- Modern web standards support
- PDF integration

### **Android**
- Chrome-like WebView
- Custom download handling
- JavaScript interface
- Cookie management

### **iOS**
- WKWebView implementation
- Navigation delegation
- Script message handling
- Custom URL scheme handling

## **📌 FAQ**

### ❓ **How does content monitoring work?**
The WebView uses a MutationObserver to track DOM changes and notifies your app through the `PageDataChanged` event.

### ❓ **How is PDF handling implemented?**
PDFs are automatically downloaded and processed using iText, with text extraction and metadata parsing.

### ❓ **Can I customize the content monitoring?**
Yes! You can modify the monitoring behavior through JavaScript injection and custom event handling.

### ❓ **Does this support secure contexts?**
Yes! The WebView maintains cookie state and supports HTTPS, making it suitable for authenticated sessions.

### ❓ **How do I handle downloads?**
Downloads are automatically managed with platform-specific handlers, including PDF processing and external file opening.

## **📌 Contributing**
Contributions welcome! Submit issues and pull requests on https://github.com/MarketAlly/MarketAlly.ViewEngine

## **📌 License**
This project is licensed under the **MIT License**.

## **📌 Support**
Need help? Open an issue on GitHub or contact us at `support@marketally.com`.
