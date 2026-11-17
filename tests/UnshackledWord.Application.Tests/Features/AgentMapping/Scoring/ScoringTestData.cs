using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Scoring;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Tests.Features.AgentMapping.Scoring;

/// <summary>Hand-made verse Mk 1:17 (book 41): four German words, three source words, one added word.</summary>
internal static class ScoringTestData
{
    public const int Book = 41;
    public const int OriginalRef = 41001017;

    public static AgentRangeData Range(Action<AgentRangeData>? _ = null, string lemma2 = "Jesus", string role2 = "head") => new()
    {
        GermanWords =
        [
            new AgentGermanWordRow { Id = 101, Chapter = 1, Verse = 17, HebRefId = OriginalRef, PositionInVerse = 1, Text = "Und", Plain = "Und", Lemma = "und", Stts = "KON" },
            new AgentGermanWordRow { Id = 102, Chapter = 1, Verse = 17, HebRefId = OriginalRef, PositionInVerse = 2, Text = "Jesus", Plain = "Jesus", Lemma = lemma2, Stts = "NE" },
            new AgentGermanWordRow { Id = 103, Chapter = 1, Verse = 17, HebRefId = OriginalRef, PositionInVerse = 3, Text = "sprach", Plain = "sprach", Lemma = "sprechen", Stts = "VVFIN" },
            new AgentGermanWordRow { Id = 104, Chapter = 1, Verse = 17, HebRefId = OriginalRef, PositionInVerse = 4, Text = "er", Plain = "er", Lemma = "er", Stts = "PPER" },
            new AgentGermanWordRow { Id = 105, Chapter = 1, Verse = 17, HebRefId = OriginalRef, PositionInVerse = 5, Text = ".", Plain = string.Empty },
        ],
        SourceWords =
        [
            new AgentSourceWordRow { Id = 201, RefId = OriginalRef, Position = 1, Text = "καὶ", Strongs = "G2532" },
            new AgentSourceWordRow { Id = 202, RefId = OriginalRef, Position = 2, Text = "Ἰησοῦς", Strongs = "G2424" },
            new AgentSourceWordRow { Id = 203, RefId = OriginalRef, Position = 3, Text = "εἶπεν", Strongs = "G3004" },
        ],
        Mappings =
        [
            new AgentMappingRow { ElbWordId = 101, StepWordId = 201 },
            new AgentMappingRow { ElbWordId = 102, StepWordId = 202, Role = role2 },
            new AgentMappingRow { ElbWordId = 103, StepWordId = 203 },
            new AgentMappingRow { ElbWordId = 104, IsAddedWord = true, ParentElbWordId = 103 },
        ],
    };

    public static ReferenceVerse Reference() => AgentVerseSnapshots.Build(Book, 1, 17, Range()).State;

    public static ScoredVerseInput Input(ReferenceVerse expected, ReferenceVerse? actual = null, bool proposal = true, bool inPackage = true, params string[] flags) => new()
    {
        Expected = expected,
        Actual = actual ?? AgentProposalState.Clone(expected),
        HasProposal = proposal,
        InPackage = inPackage,
        FlagCodes = flags,
    };

    public static AgentScoreResponse Score(params ScoredVerseInput[] verses) => AgentScorer.Score(new AgentScoreInput
    {
        RunId = 7,
        Mode = AgentModes.Lemma,
        ReferenceVersion = "v1",
        ReferenceVerses = verses.Length,
        Verses = verses,
    });

    /// <summary>A verse with <paramref name="words"/> words with lemmas, one link each, the first <paramref name="wrong"/> lemmas of the actual state wrong.</summary>
    public static ScoredVerseInput Words(int words, int wrong)
    {
        var expected = new ReferenceVerse { Book = Book, Chapter = 2, Verse = 1, Language = "greek" };
        for (var i = 1; i <= words; i++)
        {
            expected.Words.Add(new ReferenceWord { Pos = i, Text = $"w{i}", Lemma = $"l{i}", Stts = "NN" });
            expected.Links.Add(new ReferenceLink { G = i, SRef = 41002001, S = i });
        }

        var actual = AgentProposalState.Clone(expected);
        for (var i = 0; i < wrong; i++)
        {
            actual.Words[i].Lemma = "falsch";
        }

        return Input(expected, actual);
    }
}
