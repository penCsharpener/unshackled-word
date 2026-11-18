namespace UnshackledWord.Domain.Models.BibleStructure.References;

/// <summary>
/// Deep link into the chapter reader for a reference, e.g. "/bible/66/2?v=1-5,7,9-10#v1".
/// </summary>
public static class ReaderPath
{
    /// <summary>
    /// Path to the first chapter of <paramref name="reference"/>. Lists the verses of that chapter only (all spans
    /// touching it, as "v=" ranges) and hashes the first one. A whole chapter has no query and no hash.
    /// Null for non-canonical books, unknown books or chapters, and references without spans.
    /// </summary>
    public static string? Create(ScriptureReference reference)
    {
        if (reference.BookId is not int bookId || reference.Spans.Count == 0)
        {
            return null;
        }

        // Reader and DB use Hebrew counting (06-api-and-ui.md); BibleBook.VersesPerChapterHeb actually holds English counts.
        if (VerseTables.HebrewCounts(bookId) is not { } verseCounts)
        {
            return null;
        }

        var chapter = reference.Spans[0].StartChapter;
        if (chapter < 1 || chapter > verseCounts.Length)
        {
            return null;
        }

        var lastVerse = verseCounts[chapter - 1];
        var ranges = new List<string>();
        var firstVerse = 0;
        foreach (var span in reference.Spans)
        {
            if (chapter < span.StartChapter || chapter > span.EndChapter)
            {
                continue;
            }

            var startsAtChapterStart = chapter != span.StartChapter || span.StartVerse == 0;
            var endsAtChapterEnd = chapter != span.EndChapter || span.EndVerse == VerseSpan.WholeChapterEndVerse;
            if (startsAtChapterStart && endsAtChapterEnd)
            {
                return $"/bible/{bookId}/{chapter}";
            }

            var from = chapter == span.StartChapter ? Math.Max(span.StartVerse, 1) : 1;
            var to = chapter == span.EndChapter ? Math.Min(span.EndVerse, lastVerse) : lastVerse;
            if (firstVerse == 0)
            {
                firstVerse = from;
            }

            ranges.Add(from == to ? $"{from}" : $"{from}-{to}");
        }

        if (ranges.Count == 0)
        {
            return null;
        }

        return $"/bible/{bookId}/{chapter}?v={string.Join(',', ranges)}#v{firstVerse}";
    }
}
