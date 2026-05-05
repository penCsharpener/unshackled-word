using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class ElbExportLineItem
{
    public BibleReference HebRefId { get; }
    public BibleReference LxxRefId { get; }
    public string Verse { get; }
    public List<Elb1871Word> Words { get; }

    public ElbExportLineItem(string line)
    {
        var refText = line.Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hebRef = ParseBibleReference(refText[0]);
        var lxxRef = ParseBibleReference(refText[1]);
        var verse = refText[2];

        HebRefId = hebRef;
        LxxRefId = lxxRef;
        Verse = verse;
        Words = SplitAndSaveIndividualWords(verse).ToList();
    }

    public ElbExportLineItem(int hebRefId, string verse)
    {
        HebRefId = BibleReference.FromRefId(hebRefId);
        LxxRefId = HebRefId;
        Verse = verse;
        Words = SplitAndSaveIndividualWords(verse).ToList();
    }

    private BibleReference ParseBibleReference(string stringReference)
    {
        var bookRef = stringReference.Split("$");
        var chapterVerse = bookRef[1].Split(":");

        var book = bookRef[0];
        var chapter = int.Parse(chapterVerse[0]);
        var verse = int.Parse(chapterVerse[1]);
        var bookId = BibleBook.AllBooks.First(x => x.Value.Name == book).Key;
        return new BibleReference(bookId, chapter, verse);
    }

    private static IEnumerable<Elb1871Word> SplitAndSaveIndividualWords(string verseText)
    {
        var words = verseText.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        var orderCounter = 1;

        foreach (var word in words)
        {
            var cleanedWord = word.RemovePunctuation();

            yield return new Elb1871Word(new BibleReference(), orderCounter, word, cleanedWord);
            orderCounter++;
        }
    }
}
