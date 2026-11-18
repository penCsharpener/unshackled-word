namespace UnshackledWord.Domain.Models.BibleStructure.References;

/// <summary>
/// Verse counts per chapter for English and Hebrew counting. The existing tables are named the wrong way round:
/// <c>BibleBook.VersesPerChapterHeb</c> holds the ENGLISH counts (Mal [14,17,18,6], Ps 3 = 8) and
/// <c>VersesPerChapterLxx</c> the HEBREW ones (Mal [14,17,24,0]). They cannot be renamed (endpoints and the chapter picker use them),
/// so the two arrays are assigned by content: the one with 24 verses in Mal 3 is Hebrew. Trailing 0 entries (Mal 4 in Hebrew,
/// Joel 4 in English) mean the chapter does not exist and are cut off, so <c>Length</c> is the chapter count.
/// </summary>
internal static class VerseTables
{
    private const int MalachiId = 39;
    private const int MalachiHebrewChapter3Verses = 24;

    private static readonly Dictionary<int, int[]> English;
    private static readonly Dictionary<int, int[]> Hebrew;

    static VerseTables()
    {
        var malachi = BibleBook.AllBooks[MalachiId];
        var hebNamedIsHebrew = malachi.VersesPerChapterHeb is [_, _, MalachiHebrewChapter3Verses, ..];
        English = Build(hebNamedIsHebrew ? e => e.VersesPerChapterLxx : e => e.VersesPerChapterHeb);
        Hebrew = Build(hebNamedIsHebrew ? e => e.VersesPerChapterHeb : e => e.VersesPerChapterLxx);
    }

    /// <summary>Verse counts per chapter in English counting (STEP), or null for an unknown book.</summary>
    public static int[]? EnglishCounts(int bookId) => English.GetValueOrDefault(bookId);

    /// <summary>Verse counts per chapter in Hebrew counting (the reader), or null for an unknown book.</summary>
    public static int[]? HebrewCounts(int bookId) => Hebrew.GetValueOrDefault(bookId);

    private static Dictionary<int, int[]> Build(Func<BibleBook, int[]> select) =>
        BibleBook.AllBooks.ToDictionary(e => e.Key, e => Trim(select(e.Value)));

    private static int[] Trim(int[] counts)
    {
        var length = counts.Length;
        while (length > 0 && counts[length - 1] == 0)
        {
            length--;
        }

        return counts[..length];
    }
}
