using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Tests.Features.Revision;

public class RevisionCarryServiceTests
{
    private readonly IVerseMappingRepository _verseMapping = Substitute.For<IVerseMappingRepository>();
    private readonly IRevisionRepository _revisions = Substitute.For<IRevisionRepository>();

    private static List<RevisionBaseWordRow> BaseWords(params string[] words) =>
        words.Select((w, i) => new RevisionBaseWordRow { PositionInVerse = i + 1, PlainWord = w }).ToList();

    private static List<RevisionToken> Tokens(params string[] words) =>
        words.Select((w, i) => new RevisionToken(i + 1, w, w)).ToList();

    private void Verse(bool hebrew, int book, IEnumerable<(int Id, int Position)> german, IEnumerable<int> originalIds, IEnumerable<VerseMappingRow> rows)
    {
        _verseMapping.GetGermanWordsAsync(book, 3, 16, Arg.Any<CancellationToken>())
            .Returns(german.Select(g => new VerseGermanWordRow { Id = g.Id, PositionInVerse = g.Position, Text = "x" }).ToList());
        _verseMapping.GetOriginalWordsAsync(hebrew, book, 3, 16, Arg.Any<CancellationToken>())
            .Returns(originalIds.Select((id, i) => new VerseOriginalWordRow { Id = id, Position = i + 1, Original = "o" }).ToList());
        _verseMapping.GetMappingRowsAsync(hebrew, book, 3, 16, Arg.Any<CancellationToken>()).Returns(rows.ToList());
    }

    [Fact]
    public async Task CarryFromBaseAsync_TranslatesElbWordIdsToPositionsAndBack()
    {
        Verse(false, 43, [(900, 1), (901, 2), (902, 3)], [10, 20, 30], [
            new VerseMappingRow { ElbWordId = 900, StepWordId = 10, GermanWordPart = "a" },
            new VerseMappingRow { ElbWordId = 901, StepWordId = 20 },
            new VerseMappingRow { ElbWordId = 902, StepWordId = 30 },
        ]);

        var result = await new RevisionCarryService(_verseMapping, _revisions).CarryFromBaseAsync(43, 3, 16, BaseWords("a", "b", "c"), Tokens("a", "x", "c"));

        result.Words.Select(w => (w.Position, w.Change)).ShouldBe([(1, "same"), (2, "changed"), (3, "same")]);
        result.Rows.Select(r => (r.Position, r.StepWordId)).ShouldBe([(1, (int?)10), (2, 20), (3, 30)]);
        result.Rows[0].GermanWordPart.ShouldBe("a");
    }

    [Fact]
    public async Task CarryFromBaseAsync_IgnoresRowsOutsideTheVerse_AndKeepsAddedWordParents()
    {
        Verse(false, 43, [(900, 1), (901, 2)], [10], [
            new VerseMappingRow { ElbWordId = 900, StepWordId = 10 },
            new VerseMappingRow { ElbWordId = 900, StepWordId = 999 },
            new VerseMappingRow { ElbWordId = 5000, StepWordId = 10 },
            new VerseMappingRow { ElbWordId = 901, IsAddedWord = true, ParentElbWordId = 900 },
        ]);

        var result = await new RevisionCarryService(_verseMapping, _revisions).CarryFromBaseAsync(43, 3, 16, BaseWords("a", "b"), Tokens("a", "b"));

        result.Rows.Count.ShouldBe(2);
        result.Rows.Single(r => r.IsAddedWord).ParentPosition.ShouldBe(1);
        result.Rows.Single(r => !r.IsAddedWord).StepWordId.ShouldBe(10);
    }

    [Fact]
    public async Task CarryFromBaseAsync_ReadsTheHebrewMapping_ForOldTestamentBooks()
    {
        Verse(true, 1, [(1, 1)], [7], [new VerseMappingRow { ElbWordId = 1, StepWordId = 7 }]);
        _verseMapping.GetGermanWordsAsync(1, 3, 16, Arg.Any<CancellationToken>()).Returns([new VerseGermanWordRow { Id = 1, PositionInVerse = 1, Text = "a" }]);

        var result = await new RevisionCarryService(_verseMapping, _revisions).CarryFromBaseAsync(1, 3, 16, BaseWords("a"), Tokens("a"));

        result.Rows.Single().StepWordId.ShouldBe(7);
        await _verseMapping.Received(1).GetMappingRowsAsync(true, 1, 3, 16, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CarryFromVariantAsync_CarriesTheVariantsManualLinksAndComposesTheBasePositions()
    {
        // Parent variant 200: "a x c" where x replaced base word 2 (changed) and was linked by hand to step word 99.
        _revisions.GetWordsAsync(40, Arg.Any<CancellationToken>()).Returns(
        [
            new RevisionWordRow { Id = 700, PositionInVerse = 1, PlainWord = "a", BasePositionInVerse = 1, Change = "same" },
            new RevisionWordRow { Id = 701, PositionInVerse = 2, PlainWord = "x", BasePositionInVerse = 2, Change = "changed" },
            new RevisionWordRow { Id = 702, PositionInVerse = 3, PlainWord = "c", BasePositionInVerse = 3, Change = "same" },
        ]);
        _revisions.GetMappingAsync(40, Arg.Any<CancellationToken>()).Returns(
        [
            new RevisionMappingRow { RevisionWordId = 700, StepWordId = 10, Origin = "carried" },
            new RevisionMappingRow { RevisionWordId = 701, StepWordId = 99, Origin = "manual" },
            new RevisionMappingRow { RevisionWordId = 702, StepWordId = 30, Origin = "carried" },
        ]);

        var result = await new RevisionCarryService(_verseMapping, _revisions).CarryFromVariantAsync(40, Tokens("a", "x", "c", "d"));

        result.Rows.Select(r => (r.Position, r.StepWordId)).ShouldBe([(1, (int?)10), (2, 99), (3, 30)]);
        // "x" is the same as the parent word but the parent word is already changed against Elb1871, so it stays changed.
        result.Words.Select(w => (w.Position, w.BasePositionInVerse, w.Change)).ShouldBe([(1, (int?)1, "same"), (2, 2, "changed"), (3, 3, "same"), (4, null, "new")]);
        await _verseMapping.DidNotReceiveWithAnyArgs().GetMappingRowsAsync(default, default, default, default, default);
    }
}
