using CsvHelper.Configuration.Attributes;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;

public class GbtWord
{
    [Name("id")]
    public string Id { get; set; } = null!;
    [Name("text")]
    public string Text { get; set; } = null!;
    [Name("verse_id")]
    public string VerseId { get; set; } = null!;
    [Name("form_id")]
    public string FormId { get; set; } = null!;

    public GbtParsedWord ToGbtParsedWord()
    {
        var bookId = int.Parse(Id[..2]);
        var chapterId = int.Parse(Id.Substring(2, 3));
        var verseId = int.Parse(Id.Substring(5, 3));
        var sortOrder = int.Parse(Id.Substring(8, 2));

        return new GbtParsedWord
        {
            BibleReference = new BibleReference(bookId, chapterId, verseId),
            BookId = bookId,
            ChapterId = chapterId,
            VerseId = verseId,
            SortNumber = sortOrder,
            Text = Text
        };
    }
}