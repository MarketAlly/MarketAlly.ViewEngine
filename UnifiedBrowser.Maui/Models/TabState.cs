using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnifiedBrowser.Maui.Models
{
    /// <summary>
    /// Root object for browser tabs state
    /// </summary>
    public class BrowserTabsState
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("savedAt")]
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("activeTabId")]
        public string? ActiveTabId { get; set; }

        [JsonPropertyName("layout")]
        public TabLayoutState? Layout { get; set; }

        [JsonPropertyName("tabs")]
        public List<TabState> Tabs { get; set; } = new();
    }

    /// <summary>
    /// Layout configuration for tabs display
    /// </summary>
    public class TabLayoutState
    {
        [JsonPropertyName("sort")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TabSortMode Sort { get; set; } = TabSortMode.Manual;

        [JsonPropertyName("viewMode")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TabViewMode ViewMode { get; set; } = TabViewMode.Grid;

        [JsonPropertyName("maxThumbnailEdge")]
        public int MaxThumbnailEdge { get; set; } = 256;
    }

    /// <summary>
    /// Tab sort modes
    /// </summary>
    public enum TabSortMode
    {
        [JsonPropertyName("manual")]
        Manual,
        [JsonPropertyName("lastAccessed")]
        LastAccessed,
        [JsonPropertyName("title")]
        Title
    }

    /// <summary>
    /// Tab view modes
    /// </summary>
    public enum TabViewMode
    {
        [JsonPropertyName("grid")]
        Grid,
        [JsonPropertyName("list")]
        List
    }

    /// <summary>
    /// Individual tab state
    /// </summary>
    public class TabState
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("lastAccessedAt")]
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("title")]
        public string Title { get; set; } = "New Tab";

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("pinned")]
        public bool Pinned { get; set; } = false;

        [JsonPropertyName("incognito")]
        public bool Incognito { get; set; } = false;

        [JsonPropertyName("favicon")]
        public ImageRef? Favicon { get; set; }

        [JsonPropertyName("thumbnail")]
        public ImageRefWithSize? Thumbnail { get; set; }

        [JsonPropertyName("state")]
        public TabScrollState? State { get; set; }

        [JsonPropertyName("history")]
        public List<TabHistoryEntry>? History { get; set; }
    }

    /// <summary>
    /// Tab scroll and zoom state
    /// </summary>
    public class TabScrollState
    {
        [JsonPropertyName("scrollX")]
        public double ScrollX { get; set; }

        [JsonPropertyName("scrollY")]
        public double ScrollY { get; set; }

        [JsonPropertyName("zoom")]
        public double Zoom { get; set; } = 1.0;
    }

    /// <summary>
    /// Tab history entry
    /// </summary>
    public class TabHistoryEntry
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("visitedAt")]
        public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Image reference
    /// </summary>
    public class ImageRef
    {
        [JsonPropertyName("kind")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ImageRefKind Kind { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Image reference with size
    /// </summary>
    public class ImageRefWithSize : ImageRef
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    /// <summary>
    /// Image reference kinds
    /// </summary>
    public enum ImageRefKind
    {
        [JsonPropertyName("url")]
        Url,
        [JsonPropertyName("file")]
        File,
        [JsonPropertyName("dataUrl")]
        DataUrl
    }
}