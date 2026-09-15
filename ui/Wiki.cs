using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

namespace TheRoom.UI;

/// <summary>One section of a wiki page: the text under one heading (the page's top is anchor "").</summary>
public sealed record WikiSection(string Anchor, string Heading, string Text);

public sealed record WikiPage(string Id, string Title, string Markdown, IReadOnlyList<WikiSection> Sections);

public sealed record WikiSearchHit(WikiPage Page, WikiSection Section, string SnippetBbcode, int Score);

/// <summary>
/// The player wiki: the markdown pages in res://wiki/ (the same files GitHub shows), loaded at
/// runtime, turned into RichTextLabel BBCode, and searchable in full text. Links between pages use
/// normal relative markdown links (<c>combat.md#dodge</c>); anchors are GitHub-style heading slugs,
/// so a link works both on GitHub and in the game. Export presets must include <c>wiki/*.md</c>
/// (the images are imported like any texture).
/// </summary>
public static class Wiki
{
    public const string Dir = "res://wiki/";
    public const string HomeId = "intro";

    // The brand colours (UiTheme) as BBCode hex.
    private const string Accent = "#c8463d";
    private const string Link = "#e0564c";
    private const string Gold = "#f2c14e";
    private const string Muted = "#8d8c99";
    private const string Text = "#ecebe8";

    private static List<WikiPage>? _pages;

    /// <summary>Every page: the home page first, then in the order the home page links them,
    /// then any page it doesn't link, alphabetically.</summary>
    public static IReadOnlyList<WikiPage> Pages => _pages ??= LoadAll();

    public static WikiPage? Get(string id) => Pages.FirstOrDefault(p => p.Id == id);

    public static void Reload() => _pages = null;

    private static List<WikiPage> LoadAll()
    {
        var ids = DirAccess.GetFilesAt(Dir)
            .Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && f != "CLAUDE.md") // wiki standards, not a page
            .Select(f => f[..^3])
            .ToList();
        var pages = ids.Select(Load).OfType<WikiPage>().ToDictionary(p => p.Id);
        var ordered = new List<WikiPage>();
        if (pages.TryGetValue(HomeId, out var home))
        {
            ordered.Add(home);
            foreach (Match m in LinkRegex.Matches(home.Markdown))
            {
                var (id, _) = ParseTarget(m.Groups[2].Value, HomeId);
                if (id is not null && pages.TryGetValue(id, out var page) && !ordered.Contains(page))
                    ordered.Add(page);
            }
        }
        ordered.AddRange(pages.Values.Where(p => !ordered.Contains(p)).OrderBy(p => p.Id));
        return ordered;
    }

    private static WikiPage? Load(string id)
    {
        var path = Dir + id + ".md";
        if (!Godot.FileAccess.FileExists(path))
            return null;
        var markdown = Godot.FileAccess.GetFileAsString(path).Replace("\r\n", "\n");
        var title = markdown.Split('\n').FirstOrDefault(l => l.StartsWith("# "))?[2..].Trim() ?? id;
        return new WikiPage(id, title, markdown, SplitSections(markdown));
    }

    private static List<WikiSection> SplitSections(string markdown)
    {
        var sections = new List<WikiSection>();
        var heading = "";
        var anchor = "";
        var text = new StringBuilder();
        void Flush()
        {
            sections.Add(new WikiSection(anchor, heading, PlainText(text.ToString())));
            text.Clear();
        }
        foreach (var line in markdown.Split('\n'))
        {
            var m = Regex.Match(line, @"^(#{2,4})\s+(.*)$");
            if (m.Success)
            {
                Flush();
                heading = StripInline(m.Groups[2].Value.Trim());
                anchor = Slug(heading);
                continue;
            }
            if (!line.StartsWith("# "))
                text.AppendLine(line);
        }
        Flush();
        return sections;
    }

    /// <summary>GitHub's heading anchors: lower case, punctuation dropped, spaces to hyphens.</summary>
    public static string Slug(string heading)
    {
        var lower = StripInline(heading).Trim().ToLowerInvariant();
        var kept = new StringBuilder();
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                kept.Append(c);
            else if (c == ' ')
                kept.Append('-');
        }
        return kept.ToString();
    }

    /// <summary>A link target relative to <paramref name="fromPage"/> as (page id, anchor).
    /// Returns a null id for external links.</summary>
    public static (string? PageId, string Anchor) ParseTarget(string target, string fromPage)
    {
        if (target.Contains("://") || target.StartsWith("mailto:"))
            return (null, "");
        var hash = target.IndexOf('#');
        var file = hash >= 0 ? target[..hash] : target;
        var anchor = hash >= 0 ? target[(hash + 1)..] : "";
        if (file.Length == 0)
            return (fromPage, anchor);
        file = file.Replace("\\", "/");
        if (file.StartsWith("./"))
            file = file[2..];
        if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            file = file[..^3];
        return (file, anchor);
    }

    // ------------------------------------------------------------------ markdown -> BBCode

    private static readonly Regex LinkRegex = new(@"(?<!!)\[([^\]]+)\]\(([^)\s]+)\)");
    private static readonly Regex ImageRegex = new(@"^!\[([^\]]*)\]\(([^)\s]+)\)\s*$");

    /// <summary>The page as BBCode. <paramref name="imageWidth"/> caps screenshots to the view.</summary>
    public static string ToBbcode(WikiPage page, int imageWidth)
    {
        var sb = new StringBuilder();
        var paragraph = new List<string>();
        var table = new List<string[]>();

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
                return;
            sb.Append(Inline(string.Join(" ", paragraph), page.Id)).Append("\n\n");
            paragraph.Clear();
        }
        void FlushTable()
        {
            if (table.Count == 0)
                return;
            var columns = table.Max(r => r.Length);
            sb.Append($"[table={columns}]");
            for (var r = 0; r < table.Count; r++)
            {
                for (var c = 0; c < columns; c++)
                {
                    var cell = c < table[r].Length ? Inline(table[r][c], page.Id) : "";
                    var bg = r == 0 ? "#2e2e39" : r % 2 == 1 ? "#1c1c24" : "#22222b";
                    sb.Append($"[cell bg={bg} padding=10,5,10,5]{(r == 0 ? $"[b]{cell}[/b]" : cell)}[/cell]");
                }
            }
            sb.Append("[/table]\n\n");
            table.Clear();
        }

        foreach (var raw in page.Markdown.Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith('|'))
            {
                FlushParagraph();
                var cells = trimmed.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                if (!cells.All(c => Regex.IsMatch(c, @"^:?-{2,}:?$"))) // the |---|---| separator row
                    table.Add(cells);
                continue;
            }
            FlushTable();

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }
            var heading = Regex.Match(trimmed, @"^(#{1,4})\s+(.*)$");
            if (heading.Success)
            {
                FlushParagraph();
                var level = heading.Groups[1].Value.Length;
                var text = Inline(heading.Groups[2].Value, page.Id);
                sb.Append(level switch
                {
                    1 => $"[font_size=32][color={Accent}][b]{text}[/b][/color][/font_size]\n\n",
                    2 => $"[font_size=23][color={Text}][b]{text}[/b][/color][/font_size]\n",
                    _ => $"[font_size=18][color={Gold}][b]{text}[/b][/color][/font_size]\n",
                });
                continue;
            }
            var image = ImageRegex.Match(trimmed);
            if (image.Success)
            {
                FlushParagraph();
                var path = Dir + image.Groups[2].Value.TrimStart('.', '/');
                if (ResourceLoader.Exists(path))
                    sb.Append($"[center][img width={imageWidth}]{path}[/img]\n");
                else
                    sb.Append("[center]");
                sb.Append($"[font_size=13][color={Muted}][i]{Escape(image.Groups[1].Value)}[/i][/color][/font_size][/center]\n\n");
                continue;
            }
            var bullet = Regex.Match(line, @"^(\s*)([-*]|\d+\.)\s+(.*)$");
            if (bullet.Success)
            {
                FlushParagraph();
                var depth = bullet.Groups[1].Value.Length / 2;
                var marker = bullet.Groups[2].Value is "-" or "*" ? "•" : bullet.Groups[2].Value;
                sb.Append(new string(' ', 4 + depth * 4))
                  .Append($"[color={Gold}]{marker}[/color]  ")
                  .Append(Inline(bullet.Groups[3].Value, page.Id)).Append('\n');
                continue;
            }
            if (trimmed.StartsWith('>'))
            {
                FlushParagraph();
                sb.Append($"[indent][bgcolor=#22222b][color={Gold}]  |  [/color]{Inline(trimmed.TrimStart('>', ' '), page.Id)}  [/bgcolor][/indent]\n\n");
                continue;
            }
            if (Regex.IsMatch(trimmed, @"^-{3,}$"))
            {
                FlushParagraph();
                sb.Append('\n');
                continue;
            }
            paragraph.Add(trimmed);
        }
        FlushParagraph();
        FlushTable();
        return sb.ToString();
    }

    /// <summary>Inline markdown (code, links, bold, italic) to BBCode, with the text escaped.</summary>
    private static string Inline(string text, string pageId)
    {
        var tokens = new List<string>();
        string Hold(string bbcode)
        {
            tokens.Add(bbcode);
            return $"{tokens.Count - 1}";
        }

        text = Regex.Replace(text, @"`([^`]+)`", m => Hold($"[bgcolor=#2a2a33][code] {Escape(m.Groups[1].Value)} [/code][/bgcolor]"));
        text = LinkRegex.Replace(text, m =>
        {
            var (id, anchor) = ParseTarget(m.Groups[2].Value, pageId);
            var target = id is null ? m.Groups[2].Value : $"{id}#{anchor}";
            return Hold($"[url={target}][color={Link}]{Escape(m.Groups[1].Value)}[/color][/url]");
        });
        text = Escape(text);
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "[b]$1[/b]");
        text = Regex.Replace(text, @"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", "[i]$1[/i]");
        text = Regex.Replace(text, @"(?<!\w)_(?!\s)(.+?)(?<!\s)_(?!\w)", "[i]$1[/i]");
        return Regex.Replace(text, "(\\d+)", m => tokens[int.Parse(m.Groups[1].Value)]);
    }

    private static string Escape(string text) => text.Replace("[", "[lb]");

    /// <summary>Markdown inline syntax removed: what a reader sees, for search and anchors.</summary>
    private static string StripInline(string text)
    {
        text = Regex.Replace(text, @"!\[([^\]]*)\]\([^)]*\)", "$1");
        text = LinkRegex.Replace(text, "$1");
        return text.Replace("**", "").Replace("`", "").Replace("*", "").Replace("_", " ");
    }

    private static string PlainText(string markdown)
    {
        var lines = markdown.Split('\n')
            .Select(l => l.Trim().TrimStart('>', '-', '*', ' ').Trim('|').Replace("|", " "))
            .Where(l => l.Length > 0 && !Regex.IsMatch(l, @"^:?-{2,}"));
        return Regex.Replace(StripInline(string.Join(" ", lines)), @"\s+", " ").Trim();
    }

    // ------------------------------------------------------------------ full-text search

    /// <summary>Sections containing every word of <paramref name="query"/>, best first: a hit in
    /// the page title counts most, then in the section heading, then each hit in the text.</summary>
    public static List<WikiSearchHit> Search(string query, int max = 30)
    {
        var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hits = new List<WikiSearchHit>();
        if (terms.Length == 0)
            return hits;
        foreach (var page in Pages)
        {
            foreach (var section in page.Sections)
            {
                var title = page.Title.ToLowerInvariant();
                var heading = section.Heading.ToLowerInvariant();
                var body = section.Text.ToLowerInvariant();
                if (!terms.All(t => title.Contains(t) || heading.Contains(t) || body.Contains(t)))
                    continue;
                var score = terms.Sum(t => (title.Contains(t) ? 10 : 0) + (heading.Contains(t) ? 6 : 0) + Count(body, t));
                if (section.Anchor.Length == 0 && section.Text.Length == 0)
                    continue;
                hits.Add(new WikiSearchHit(page, section, Snippet(section.Text, terms), score));
            }
        }
        return hits.OrderByDescending(h => h.Score).Take(max).ToList();
    }

    private static int Count(string text, string term)
    {
        var count = 0;
        for (var i = text.IndexOf(term, StringComparison.Ordinal); i >= 0; i = text.IndexOf(term, i + term.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    /// <summary>About 160 characters around the first match, with every term highlighted.</summary>
    private static string Snippet(string text, string[] terms)
    {
        var lower = text.ToLowerInvariant();
        var first = terms.Select(t => lower.IndexOf(t, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, first - 60);
        var length = Math.Min(text.Length - start, 170);
        var piece = text.Substring(start, length);
        var escaped = Escape(piece);
        foreach (var term in terms.OrderByDescending(t => t.Length))
            escaped = Regex.Replace(escaped, Regex.Escape(term), m => $"{m.Value}", RegexOptions.IgnoreCase);
        escaped = escaped.Replace("", $"[color={Gold}][b]").Replace("", "[/b][/color]");
        return (start > 0 ? "…" : "") + escaped + (start + length < text.Length ? "…" : "");
    }

    /// <summary>The search results as a clickable BBCode list for the reader.</summary>
    public static string ResultsBbcode(string query, List<WikiSearchHit> hits)
    {
        var sb = new StringBuilder();
        sb.Append($"[font_size=24][color={Accent}][b]Search: {Escape(query)}[/b][/color][/font_size]\n");
        sb.Append($"[color={Muted}]{hits.Count} result{(hits.Count == 1 ? "" : "s")}[/color]\n\n");
        if (hits.Count == 0)
            sb.Append($"[color={Muted}]Nothing matches every word. Try fewer or shorter words.[/color]\n");
        foreach (var hit in hits)
        {
            var where = hit.Section.Heading.Length > 0 ? $"{hit.Page.Title}  ›  {hit.Section.Heading}" : hit.Page.Title;
            sb.Append($"[url={hit.Page.Id}#{hit.Section.Anchor}][font_size=17][color={Link}][b]{Escape(where)}[/b][/color][/font_size][/url]\n");
            sb.Append($"[color={Text}]{hit.SnippetBbcode}[/color]\n\n");
        }
        return sb.ToString();
    }
}
