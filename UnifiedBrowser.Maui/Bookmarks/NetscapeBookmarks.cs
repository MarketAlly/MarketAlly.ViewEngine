// NetscapeBookmarks.cs
// Drop-in, dependency-free importer/exporter for the Netscape Bookmark File Format.
// Supports links, folders (recursive), and separators, with date/meta preservation.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnifiedBrowser.Maui.Bookmarks
{
    #region Model

    /// <summary>Base class for any bookmark item stored inside a folder.</summary>
    public abstract class BookmarkItem
    {
        public string Title { get; set; } = string.Empty;

        /// <summary>UNIX seconds since epoch (UTC) if present.</summary>
        public long? AddDateUnix { get; set; }

        /// <summary>UNIX seconds since epoch (UTC) if present.</summary>
        public long? LastModifiedUnix { get; set; }

        public DateTimeOffset? AddDate
        {
            get => AddDateUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(AddDateUnix.Value) : null;
            set => AddDateUnix = value.HasValue ? value.Value.ToUnixTimeSeconds() : null;
        }

        public DateTimeOffset? LastModified
        {
            get => LastModifiedUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(LastModifiedUnix.Value) : null;
            set => LastModifiedUnix = value.HasValue ? value.Value.ToUnixTimeSeconds() : null;
        }
    }

    /// <summary>A bookmark link item (<A ...>title</A>).</summary>
    public sealed class BookmarkLink : BookmarkItem
    {
        public string Url { get; set; } = string.Empty;
        public string? Icon { get; set; }         // Some exporters put raw data URI here
        public string? IconUri { get; set; }      // Or here, depends on browser
        public bool? Feed { get; set; }           // Some exporters include FEED="true"

        public override string ToString() => $"[Link] {Title} => {Url}";
    }

    /// <summary>A visual separator (<HR>). Netscape format doesn't carry a title; we keep dates if present.</summary>
    public sealed class BookmarkSeparator : BookmarkItem
    {
        public override string ToString() => "[Separator]";
    }

    /// <summary>A folder (<H3> inside a <DT>, with nested <DL> ...)</summary>
    public sealed class BookmarkFolder : BookmarkItem
    {
        public bool PersonalToolbarFolder { get; set; }  // PERSONAL_TOOLBAR_FOLDER="true"
        public List<BookmarkItem> Children { get; } = new();
        public override string ToString() => $"[Folder] {Title} ({Children.Count} items)";
    }

    /// <summary>Bookmark root document.</summary>
    public sealed class BookmarkDocument
    {
        public string Title { get; set; } = "Bookmarks";
        public List<BookmarkItem> Children { get; } = new();

        /// <summary>Convenience to find the toolbar folder if present.</summary>
        public BookmarkFolder? GetToolbarFolder()
            => EnumerateFolders(this).FirstOrDefault(f => f.PersonalToolbarFolder);

        private static IEnumerable<BookmarkFolder> EnumerateFolders(BookmarkDocument doc)
        {
            foreach (var i in doc.Children)
            {
                if (i is BookmarkFolder f)
                {
                    yield return f;
                    foreach (var sub in EnumerateFolders(f)) yield return sub;
                }
            }
        }

        private static IEnumerable<BookmarkFolder> EnumerateFolders(BookmarkFolder folder)
        {
            foreach (var i in folder.Children)
            {
                if (i is BookmarkFolder f)
                {
                    yield return f;
                    foreach (var sub in EnumerateFolders(f)) yield return sub;
                }
            }
        }
    }

    #endregion

    #region Parser

    /// <summary>
    /// Parser for the Netscape Bookmark HTML format.
    /// This is a tolerant, token-based parser (no external HTML libs).
    /// </summary>
    public static class NetscapeBookmarkParser
    {
        // Regex helpers – simple and robust for this constrained HTML.
        private static readonly Regex TagRegex = new Regex(
            @"<(?<closing>/)?(?<name>[A-Za-z0-9]+)(?<attrs>(?:\s+[^<>]*?)?)\s*(?<self>/)?>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex AttrRegex = new Regex(
            @"(?<k>[A-Za-z0-9_\-:]+)\s*=\s*(""(?<v>[^""]*)""|'(?<v>[^']*)'|(?<v>[^\s""><]+))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // HTML entity decode for a few common entities (enough for bookmark titles).
        private static string HtmlDecode(string s) => s
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&amp;", "&")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'");

        private static string HtmlEncode(string s) => s
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");

        private sealed class Token
        {
            public string Name = "";
            public Dictionary<string, string> Attrs = new(StringComparer.OrdinalIgnoreCase);
            public bool Closing;
            public bool SelfClosing;
            public int Position;
            public int Length;
        }

        private static IEnumerable<(Token token, string innerText)> Tokenize(string html)
        {
            int lastIndex = 0;
            foreach (Match m in TagRegex.Matches(html))
            {
                // preceding text
                if (m.Index > lastIndex)
                {
                    var txt = html.Substring(lastIndex, m.Index - lastIndex);
                    if (!string.IsNullOrWhiteSpace(txt))
                        yield return (new Token { Name = "#text", Position = lastIndex, Length = txt.Length }, txt);
                }

                var name = m.Groups["name"].Value;
                var closing = m.Groups["closing"].Success;
                var self = m.Groups["self"].Success;
                var attrsRaw = m.Groups["attrs"].Value;

                var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match am in AttrRegex.Matches(attrsRaw))
                {
                    attrs[am.Groups["k"].Value] = am.Groups["v"].Value;
                }

                yield return (new Token
                {
                    Name = name.ToUpperInvariant(),
                    Attrs = attrs,
                    Closing = closing,
                    SelfClosing = self,
                    Position = m.Index,
                    Length = m.Length
                }, "");
                lastIndex = m.Index + m.Length;
            }

            if (lastIndex < html.Length)
            {
                var tail = html.Substring(lastIndex);
                if (!string.IsNullOrWhiteSpace(tail))
                    yield return (new Token { Name = "#text", Position = lastIndex, Length = tail.Length }, tail);
            }
        }

        /// <summary>Import from a Netscape bookmark HTML string.</summary>
        public static BookmarkDocument Import(string html)
        {
            // Normalize line breaks to simplify <DT> / <DL> adjacency expectations.
            var tokens = Tokenize(html).ToList();

            var doc = new BookmarkDocument();
            var folderStack = new Stack<BookmarkFolder>();
            // Root "DL" corresponds to doc.Children
            List<BookmarkItem> currentList = doc.Children;

            // For reading title text after <TITLE> or <H1> or <H3> or <A>
            string? pendingText = null;

            // We need to know when an <H3> belongs to a folder (within a <DT> before a <DL>)
            BookmarkFolder? pendingFolder = null;
            BookmarkLink? pendingLink = null;

            for (int i = 0; i < tokens.Count; i++)
            {
                var (tok, inner) = tokens[i];

                if (tok.Name == "#text")
                {
                    // Clean the text node
                    var text = HtmlDecode(inner.Trim());
                    if (!string.IsNullOrEmpty(text))
                        pendingText = text;
                    continue;
                }

                // Document title
                if (!tok.Closing && tok.Name == "TITLE")
                {
                    // The next text node will be the title (pendingText already set by tokenizer order)
                    // We'll pick it up when TITLE closes or at next #text.
                    // Just continue to gather.
                    continue;
                }
                if (tok.Closing && tok.Name == "TITLE")
                {
                    if (!string.IsNullOrEmpty(pendingText))
                    {
                        doc.Title = pendingText;
                        pendingText = null;
                    }
                    continue;
                }

                // Root heading <H1>Bookmarks</H1>
                if (tok.Name == "H1")
                {
                    if (tok.Closing)
                    {
                        // ignore; title already handled by <TITLE>, but if missing, we can fall back
                        if (string.IsNullOrEmpty(doc.Title) && !string.IsNullOrEmpty(pendingText))
                        {
                            doc.Title = pendingText;
                        }
                        pendingText = null;
                    }
                    continue;
                }

                // Structure: <DL> opens a list; </DL> closes it.
                if (!tok.Closing && tok.Name == "DL")
                {
                    // When we see a DL after a pendingFolder, this DL contains its children.
                    if (pendingFolder != null)
                    {
                        // Add the folder to the current list BEFORE pushing to stack
                        currentList.Add(pendingFolder);
                        folderStack.Push(pendingFolder);
                        currentList = pendingFolder.Children;
                        pendingFolder = null;
                    }
                    else
                    {
                        // Nested DL without pendingFolder; assume it's still within the current folder.
                        // (Some exporters may nest DLs for spacing.)
                        // No stack change if already at desired level.
                    }
                    continue;
                }
                if (tok.Closing && tok.Name == "DL")
                {
                    // Close one level of nesting if we have a folder on the stack.
                    if (folderStack.Count > 0)
                    {
                        folderStack.Pop();
                        currentList = folderStack.Count == 0 ? doc.Children : folderStack.Peek().Children;
                    }
                    continue;
                }

                // <DT> typically wraps a folder header (<H3>) or a link (<A>) or <HR>
                if (!tok.Closing && tok.Name == "DT")
                {
                    // Nothing to do on open. We'll handle inner tags (<H3>, <A>, <HR>).
                    continue;
                }

                // Separators
                if (!tok.Closing && tok.Name == "HR")
                {
                    var sep = new BookmarkSeparator
                    {
                        AddDateUnix = ParseLongAttr(tok.Attrs, "ADD_DATE"),
                        LastModifiedUnix = ParseLongAttr(tok.Attrs, "LAST_MODIFIED"),
                    };
                    currentList.Add(sep);
                    continue;
                }

                // Folders: <H3 ...>Title</H3> inside a DT
                if (tok.Name == "H3")
                {
                    if (!tok.Closing)
                    {
                        // Opening H3: set up pending folder and capture attributes
                        pendingFolder = new BookmarkFolder
                        {
                            AddDateUnix = ParseLongAttr(tok.Attrs, "ADD_DATE"),
                            LastModifiedUnix = ParseLongAttr(tok.Attrs, "LAST_MODIFIED"),
                            PersonalToolbarFolder = ParseBoolAttr(tok.Attrs, "PERSONAL_TOOLBAR_FOLDER")
                        };
                    }
                    else
                    {
                        if (pendingFolder != null)
                        {
                            if (!string.IsNullOrEmpty(pendingText)) pendingFolder.Title = pendingText;
                            pendingText = null;

                            // We don't add to current list yet; we wait for <DL> to push and add children.
                            // But some exporters omit <DL> for empty folders—handle that by adding now.
                            // If the next non-text token isn't a <DL>, we add immediately.
                            var nextNonText = NextNonText(tokens, i + 1);
                            if (nextNonText?.Name != "DL")
                            {
                                // Empty folder
                                currentList.Add(pendingFolder);
                                pendingFolder = null;
                            }
                        }
                    }
                    continue;
                }

                // Links: <A HREF="...">Title</A> inside a DT
                if (tok.Name == "A")
                {
                    if (!tok.Closing)
                    {
                        pendingLink = new BookmarkLink
                        {
                            Url = tok.Attrs.TryGetValue("HREF", out var href) ? href : "",
                            AddDateUnix = ParseLongAttr(tok.Attrs, "ADD_DATE"),
                            LastModifiedUnix = ParseLongAttr(tok.Attrs, "LAST_MODIFIED"),
                            Icon = tok.Attrs.TryGetValue("ICON", out var icon) ? icon : null,
                            IconUri = tok.Attrs.TryGetValue("ICON_URI", out var iconUri) ? iconUri : null,
                            Feed = ParseBoolAttrNullable(tok.Attrs, "FEED")
                        };
                    }
                    else
                    {
                        if (pendingLink != null)
                        {
                            if (!string.IsNullOrEmpty(pendingText)) pendingLink.Title = pendingText;
                            pendingText = null;
                            currentList.Add(pendingLink);
                            pendingLink = null;
                        }
                    }
                    continue;
                }

                // When </DT> arrives, if we had a pendingFolder that didn't get a DL, ensure it's added.
                if (tok.Closing && tok.Name == "DT")
                {
                    if (pendingFolder != null)
                    {
                        currentList.Add(pendingFolder);
                        pendingFolder = null;
                    }
                    // Links handled on </A>, nothing to do.
                    continue;
                }
            }

            return doc;
        }

        private static Token? NextNonText(List<(Token token, string innerText)> tokens, int start)
        {
            for (int j = start; j < tokens.Count; j++)
            {
                var (t, text) = tokens[j];
                if (t.Name != "#text" && !string.IsNullOrWhiteSpace(t.Name))
                    return t;
            }
            return null;
        }

        private static long? ParseLongAttr(Dictionary<string, string> attrs, string key)
            => attrs.TryGetValue(key, out var s) && long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : (long?)null;

        private static bool ParseBoolAttr(Dictionary<string, string> attrs, string key)
            => attrs.TryGetValue(key, out var s) && IsTrue(s);

        private static bool? ParseBoolAttrNullable(Dictionary<string, string> attrs, string key)
            => attrs.TryGetValue(key, out var s) ? IsTrue(s) : (bool?)null;

        private static bool IsTrue(string s)
            => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1" || s.Equals("yes", StringComparison.OrdinalIgnoreCase);

    }

    #endregion

    #region Exporter

    public static class NetscapeBookmarkExporter
    {
        /// <summary>Export a document to Netscape Bookmark HTML.</summary>
        public static string Export(BookmarkDocument doc, bool includeGeneratorComment = true, string? charset = "UTF-8")
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
            sb.AppendLine("<!-- This is an automatically generated file.");
            sb.AppendLine("     It will be read and overwritten. Do Not Edit! -->");
            if (includeGeneratorComment)
                sb.AppendLine("<!-- Generated by NetscapeBookmarks (C#) -->");

            if (!string.IsNullOrEmpty(charset))
                sb.AppendLine($"<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset={charset}\">");

            sb.AppendLine($"<TITLE>{HtmlEncode(doc.Title)}</TITLE>");
            sb.AppendLine($"<H1>{HtmlEncode(doc.Title)}</H1>");
            sb.AppendLine("<DL><p>");
            foreach (var item in doc.Children)
            {
                WriteItem(sb, item, indent: 1);
            }
            sb.AppendLine("</DL><p>");
            return sb.ToString();

            static string HtmlEncode(string s) => s
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        private static void WriteItem(StringBuilder sb, BookmarkItem item, int indent)
        {
            string ind = new string(' ', indent * 4);

            switch (item)
            {
                case BookmarkLink a:
                    {
                        sb.Append(ind).Append("<DT><A");
                        sb.Append($" HREF=\"{EscapeAttr(a.Url)}\"");
                        if (a.AddDateUnix.HasValue) sb.Append($" ADD_DATE=\"{a.AddDateUnix.Value}\"");
                        if (a.LastModifiedUnix.HasValue) sb.Append($" LAST_MODIFIED=\"{a.LastModifiedUnix.Value}\"");
                        if (!string.IsNullOrEmpty(a.Icon)) sb.Append($" ICON=\"{EscapeAttr(a.Icon)}\"");
                        if (!string.IsNullOrEmpty(a.IconUri)) sb.Append($" ICON_URI=\"{EscapeAttr(a.IconUri)}\"");
                        if (a.Feed.HasValue) sb.Append($" FEED=\"{(a.Feed.Value ? "true" : "false")}\"");
                        sb.Append(">");
                        sb.Append(HtmlEncode(a.Title));
                        sb.AppendLine("</A>");
                        break;
                    }

                case BookmarkSeparator s:
                    {
                        sb.Append(ind).Append("<DT><HR");
                        if (s.AddDateUnix.HasValue) sb.Append($" ADD_DATE=\"{s.AddDateUnix.Value}\"");
                        if (s.LastModifiedUnix.HasValue) sb.Append($" LAST_MODIFIED=\"{s.LastModifiedUnix.Value}\"");
                        sb.AppendLine(">");
                        break;
                    }

                case BookmarkFolder f:
                    {
                        sb.Append(ind).Append("<DT><H3");
                        if (f.AddDateUnix.HasValue) sb.Append($" ADD_DATE=\"{f.AddDateUnix.Value}\"");
                        if (f.LastModifiedUnix.HasValue) sb.Append($" LAST_MODIFIED=\"{f.LastModifiedUnix.Value}\"");
                        if (f.PersonalToolbarFolder) sb.Append(" PERSONAL_TOOLBAR_FOLDER=\"true\"");
                        sb.Append(">");
                        sb.Append(HtmlEncode(f.Title));
                        sb.AppendLine("</H3>");
                        sb.Append(ind).AppendLine("<DL><p>");
                        foreach (var child in f.Children)
                            WriteItem(sb, child, indent + 1);
                        sb.Append(ind).AppendLine("</DL><p>");
                        break;
                    }
            }

            static string EscapeAttr(string s) => s
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;");

            static string HtmlEncode(string s) => s
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }

    #endregion

    #region Convenience API

    public static class NetscapeBookmarksIO
    {
        public static BookmarkDocument LoadFromFile(string path)
            => NetscapeBookmarkParser.Import(File.ReadAllText(path, DetectEncoding(path)));

        public static BookmarkDocument LoadFromString(string html)
            => NetscapeBookmarkParser.Import(html);

        public static void SaveToFile(BookmarkDocument doc, string path, Encoding? encoding = null)
        {
            var html = NetscapeBookmarkExporter.Export(doc);
            File.WriteAllText(path, html, encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static Encoding DetectEncoding(string path)
        {
            // Minimal detection: if BOM present, use it; else assume UTF-8.
            using var fs = File.OpenRead(path);
            var bom = new byte[4];
            int read = fs.Read(bom, 0, 4);
            fs.Position = 0;

            if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return Encoding.UTF8;
            if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;          // UTF-16 LE
            if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode; // UTF-16 BE
            if (read == 4 && bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF) return Encoding.GetEncoding("utf-32BE");
            if (read == 4 && bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00) return Encoding.UTF32;

            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
    }

    #endregion
}
