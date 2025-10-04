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
		private PageRawData _cachedRawData;

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
		/// Extracts routes on-demand from the cached raw data or re-extracts from the page.
		/// </summary>
		public async Task<PageData> ExtractRoutesAsync()
		{
			// If we have cached raw data, use it
			if (_cachedRawData != null)
			{
				var webView = VirtualView as WebView;
				int maxRoutes = webView?.MaxRoutes ?? 100;

				var routes = PageDataExtractor.CreateBasicRoutes(_cachedRawData.Links, maxRoutes);
				var bodyRoutes = PageDataExtractor.CreateBasicRoutes(_cachedRawData.BodyLinks, maxRoutes);

				var pageData = new PageData
				{
					Title = _cachedRawData.Title ?? "Untitled",
					Body = !string.IsNullOrEmpty(_cachedRawData.Body) ? DecodeBase64(_cachedRawData.Body) : string.Empty,
					Url = _cachedRawData.Url ?? string.Empty,
					Routes = routes,
					BodyRoutes = bodyRoutes
				};

				// Process ad detection asynchronously in background
				_ = Task.Run(async () =>
				{
					await PageDataExtractor.ProcessAdsAsync(routes, _cachedRawData.Url);
					await PageDataExtractor.ProcessAdsAsync(bodyRoutes, _cachedRawData.Url);
				});

				return pageData;
			}

			// No cached data, extract fresh from page
			return await ExtractPageDataAsync(forceRouteExtraction: true);
		}

		/// <summary>
		/// Platform-specific method to execute JavaScript and extract page data.
		/// </summary>
		public partial Task<PageData> ExtractPageDataAsync(bool forceRouteExtraction = false);

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
		public bool IsAdProcessed { get; set; } = false;
		internal List<LinkData> _linkInstances { get; set; }
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

		/// <summary>
		/// Fast method to create basic routes without ad detection (lazy processing)
		/// </summary>
		public static List<RouteInfo> CreateBasicRoutes(List<LinkData> links, int maxRoutes = 100)
		{
			if (links == null || links.Count == 0)
				return new List<RouteInfo>();

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

			// Apply limit early if specified
			var urlGroups = maxRoutes > 0
				? linksByUrl.Take(maxRoutes).ToList()
				: linksByUrl.ToList();

			var routes = new List<RouteInfo>();
			int rank = 1;

			foreach (var urlGroup in urlGroups)
			{
				var url = urlGroup.Key;
				var linkInstances = urlGroup.Value;
				var firstInstance = linkInstances.First();

				var routeInfo = new RouteInfo
				{
					Url = url,
					Text = firstInstance.Text,
					Occurrences = linkInstances.Count,
					Rank = rank++,
					AllTexts = linkInstances
						.Select(l => l.Text)
						.Where(t => !string.IsNullOrWhiteSpace(t))
						.Distinct()
						.ToList(),
					IsAdProcessed = false,
					_linkInstances = linkInstances // Store for later ad processing
				};

				routes.Add(routeInfo);
			}

			return routes;
		}

		/// <summary>
		/// Async method to detect ads on already-created routes (lazy ad detection)
		/// </summary>
		public static async Task ProcessAdsAsync(List<RouteInfo> routes, string currentPageUrl)
		{
			if (routes == null || routes.Count == 0)
				return;

			const int batchSize = 25;

			// Simplified ad detection patterns (faster)
			var adClassIdPatterns = new[] {
				@"\bad[_-]?\b", @"\bads[_-]?\b", @"\bsponsored\b",
				@"\bpromo[_-]", @"\bgoogle[_-]?ad\b"
			};

			// Known ad network domains (reduced set)
			var adDomains = new[] {
				"doubleclick.net", "googlesyndication.com", "googleadservices.com",
				"amazon-adsystem.com", "outbrain.com", "taboola.com"
			};

			// Tracking/affiliate URL patterns (reduced set)
			var trackingPatterns = new[] {
				@"[?&]aff(?:iliate)?[_-]?id=", @"[?&]ref(?:erral)?[_-]?id=",
				@"[?&]fbclid=", @"[?&]gclid="
			};

			int processedCount = 0;
			foreach (var routeInfo in routes)
			{
				if (routeInfo.IsAdProcessed || routeInfo._linkInstances == null)
					continue;

				var linkInstances = routeInfo._linkInstances;
				var url = routeInfo.Url;
				var reasons = new List<string>();
				int adScore = 0;

				// Analyze link instances for ad indicators
				foreach (var link in linkInstances)
				{
					// Check for rel="sponsored"
					if (!string.IsNullOrEmpty(link.Rel) && link.Rel.Contains("sponsored", StringComparison.OrdinalIgnoreCase))
					{
						adScore += 10;
						if (!reasons.Contains("Rel=sponsored"))
							reasons.Add("Rel=sponsored");
					}

					// Check class and ID for ad patterns (simplified)
					var classId = $"{link.Classes} {link.Id}".ToLowerInvariant();
					if (!string.IsNullOrEmpty(classId))
					{
						foreach (var pattern in adClassIdPatterns)
						{
							if (System.Text.RegularExpressions.Regex.IsMatch(classId, pattern))
							{
								adScore += 5;
								if (!reasons.Contains("Ad CSS"))
									reasons.Add("Ad CSS");
								break;
							}
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
							reasons.Add("Ad domain");
							break;
						}
					}
				}
				catch { }

				// Check for tracking/affiliate parameters
				foreach (var pattern in trackingPatterns)
				{
					if (System.Text.RegularExpressions.Regex.IsMatch(url, pattern))
					{
						adScore += 3;
						if (!reasons.Contains("Tracking"))
							reasons.Add("Tracking");
						break;
					}
				}

				// Mark as ad if score is high enough
				if (adScore >= 5)
				{
					routeInfo.IsPotentialAd = true;
					routeInfo.AdReason = $"Score: {adScore} - {string.Join(", ", reasons)}";
				}

				routeInfo.IsAdProcessed = true;
				routeInfo._linkInstances = null; // Free memory

				// Yield to UI thread every batch
				processedCount++;
				if (processedCount % batchSize == 0)
				{
					await Task.Delay(1);
				}
			}

			// Re-rank routes: prioritize non-ads and internal links
			var sortedRoutes = routes
				.OrderBy(r => r.IsPotentialAd ? 1 : 0)
				.ThenBy(r => r.Url.StartsWith(currentPageUrl, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
				.ToList();

			// Update ranks
			for (int i = 0; i < sortedRoutes.Count; i++)
			{
				sortedRoutes[i].Rank = i + 1;
			}

			routes.Clear();
			routes.AddRange(sortedRoutes);
		}
	}
}
