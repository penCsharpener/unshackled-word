using System.Globalization;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetPassageText;

/// <summary>A closed range of ref ids (bookId * 1_000_000 + chapter * 1000 + verse) within one book, start before or equal to end.</summary>
public readonly record struct PassageSpan(int StartRefId, int EndRefId);

/// <summary>Parses the <c>spans</c> query value of <c>GET api/passageText</c>: "66002001-66002005,66002007".</summary>
public static class PassageSpanParser
{
    private static readonly Dictionary<int, BibleBook> Books = BibleBook.AllBooks;

    public const int MaxSpans = 10;
    public const int MaxVerses = 40;

    public static bool IsValid(string? spans) => TryParse(spans, out _);

    /// <summary>Every item must be a valid verse id or a valid id range of one book in Hebrew counting; anything else fails the whole value.</summary>
    public static bool TryParse(string? spans, out List<PassageSpan> result)
    {
        result = [];
        if (string.IsNullOrWhiteSpace(spans))
        {
            return false;
        }

        foreach (var item in spans.Split(','))
        {
            var parts = item.Trim().Split('-');
            if (parts.Length is not (1 or 2)
                || !TryParseRefId(parts[0], out var start)
                || !TryParseRefId(parts[^1], out var end)
                || !IsVerse(start)
                || !IsVerse(end)
                || start / 1_000_000 != end / 1_000_000
                || end < start)
            {
                result = [];
                return false;
            }

            result.Add(new PassageSpan(start, end));
        }

        return true;
    }

    /// <summary>Book, chapter and verse of a ref id (no range checks, see <see cref="IsVerse"/>).</summary>
    public static (int BookId, int Chapter, int Verse) Decompose(int refId) =>
        (refId / 1_000_000, refId / 1000 % 1000, refId % 1000);

    public static int ToRefId(int bookId, int chapter, int verse) => bookId * 1_000_000 + chapter * 1000 + verse;

    private static bool TryParseRefId(string text, out int refId) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out refId);

    private static bool IsVerse(int refId)
    {
        var (bookId, chapter, verse) = Decompose(refId);
        if (!Books.TryGetValue(bookId, out var book) || chapter < 1 || chapter > book.VersesPerChapterHeb.Length)
        {
            return false;
        }

        return verse >= 1 && verse <= book.VersesPerChapterHeb[chapter - 1];
    }
}
