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
		public WebViewHandler() : base(Mapper)
		{
		}

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
				[nameof(WebView.UserAgent)] = MapUserAgent,
				[nameof(WebView.Source)] = MapSource
			};

		public static void MapUserAgent(WebViewHandler handler, WebView view)
		{
// handler.SetUserAgent(view.UserAgent); // Commented out - causes crash on Windows, UserAgent set after first navigation
		}

		public static void MapSource(WebViewHandler handler, WebView view)
		{
			// Call the base mapper first to set the source
			Microsoft.Maui.Handlers.WebViewHandler.MapSource(handler, view);

			// Then ensure our custom WebViewClient is set (Android-specific)
			handler.EnsureCustomWebViewClient();
		}

		public partial void EnsureCustomWebViewClient();

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

		public string ExtractTextFromHtml(string html)
		{
			if (string.IsNullOrEmpty(html))
				return string.Empty;

			try
			{
				// Remove script and style tags and their contents
				html = System.Text.RegularExpressions.Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
				html = System.Text.RegularExpressions.Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

				// Remove HTML tags
				html = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", " ");

				// Decode HTML entities
				html = System.Net.WebUtility.HtmlDecode(html);

				// Replace multiple spaces with single space
				html = System.Text.RegularExpressions.Regex.Replace(html, @"\s+", " ");

				// Trim and return
				return html.Trim();
			}
			catch
			{
				return html;
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
			}
		}

		public bool IsPotentialPdfUrl(string url)
		{
			if (string.IsNullOrEmpty(url)) return false;


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

			return isPdf;
		}

		private static readonly HttpClient _httpClient = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(30)
		};

		public async Task<bool> ConfirmPdfContent(string url)
		{
			if (string.IsNullOrEmpty(url))
				return false;

			int retryCount = 0;
			const int maxRetries = 3;

			while (retryCount < maxRetries)
			{
				try
				{
					// Only get headers, don't download content yet
					using var request = new HttpRequestMessage(HttpMethod.Head, url);
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
					using var response = await _httpClient.SendAsync(request, cts.Token);

					var contentType = response.Content.Headers.ContentType?.MediaType;
					return contentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) == true ||
						   contentType?.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase) == true;
				}
				catch (Exception ex) when (retryCount < maxRetries - 1)
				{
					retryCount++;
					await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount))); // Exponential backoff
				}
				catch (Exception ex)
				{
					// If we can't check content type, fall back to URL pattern
					return IsPotentialPdfUrl(url);
				}
			}

			return IsPotentialPdfUrl(url);
		}

		public async Task HandlePotentialPdfUrl(string url)
		{
			if (string.IsNullOrEmpty(url))
				return;

			if (!await ConfirmPdfContent(url))
				return;

			int retryCount = 0;
			const int maxRetries = 3;

			while (retryCount < maxRetries)
			{
				try
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
					var pdfData = await _httpClient.GetByteArrayAsync(url, cts.Token);
					await HandlePdfDownload(pdfData, url);
					return; // Success
				}
				catch (Exception ex) when (retryCount < maxRetries - 1)
				{
					retryCount++;
					await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount))); // Exponential backoff
				}
				catch (Exception ex)
				{
					// Notify user of failure
					var failureData = new PageData
					{
						Title = "PDF Download Failed",
						Body = $"Failed to download PDF from {url} after {maxRetries} attempts: {ex.Message}",
						Url = url
					};
					PageDataChanged?.Invoke(this, failureData);
					return;
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
		public string Url { get; set; }
		public List<RouteInfo> Routes { get; set; } = new List<RouteInfo>();
		public List<RouteInfo> BodyRoutes { get; set; } = new List<RouteInfo>();
	}

	public class RouteInfo
	{
		public string Url { get; set; }
		public string Text { get; set; }
		public int Rank { get; set; }
		public int Occurrences { get; set; }
		public bool IsPotentialAd { get; set; }
		public string AdReason { get; set; }
		public List<string> AllTexts { get; set; } = new List<string>();
	}

	public class PageRawData
	{
		public string Title { get; set; }
		public string Body { get; set; }
		public string Url { get; set; }
		public List<LinkData> Links { get; set; }
		public List<LinkData> BodyLinks { get; set; }
	}

	public class LinkData
	{
		public string Href { get; set; }
		public string Text { get; set; }
		public string Classes { get; set; }
		public string Id { get; set; }
		public string Rel { get; set; }
		public bool IsExternal { get; set; }
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

                function sanitizeString(str) {
                    if (!str) return '';
                    // Remove control characters and replace with space
                    return str.replace(/[\x00-\x1F\x7F-\x9F]/g, ' ')
                              .replace(/\s+/g, ' ')
                              .trim();
                }

                // Extract all links from the page
                let links = [];
                let bodyLinks = [];
                let currentUrl = new URL(window.location.href);
                let allLinks = document.querySelectorAll('a[href]');

                // Function to extract link data
                function extractLinkData(link) {
                    try {
                        let href = link.href;
                        let linkUrl = new URL(href, window.location.href);
                        let linkText = (link.innerText || link.textContent || '').trim();

                        return {
                            href: linkUrl.href,
                            text: sanitizeString(linkText.substring(0, 200)),
                            classes: sanitizeString(link.className || ''),
                            id: sanitizeString(link.id || ''),
                            rel: sanitizeString(link.rel || ''),
                            isExternal: linkUrl.hostname !== currentUrl.hostname
                        };
                    } catch (e) {
                        return null;
                    }
                }

                // Extract all links
                allLinks.forEach(function(link) {
                    let linkData = extractLinkData(link);
                    if (linkData) {
                        links.push(linkData);
                    }
                });

                // Extract body-only links (main content areas)
                let bodySelectors = [
                    'main a[href]',
                    'article a[href]',
                    '[role=""main""] a[href]',
                    '.content a[href]',
                    '.main-content a[href]',
                    '.post-content a[href]',
                    '.entry-content a[href]',
                    '#content a[href]',
                    '#main a[href]'
                ];

                let bodyLinkElements = new Set();
                bodySelectors.forEach(function(selector) {
                    try {
                        document.querySelectorAll(selector).forEach(function(link) {
                            bodyLinkElements.add(link);
                        });
                    } catch (e) {
                        // Skip invalid selectors
                    }
                });

                // If no body links found using selectors, fall back to body tag
                if (bodyLinkElements.size === 0 && document.body) {
                    document.body.querySelectorAll('a[href]').forEach(function(link) {
                        bodyLinkElements.add(link);
                    });
                }

                bodyLinkElements.forEach(function(link) {
                    let linkData = extractLinkData(link);
                    if (linkData) {
                        bodyLinks.push(linkData);
                    }
                });

                let pageData = {
                    title: sanitizeString(document.title || ''),
                    body: toBase64(document.body ? document.body.innerText || document.body.textContent || '' : ''),
                    url: window.location.href,
                    links: links,
                    bodyLinks: bodyLinks
                };

                console.log('Extracted JSON:', JSON.stringify(pageData));
                return JSON.stringify(pageData);
            })();";

		public static List<RouteInfo> ProcessLinks(List<LinkData> links, string currentPageUrl)
		{
			if (links == null || links.Count == 0)
				return new List<RouteInfo>();

			// More specific class/ID patterns that indicate ads
			var adClassIdPatterns = new[] {
				@"\bad[_-]?\b", @"\bads[_-]?\b", @"\badvert", @"\bsponsored\b",
				@"\bpromo[_-]", @"\bbanner[_-]", @"\baffiliate\b",
				@"\bad[_-]slot\b", @"\bad[_-]unit\b", @"\bad[_-]container\b",
				@"\bgoogle[_-]?ad\b", @"\bdfp[_-]", @"\bad[_-]leaderboard\b"
			};

			// Known ad network domains
			var adDomains = new[] {
				"doubleclick.net", "googlesyndication.com", "googleadservices.com",
				"amazon-adsystem.com", "adnxs.com", "outbrain.com", "taboola.com",
				"criteo.com", "pubmatic.com", "rubiconproject.com", "openx.net",
				"advertising.com", "adroll.com", "adsrvr.org", "adsystem.com",
				"bidswitch.net", "casalemedia.com", "quantserve.com"
			};

			// Tracking/affiliate URL patterns
			var trackingPatterns = new[] {
				@"[?&]utm_source=", @"[?&]utm_medium=", @"[?&]utm_campaign=",
				@"[?&]aff(?:iliate)?[_-]?id=", @"[?&]ref(?:erral)?[_-]?id=",
				@"[?&]click[_-]?id=", @"[?&]tracking[_-]?id=",
				@"[?&]partner[_-]?id=", @"[?&]fbclid=", @"[?&]gclid="
			};

			// Group links by URL to detect duplicates
			var linksByUrl = new Dictionary<string, List<LinkData>>(StringComparer.OrdinalIgnoreCase);
			foreach (var link in links)
			{
				if (!linksByUrl.ContainsKey(link.Href))
				{
					linksByUrl[link.Href] = new List<LinkData>();
				}
				linksByUrl[link.Href].Add(link);
			}

			// Process each unique URL
			var routes = new List<RouteInfo>();
			foreach (var urlGroup in linksByUrl)
			{
				var url = urlGroup.Key;
				var linkInstances = urlGroup.Value;
				var firstInstance = linkInstances.First();

				var routeInfo = new RouteInfo
				{
					Url = url,
					Text = firstInstance.Text,
					Occurrences = linkInstances.Count,
					AllTexts = linkInstances
						.Select(l => l.Text)
						.Where(t => !string.IsNullOrWhiteSpace(t))
						.Distinct()
						.ToList()
				};

				var reasons = new List<string>();
				int adScore = 0;

				// Analyze all instances to determine if it's an ad
				foreach (var link in linkInstances)
				{
					// Check for rel="sponsored" - very strong indicator
					if (!string.IsNullOrEmpty(link.Rel))
					{
						if (link.Rel.Contains("sponsored", StringComparison.OrdinalIgnoreCase))
						{
							adScore += 10;
							if (!reasons.Contains("Rel=sponsored"))
								reasons.Add("Rel=sponsored");
						}
					}

					// Check class and ID for ad-specific patterns
					var classId = $"{link.Classes} {link.Id}".ToLowerInvariant();
					foreach (var pattern in adClassIdPatterns)
					{
						if (System.Text.RegularExpressions.Regex.IsMatch(classId, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
						{
							adScore += 5;
							if (!reasons.Contains("Ad-related CSS class/ID"))
								reasons.Add("Ad-related CSS class/ID");
							break;
						}
					}
				}

				// Check if URL points to known ad network
				try
				{
					var uri = new Uri(url);
					foreach (var domain in adDomains)
					{
						if (uri.Host.Contains(domain, StringComparison.OrdinalIgnoreCase))
						{
							adScore += 10;
							reasons.Add($"Ad network: {domain}");
							break;
						}
					}
				}
				catch { }

				// Check for tracking/affiliate parameters
				foreach (var pattern in trackingPatterns)
				{
					if (System.Text.RegularExpressions.Regex.IsMatch(url, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
					{
						adScore += 3;
						if (!reasons.Contains("Tracking parameters"))
							reasons.Add("Tracking parameters");
						break;
					}
				}

				// Check for suspicious text patterns (common ad text)
				foreach (var text in routeInfo.AllTexts)
				{
					var textLower = text.ToLowerInvariant();
					var adTextPatterns = new[] {
						"sponsored", "advertisement", "promoted", "partner content",
						"buy now", "shop now", "limited time", "click here"
					};

					foreach (var adText in adTextPatterns)
					{
						if (textLower.Contains(adText))
						{
							adScore += 2;
							if (!reasons.Contains($"Ad text: '{adText}'"))
								reasons.Add($"Ad text: '{adText}'");
							break;
						}
					}
				}

				// Only flag as ad if score is high enough (reduces false positives)
				if (adScore >= 5)
				{
					routeInfo.IsPotentialAd = true;
					routeInfo.AdReason = $"Score: {adScore} - {string.Join(", ", reasons)}";
				}

				routes.Add(routeInfo);
			}

			// Re-rank: prioritize non-ads and internal links
			routes = routes.OrderBy(r => r.IsPotentialAd ? 1 : 0)
						   .ThenBy(r => r.Url.StartsWith(currentPageUrl) ? 0 : 1)
						   .ToList();

			// Update ranks after sorting
			for (int i = 0; i < routes.Count; i++)
			{
				routes[i].Rank = i + 1;
			}

			return routes;
		}
	}
}
