using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnifiedBrowser.Maui.Bookmarks;
using UnifiedBrowser.Maui.Models;

namespace UnifiedBrowser.Maui.Services
{
    /// <summary>
    /// Manages bookmark operations including loading, saving, adding, and deleting bookmarks
    /// </summary>
    public class BookmarkManager
    {
        private readonly ILogger<BookmarkManager>? _logger;
        private string? _bookmarksFilePath;
        private BookmarkDocument _document;
        private BookmarkFolder? _toolbarFolder;
        private string _defaultBookmarkTitle = "MarketAlly";
        private string _defaultBookmarkUrl = "https://www.marketally.com";

        public ObservableCollection<BookmarkItemViewModel> Bookmarks { get; }

        /// <summary>
        /// Gets or sets the file path for the bookmarks file
        /// </summary>
        public string? BookmarksFilePath
        {
            get => _bookmarksFilePath;
            set
            {
                _bookmarksFilePath = value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    System.Diagnostics.Debug.WriteLine($"[BookmarkManager] BookmarksFilePath set to: {value}, triggering LoadBookmarksAsync");
                    _logger?.LogInformation("BookmarksFilePath set to: {Path}, triggering LoadBookmarksAsync", value);
                    _ = LoadBookmarksAsync();
                }
            }
        }

        public BookmarkManager(ILogger<BookmarkManager>? logger = null)
        {
            _logger = logger;
            _document = new BookmarkDocument();
            Bookmarks = new ObservableCollection<BookmarkItemViewModel>();

            // Initialize with an empty toolbar folder
            _toolbarFolder = new BookmarkFolder
            {
                Title = "Bookmarks Bar",
                PersonalToolbarFolder = true
            };
            _document.Children.Add(_toolbarFolder);
        }

        /// <summary>
        /// Load bookmarks from the configured file path
        /// </summary>
        public async Task<bool> LoadBookmarksAsync()
        {
            if (string.IsNullOrWhiteSpace(_bookmarksFilePath))
            {
                System.Diagnostics.Debug.WriteLine("[BookmarkManager] Bookmarks file path is not set");
                _logger?.LogWarning("Bookmarks file path is not set");
                return false;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[BookmarkManager] LoadBookmarksAsync called, checking if file exists: {_bookmarksFilePath}");
                _logger?.LogInformation("LoadBookmarksAsync called, checking if file exists: {Path}", _bookmarksFilePath);

                if (!File.Exists(_bookmarksFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[BookmarkManager] Bookmarks file does not exist, creating new with default bookmark: {_bookmarksFilePath}");
                    _logger?.LogInformation("Bookmarks file does not exist, creating new with default bookmark: {Path}", _bookmarksFilePath);

                    // Add default MarketAlly bookmark
                    await AddDefaultBookmarkAsync();

                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"[BookmarkManager] Bookmarks file exists, loading from: {_bookmarksFilePath}");
                _logger?.LogInformation("Bookmarks file exists, loading from: {Path}", _bookmarksFilePath);

                await Task.Run(() =>
                {
                    _document = NetscapeBookmarksIO.LoadFromFile(_bookmarksFilePath);
                });

                // Find or create toolbar folder
                _toolbarFolder = _document.GetToolbarFolder();
                if (_toolbarFolder == null)
                {
                    _toolbarFolder = new BookmarkFolder
                    {
                        Title = "Bookmarks Bar",
                        PersonalToolbarFolder = true
                    };
                    _document.Children.Insert(0, _toolbarFolder);
                }

                // Update observable collection
                UpdateBookmarksCollection();

                System.Diagnostics.Debug.WriteLine($"[BookmarkManager] Successfully loaded {Bookmarks.Count} bookmarks from {_bookmarksFilePath}");
                _logger?.LogInformation("Successfully loaded {Count} bookmarks from {Path}", Bookmarks.Count, _bookmarksFilePath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BookmarkManager] Failed to load bookmarks: {ex.Message}");
                _logger?.LogError(ex, "Failed to load bookmarks from {Path}", _bookmarksFilePath);
                return false;
            }
        }

        /// <summary>
        /// Sets the default bookmark title and URL
        /// </summary>
        public void SetDefaultBookmark(string title, string url)
        {
            _defaultBookmarkTitle = title ?? "MarketAlly";
            _defaultBookmarkUrl = url ?? "https://www.marketally.com";
        }

        /// <summary>
        /// Adds the default bookmark
        /// </summary>
        private async Task AddDefaultBookmarkAsync()
        {
            try
            {
                var bookmarkLink = new BookmarkLink
                {
                    Title = _defaultBookmarkTitle,
                    Url = _defaultBookmarkUrl,
                    AddDate = DateTimeOffset.UtcNow
                };

                _toolbarFolder?.Children.Add(bookmarkLink);

                var viewModel = new BookmarkItemViewModel(
                    _defaultBookmarkTitle,
                    _defaultBookmarkUrl,
                    null,
                    null,
                    bookmarkLink.AddDate,
                    bookmarkLink.LastModified);

                Bookmarks.Add(viewModel);

                await SaveBookmarksAsync();

                _logger?.LogInformation("Added default bookmark: {Title} -> {Url}", _defaultBookmarkTitle, _defaultBookmarkUrl);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to add default bookmark");
            }
        }

        /// <summary>
        /// Save bookmarks to the configured file path
        /// </summary>
        public async Task<bool> SaveBookmarksAsync()
        {
            if (string.IsNullOrWhiteSpace(_bookmarksFilePath))
            {
                _logger?.LogWarning("Bookmarks file path is not set");
                return false;
            }

            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_bookmarksFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await Task.Run(() =>
                {
                    NetscapeBookmarksIO.SaveToFile(_document, _bookmarksFilePath);
                });

                _logger?.LogInformation("Saved {Count} bookmarks to {Path}", Bookmarks.Count, _bookmarksFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save bookmarks to {Path}", _bookmarksFilePath);
                return false;
            }
        }

        /// <summary>
        /// Add a new bookmark
        /// </summary>
        public async Task<bool> AddBookmarkAsync(string title, string url, string? icon = null, string? iconUri = null)
        {
            try
            {
                if (_toolbarFolder == null)
                {
                    _logger?.LogError("Toolbar folder is null, cannot add bookmark");
                    return false;
                }

                var bookmarkLink = new BookmarkLink
                {
                    Title = title,
                    Url = url,
                    Icon = icon,
                    IconUri = iconUri,
                    AddDate = DateTimeOffset.UtcNow
                };

                _toolbarFolder.Children.Add(bookmarkLink);

                var viewModel = new BookmarkItemViewModel(
                    title,
                    url,
                    icon,
                    iconUri,
                    bookmarkLink.AddDate,
                    bookmarkLink.LastModified);

                Bookmarks.Add(viewModel);

                await SaveBookmarksAsync();

                _logger?.LogInformation("Added bookmark: {Title} -> {Url}", title, url);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to add bookmark: {Title}", title);
                return false;
            }
        }

        /// <summary>
        /// Delete a bookmark by URL
        /// </summary>
        public async Task<bool> DeleteBookmarkAsync(string url)
        {
            try
            {
                if (_toolbarFolder == null)
                {
                    _logger?.LogError("Toolbar folder is null, cannot delete bookmark");
                    return false;
                }

                var bookmark = _toolbarFolder.Children.OfType<BookmarkLink>().FirstOrDefault(b => b.Url == url);
                if (bookmark == null)
                {
                    _logger?.LogWarning("Bookmark with URL {Url} not found", url);
                    return false;
                }

                _toolbarFolder.Children.Remove(bookmark);

                var viewModel = Bookmarks.FirstOrDefault(b => b.Url == url);
                if (viewModel != null)
                {
                    Bookmarks.Remove(viewModel);
                }

                await SaveBookmarksAsync();

                _logger?.LogInformation("Deleted bookmark: {Url}", url);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete bookmark: {Url}", url);
                return false;
            }
        }

        /// <summary>
        /// Delete a bookmark by view model
        /// </summary>
        public async Task<bool> DeleteBookmarkAsync(BookmarkItemViewModel bookmark)
        {
            return await DeleteBookmarkAsync(bookmark.Url);
        }

        /// <summary>
        /// Check if a URL is bookmarked
        /// </summary>
        public bool IsBookmarked(string url)
        {
            if (_toolbarFolder == null)
                return false;

            return _toolbarFolder.Children.OfType<BookmarkLink>().Any(b => b.Url == url);
        }

        /// <summary>
        /// Import bookmarks from a file
        /// </summary>
        public async Task<bool> ImportBookmarksAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger?.LogError("Import file does not exist: {Path}", filePath);
                    return false;
                }

                BookmarkDocument importedDoc = await Task.Run(() => NetscapeBookmarksIO.LoadFromFile(filePath));

                // Merge imported bookmarks into toolbar folder
                if (_toolbarFolder != null)
                {
                    var importedLinks = GetAllLinks(importedDoc);
                    var existingUrls = _toolbarFolder.Children.OfType<BookmarkLink>().Select(b => b.Url).ToHashSet();

                    foreach (var link in importedLinks)
                    {
                        if (!existingUrls.Contains(link.Url))
                        {
                            _toolbarFolder.Children.Add(link);
                        }
                    }
                }

                UpdateBookmarksCollection();
                await SaveBookmarksAsync();

                _logger?.LogInformation("Imported bookmarks from {Path}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to import bookmarks from {Path}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Export bookmarks to a file
        /// </summary>
        public async Task<bool> ExportBookmarksAsync(string filePath)
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await Task.Run(() =>
                {
                    NetscapeBookmarksIO.SaveToFile(_document, filePath);
                });

                _logger?.LogInformation("Exported bookmarks to {Path}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to export bookmarks to {Path}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Clear all bookmarks
        /// </summary>
        public async Task<bool> ClearAllBookmarksAsync()
        {
            try
            {
                if (_toolbarFolder != null)
                {
                    _toolbarFolder.Children.Clear();
                }

                Bookmarks.Clear();

                await SaveBookmarksAsync();

                _logger?.LogInformation("Cleared all bookmarks");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to clear bookmarks");
                return false;
            }
        }

        /// <summary>
        /// Update the observable collection from the document
        /// </summary>
        private void UpdateBookmarksCollection()
        {
            Bookmarks.Clear();
            System.Diagnostics.Debug.WriteLine($"[BookmarkManager] UpdateBookmarksCollection called");

            if (_toolbarFolder != null)
            {
                System.Diagnostics.Debug.WriteLine($"[BookmarkManager] Toolbar folder has {_toolbarFolder.Children.Count} children");

                var bookmarkLinks = _toolbarFolder.Children.OfType<BookmarkLink>().ToList();
                System.Diagnostics.Debug.WriteLine($"[BookmarkManager] Found {bookmarkLinks.Count} BookmarkLink items");

                foreach (var item in bookmarkLinks)
                {
                    System.Diagnostics.Debug.WriteLine($"[BookmarkManager] Adding bookmark: {item.Title} -> {item.Url}");

                    var viewModel = new BookmarkItemViewModel(
                        item.Title,
                        item.Url,
                        item.Icon,
                        item.IconUri,
                        item.AddDate,
                        item.LastModified);

                    Bookmarks.Add(viewModel);
                }

                System.Diagnostics.Debug.WriteLine($"[BookmarkManager] Final Bookmarks collection count: {Bookmarks.Count}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[BookmarkManager] Toolbar folder is NULL!");
            }
        }

        /// <summary>
        /// Get all links from a document (recursively)
        /// </summary>
        private List<BookmarkLink> GetAllLinks(BookmarkDocument doc)
        {
            var links = new List<BookmarkLink>();

            foreach (var item in doc.Children)
            {
                CollectLinks(item, links);
            }

            return links;
        }

        /// <summary>
        /// Recursively collect links from bookmark items
        /// </summary>
        private void CollectLinks(Bookmarks.BookmarkItem item, List<BookmarkLink> links)
        {
            if (item is BookmarkLink link)
            {
                links.Add(link);
            }
            else if (item is BookmarkFolder folder)
            {
                foreach (var child in folder.Children)
                {
                    CollectLinks(child, links);
                }
            }
        }
    }
}
