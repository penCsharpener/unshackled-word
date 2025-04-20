using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services;

public class ParseHelper
{
    public BibleBook ParseNtVerseId(string verseString)
    {
        if (int.TryParse(verseString, out var verseId) is false)
        {
            throw new ArgumentException("Verse Id is not a number.", nameof(verseString));
        }

        if (verseId < 40001001)
        {
            throw new ArgumentOutOfRangeException(nameof(verseId), "Verse Id is too low for New Testament.");
        }

        if (verseId > 66022021)
        {
            throw new ArgumentOutOfRangeException(nameof(verseId), "Verse Id is too high for New Testament.");
        }

        var bookId = int.Parse(verseString[0..2]);
        var chapterId = int.Parse(verseString[2..5]);
        var verseIdInChapter = int.Parse(verseString[5..8]);

        return BibleBook.NewTestamentBooks.TryGetValue(bookId, out var book)
            ? book
            : throw new ArgumentException(nameof(bookId), "Book Id is not valid.");
    }
}
