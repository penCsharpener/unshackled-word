using System.Net;
using System.Text.RegularExpressions;
using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Domain.Models.Study;

/// <summary>A reference found in lexicon text, already in Hebrew (reader) verse counting.</summary>
public sealed record LexiconRef(int BookId, IReadOnlyList<VerseSpan> Spans);

/// <summary>A piece of lexicon text. <see cref="Ref"/> is null for plain text; otherwise <see cref="Text"/> is the link label.</summary>
public sealed record LexiconSegment(string Text, LexiconRef? Ref);

/// <summary>
/// Splits cleaned STEP lexicon text into plain and reference segments (roadmap 0019). Tagged <c>&lt;ref='Mat.5.29'&gt;</c> tags are read from the
/// OSIS attribute (fallback: the visible label), untagged notes (<c>1Ch.8.39</c>) are found by <see cref="ScriptureScanner"/>.
/// The concatenated segment texts equal <see cref="LexiconTextCleaner.Clean"/> as long as no reference is cut off by the length limit.
/// </summary>
public static partial class LexiconTextSegmenter
{
    private const char MarkStart = '';
    private const char MarkEnd = '';

    [GeneratedRegex(@"<ref='(?<osis>[^']*)'>(?<text>.*?)</ref>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RefTagRegex();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@" +([,.;:!?)])")]
    private static partial Regex SpaceBeforePunctuationRegex();

    [GeneratedRegex("(\\d+)")]
    private static partial Regex MarkRegex();

    private sealed record TaggedRef(string Label, string Suffix, LexiconRef? Ref);

    public static (IReadOnlyList<LexiconSegment> Segments, bool Truncated) Segment(string? raw, int maxLength, IVerseCountingMap map)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ([], false);
        }

        // 1. <ref> tags become numbered marks; all other tags become spaces exactly like LexiconTextCleaner does.
        var tagged = new List<TaggedRef>();
        var marked = RefTagRegex().Replace(raw, m =>
        {
            var item = ReadTaggedRef(m.Groups["osis"].Value, m.Groups["text"].Value, map);
            tagged.Add(item);
            return $" {MarkStart}{tagged.Count - 1}{MarkEnd}{item.Suffix} ";
        });

        var text = TagRegex().Replace(marked, " ");
        text = WebUtility.HtmlDecode(text);
        text = WhitespaceRegex().Replace(text, " ").Trim();
        text = SpaceBeforePunctuationRegex().Replace(text, "$1");

        // 2. Split at the marks, scan the plain pieces for untagged references.
        var parts = new List<LexiconSegment>();
        var last = 0;
        foreach (Match mark in MarkRegex().Matches(text))
        {
            AddScanned(parts, text[last..mark.Index], map);
            var item = tagged[int.Parse(mark.Groups[1].Value)];
            parts.Add(new LexiconSegment(item.Label, item.Ref));
            last = mark.Index + mark.Length;
        }

        AddScanned(parts, text[last..], map);

        // 3. Truncate with the cut position LexiconTextCleaner would use; never cut inside a reference.
        var total = parts.Sum(p => p.Text.Length);
        if (total <= maxLength)
        {
            return (Merge(parts), false);
        }

        var flat = string.Concat(parts.Select(p => p.Text));
        var cut = flat.LastIndexOf(' ', maxLength - 1);
        var limit = cut > 0 ? cut : maxLength;

        var kept = new List<LexiconSegment>();
        var offset = 0;
        foreach (var part in parts)
        {
            var end = offset + part.Text.Length;
            if (end <= limit)
            {
                kept.Add(part);
            }
            else if (part.Ref is null)
            {
                var piece = part.Text[..(limit - offset)];
                if (piece.Length > 0)
                {
                    kept.Add(new LexiconSegment(piece, null));
                }

                break;
            }
            else
            {
                break; // the reference crosses the limit: dropped together with everything after it
            }

            offset = end;
        }

        var merged = Merge(kept);
        return (AppendEllipsis(merged), true);
    }

    private static List<LexiconSegment> AppendEllipsis(List<LexiconSegment> segments)
    {
        if (segments.Count > 0 && segments[^1].Ref is null)
        {
            var trimmed = segments[^1].Text.TrimEnd(',', ';', ':', ' ');
            segments[^1] = new LexiconSegment(trimmed + "…", null);
        }
        else
        {
            segments.Add(new LexiconSegment("…", null));
        }

        return segments;
    }

    private static void AddScanned(List<LexiconSegment> parts, string piece, IVerseCountingMap map)
    {
        if (piece.Length == 0)
        {
            return;
        }

        var last = 0;
        foreach (var reference in ScriptureScanner.Scan(piece))
        {
            var label = piece.Substring(reference.Start, reference.Length);
            var linked = ToLexiconRef(reference, map);
            if (linked is null)
            {
                continue; // stays plain text, merged into the surrounding text
            }

            parts.Add(new LexiconSegment(piece[last..reference.Start], null));
            parts.Add(new LexiconSegment(label, linked));
            last = reference.Start + reference.Length;
        }

        parts.Add(new LexiconSegment(piece[last..], null));
    }

    private static List<LexiconSegment> Merge(List<LexiconSegment> parts)
    {
        var result = new List<LexiconSegment>();
        foreach (var part in parts)
        {
            if (part.Text.Length == 0)
            {
                continue;
            }

            if (part.Ref is null && result.Count > 0 && result[^1].Ref is null)
            {
                result[^1] = new LexiconSegment(result[^1].Text + part.Text, null);
            }
            else
            {
                result.Add(part);
            }
        }

        return result;
    }

    // ---- OSIS attribute ----

    private static TaggedRef ReadTaggedRef(string osis, string rawLabel, IVerseCountingMap map)
    {
        var label = TagRegex().Replace(rawLabel, " ");
        label = WebUtility.HtmlDecode(label);
        label = WhitespaceRegex().Replace(label, " ").Trim();
        label = SpaceBeforePunctuationRegex().Replace(label, "$1");

        // Trailing list punctuation belongs to the following text, not to the link label.
        var body = label.TrimEnd(';', ',', '.', ' ');
        var suffix = label[body.Length..].Replace(" ", string.Empty);

        var linked = ParseOsis(osis, map) ?? ParseOsis(body, map);
        return linked is null ? new TaggedRef(label, string.Empty, null) : new TaggedRef(body, suffix, linked);
    }

    /// <summary>Parses an OSIS attribute (<c>Heb.5.4; 7.11; 9.4</c>) or its visible text. Null for non-canonical books, several books or invalid spans.</summary>
    internal static LexiconRef? ParseOsis(string? value, IVerseCountingMap map)
    {
        var trimmed = (value ?? string.Empty).Trim().TrimEnd('.', ';', ',', ' ');
        if (trimmed.Length == 0)
        {
            return null;
        }

        var options = ScriptureScanner.OsisOptions with { WholeInput = true };
        var result = ScriptureReferenceParser.Parse(trimmed, options);
        if (!result.IsSuccess || result.References.Count != 1)
        {
            return null;
        }

        return ToLexiconRef(result.References[0], map);
    }

    /// <summary>English spans to Hebrew counting; null when the book is not canonical, no chapter was given or a mapped span is invalid.</summary>
    private static LexiconRef? ToLexiconRef(ScriptureReference reference, IVerseCountingMap map)
    {
        if (reference.BookId is not { } bookId || reference.BookOnly || reference.Spans.Count == 0)
        {
            return null;
        }

        var counts = VerseTables.HebrewCounts(bookId);
        if (counts is null)
        {
            return null;
        }

        var mapped = new List<VerseSpan>(reference.Spans.Count);
        foreach (var span in reference.Spans)
        {
            var m = map.MapSpan(bookId, span);
            if (!IsValid(m, counts))
            {
                return null;
            }

            mapped.Add(m);
        }

        return new LexiconRef(bookId, mapped);
    }

    private static bool IsValid(VerseSpan span, int[] counts)
    {
        if (span.StartChapter < 1 || span.EndChapter < span.StartChapter || span.EndChapter > counts.Length)
        {
            return false;
        }

        if (span.StartVerse < 0 || span.StartVerse > counts[span.StartChapter - 1])
        {
            return false;
        }

        var wholeEnd = span.EndVerse == VerseSpan.WholeChapterEndVerse;
        if (!wholeEnd && (span.EndVerse < 1 || span.EndVerse > counts[span.EndChapter - 1]))
        {
            return false;
        }

        return span.EndChapter > span.StartChapter || wholeEnd || span.EndVerse >= span.StartVerse;
    }
}
