using System.Text.RegularExpressions;

namespace UnshackledWord.Domain.Models.BibleStructure;

/// <summary>An inclusive range of Elberfelder (Hebrew counting) reference ids.</summary>
public sealed record PassageRange(int BookId, int StartRefId, int EndRefId);

public sealed record ParseResult(PassageRange? Range, string? Error)
{
    public bool IsSuccess => Range is not null;
}

/// <summary>
/// Parses references like "Joh 3,16-18", "John 3:16", "Röm 8", "Röm 8-9", "1Mo 1,1-2,3".
/// Never throws; invalid input yields an error message.
/// </summary>
public static partial class BibleReferenceParser
{
    // book, chapter, [verse], [-, [endChapter ,] endVerse | endChapter]
    [GeneratedRegex(@"^\s*(?<book>[1-3]?\s*\.?\s*[^\d\s.,:;-][^\d,:;-]*?)\s*\.?\s*(?<c1>\d+)(?:\s*[,:]\s*(?<v1>\d+))?(?:\s*[-–]\s*(?:(?<c2>\d+)\s*[,:]\s*)?(?<n2>\d+))?\s*$")]
    private static partial Regex ReferenceRegex();

    public static ParseResult Parse(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Fail("Reference is empty. Example: 'Joh 3,16-18'.");
        }

        var match = ReferenceRegex().Match(reference);
        if (!match.Success)
        {
            return Fail($"Could not parse reference '{reference}'. Examples: 'Joh 3,16-18', 'Röm 8', '1Mo 1,1-2,3'.");
        }

        var bookText = match.Groups["book"].Value;
        var bookId = GermanBookNames.FindBookId(bookText) ?? BibleBook.FindByAbbreviation(bookText.Replace(" ", ""))?.Id
                     ?? BibleBook.FindByAbbreviation(bookText.Trim())?.Id;
        if (bookId is null)
        {
            return Fail($"Unknown book '{bookText.Trim()}'. Use a German name or abbreviation such as 'Joh', 'Röm', '1Mo'.");
        }

        var c1 = int.Parse(match.Groups["c1"].Value);
        var hasV1 = match.Groups["v1"].Success;
        var hasEnd = match.Groups["n2"].Success;
        var hasC2 = match.Groups["c2"].Success;
        var n2 = hasEnd ? int.Parse(match.Groups["n2"].Value) : 0;

        int startChapter = c1, endChapter = c1, startVerse, endVerse;
        if (!hasV1)
        {
            // whole chapter(s): "Röm 8" or "Röm 8-9"
            startVerse = 0;
            endVerse = 999;
            if (hasEnd)
            {
                if (hasC2)
                {
                    return Fail($"Could not parse reference '{reference}'.");
                }

                endChapter = n2;
            }
        }
        else
        {
            startVerse = int.Parse(match.Groups["v1"].Value);
            endVerse = startVerse;
            if (hasEnd)
            {
                if (hasC2)
                {
                    endChapter = int.Parse(match.Groups["c2"].Value);
                }

                endVerse = n2;
            }
        }

        if (startChapter < 1 || endChapter < 1 || (hasV1 && (startVerse < 1 || endVerse < 1)))
        {
            return Fail("Chapter and verse numbers start at 1.");
        }

        if (endChapter < startChapter || (endChapter == startChapter && endVerse < startVerse))
        {
            return Fail($"The end of '{reference}' lies before its start.");
        }

        var start = bookId.Value * 1_000_000 + startChapter * 1_000 + startVerse;
        var end = bookId.Value * 1_000_000 + endChapter * 1_000 + endVerse;
        return new ParseResult(new PassageRange(bookId.Value, start, end), null);
    }

    private static ParseResult Fail(string message) => new(null, message);
}
