namespace UnshackledWord.Domain.Models.BibleStructure.References;

/// <summary>One differing row of <c>BibleVerseCountingMapping</c>: English (STEP, LXX) ref id and the Hebrew (reader) ref id.</summary>
public sealed record VerseCountingRow(int LxxRefId, int HebRefId);

/// <summary>Maps English verse counting (STEP references) to Hebrew counting (the reader). Unmapped ids stay as they are.</summary>
public interface IVerseCountingMap
{
    /// <summary>Hebrew ref id for the start of a span: the smallest Hebrew id of the English id.</summary>
    int MapStart(int lxxRefId);

    /// <summary>Hebrew ref id for the end of a span: the largest Hebrew id of the English id.</summary>
    int MapEnd(int lxxRefId);

    /// <summary>Maps a span in English counting to Hebrew counting; whole-chapter spans (verse 0 to 999) use the first and last verse of the English chapter.</summary>
    VerseSpan MapSpan(int bookId, VerseSpan span);
}

/// <summary>Pure map built from the differing rows (about 2,026); see roadmap 0019 section "English to Hebrew verse counting".</summary>
public sealed class VerseCountingMap : IVerseCountingMap
{
    private readonly Dictionary<int, (int Min, int Max)> _byLxx = [];

    public VerseCountingMap(IEnumerable<VerseCountingRow> rows)
    {
        foreach (var row in rows)
        {
            _byLxx[row.LxxRefId] = _byLxx.TryGetValue(row.LxxRefId, out var range)
                ? (Math.Min(range.Min, row.HebRefId), Math.Max(range.Max, row.HebRefId))
                : (row.HebRefId, row.HebRefId);
        }
    }

    public int MapStart(int lxxRefId) => _byLxx.TryGetValue(lxxRefId, out var range) ? range.Min : lxxRefId;

    public int MapEnd(int lxxRefId) => _byLxx.TryGetValue(lxxRefId, out var range) ? range.Max : lxxRefId;

    public VerseSpan MapSpan(int bookId, VerseSpan span)
    {
        var (startRef, endRef) = span.ToRefIds(bookId);
        var wholeChapters = span.EndVerse == VerseSpan.WholeChapterEndVerse;

        var start = MapStart(startRef);
        if (wholeChapters && span.StartVerse == 0)
        {
            // The chapter starts at its first real verse; a Hebrew-only title (English verse 0) may reach further back.
            var firstVerse = MapStart(startRef + 1);
            start = _byLxx.ContainsKey(startRef) ? Math.Min(start, firstVerse) : firstVerse;
        }

        var end = endRef;
        if (wholeChapters)
        {
            var lastVerse = LastEnglishVerse(bookId, span.EndChapter);
            end = lastVerse > 0 ? MapEnd(bookId * 1_000_000 + span.EndChapter * 1_000 + lastVerse) : endRef;
        }
        else
        {
            end = MapEnd(endRef);
        }

        return new VerseSpan(Chapter(start), Verse(start), Chapter(end), Verse(end));
    }

    private static int LastEnglishVerse(int bookId, int chapter)
    {
        var counts = VerseTables.EnglishCounts(bookId);
        return counts is null || chapter < 1 || chapter > counts.Length ? 0 : counts[chapter - 1];
    }

    private static int Chapter(int refId) => refId / 1_000 % 1_000;

    private static int Verse(int refId) => refId % 1_000;
}
