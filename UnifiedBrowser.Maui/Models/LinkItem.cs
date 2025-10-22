namespace UnifiedBrowser.Maui.Models
{
    /// <summary>
    /// Represents a link extracted from a web page
    /// </summary>
    public class LinkItem
    {
        /// <summary>
        /// The URL of the link
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// The display title or text of the link
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Number of times this link appears on the page
        /// </summary>
        public int Occurrences { get; set; } = 1;

        /// <summary>
        /// The ranking/position of the first occurrence
        /// </summary>
        public int Ranking { get; set; } = 0;

        /// <summary>
        /// Whether this link appears to be an advertisement
        /// </summary>
        public bool IsPotentialAd { get; set; } = false;

        /// <summary>
        /// Additional metadata about the link
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }
}