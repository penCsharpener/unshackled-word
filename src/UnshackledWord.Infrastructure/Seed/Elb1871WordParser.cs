using UnshackledWord.Domain.Models.Dbo;

namespace UnshackledWord.Infrastructure.Seed;

/// <summary>
/// Turns the lines of <c>assets/elberfelder1871-theword-export.txt</c> into <c>Elb1871Words</c> rows (ids from 1, in file order).
/// Shared by the importer (the Elberfelder 1871 importer in SeedDb) and by the seed tools that need the freshly parsed words without a
/// database (<c>anchor-check</c>, <c>export-seed</c>), so all of them see exactly the words a fresh install gets.
/// </summary>
public static class Elb1871WordParser
{
    public static List<Elb1871WordDbo> Parse(string[] lines)
    {
        var totalWords = new List<Elb1871WordDbo>();
        var id = 1;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineItem = new ElbExportLineItem(lines[i]);
            var wordDbos = new List<Elb1871WordDbo>(120);

            if (i < lines.Length - 1)
            {
                var nextLineItem = new ElbExportLineItem(lines[i + 1]);

                wordDbos = lineItem.Words.Select(x =>
                {
                    var word = new Elb1871WordDbo
                    {
                        Id = id,
                        BibleBookId = lineItem.HebRefId.BookId,
                        Chapter = lineItem.HebRefId.Chapter,
                        Verse = lineItem.HebRefId.Verse,
                        HebRefId = lineItem.HebRefId.RefId,
                        WordInContext = x.InContext,
                        PlainWord = x.PlainWord,
                        PositionInVerse = x.Order
                    };
                    id++;

                    return word;
                }).ToList();

                if (lineItem.HebRefId.RefId == nextLineItem.HebRefId.RefId)
                {
                    var nextWordDtos = nextLineItem.Words.Select(x =>
                    {
                        var word = new Elb1871WordDbo
                        {
                            Id = id,
                            BibleBookId = nextLineItem.HebRefId.BookId,
                            Chapter = nextLineItem.HebRefId.Chapter,
                            Verse = nextLineItem.HebRefId.Verse,
                            HebRefId = nextLineItem.HebRefId.RefId,
                            WordInContext = x.InContext,
                            PlainWord = x.PlainWord,
                            PositionInVerse = x.Order + lineItem.Words.Count
                        };
                        id++;

                        return word;
                    }).ToList();
                    wordDbos.AddRange(nextWordDtos);
                    i++;
                }
            }

            totalWords.AddRange(wordDbos);
        }

        return totalWords;
    }
}
