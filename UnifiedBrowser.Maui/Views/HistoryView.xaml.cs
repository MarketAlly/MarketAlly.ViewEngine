using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketAlly.Dialogs.Maui.Dialogs;
using MarketAlly.Dialogs.Maui.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using UnifiedBrowser.Maui.ViewModels;
using UnifiedData.Maui.Core;

namespace UnifiedBrowser.Maui.Views
{
    /// <summary>
    /// View for displaying browsing history from saved web documents
    /// </summary>
    public partial class HistoryView : ContentView
    {
        public HistoryView()
        {
            InitializeComponent();

            // Subscribe to SizeChanged event for responsive search bar
            HistoryToolbar.SizeChanged += OnToolbarSizeChanged;
        }

        /// <summary>
        /// Sets the view model and initializes the view
        /// </summary>
        public void Initialize(BrowserViewModel viewModel, IUnifiedDataService? dataService, ILogger? logger, bool showMarkdownHistory = true)
        {
            var historyViewModel = new HistoryViewModel(viewModel, dataService, logger, showMarkdownHistory);
            BindingContext = historyViewModel;

            // Load history when view is shown
            _ = historyViewModel.LoadHistoryAsync();
        }

        private void OnToolbarSizeChanged(object? sender, EventArgs e)
        {
            var toolbarWidth = HistoryToolbar.Width;
            if (toolbarWidth <= 0 || double.IsNaN(toolbarWidth))
                toolbarWidth = this.Width;
            SearchEntry.WidthRequest = toolbarWidth - 80;
        }

        /// <summary>
        /// Cleanup event subscriptions to prevent memory leaks
        /// </summary>
        protected override void OnHandlerChanging(HandlerChangingEventArgs args)
        {
            base.OnHandlerChanging(args);

            // Unsubscribe when handler is being removed (view is being disposed)
            if (args.NewHandler == null)
            {
                HistoryToolbar.SizeChanged -= OnToolbarSizeChanged;
            }
        }
    }

    /// <summary>
    /// View model for the history view
    /// </summary>
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly BrowserViewModel _browserViewModel;
        private readonly IUnifiedDataService? _dataService;
        private readonly ILogger? _logger;
        private readonly bool _showMarkdownHistory;

        [ObservableProperty]
        private ObservableCollection<UnifiedDocument> _filteredHistoryItems = new();

        [ObservableProperty]
        private ObservableCollection<TopicGroup> _topicGroups = new();

        [ObservableProperty]
        private ObservableCollection<DateGroup> _dateGroups = new();

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private bool _isEmpty = false;

        [ObservableProperty]
        private bool _isRefreshing = false;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isFavorite = false;

        private List<UnifiedDocument> _allHistoryItems = new();
        private string? _selectedTopic = null;
        private DateTime? _selectedDate = null;

        public HistoryViewModel(BrowserViewModel browserViewModel, IUnifiedDataService? dataService, ILogger? logger, bool showMarkdownHistory = true)
        {
            _browserViewModel = browserViewModel;
            _dataService = dataService;
            _logger = logger;
            _showMarkdownHistory = showMarkdownHistory;
	
			// Subscribe to search query and favorite changes
			PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SearchQuery) || e.PropertyName == nameof(IsFavorite))
                {
                    ApplyFilters();
                }
            };
        }

        /// <summary>
        /// Loads history from the data service
        /// </summary>
        public async Task LoadHistoryAsync()
        {
            if (_dataService == null)
            {
                _logger?.LogWarning("Data service is null, cannot load history");
                IsEmpty = true;
                return;
            }

            IsLoading = true;
            IsEmpty = false;

            try
            {
                // Get all web documents
                var documents = await _dataService.GetAllDocumentsAsync(
                    offset: 0,
                    limit: 1000,
                    contentType: (int)DocumentContentType.Web
                );

                _allHistoryItems = documents
                    .OrderByDescending(d => d.CreatedAt)
                    .ToList();

                IsEmpty = _allHistoryItems.Count == 0;

                // Build topic and date groups
                await BuildTopicGroupsAsync();
                BuildDateGroups();

                // Apply filters to show initial results
                ApplyFilters();

                _logger?.LogInformation("Loaded {Count} history items", _allHistoryItems.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading history");
                IsEmpty = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Refreshes the history list
        /// </summary>
        [RelayCommand]
        private async Task RefreshHistory()
        {
            IsRefreshing = true;
            try
            {
                await LoadHistoryAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// Builds topic groups with document counts
        /// </summary>
        private async Task BuildTopicGroupsAsync()
        {
            if (_dataService == null) return;

            var topicDict = new Dictionary<string, int>();

            try
            {
                foreach (var doc in _allHistoryItems)
                {
                    var topics = await _dataService.GetDocumentTopicsAsync(doc.Id);

                    if (topics != null && topics.Any())
                    {
                        var primaryTopic = topics.OrderByDescending(t => t.Confidence).First().Topic;
                        if (!topicDict.ContainsKey(primaryTopic))
                            topicDict[primaryTopic] = 0;
                        topicDict[primaryTopic]++;
                    }
                    else
                    {
                        if (!topicDict.ContainsKey("Uncategorized"))
                            topicDict["Uncategorized"] = 0;
                        topicDict["Uncategorized"]++;
                    }
                }

                TopicGroups = new ObservableCollection<TopicGroup>(
                    topicDict.OrderByDescending(kvp => kvp.Value)
                             .Select(kvp => new TopicGroup { TopicName = kvp.Key, Count = kvp.Value })
                );
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error building topic groups");
            }
        }

        /// <summary>
        /// Builds date groups with document counts
        /// </summary>
        private void BuildDateGroups()
        {
            var dateDict = new Dictionary<DateTime, int>();

            foreach (var doc in _allHistoryItems)
            {
                var dateOnly = doc.CreatedAt.Date;
                if (!dateDict.ContainsKey(dateOnly))
                    dateDict[dateOnly] = 0;
                dateDict[dateOnly]++;
            }

            DateGroups = new ObservableCollection<DateGroup>(
                dateDict.OrderByDescending(kvp => kvp.Key)
                        .Select(kvp => new DateGroup
                        {
                            Date = kvp.Key,
                            Count = kvp.Value,
                            DisplayText = FormatDateGroup(kvp.Key, kvp.Value)
                        })
            );
        }

        /// <summary>
        /// Formats a date group display text
        /// </summary>
        private string FormatDateGroup(DateTime date, int count)
        {
            var today = DateTime.Today;
            string dateLabel;

            if (date == today)
            {
                dateLabel = "Today";
            }
            else if (date == today.AddDays(-1))
            {
                dateLabel = "Yesterday";
            }
            else
            {
                // Only show year if it's different from the current year
                dateLabel = date.Year == today.Year
                    ? date.ToString("MMM d")
                    : date.ToString("MMM d, yyyy");
            }

            return $"{dateLabel} ({count})";
        }

        /// <summary>
        /// Applies current filters to the history list
        /// </summary>
        private void ApplyFilters()
        {
            var filtered = _allHistoryItems.AsEnumerable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(d =>
                    (d.Title?.ToLower().Contains(query) ?? false) ||
                    (d.SourceUrl?.ToLower().Contains(query) ?? false) ||
                    (d.Summary?.ToLower().Contains(query) ?? false)
                );
            }

            // Apply topic filter
            if (!string.IsNullOrWhiteSpace(_selectedTopic))
            {
                // Note: This requires caching topic assignments during BuildTopicGroupsAsync
                // For now, we'll filter this asynchronously when the user selects a topic
            }

            // Apply date filter
            if (_selectedDate.HasValue)
            {
                filtered = filtered.Where(d => d.CreatedAt.Date == _selectedDate.Value.Date);
            }

            // Apply favorite filter
            if (IsFavorite)
            {
                filtered = filtered.Where(d => d.Visibility == 2);
            }

            FilteredHistoryItems = new ObservableCollection<UnifiedDocument>(filtered);
            IsEmpty = !FilteredHistoryItems.Any();
        }

        /// <summary>
        /// Filters history by selected topic
        /// </summary>
        [RelayCommand]
        private async Task FilterByTopic(TopicGroup topicGroup)
        {
            if (_dataService == null) return;

            // If topicGroup is null or the same topic was clicked again, clear the filter
            if (topicGroup == null || _selectedTopic == topicGroup.TopicName)
            {
                _selectedTopic = null;
                _selectedDate = null;
                ApplyFilters();
                return;
            }

            _selectedTopic = topicGroup.TopicName;
            _selectedDate = null;
            SearchQuery = string.Empty;

            IsLoading = true;
            try
            {
                var filtered = new List<UnifiedDocument>();

                foreach (var doc in _allHistoryItems)
                {
                    var topics = await _dataService.GetDocumentTopicsAsync(doc.Id);
                    var hasTopic = topicGroup.TopicName == "Uncategorized"
                        ? (topics == null || !topics.Any())
                        : (topics?.Any(t => t.Topic == topicGroup.TopicName) ?? false);

                    if (hasTopic)
                        filtered.Add(doc);
                }

                FilteredHistoryItems = new ObservableCollection<UnifiedDocument>(filtered);
                IsEmpty = !FilteredHistoryItems.Any();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Filters history by selected date
        /// </summary>
        [RelayCommand]
        private void FilterByDate(DateGroup dateGroup)
        {
            // If dateGroup is null or the same date was clicked again, clear the filter
            if (dateGroup == null || _selectedDate == dateGroup.Date)
            {
                _selectedDate = null;
                _selectedTopic = null;
                ApplyFilters();
                return;
            }

            _selectedDate = dateGroup.Date;
            _selectedTopic = null;
            SearchQuery = string.Empty;

            ApplyFilters();
        }

        /// <summary>
        /// Navigates to a history item
        /// </summary>
        [RelayCommand]
        private async Task NavigateToHistoryItem(UnifiedDocument document)
        {
            if (document == null || string.IsNullOrWhiteSpace(document.SourceUrl))
                return;

            try
            {
                _logger?.LogInformation("Navigating to history item: {Title} -> {Url}", document.Title, document.SourceUrl);

                // Only show markdown view if ShowMarkdownHistory is true and we have content
                if (_showMarkdownHistory && !string.IsNullOrWhiteSpace(document.Content))
                {
                    // Show markdown view with saved content FIRST (before hiding history)
                    // This prevents the flash of the web view
                    await _browserViewModel.NavigateToHistoryItemAsync(
                        document.SourceUrl,
                        document.Content,
                        document.Visibility,
                        document.Id,
                        document.Title);

                    // Load webpage in background if internet is available
                    // This ensures the page is ready when user switches to web view
                    if (IsInternetAvailable())
                    {
                        _browserViewModel.NavigateToUrl(document.SourceUrl, true);
                    }

                    // Now hide history view - markdown view is already showing
                    _browserViewModel.IsHistoryVisible = false;
                }
                else
                {
                    // Hide history view first
                    _browserViewModel.IsHistoryVisible = false;

                    // Navigate to URL and show web view
                    _browserViewModel.NavigateToUrl(document.SourceUrl, true);
                    _browserViewModel.IsWebViewVisible = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error navigating to history item");
            }
        }

        /// <summary>
        /// Checks if internet is available
        /// </summary>
        private bool IsInternetAvailable()
        {
            try
            {
                var current = Connectivity.Current.NetworkAccess;
                return current == NetworkAccess.Internet;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error checking internet connectivity");
                // If we can't check, assume it's available
                return true;
            }
        }

        /// <summary>
        /// Copies URL to clipboard on long press
        /// </summary>
        [RelayCommand]
        private async Task CopyUrlToClipboard(UnifiedDocument document)
        {
            if (document == null || string.IsNullOrWhiteSpace(document.SourceUrl))
                return;

            try
            {
                await Clipboard.Default.SetTextAsync(document.SourceUrl);
                _logger?.LogInformation("Copied URL to clipboard: {Url}", document.SourceUrl);

                // Optional: Show a toast notification to the user
                // You could use a Toast library or display a temporary message
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error copying URL to clipboard");
            }
        }

        /// <summary>
        /// Deletes a history item
        /// </summary>
        [RelayCommand]
        private async Task DeleteHistoryItem(UnifiedDocument document)
        {
            if (document == null || _dataService == null)
                return;

            try
            {
                // Show confirmation dialog
                var title = string.IsNullOrWhiteSpace(document.Title) ? "this page" : document.Title;
                var confirmed = await ConfirmDialog.ShowAsync(
                    "Delete History Item",
                    $"Are you sure you want to delete '{title}' from your browsing history?",
                    "Delete",
                    "Cancel"
                );

                if (!confirmed)
                {
                    _logger?.LogInformation("User cancelled deletion of history item: {Title}", document.Title);
                    return;
                }

                _logger?.LogInformation("Attempting to delete history item: {Title} (ID: {Id})", document.Title, document.Id);

                var success = await _dataService.DeleteAsync(document.Id);

                _logger?.LogInformation("Delete result: {Success}", success);

                if (success)
                {
                    // Remove from collections on the main thread
                    await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Remove from all collections
                        var removed1 = _allHistoryItems.Remove(document);
                        var removed2 = FilteredHistoryItems.Remove(document);

                        _logger?.LogInformation("Removed from _allHistoryItems: {Removed1}, from FilteredHistoryItems: {Removed2}",
                            removed1, removed2);

                        // Update empty state immediately
                        IsEmpty = _allHistoryItems.Count == 0;
                    });

                    // Rebuild groups
                    await BuildTopicGroupsAsync();
                    BuildDateGroups();

                    _logger?.LogInformation("Deleted history item: {Title}. Remaining items: {Count}",
                        document.Title, _allHistoryItems.Count);

                    // Show success message
                    await AlertDialog.ShowAsync(
                        "Deleted",
                        $"'{title}' has been removed from your browsing history.",
                        DialogType.Success
                    );
                }
                else
                {
                    _logger?.LogWarning("Failed to delete history item: {Title} (ID: {Id})", document.Title, document.Id);

                    // Show error message
                    await AlertDialog.ShowAsync(
                        "Delete Failed",
                        "Failed to delete the history item. Please try again.",
                        DialogType.Error
                    );
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting history item: {Title} (ID: {Id})", document.Title, document.Id);

                // Show error message
                await AlertDialog.ShowAsync(
                    "Error",
                    $"An error occurred while deleting the history item: {ex.Message}",
                    DialogType.Error
                );
            }
        }

        /// <summary>
        /// Clears all history
        /// </summary>
        [RelayCommand]
        private async Task ClearHistory()
        {
            if (_dataService == null)
                return;

            try
            {
                // Show confirmation dialog
                var confirmed = await ConfirmDialog.ShowAsync(
                    "Clear All History",
                    "Are you sure you want to delete all browsing history? This action cannot be undone.",
                    "Delete All",
                    "Cancel"
                );

                if (!confirmed)
                {
                    _logger?.LogInformation("User cancelled clearing all history");
                    return;
                }

                // Delete all web documents
                var deleted = await _dataService.DeleteByTypeAsync((int)DocumentContentType.Web);

                _allHistoryItems.Clear();
                FilteredHistoryItems.Clear();
                TopicGroups.Clear();
                DateGroups.Clear();
                IsEmpty = true;

                _logger?.LogInformation("Cleared all history. Deleted {Count} documents", deleted);

                // Show success message
                await AlertDialog.ShowAsync(
                    "History Cleared",
                    $"All browsing history has been deleted ({deleted} items removed).",
                    DialogType.Success
                );
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error clearing history");

                // Show error message
                await AlertDialog.ShowAsync(
                    "Error",
                    $"An error occurred while clearing history: {ex.Message}",
                    DialogType.Error
                );
            }
        }
    }

    /// <summary>
    /// Represents a topic group with document count
    /// </summary>
    public class TopicGroup
    {
        public string TopicName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// Represents a date group with document count
    /// </summary>
    public class DateGroup
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }
}
