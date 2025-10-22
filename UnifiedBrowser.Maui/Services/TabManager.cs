using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using UnifiedBrowser.Maui.Models;

namespace UnifiedBrowser.Maui.Services
{
    /// <summary>
    /// Manages browser tabs including creation, deletion, and active tab tracking
    /// </summary>
    public sealed class TabManager : INotifyPropertyChanged
    {
        private BrowserTab? _activeTab;
        private readonly int _maxTabs;
        private readonly bool _enableRouteExtraction;
        private readonly bool _normalizeRoutes;
        private readonly string? _userAgent;
        private readonly MarketAlly.Maui.ViewEngine.UserAgentMode _userAgentMode;
        private readonly int _maxRoutes;
        private readonly bool _enableAdDetection;
        private readonly string _defaultUrl;
        private string? _thumbnailStoragePath;

        /// <summary>
        /// Collection of all open tabs
        /// </summary>
        public ObservableCollection<BrowserTab> Tabs { get; } = new();

        /// <summary>
        /// The currently active tab
        /// </summary>
        public BrowserTab? ActiveTab
        {
            get => _activeTab;
            set
            {
                if (_activeTab != value)
                {
                    _activeTab = value;
                    if (_activeTab != null)
                    {
                        _activeTab.LastAccessed = DateTime.Now;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasTabs));
                    OnPropertyChanged(nameof(TabCount));
                    OnPropertyChanged(nameof(CanAddTab));
                    ActiveTabChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Returns true if there are any tabs open
        /// </summary>
        public bool HasTabs => Tabs.Count > 0;

        /// <summary>
        /// The number of open tabs
        /// </summary>
        public int TabCount => Tabs.Count;

        /// <summary>
        /// Whether a new tab can be added (respects max tab limit)
        /// </summary>
        public bool CanAddTab => Tabs.Count < _maxTabs;

        /// <summary>
        /// Event raised when the active tab changes
        /// </summary>
        public event EventHandler? ActiveTabChanged;

        /// <summary>
        /// Gets or sets the path where thumbnails are stored
        /// </summary>
        public string? ThumbnailStoragePath
        {
            get => _thumbnailStoragePath;
            set
            {
                if (_thumbnailStoragePath != value)
                {
                    _thumbnailStoragePath = value;
                    OnPropertyChanged();
                    EnsureThumbnailDirectory();
                }
            }
        }

        /// <summary>
        /// Creates a new tab manager
        /// </summary>
        /// <param name="maxTabs">Maximum number of tabs allowed (default 10 for mobile performance)</param>
        /// <param name="defaultUrl">Default URL for new tabs</param>
        /// <param name="enableRouteExtraction">Enable route extraction for new tabs</param>
        /// <param name="normalizeRoutes">Normalize routes for new tabs</param>
        /// <param name="userAgent">Custom user agent for new tabs</param>
        /// <param name="userAgentMode">User agent mode for new tabs</param>
        /// <param name="maxRoutes">Maximum number of routes to extract</param>
        /// <param name="enableAdDetection">Enable ad detection for new tabs</param>
        public TabManager(
            int maxTabs = 10,
            string defaultUrl = "https://www.google.com",
            bool enableRouteExtraction = true,
            bool normalizeRoutes = true,
            string? userAgent = null,
            MarketAlly.Maui.ViewEngine.UserAgentMode userAgentMode = MarketAlly.Maui.ViewEngine.UserAgentMode.Default,
            int maxRoutes = 200,
            bool enableAdDetection = true)
        {
            _maxTabs = maxTabs;
            _defaultUrl = defaultUrl;
            _enableRouteExtraction = enableRouteExtraction;
            _normalizeRoutes = normalizeRoutes;
            _userAgent = userAgent;
            _userAgentMode = userAgentMode;
            _maxRoutes = maxRoutes;
            _enableAdDetection = enableAdDetection;
        }

        /// <summary>
        /// Creates and adds a new tab
        /// </summary>
        /// <param name="url">Optional URL to navigate to</param>
        /// <returns>The newly created tab, or null if max tabs reached</returns>
        public BrowserTab? NewTab(string? url = null)
        {
            if (!CanAddTab)
                return null;

            var tab = new BrowserTab(
                url ?? _defaultUrl,
                _enableRouteExtraction,
                _normalizeRoutes,
                _userAgent,
                _userAgentMode,
                _maxRoutes,
                _enableAdDetection);

            Tabs.Add(tab);
            ActiveTab = tab;

            OnPropertyChanged(nameof(HasTabs));
            OnPropertyChanged(nameof(TabCount));
            OnPropertyChanged(nameof(CanAddTab));

            return tab;
        }

        /// <summary>
        /// Closes a specific tab
        /// </summary>
        /// <param name="tab">The tab to close</param>
        public void CloseTab(BrowserTab tab)
        {
            if (tab == null)
                return;

            var index = Tabs.IndexOf(tab);
            if (index == -1)
                return;

            // Delete the thumbnail for this tab
            DeleteThumbnail(tab.Id);

            Tabs.Remove(tab);
            tab.Dispose();

            // If this was the active tab, select a new one
            if (ActiveTab == tab)
            {
                if (Tabs.Count > 0)
                {
                    // Try to select the tab at the same index, or the previous one
                    var newIndex = Math.Min(index, Tabs.Count - 1);
                    ActiveTab = Tabs[newIndex];
                }
                else
                {
                    ActiveTab = null;
                }
            }

            OnPropertyChanged(nameof(HasTabs));
            OnPropertyChanged(nameof(TabCount));
            OnPropertyChanged(nameof(CanAddTab));
        }

        /// <summary>
        /// Closes all tabs except the active one
        /// </summary>
        public void CloseOtherTabs()
        {
            if (ActiveTab == null)
                return;

            var tabsToClose = Tabs.Where(t => t != ActiveTab).ToList();
            foreach (var tab in tabsToClose)
            {
                Tabs.Remove(tab);
                tab.Dispose();
            }

            OnPropertyChanged(nameof(TabCount));
            OnPropertyChanged(nameof(CanAddTab));
        }

        /// <summary>
        /// Closes all tabs
        /// </summary>
        public void CloseAllTabs()
        {
            var allTabs = Tabs.ToList();
            Tabs.Clear();

            foreach (var tab in allTabs)
            {
                // Delete thumbnail for each tab
                DeleteThumbnail(tab.Id);
                tab.Dispose();
            }

            ActiveTab = null;

            OnPropertyChanged(nameof(HasTabs));
            OnPropertyChanged(nameof(TabCount));
            OnPropertyChanged(nameof(CanAddTab));
        }

        /// <summary>
        /// Switches to the next tab
        /// </summary>
        public void NextTab()
        {
            if (Tabs.Count <= 1 || ActiveTab == null)
                return;

            var currentIndex = Tabs.IndexOf(ActiveTab);
            var nextIndex = (currentIndex + 1) % Tabs.Count;
            ActiveTab = Tabs[nextIndex];
        }

        /// <summary>
        /// Switches to the previous tab
        /// </summary>
        public void PreviousTab()
        {
            if (Tabs.Count <= 1 || ActiveTab == null)
                return;

            var currentIndex = Tabs.IndexOf(ActiveTab);
            var prevIndex = currentIndex - 1;
            if (prevIndex < 0)
                prevIndex = Tabs.Count - 1;
            ActiveTab = Tabs[prevIndex];
        }

        /// <summary>
        /// Finds a tab by its ID
        /// </summary>
        public BrowserTab? FindTab(Guid id)
        {
            return Tabs.FirstOrDefault(t => t.Id == id);
        }

        /// <summary>
        /// Duplicates the current tab
        /// </summary>
        public BrowserTab? DuplicateTab(BrowserTab? tab = null)
        {
            tab ??= ActiveTab;
            if (tab == null || !CanAddTab)
                return null;

            return NewTab(tab.Url);
        }

        #region Thumbnail Management

        /// <summary>
        /// Gets the default thumbnail storage path
        /// </summary>
        public static string GetDefaultThumbnailPath()
        {
            var appDataPath = FileSystem.Current.AppDataDirectory;
            return Path.Combine(appDataPath, "thumbnails");
        }

        /// <summary>
        /// Ensures the thumbnail directory exists
        /// </summary>
        private void EnsureThumbnailDirectory()
        {
            if (string.IsNullOrWhiteSpace(_thumbnailStoragePath))
            {
                _thumbnailStoragePath = GetDefaultThumbnailPath();
            }

            if (!Directory.Exists(_thumbnailStoragePath))
            {
                Directory.CreateDirectory(_thumbnailStoragePath);
            }
        }

        /// <summary>
        /// Gets the thumbnail file path for a tab
        /// </summary>
        public string GetThumbnailPath(Guid tabId)
        {
            EnsureThumbnailDirectory();
            return Path.Combine(_thumbnailStoragePath!, $"{tabId}.png");
        }

        /// <summary>
        /// Saves a thumbnail for a tab
        /// </summary>
        public async Task<bool> SaveThumbnailAsync(BrowserTab tab, byte[] imageData)
        {
            try
            {
                var path = GetThumbnailPath(tab.Id);
                await File.WriteAllBytesAsync(path, imageData);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving thumbnail: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads a thumbnail for a tab
        /// </summary>
        public async Task<byte[]?> LoadThumbnailAsync(Guid tabId)
        {
            try
            {
                var path = GetThumbnailPath(tabId);
                if (File.Exists(path))
                {
                    return await File.ReadAllBytesAsync(path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading thumbnail: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Deletes a thumbnail for a tab
        /// </summary>
        public void DeleteThumbnail(Guid tabId)
        {
            try
            {
                var path = GetThumbnailPath(tabId);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting thumbnail: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up orphaned thumbnails (thumbnails without corresponding tabs)
        /// </summary>
        public void CleanupOrphanedThumbnails()
        {
            try
            {
                EnsureThumbnailDirectory();
                var thumbnailFiles = Directory.GetFiles(_thumbnailStoragePath!, "*.png");
                var tabIds = Tabs.Select(t => t.Id.ToString()).ToHashSet();

                foreach (var file in thumbnailFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (!tabIds.Contains(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            System.Diagnostics.Debug.WriteLine($"Deleted orphaned thumbnail: {fileName}");
                        }
                        catch
                        {
                            // Ignore individual file deletion errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cleaning up thumbnails: {ex.Message}");
            }
        }

        #endregion

        #region State Persistence

        /// <summary>
        /// Saves the current tab state to JSON
        /// </summary>
        /// <returns>JSON string representation of the tab state</returns>
        public string SaveTabState()
        {
            var state = new BrowserTabsState
            {
                Version = 1,
                SavedAt = DateTime.UtcNow,
                ActiveTabId = ActiveTab?.Id.ToString(),
                Layout = new TabLayoutState
                {
                    Sort = TabSortMode.Manual,
                    ViewMode = TabViewMode.Grid,
                    MaxThumbnailEdge = 256
                },
                Tabs = new List<TabState>()
            };

            foreach (var tab in Tabs)
            {
                var tabState = new TabState
                {
                    Id = tab.Id.ToString(),
                    CreatedAt = tab.CreatedAt,
                    LastAccessedAt = tab.LastAccessed,
                    Title = tab.Title,
                    Url = tab.Url,
                    Pinned = false,
                    Incognito = false
                };

                // Convert favicon if available
                if (tab.Favicon != null)
                {
                    // For now, we'll store as URL if it's a URI-based ImageSource
                    // This would need enhancement based on the actual ImageSource type
                    tabState.Favicon = new ImageRef
                    {
                        Kind = ImageRefKind.Url,
                        Value = tab.Favicon.ToString() ?? string.Empty,
                        UpdatedAt = DateTime.UtcNow
                    };
                }

                // Reference thumbnail file if it exists
                var thumbnailPath = GetThumbnailPath(tab.Id);
                if (File.Exists(thumbnailPath))
                {
                    tabState.Thumbnail = new ImageRefWithSize
                    {
                        Kind = ImageRefKind.File,
                        Value = Path.GetFileName(thumbnailPath), // Store just the filename
                        UpdatedAt = File.GetLastWriteTimeUtc(thumbnailPath),
                        Width = 256,
                        Height = 256
                    };
                }

                // Add scroll state if we can get it
                tabState.State = new TabScrollState
                {
                    ScrollX = 0,
                    ScrollY = 0,
                    Zoom = 1.0
                };

                // History could be populated from WebView navigation history if accessible
                tabState.History = new List<TabHistoryEntry>();

                state.Tabs.Add(tabState);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Serialize(state, options);
        }

        /// <summary>
        /// Restores tab state from JSON
        /// </summary>
        /// <param name="jsonState">JSON string containing the tab state</param>
        /// <returns>True if restore was successful</returns>
        public async Task<bool> RestoreTabState(string jsonState)
        {
            if (string.IsNullOrWhiteSpace(jsonState))
                return false;

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var state = JsonSerializer.Deserialize<BrowserTabsState>(jsonState, options);
                if (state == null || state.Tabs == null || state.Tabs.Count == 0)
                    return false;

                // Clear existing tabs
                CloseAllTabs();

                BrowserTab? activeTabToSet = null;

                // Restore each tab
                foreach (var tabState in state.Tabs)
                {
                    if (string.IsNullOrWhiteSpace(tabState.Url))
                        continue;

                    var tab = new BrowserTab(
                        tabState.Url,
                        _enableRouteExtraction,
                        _normalizeRoutes,
                        _userAgent,
                        _userAgentMode,
                        _maxRoutes,
                        _enableAdDetection);

                    // Restore tab properties
                    tab.Title = tabState.Title ?? "New Tab";
                    tab.CreatedAt = tabState.CreatedAt;
                    tab.LastAccessed = tabState.LastAccessedAt;

                    // IMPORTANT: Explicitly set loading state to false for restored tabs
                    // Restored tabs are not actively loading, they're being restored from saved state
                    tab.IsLoading = false;

                    // Restore thumbnail if it's a file reference
                    // IMPORTANT: The tabState has the OLD tab ID, and tab.Id is the NEW ID
                    // We need to look for the thumbnail using the OLD ID from the saved state
                    if (tabState.Thumbnail != null && tabState.Thumbnail.Kind == ImageRefKind.File)
                    {
                        // Parse the old tab ID from the state
                        if (Guid.TryParse(tabState.Id, out var oldTabId))
                        {
                            var oldThumbnailPath = GetThumbnailPath(oldTabId);
                            if (File.Exists(oldThumbnailPath))
                            {
                                // Copy the thumbnail to the new tab's ID location
                                var newThumbnailPath = GetThumbnailPath(tab.Id);
                                try
                                {
                                    File.Copy(oldThumbnailPath, newThumbnailPath, overwrite: true);

                                    // Set the thumbnail to an ImageSource from the new file
                                    tab.Thumbnail = ImageSource.FromFile(newThumbnailPath);

                                    System.Diagnostics.Debug.WriteLine($"Restored thumbnail from {oldTabId} to {tab.Id}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error copying thumbnail: {ex.Message}");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Thumbnail file not found at: {oldThumbnailPath}");
                            }
                        }
                    }

                    // Restore favicon if available
                    if (tabState.Favicon != null)
                    {
                        if (tabState.Favicon.Kind == ImageRefKind.Url)
                        {
                            tab.Favicon = ImageSource.FromUri(new Uri(tabState.Favicon.Value));
                        }
                        else if (tabState.Favicon.Kind == ImageRefKind.File)
                        {
                            tab.Favicon = ImageSource.FromFile(tabState.Favicon.Value);
                        }
                    }

                    Tabs.Add(tab);

                    // Track which tab should be active
                    if (tabState.Id == state.ActiveTabId)
                    {
                        activeTabToSet = tab;
                    }
                }

                // If no tabs were restored, create a default one
                if (Tabs.Count == 0)
                {
                    NewTab(_defaultUrl);
                    return false;
                }

                // Set the active tab
                ActiveTab = activeTabToSet ?? Tabs.FirstOrDefault();

                OnPropertyChanged(nameof(HasTabs));
                OnPropertyChanged(nameof(TabCount));
                OnPropertyChanged(nameof(CanAddTab));

                return true;
            }
            catch (Exception ex)
            {
                // Log error if logger is available
                System.Diagnostics.Debug.WriteLine($"Error restoring tab state: {ex.Message}");

                // Ensure at least one tab exists
                if (Tabs.Count == 0)
                {
                    NewTab(_defaultUrl);
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the file path for storing tab state
        /// </summary>
        /// <returns>Full path to the tab state file</returns>
        public static string GetTabStateFilePath()
        {
            var appDataPath = FileSystem.Current.AppDataDirectory;
            return Path.Combine(appDataPath, "browser_tabs_state.json");
        }

        /// <summary>
        /// Saves tab state to file
        /// </summary>
        public async Task SaveTabStateToFile()
        {
            try
            {
                var json = SaveTabState();
                var filePath = GetTabStateFilePath();
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving tab state to file: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads tab state from file
        /// </summary>
        public async Task<bool> LoadTabStateFromFile()
        {
            try
            {
                var filePath = GetTabStateFilePath();
                if (!File.Exists(filePath))
                    return false;

                var json = await File.ReadAllTextAsync(filePath);
                return await RestoreTabState(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tab state from file: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}