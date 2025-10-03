using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Maui.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MarketAlly.Maui.ViewEngine
{
	public partial class WebViewHandler : Microsoft.Maui.Handlers.WebViewHandler
	{
		public WebViewHandler() : base(Mapper) { }

		public event EventHandler<PageData> PageDataChanged;

		// Method to trigger the event
		public async Task OnPageDataChangedAsync()
		{
			var pageData = await GetPageDataAsync();
			PageDataChanged?.Invoke(this, pageData);
		}

		public static new IPropertyMapper<WebView, WebViewHandler> Mapper =
			new PropertyMapper<WebView, WebViewHandler>(Microsoft.Maui.Handlers.WebViewHandler.Mapper)
			{
				[nameof(WebView.UserAgent)] = MapUserAgent
			};

		public static void MapUserAgent(WebViewHandler handler, WebView view)
		{
			handler.SetUserAgent(view.UserAgent);
		}

		/// <summary>
		/// Extracts key webpage details (title, body text, metadata) from the currently loaded WebView.
		/// </summary>
		public async Task<PageData> GetPageDataAsync()
		{
			return await ExtractPageDataAsync();
		}

		/// <summary>
		/// Platform-specific method to execute JavaScript and extract page data.
		/// </summary>
		public partial Task<PageData> ExtractPageDataAsync();

		public string DecodeBase64(string base64String)
		{
			try
			{
				byte[] bytes = Convert.FromBase64String(base64String);
				return Encoding.UTF8.GetString(bytes);
			}
			catch
			{
				return "Failed to decode HTML content.";
			}
		}

		// Method to inject JavaScript into WebView
		public partial Task InjectJavaScriptAsync(string script);

		// Method to inject JavaScript that listens for URL changes
		public async Task InjectLocationChangeListener()
		{
			var script = @"
                (function() {
                    window.addEventListener('locationchange', function() {
                        window.external.notify('LocationChanged');
                    });
                })();
            ";
			await InjectJavaScriptAsync(script);
		}

		public async Task HandlePdfDownload(byte[] pdfData, string url)
		{
			try
			{
				using (var stream = new MemoryStream(pdfData))
				using (var pdfReader = new PdfReader(stream))
				using (var pdfDocument = new PdfDocument(pdfReader))
				{
					var text = new StringBuilder();
					for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
					{
						var page = pdfDocument.GetPage(i);
						text.Append(PdfTextExtractor.GetTextFromPage(page, new LocationTextExtractionStrategy()));
					}

					var pageData = new PageData
					{
						Title = "PDF Document",
						Body = text.ToString(),
						Url = url,
						MetaDescription = $"PDF document with {pdfDocument.GetNumberOfPages()} pages"
					};

					PageDataChanged?.Invoke(this, pageData);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error processing PDF: {ex.Message}");
			}
		}

		public bool IsPotentialPdfUrl(string url)
		{
			if (string.IsNullOrEmpty(url)) return false;

			Console.WriteLine($"Checking if URL is potential PDF: {url}");

			// Common PDF URL patterns
			var pdfPatterns = new[]
			{
			@"\.pdf$",                           // Ends with .pdf
            @"arxiv\.org/pdf/\d{4}\.\d{4,5}",   // arXiv PDF pattern
            @"/pdf/",                            // Contains /pdf/ in path
            @"content-type=pdf",                 // Query parameter indicating PDF
            @"type=pdf",                         // Alternative query parameter
            @"format=pdf"                        // Another common parameter
        };

			var isPdf = pdfPatterns.Any(pattern =>
				System.Text.RegularExpressions.Regex.IsMatch(url, pattern,
					System.Text.RegularExpressions.RegexOptions.IgnoreCase));

			Console.WriteLine($"URL {url} is{(isPdf ? "" : " not")} a potential PDF");
			return isPdf;
		}

		public async Task<bool> ConfirmPdfContent(string url)
		{
			try
			{
				using var client = new HttpClient();
				// Only get headers, don't download content yet
				using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));

				var contentType = response.Content.Headers.ContentType?.MediaType;
				return contentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) == true ||
					   contentType?.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase) == true;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error checking content type: {ex.Message}");
				// If we can't check content type, fall back to URL pattern
				return IsPotentialPdfUrl(url);
			}
		}

		public async Task HandlePotentialPdfUrl(string url)
		{
			if (await ConfirmPdfContent(url))
			{
				try
				{
					using var client = new HttpClient();
					var pdfData = await client.GetByteArrayAsync(url);
					await HandlePdfDownload(pdfData, url);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error downloading PDF: {ex.Message}");
				}
			}
		}
	}

	/// <summary>
	/// Represents extracted webpage details.
	/// </summary>
	public class PageData
	{
		public string Title { get; set; }
		public string Body { get; set; }
		public string MetaDescription { get; set; }
		public string Url { get; set; }  // New URL property
	}
	public class PageRawData
	{
		public string Title { get; set; }
		public string Html { get; set; }
		public string Url { get; set; }  // New URL property
	}

	// JavaScript for extracting page data (to be used in all platforms)
	public static class PageDataExtractor
	{
		public static string ExtractScript = @"
            (function() {
                function toBase64(str) {
                    try {
                        return btoa(unescape(encodeURIComponent(str)));
                    } catch (e) {
                        return 'ERROR_ENCODING';
                    }
                }
                
                let pageData = {
                    title: document.title || '',
                    html: toBase64(document.documentElement.outerHTML || ''),
                    url: window.location.href
                };

                console.log('Extracted JSON:', JSON.stringify(pageData));
                return JSON.stringify(pageData);
            })();";
	}
}
