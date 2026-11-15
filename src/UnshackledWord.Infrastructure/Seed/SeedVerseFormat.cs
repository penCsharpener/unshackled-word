using System.Globalization;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Infrastructure.Seed;

/// <summary>Verse references of the seed reports as readable text.</summary>
public static class SeedVerseFormat
{
    private static readonly Dictionary<int, BibleBook> Books = BibleBook.AllBooks;

    /// <summary>"Genesis 1:1" for a <c>BBCCCVVV</c> reference, with " · 200" when a level is given.</summary>
    public static string Verse(int hebRefId, int? level = null)
    {
        var book = hebRefId / 1_000_000;
        var chapter = hebRefId / 1_000 % 1_000;
        var verse = hebRefId % 1_000;
        var name = Books.TryGetValue(book, out var info) ? info.Name : book.ToString(CultureInfo.InvariantCulture);

        return level is null ? $"{name} {chapter}:{verse}" : $"{name} {chapter}:{verse} · {level}";
    }
}
