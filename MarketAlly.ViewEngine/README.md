# MarketAlly.Maui.ViewEngine

## 📢 Overview
`MarketAlly.ViewEngine` is a powerful `.NET MAUI` WebView control that mimics a **real browser**, enabling full JavaScript support, cookies, WebRTC, and custom User-Agent overrides. It works seamlessly across **Android, iOS, and Windows**.

## 🚀 Features
✅ Supports **custom User-Agent**  
✅ Enables **cookies, storage, WebRTC, and WebGL**  
✅ **Bypasses WebView detection techniques**  
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
   Install-Package MarketAlly.ViewEngine
   ```

---

## **📌 Setup in `MauiProgram.cs`**
After installing, you **must register the custom WebView handler** inside your `.NET MAUI` app.

### **Modify `MauiProgram.cs` to add the handler:**
```csharp
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using MarketAlly.Maui.ViewEngine.Controls;
using MarketAlly.Maui.ViewEngine.Handlers;

var builder = MauiApp.CreateBuilder();
builder.UseMauiApp<App>();

// Register the custom WebView handler
builder.ConfigureMauiHandlers(handlers =>
{
    handlers.AddHandler(typeof(CustomWebView), typeof(CustomWebViewHandler));
});

return builder.Build();
```
✅ **This ensures your app correctly loads the `CustomWebView` with platform-specific optimizations.**

---

## **📌 How to Use `CustomWebView`**
Once registered, you can use `CustomWebView` in **XAML** or **C#**.

### **🔹 Using in XAML**
```xml
<ContentPage xmlns:controls="clr-namespace:MarketAlly.ViewEngine.Controls;assembly=MarketAlly.ViewEngine">
    <controls:CustomWebView 
        Source="https://example.com"
        UserAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"/>
</ContentPage>
```

### **🔹 Using in C#**
```csharp
using MarketAlly.Maui.ViewEngine.Controls;

var webView = new CustomWebView
{
    Source = "https://example.com",
    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
};

Content = new StackLayout
{
    Children = { webView }
};
```

---

## **📌 FAQ**
### ❓ **How does this WebView differ from the default .NET MAUI WebView?**
- `CustomWebView` allows **custom User-Agent overrides**, enables **cookies, storage, WebRTC**, and improves **browser detection evasion**.
- The default `.NET MAUI WebView` lacks these advanced features.

### ❓ **Does this work with authentication-based websites?**
Yes! **Cookies and session data persist** between navigations, making it suitable for login-based websites.

### ❓ **Does this work on iOS, Android, and Windows?**
Yes! It uses:
- **Android:** Native WebView (with Chrome-like behavior).
- **iOS:** `WKWebView` with full JavaScript and cookie support.
- **Windows:** `WebView2` (Chromium-based).

### ❓ **Can I use this with OAuth authentication?**
Yes! The WebView can be used for **OAuth authentication flows**, but we recommend opening authentication pages in the **system browser** (e.g., `SFSafariViewController` for iOS or `Custom Tabs` for Android).

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
