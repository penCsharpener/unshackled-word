using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.SeedDb.Services.Tsk.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.Tsk.Extensions;

public static class MappingExtensions
{
    public static TskReference ToTskReference(this TskRow row)
    {
        return new TskReference
        {
            Reference = new BibleReference(row.Book, row.Chapter, row.Verse),
            Words = row.Words,
            CrossReferences = new TskCrossReferenceList(row.Refs).ToBibleReferences()
        };
    }

    public static ICollection<IBibleReference> ToBibleReferences(this TskCrossReferenceList tskCrossReferences)
    {
        return tskCrossReferences.CrossReferences.Split(';').Select(x => new TskCrossReference(x)).ToBibleReferences()
            .ToList();
    }

    public static IEnumerable<IBibleReference> ToBibleReferences(this IEnumerable<TskCrossReference> tskCrossReferences)
    {
        return tskCrossReferences.SelectMany(tskCrossReference => tskCrossReference.ToBibleReference());
    }

    public static int CountChars(this string str, char @char)
    {
        return str.Count(c => c == @char);
    }

    public static IEnumerable<IBibleReference> ToBibleReference(this TskCrossReference tskCrossReference)
    {
        var bookSplit = tskCrossReference.CrossReference.Split(' ');
        var book = bookSplit.First();

        var bibleBook = BibleBook.FindByAbbreviation(book);

        if (bibleBook is null)
        {
            yield break;
        }

        var bookId = bibleBook.Value.Id;

        var chapterVerse = bookSplit.Last();
        var chapterVerseSplit = chapterVerse.Split(':');

        var chapterPart = chapterVerseSplit.First();
        var startChapter = chapterPart;
        var endChapter = startChapter;
        if (chapterPart.Contains('-') && chapterPart.Contains(':'))
        {
            var chapterRangeSplit = chapterPart.Split('-');
            startChapter = chapterRangeSplit[0];
            endChapter = chapterRangeSplit[1];
        }

        var startChapterId = int.Parse(startChapter);
        var endChapterId = int.Parse(endChapter);
        var verse = chapterVerseSplit.Last();
        var endVerse = string.Empty;

        foreach (var verseOptions in verse.Split(','))
        {
            if (verseOptions.Contains('-'))
            {
                var spanSplit = verseOptions.Split('-');
                var verseId = int.Parse(spanSplit.First());
                var endVerseId = int.Parse(spanSplit.Last());

                yield return new BibleReferenceRange(new(bookId, startChapterId, verseId), new(bookId, endChapterId, endVerseId));
                continue;
            }

            yield return new BibleReference(bookId, startChapterId, int.Parse(verseOptions));
        }
    }
}

public record TskCrossReference(string CrossReference);
