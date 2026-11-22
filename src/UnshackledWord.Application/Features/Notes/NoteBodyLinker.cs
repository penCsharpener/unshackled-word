using System.Text;
using System.Text.RegularExpressions;
using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Application.Features.Notes;

/// <summary>
/// Turns every German scripture reference in a note body into a Markdown link <c>[Röm 8,28](/bible/45/8?v=28#v28)</c> (roadmap 0023.3).
/// Fenced code, inline code, existing links and images, autolinks, HTML tags and bare URLs are left untouched; the visible text stays as typed.
/// </summary>
public static partial class NoteBodyLinker
{
    [GeneratedRegex(@"\G!?\[(?:[^\[\]\\\n]|\\.)*\]\((?:[^()\\\n]|\\.|\([^()\n]*\))*\)")]
    private static partial Regex InlineLinkRegex();

    [GeneratedRegex(@"\G\[(?:[^\[\]\\\n]|\\.)*\]\[[^\]\n]*\]")]
    private static partial Regex ReferenceLinkRegex();

    [GeneratedRegex(@"\G<[^<>\n]+>")]
    private static partial Regex AngleRegex();

    [GeneratedRegex(@"\Ghttps?://[^\s<>)\]]+", RegexOptions.IgnoreCase)]
    private static partial Regex BareUrlRegex();

    [GeneratedRegex(@"\G {0,3}\[[^\]\n]+\]:[^\n]*")]
    private static partial Regex DefinitionRegex();

    [GeneratedRegex(@"\G {0,3}(`{3,}|~{3,})")]
    private static partial Regex FenceRegex();

    public static string Link(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return markdown ?? string.Empty;
        }

        var output = new StringBuilder(markdown.Length + 64);
        var prose = new StringBuilder();
        var i = 0;
        var atLineStart = true;

        void Flush()
        {
            if (prose.Length > 0)
            {
                output.Append(LinkProse(prose.ToString()));
                prose.Clear();
            }
        }

        void Protect(int length)
        {
            Flush();
            output.Append(markdown, i, length);
            i += length;
            atLineStart = false;
        }

        while (i < markdown.Length)
        {
            if (atLineStart)
            {
                if (FenceRegex().Match(markdown, i) is { Success: true } fence)
                {
                    Protect(FencedLength(markdown, i, fence.Groups[1].Value));
                    atLineStart = true;
                    continue;
                }

                if (DefinitionRegex().Match(markdown, i) is { Success: true } definition)
                {
                    Protect(definition.Length);
                    continue;
                }
            }

            var c = markdown[i];
            if (c == '`')
            {
                var run = 1;
                while (i + run < markdown.Length && markdown[i + run] == '`')
                {
                    run++;
                }

                var close = FindBacktickRun(markdown, i + run, run);
                if (close >= 0)
                {
                    Protect(close + run - i);
                }
                else
                {
                    prose.Append(markdown, i, run);
                    i += run;
                    atLineStart = false;
                }

                continue;
            }

            var protectedLength = c switch
            {
                '[' or '!' => MatchLength(InlineLinkRegex(), markdown, i) is var inline and > 0 ? inline : c == '[' ? MatchLength(ReferenceLinkRegex(), markdown, i) : 0,
                '<' => MatchLength(AngleRegex(), markdown, i),
                'h' or 'H' => MatchLength(BareUrlRegex(), markdown, i),
                _ => 0,
            };
            if (protectedLength > 0 && (c != 'h' && c != 'H' || i == 0 || !char.IsLetterOrDigit(markdown[i - 1])))
            {
                Protect(protectedLength);
                continue;
            }

            prose.Append(c);
            i++;
            atLineStart = c == '\n';
        }

        Flush();
        return output.ToString();
    }

    private static int MatchLength(Regex regex, string text, int index) => regex.Match(text, index) is { Success: true } m ? m.Length : 0;

    // A fenced block runs to the closing fence of the same character (at least as long) or to the end of the text.
    private static int FencedLength(string text, int start, string fence)
    {
        var lineEnd = text.IndexOf('\n', start);
        while (lineEnd >= 0)
        {
            var next = lineEnd + 1;
            var end = text.IndexOf('\n', next);
            var line = end < 0 ? text[next..] : text[next..end];
            var trimmed = line.TrimStart(' ');
            if (line.Length - trimmed.Length <= 3 && trimmed.StartsWith(new string(fence[0], fence.Length), StringComparison.Ordinal)
                && trimmed.TrimEnd().All(ch => ch == fence[0]))
            {
                return (end < 0 ? text.Length : end + 1) - start;
            }

            lineEnd = end;
        }

        return text.Length - start;
    }

    private static int FindBacktickRun(string text, int from, int run)
    {
        var i = from;
        while (i < text.Length)
        {
            if (text[i] != '`')
            {
                i++;
                continue;
            }

            var length = 0;
            while (i + length < text.Length && text[i + length] == '`')
            {
                length++;
            }

            if (length == run)
            {
                return i;
            }

            i += length;
        }

        return -1;
    }

    private static string LinkProse(string text)
    {
        var references = ScriptureScanner.Scan(text, ScriptureScanner.GermanTextOptions);
        if (references.Count == 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + references.Count * 32);
        var last = 0;
        foreach (var reference in references)
        {
            sb.Append(text, last, reference.Start - last);
            AppendLinked(sb, text, reference);
            last = reference.Start + reference.Length;
        }

        sb.Append(text, last, text.Length - last);
        return sb.ToString();
    }

    // One link per ";" group. Later groups are read together with the book name so their path equals the one of api/references/parse.
    private static void AppendLinked(StringBuilder sb, string text, ScriptureReference reference)
    {
        var whole = text.Substring(reference.Start, reference.Length);
        var bookText = text.Substring(reference.Start, reference.BookLength);
        var pieces = whole.Split(';');
        for (var n = 0; n < pieces.Length; n++)
        {
            if (n > 0)
            {
                sb.Append(';');
            }

            var piece = pieces[n];
            var trimmed = piece.Trim();
            var path = trimmed.Length == 0 ? null : PathOf(n == 0 ? trimmed : $"{bookText} {trimmed}");
            if (path is null)
            {
                sb.Append(piece);
                continue;
            }

            var leading = piece.Length - piece.TrimStart().Length;
            sb.Append(piece, 0, leading).Append('[').Append(Escape(trimmed)).Append("](").Append(path).Append(')')
                .Append(piece, leading + trimmed.Length, piece.Length - leading - trimmed.Length);
        }
    }

    private static string? PathOf(string source)
    {
        var parsed = ScriptureReferenceParser.ParseAt(source, 0, ScriptureScanner.GermanTextOptions);
        return parsed is null ? null : ReaderPath.Create(parsed);
    }

    private static string Escape(string text) => text.Replace("[", "\\[").Replace("]", "\\]");
}
