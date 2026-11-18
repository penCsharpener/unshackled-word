using System.Text;

namespace UnshackledWord.Domain.Models.BibleStructure.References;

/// <summary>
/// Writes a <see cref="ScriptureReference"/> in German citation style, the inverse of the German profile of
/// <see cref="ScriptureReferenceParser"/>: "Offb 2,1–5.7.9–10", "Röm 8,28; 12,1–2", "Röm 8–9", "1Mo 1,1–2,3".
/// Verse spans of one chapter are joined with "." and a new chapter starts after "; ". A whole chapter is written as its number.
/// </summary>
public static class GermanReferenceFormatter
{
    private const char Dash = '–';
    private const string ChapterSeparator = "; ";
    private const string VerseSeparator = ".";

    public static string Format(ScriptureReference reference)
    {
        var book = reference.BookId is int bookId
            ? GermanBookNames.GetAbbreviation(bookId)
            : reference.NonCanonical ?? string.Empty;

        var builder = new StringBuilder(book);
        VerseSpan? previous = null;
        foreach (var span in reference.Spans)
        {
            if (previous is null)
            {
                builder.Append(' ').Append(FormatSpan(span, chapterPrefixed: true));
            }
            else if (IsVerseContinuation(previous.Value, span))
            {
                builder.Append(VerseSeparator).Append(FormatSpan(span, chapterPrefixed: false));
            }
            else
            {
                builder.Append(ChapterSeparator).Append(FormatSpan(span, chapterPrefixed: true));
            }

            previous = span;
        }

        return builder.ToString();
    }

    /// <summary>The next span continues the verse list of the same chapter ("2,1–5.7"), so it needs no chapter number.</summary>
    private static bool IsVerseContinuation(VerseSpan previous, VerseSpan next) =>
        next.StartVerse > 0
        && previous.EndVerse != VerseSpan.WholeChapterEndVerse
        && previous.EndChapter == next.StartChapter;

    private static string FormatSpan(VerseSpan span, bool chapterPrefixed)
    {
        if (span.StartVerse == 0)
        {
            if (span.EndVerse == VerseSpan.WholeChapterEndVerse)
            {
                return span.EndChapter == span.StartChapter
                    ? $"{span.StartChapter}"
                    : $"{span.StartChapter}{Dash}{span.EndChapter}";
            }

            // Chapter start with an explicit end verse, e.g. "8–9,5".
            return $"{span.StartChapter}{Dash}{span.EndChapter},{span.EndVerse}";
        }

        var start = chapterPrefixed ? $"{span.StartChapter},{span.StartVerse}" : $"{span.StartVerse}";
        if (span.EndChapter != span.StartChapter)
        {
            return $"{start}{Dash}{span.EndChapter},{span.EndVerse}";
        }

        return span.EndVerse == span.StartVerse ? start : $"{start}{Dash}{span.EndVerse}";
    }
}
