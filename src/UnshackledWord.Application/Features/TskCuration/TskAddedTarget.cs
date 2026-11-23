using UnshackledWord.Domain.Models.BibleStructure.References;
using HebrewCounting = UnshackledWord.Domain.Models.BibleStructure.References.VerseCounting;

namespace UnshackledWord.Application.Features.TskCuration;

/// <summary>
/// Target of an added link (roadmap 0024.4): a German reference parsed like <c>api/references/parse</c> (German aliases, Elberfelder/Hebrew
/// numbering, no book prefixes), accepted only as one verse or a range within one chapter and normalised to the key form (end null for one verse).
/// </summary>
public static class TskAddedTarget
{
    private static readonly ScriptureParseOptions Options = new(AliasProfile.German, AllowPrefix: false, WholeInput: true, HebrewCounting.Hebrew);

    public const string OneChapterMessage = "The target must be one verse or a range within one chapter, e.g. 'Röm 8,28-30'.";

    /// <summary>Start and end (null for one verse) in Elberfelder ref ids, or an English error message.</summary>
    public sealed record Result(int? Start, int? End, string? Error);

    public static Result Parse(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Fail("reference is empty. Example: 'Röm 8,28-30'.");
        }

        var parsed = ScriptureReferenceParser.Parse(reference, Options);
        if (parsed.Error is { } error)
        {
            return Fail(Message(error, reference.Trim()));
        }

        if (parsed.References is not [{ BookId: { } bookId, BookOnly: false, Spans: [var span] }])
        {
            return Fail(OneChapterMessage);
        }

        // A whole chapter is verse 0..999; a range over two chapters has different chapters.
        if (span.StartVerse < 1 || span.EndVerse == VerseSpan.WholeChapterEndVerse || span.StartChapter != span.EndChapter)
        {
            return Fail(OneChapterMessage);
        }

        var (start, end) = span.ToRefIds(bookId);
        return new Result(start, end > start ? end : null, null);
    }

    private static Result Fail(string message) => new(null, null, message);

    private static string Message(ScriptureParseError error, string reference) => error.Code switch
    {
        ScriptureParseErrorCodes.UnknownBook => $"Unknown book in '{reference}'. Use a German name or abbreviation such as 'Joh', 'Röm', '1Mo'.",
        ScriptureParseErrorCodes.AmbiguousBook => $"The book in '{reference}' is ambiguous.",
        ScriptureParseErrorCodes.MissingChapter => $"'{reference}' has no chapter and verse.",
        ScriptureParseErrorCodes.ChapterOutOfRange or ScriptureParseErrorCodes.VerseOutOfRange => $"Chapter or verse out of range in '{reference}'.",
        ScriptureParseErrorCodes.EndBeforeStart => $"The end of '{reference}' lies before its start.",
        _ => $"Could not parse reference '{reference}'. Example: 'Röm 8,28-30'.",
    };
}
