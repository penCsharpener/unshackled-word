using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Domain.Models.BibleStructure;

/// <summary>An inclusive range of Elberfelder (Hebrew counting) reference ids.</summary>
public sealed record PassageRange(int BookId, int StartRefId, int EndRefId);

public sealed record ParseResult(PassageRange? Range, string? Error)
{
    public bool IsSuccess => Range is not null;
}

/// <summary>
/// Parses references like "Joh 3,16-18", "John 3:16", "Röm 8", "Röm 8-9", "1Mo 1,1-2,3" for the MCP tools.
/// A thin wrapper over <see cref="ScriptureReferenceParser"/> with profile <see cref="AliasProfile.Legacy"/>:
/// exactly one span in one book, Elberfelder counting. Never throws; invalid input yields an error message.
/// </summary>
public static class BibleReferenceParser
{
    // English counting skips the verse-table check, as the old parser did: numbers are taken as given and
    // unknown chapters or verses reach the database lookup ("No verses found").
    private static readonly ScriptureParseOptions Options = new(AliasProfile.Legacy, AllowPrefix: false, WholeInput: true, Counting: VerseCounting.English);

    public static ParseResult Parse(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Fail("Reference is empty. Example: 'Joh 3,16-18'.");
        }

        var parsed = ScriptureReferenceParser.Parse(reference, Options);
        if (parsed.Error is not null)
        {
            return MapError(parsed.Error, reference);
        }

        if (parsed.References.Count != 1)
        {
            return CouldNotParse(reference);
        }

        var single = parsed.References[0];
        if (single.BookOnly || single.Spans.Count != 1)
        {
            return CouldNotParse(reference);
        }

        if (single.BookId is not { } bookId)
        {
            return UnknownBook(single.NonCanonical ?? reference);
        }

        var span = single.Spans[0];
        var isWholeChapter = span.StartVerse == 0 && span.EndVerse == VerseSpan.WholeChapterEndVerse;
        if (!isWholeChapter && (span.StartVerse < 1 || span.EndVerse < 1))
        {
            return Fail("Chapter and verse numbers start at 1.");
        }

        var (start, end) = span.ToRefIds(bookId);
        return new ParseResult(new PassageRange(bookId, start, end), null);
    }

    private static ParseResult MapError(ScriptureParseError error, string reference) => error.Code switch
    {
        ScriptureParseErrorCodes.Empty => Fail("Reference is empty. Example: 'Joh 3,16-18'."),
        ScriptureParseErrorCodes.UnknownBook => UnknownBook(error.Args.GetValueOrDefault("text", reference).Trim()),
        ScriptureParseErrorCodes.EndBeforeStart => Fail($"The end of '{reference}' lies before its start."),
        ScriptureParseErrorCodes.ChapterOutOfRange or ScriptureParseErrorCodes.VerseOutOfRange => OutOfRange(error, reference),
        _ => CouldNotParse(reference),
    };

    private static ParseResult OutOfRange(ScriptureParseError error, string reference) =>
        error.Args.GetValueOrDefault("value") == "0"
            ? Fail("Chapter and verse numbers start at 1.")
            : Fail($"Chapter or verse out of range in '{reference}'.");

    private static ParseResult UnknownBook(string bookText) =>
        Fail($"Unknown book '{bookText}'. Use a German name or abbreviation such as 'Joh', 'Röm', '1Mo'.");

    private static ParseResult CouldNotParse(string reference) =>
        Fail($"Could not parse reference '{reference}'. Examples: 'Joh 3,16-18', 'Röm 8', '1Mo 1,1-2,3'.");

    private static ParseResult Fail(string message) => new(null, message);
}
