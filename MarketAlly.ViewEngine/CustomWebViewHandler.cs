using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Internals;

namespace MarketAlly.Maui.ViewEngine
{
	[Preserve(AllMembers = true)]
	public partial class WebViewHandler : Microsoft.Maui.Handlers.WebViewHandler
	{
		private PageRawData _cachedRawData;

		public static new IPropertyMapper<WebView, WebViewHandler> Mapper { get; set; }

		static WebViewHandler()
		{
			// Initialize mapper in static constructor to ensure it's ready before any instances are created
			Mapper = new PropertyMapper<WebView, WebViewHandler>(Microsoft.Maui.Handlers.WebViewHandler.Mapper)
			{
				[nameof(WebView.UserAgent)] = MapUserAgent,
				[nameof(WebView.Source)] = MapSource,
				[nameof(WebView.EnableZoom)] = MapEnableZoom
			};
		}

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

		public static void MapEnableZoom(WebViewHandler handler, WebView view)
		{
			handler.SetEnableZoom(view.EnableZoom);
		}

		public partial void EnsureCustomWebViewClient();
		public partial void SetEnableZoom(bool enable);

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
				bool normalizeRoutes = webView?.NormalizeRoutes ?? true;
				var excludeDomains = webView?.ExcludeDomains;
				bool enableAdDetection = webView?.EnableAdDetection ?? false;

				var routes = PageDataExtractor.CreateBasicRoutes(_cachedRawData.Links, maxRoutes, normalizeRoutes, excludeDomains);
				var bodyRoutes = PageDataExtractor.CreateBasicRoutes(_cachedRawData.BodyLinks, maxRoutes, normalizeRoutes, excludeDomains);

				var pageData = new PageData
				{
					Title = _cachedRawData.Title ?? "Untitled",
					Body = !string.IsNullOrEmpty(_cachedRawData.Body) ? DecodeBase64(_cachedRawData.Body) : string.Empty,
					Url = _cachedRawData.Url ?? string.Empty,
					Routes = routes,
					BodyRoutes = bodyRoutes
				};

				// Process ad detection asynchronously in background
				if (enableAdDetection)
				{
					_ = Task.Run(async () =>
					{
						await PageDataExtractor.ProcessAdsAsync(routes, _cachedRawData.Url);
						await PageDataExtractor.ProcessAdsAsync(bodyRoutes, _cachedRawData.Url);
					});
				}

				return pageData;
			}

			// No cached data, extract fresh from page
			return await ExtractPageDataAsync(forceRouteExtraction: true);
		}

		/// <summary>
		/// Platform-specific method to execute JavaScript and extract page data.
		/// </summary>
		public partial Task<PageData> ExtractPageDataAsync(bool forceRouteExtraction = false);

		/// <summary>
		/// Platform-specific method to capture a thumbnail screenshot of the current webpage.
		/// </summary>
		/// <param name="width">Target thumbnail width in pixels (default: 320)</param>
		/// <param name="height">Target thumbnail height in pixels (default: 180)</param>
		/// <returns>ImageSource containing the thumbnail, or null if capture fails</returns>
		public partial Task<Microsoft.Maui.Controls.ImageSource> CaptureThumbnailAsync(int width = 320, int height = 180);

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

		public async Task<string> HandlePdfDownload(byte[] pdfData, string url)
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
						text.Append("\n"); // Add newline between pages
					}

					return text.ToString();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error extracting PDF text: {ex.Message}");
				return string.Empty;
			}
		}

		public bool IsPotentialPdfUrl(string url)
		{
			if (string.IsNullOrEmpty(url)) return false;


			// Use patterns from WebView if available, otherwise use defaults
			var webView = VirtualView as WebView;
		var pdfPatterns = webView?.PdfUrlPatterns ?? new[]
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

			// Primary check: Trust URL patterns for common PDF URLs
			// This is fast, reliable, and works for most cases including academic sites
			// that may redirect or return incorrect Content-Type headers
			if (IsPotentialPdfUrl(url))
				return true;

			// Secondary check: For non-standard URLs, try HTTP HEAD request
			// This handles edge cases where PDFs are served with unusual URLs
			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Head, url);
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				using var response = await _httpClient.SendAsync(request, cts.Token);

				var contentType = response.Content.Headers.ContentType?.MediaType;
				return contentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) == true ||
					   contentType?.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase) == true;
			}
			catch
			{
				// If HTTP HEAD fails, assume it's not a PDF
				return false;
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
		public string FaviconUrl { get; set; }
		public List<RouteInfo> Routes { get; set; } = new List<RouteInfo>();
		public List<RouteInfo> BodyRoutes { get; set; } = new List<RouteInfo>();

		/// <summary>
		/// Optional thumbnail image of the webpage.
		/// Only populated when EnableThumbnailCapture is true or when CaptureThumbnailAsync() is called.
		/// </summary>
		public Microsoft.Maui.Controls.ImageSource Thumbnail { get; set; }
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
		public string FaviconUrl { get; set; }
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

                // Function to check if we can access an iframe's content (same-origin check)
                function canAccessIframe(iframe) {
                    try {
                        // Try to access the contentDocument - will throw if cross-origin
                        var doc = iframe.contentDocument || iframe.contentWindow.document;
                        return doc != null;
                    } catch (e) {
                        return false;
                    }
                }

                // Function to extract content from an iframe
                function extractFromIframe(iframe) {
                    try {
                        if (!canAccessIframe(iframe)) {
                            return { bodyText: '', links: [], bodyLinks: [] };
                        }

                        var iframeDoc = iframe.contentDocument || iframe.contentWindow.document;
                        if (!iframeDoc) {
                            return { bodyText: '', links: [], bodyLinks: [] };
                        }

                        // Extract body text from iframe
                        var iframeBodyText = '';
                        if (iframeDoc.body && (iframeDoc.body.innerText || iframeDoc.body.textContent)) {
                            iframeBodyText = iframeDoc.body.innerText || iframeDoc.body.textContent || '';
                        } else if (iframeDoc.documentElement) {
                            iframeBodyText = iframeDoc.documentElement.innerText || iframeDoc.documentElement.textContent || '';
                        }

                        // Extract links from iframe
                        var iframeLinks = [];
                        var iframeBodyLinks = [];
                        var iframeAllLinks = iframeDoc.querySelectorAll('a[href]');

                        iframeAllLinks.forEach(function(link) {
                            var linkData = extractLinkData(link);
                            if (linkData && linkData.text) {
                                iframeLinks.push(linkData);
                            }
                        });

                        // Extract body-only links from iframe
                        var bodyLinkElements = new Set();
                        bodySelectors.forEach(function(selector) {
                            try {
                                iframeDoc.querySelectorAll(selector).forEach(function(link) {
                                    bodyLinkElements.add(link);
                                });
                            } catch (e) {
                                // Skip invalid selectors
                            }
                        });

                        bodyLinkElements.forEach(function(link) {
                            var linkData = extractLinkData(link);
                            if (linkData && linkData.text) {
                                iframeBodyLinks.push(linkData);
                            }
                        });

                        // Fallback to body tag if no body links found
                        if (iframeBodyLinks.length === 0 && iframeDoc.body) {
                            iframeDoc.body.querySelectorAll('a[href]').forEach(function(link) {
                                var linkData = extractLinkData(link);
                                if (linkData && linkData.text) {
                                    iframeBodyLinks.push(linkData);
                                }
                            });
                        }

                        return {
                            bodyText: iframeBodyText,
                            links: iframeLinks,
                            bodyLinks: iframeBodyLinks
                        };
                    } catch (e) {
                        // Cross-origin or other error - skip this iframe
                        return { bodyText: '', links: [], bodyLinks: [] };
                    }
                }

                // Extract all links from the page
                let links = [];
                let bodyLinks = [];
                let currentUrl = new URL(window.location.href);

                // Body selectors (needed for iframe extraction too)
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

                // Function to extract link data
                function extractLinkData(link) {
                    try {
                        let href = link.href;
                        let linkUrl = new URL(href, window.location.href);

                        // Get text, filtering out style attributes and hidden elements
                        let linkText = '';

                        // Try multiple text sources in priority order
                        if (link.getAttribute('aria-label')) {
                            linkText = link.getAttribute('aria-label').trim();
                        } else if (link.title) {
                            linkText = link.title.trim();
                        } else if (link.textContent && link.textContent.trim()) {
                            linkText = link.textContent.trim();
                        }

                        // Skip if still empty
                        if (!linkText) {
                            return null;
                        }

                        // Remove CSS rules and styles that sometimes appear in text
                        linkText = linkText.replace(/\.[a-zA-Z0-9_-]+\s*\{[^}]*\}/g, '')  // Remove .class{...}
                                          .replace(/#[a-zA-Z0-9_-]+\s*\{[^}]*\}/g, '')  // Remove #id{...}
                                          .replace(/[a-zA-Z-]+\s*:\s*[^;]+;?/g, '')     // Remove property:value
                                          .replace(/\s+/g, ' ')
                                          .trim();

                        // Skip if empty after cleaning
                        if (!linkText) {
                            return null;
                        }

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

                // Extract from main document
                let allLinks = document.querySelectorAll('a[href]');

                // Extract all links (skip links without text to reduce processing)
                allLinks.forEach(function(link) {
                    let linkData = extractLinkData(link);
                    if (linkData && linkData.text) {
                        links.push(linkData);
                    }
                });

                // Extract body-only links from main document
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

                // Extract body links with text filtering
                bodyLinkElements.forEach(function(link) {
                    let linkData = extractLinkData(link);
                    if (linkData && linkData.text) {
                        bodyLinks.push(linkData);
                    }
                });

                // If no body links found using selectors, fall back to body tag
                if (bodyLinks.length === 0 && document.body) {
                    document.body.querySelectorAll('a[href]').forEach(function(link) {
                        let linkData = extractLinkData(link);
                        if (linkData && linkData.text) {
                            bodyLinks.push(linkData);
                        }
                    });
                }

                // Extract body text from main document with fallback for pages without proper body tag (like arXiv HTML)
                let bodyText = '';
                if (document.body && (document.body.innerText || document.body.textContent)) {
                    bodyText = document.body.innerText || document.body.textContent || '';
                } else if (document.documentElement) {
                    // Fallback for pages without body tag or with empty body
                    bodyText = document.documentElement.innerText || document.documentElement.textContent || '';
                }

                // Now extract from all iframes (same-origin only)
                let iframes = document.querySelectorAll('iframe');
                iframes.forEach(function(iframe) {
                    try {
                        var iframeData = extractFromIframe(iframe);

                        // Merge iframe body text with main body text
                        if (iframeData.bodyText) {
                            bodyText += '\n\n--- IFRAME CONTENT ---\n' + iframeData.bodyText;
                        }

                        // Merge iframe links
                        if (iframeData.links && iframeData.links.length > 0) {
                            links = links.concat(iframeData.links);
                        }

                        // Merge iframe body links
                        if (iframeData.bodyLinks && iframeData.bodyLinks.length > 0) {
                            bodyLinks = bodyLinks.concat(iframeData.bodyLinks);
                        }
                    } catch (e) {
                        // Skip this iframe - likely cross-origin
                    }
                });

                // Extract favicon
                let faviconUrl = '';
                let linkElements = document.querySelectorAll('link[rel*=""icon""]');
                if (linkElements.length > 0) {
                    // Prefer apple-touch-icon or icon with largest size
                    let bestIcon = null;
                    let maxSize = 0;

                    linkElements.forEach(function(link) {
                        let rel = link.getAttribute('rel').toLowerCase();
                        let sizes = link.getAttribute('sizes');

                        if (rel.includes('apple-touch-icon')) {
                            bestIcon = link;
                            return;
                        }

                        if (sizes) {
                            let match = sizes.match(/(\d+)x(\d+)/);
                            if (match) {
                                let size = parseInt(match[1]);
                                if (size > maxSize) {
                                    maxSize = size;
                                    bestIcon = link;
                                }
                            }
                        } else if (!bestIcon) {
                            bestIcon = link;
                        }
                    });

                    if (bestIcon) {
                        faviconUrl = bestIcon.href;
                    }
                } else {
                    // Default favicon.ico
                    try {
                        let url = new URL(window.location.href);
                        faviconUrl = url.origin + '/favicon.ico';
                    } catch (e) {
                        faviconUrl = '';
                    }
                }

                let pageData = {
                    title: sanitizeString(document.title || ''),
                    body: toBase64(bodyText),
                    url: window.location.href,
                    faviconUrl: faviconUrl,
                    links: links,
                    bodyLinks: bodyLinks
                };

                console.log('Extracted JSON:', JSON.stringify(pageData));
                return JSON.stringify(pageData);
            })();";

		/// <summary>
		/// Normalizes URL by removing common tracking parameters
		/// </summary>
		private static string NormalizeUrl(string url, List<string> excludeDomains = null)
		{
			if (string.IsNullOrEmpty(url))
				return url;

			try
			{
				var uri = new Uri(url);

				// Check if domain should be excluded from normalization
				if (excludeDomains != null && excludeDomains.Count > 0)
				{
					var host = uri.Host.ToLowerInvariant();
					foreach (var domain in excludeDomains)
					{
						var excludeDomain = domain.ToLowerInvariant();
						// Match exact domain or subdomain
						if (host == excludeDomain || host.EndsWith("." + excludeDomain))
						{
							return url; // Return original URL without normalization
						}
					}
				}

				var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

				// Remove common tracking parameters
				var trackingParams = new[] {
					"zx", "no_sw_cr", "ved", "sa", "usg", "ei",
					"utm_source", "utm_medium", "utm_campaign", "utm_content", "utm_term",
					"fbclid", "gclid", "msclkid", "_ga", "mc_cid", "mc_eid"
				};

				foreach (var param in trackingParams)
				{
					query.Remove(param);
				}

				// Rebuild URL without tracking params
				var builder = new UriBuilder(uri)
				{
					Query = query.Count > 0 ? query.ToString() : string.Empty,
					Fragment = string.Empty // Also remove fragments for grouping
				};

				return builder.Uri.ToString();
			}
			catch
			{
				// If URL parsing fails, return original
				return url;
			}
		}

		/// <summary>
		/// Fast method to create basic routes without ad detection (lazy processing)
		/// </summary>
		public static List<RouteInfo> CreateBasicRoutes(List<LinkData> links, int maxRoutes = 100, bool normalizeRoutes = true, List<string> excludeDomains = null)
		{
			if (links == null || links.Count == 0)
				return new List<RouteInfo>();

			// Filter out links without text first to reduce processing
			var validLinks = links.Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();

			if (validLinks.Count == 0)
				return new List<RouteInfo>();

			// Safety limit: If we have too many links, enforce a maximum to prevent lockups
			const int absoluteMaxLinks = 500;
			if (validLinks.Count > absoluteMaxLinks)
			{
				validLinks = validLinks.Take(absoluteMaxLinks).ToList();
			}

			// Group links by URL (normalized or exact) to detect duplicates
			var linksByUrl = new Dictionary<string, List<LinkData>>(StringComparer.OrdinalIgnoreCase);
			foreach (var link in validLinks)
			{
				var groupKey = normalizeRoutes ? NormalizeUrl(link.Href, excludeDomains) : link.Href;
				if (!linksByUrl.ContainsKey(groupKey))
				{
					linksByUrl[groupKey] = new List<LinkData>();
				}
				linksByUrl[groupKey].Add(link);
			}

			// Apply limit early if specified
			var urlGroups = maxRoutes > 0
				? linksByUrl.Take(maxRoutes).ToList()
				: linksByUrl.ToList();

			var routes = new List<RouteInfo>(urlGroups.Count);
			int rank = 1;

			foreach (var urlGroup in urlGroups)
			{
				var url = urlGroup.Key;
				var linkInstances = urlGroup.Value;
				var firstInstance = linkInstances.First();

				// Only collect distinct texts if there are multiple instances
				List<string> allTexts;
				if (linkInstances.Count > 1)
				{
					allTexts = linkInstances
						.Select(l => l.Text)
						.Where(t => !string.IsNullOrWhiteSpace(t))
						.Distinct()
						.ToList();
				}
				else
				{
					allTexts = new List<string> { firstInstance.Text };
				}

				var routeInfo = new RouteInfo
				{
					Url = url,
					Text = firstInstance.Text,
					Occurrences = linkInstances.Count,
					Rank = rank++,
					AllTexts = allTexts,
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

			const int batchSize = 10; // Reduced from 25 for better responsiveness

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

	public partial class WebViewHandler
	{
		/// <summary>
		/// Renders a PDF thumbnail from PDF data. Platform-specific implementation.
		/// </summary>
		/// <param name="pdfData">The PDF byte array</param>
		/// <param name="width">Target width</param>
		/// <param name="height">Target height</param>
		/// <returns>ImageSource containing the thumbnail, or null if rendering failed</returns>
		public partial Task<Microsoft.Maui.Controls.ImageSource> RenderPdfThumbnailAsync(byte[] pdfData, int width, int height);
	}
}
