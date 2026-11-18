using UnshackledWord.Domain.Models.BibleStructure.References;
using UnshackledWord.Domain.WebApi.MetaBible.ParseReference;
using GermanFormatter = UnshackledWord.Domain.Models.BibleStructure.References.GermanReferenceFormatter;

namespace UnshackledWord.Application.Features.ScriptureReferences;

/// <summary>Parser + German formatter + reader path in one call, shared by the parse endpoint and later MCP.</summary>
public sealed class ScriptureReferenceService : IScriptureReferenceService
{
    private static readonly ScriptureParseOptions SearchOptions =
        new(AliasProfile.German, AllowPrefix: true, WholeInput: true, Domain.Models.BibleStructure.References.VerseCounting.Hebrew);

    public ParseReferenceResponse Parse(string? query)
    {
        var result = ScriptureReferenceParser.Parse(query, SearchOptions);
        var response = new ParseReferenceResponse
        {
            References = result.References.Select(ToContract).ToList(),
            BookCandidates = result.BookCandidates.ToList(),
        };

        // Text that is not a reference at all (a search word) is a normal answer, not an error.
        var isPlainText = result.Error is { Code: ScriptureParseErrorCodes.UnknownBook or ScriptureParseErrorCodes.Empty }
            && result.References.Count == 0;
        if (result.Error is { } error && !isPlainText)
        {
            response.Error = new ParseReferenceError
            {
                Code = error.Code,
                Position = error.Position,
                Args = error.Args.ToDictionary(e => e.Key, e => e.Value),
            };
        }

        return response;
    }

    private static ParsedReference ToContract(ScriptureReference reference)
    {
        var groups = SplitByChapter(reference)
            .Select(g => new ParsedReferenceGroup { Display = GermanFormatter.Format(g), Path = ReaderPath.Create(g) })
            .ToList();

        return new ParsedReference
        {
            BookId = reference.BookId,
            BookOnly = reference.BookOnly,
            Display = GermanFormatter.Format(reference),
            Spans = reference.Spans
                .Select(s => new ParsedSpan { StartChapter = s.StartChapter, StartVerse = s.StartVerse, EndChapter = s.EndChapter, EndVerse = s.EndVerse })
                .ToList(),
            Path = groups.FirstOrDefault()?.Path,
            Groups = groups,
        };
    }

    /// <summary>Consecutive spans that start in the same chapter form one group; each group links to its own chapter.</summary>
    private static IEnumerable<ScriptureReference> SplitByChapter(ScriptureReference reference)
    {
        if (reference.Spans.Count == 0)
        {
            yield break;
        }

        var current = new List<VerseSpan>();
        foreach (var span in reference.Spans)
        {
            if (current.Count > 0 && current[0].StartChapter != span.StartChapter)
            {
                yield return reference with { Spans = current };
                current = [];
            }

            current.Add(span);
        }

        yield return reference with { Spans = current };
    }
}
