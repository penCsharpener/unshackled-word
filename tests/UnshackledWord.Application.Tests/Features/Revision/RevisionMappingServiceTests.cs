using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Tests.Features.Revision;

public class RevisionMappingServiceTests
{
    private readonly IRevisionRepository _repository = Substitute.For<IRevisionRepository>();
    private readonly IVerseMappingRepository _verseMapping = Substitute.For<IVerseMappingRepository>();

    private RevisionMappingService Service() => new(_repository, _verseMapping);

    private void Variant(int id = 50)
    {
        _repository.GetHeaderAsync(id, Arg.Any<CancellationToken>()).Returns(new RevisionVerseHeaderRow { Id = id, HebRefId = 43003016, Level = 200, Version = 2 });
        _repository.GetWordsAsync(id, Arg.Any<CancellationToken>()).Returns([
            new RevisionWordRow { Id = 501, PositionInVerse = 1, WordInContext = "Denn", BasePositionInVerse = 1, Change = "same" },
            new RevisionWordRow { Id = 502, PositionInVerse = 2, WordInContext = "einzigen", BasePositionInVerse = 2, Change = "changed" },
            new RevisionWordRow { Id = 503, PositionInVerse = 3, WordInContext = "neu", Change = "new" },
        ]);
        _verseMapping.GetOriginalWordsAsync(false, 43, 3, 16, Arg.Any<CancellationToken>()).Returns([
            new VerseOriginalWordRow { Id = 10, Position = 1, Original = "ὁ", Gloss = "" },
            new VerseOriginalWordRow { Id = 20, Position = 2, Original = "μονογενῆ", Gloss = "only" },
        ]);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenTheVariantDoesNotExist()
    {
        (await Service().GetAsync(1)).ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_BuildsWordsLinksAndOrigins()
    {
        Variant();
        _repository.GetMappingAsync(50, Arg.Any<CancellationToken>()).Returns([
            new RevisionMappingRow { RevisionWordId = 502, StepWordId = 20, Origin = "carried" },
            new RevisionMappingRow { RevisionWordId = 501, StepWordId = 10, Origin = "manual" },
            new RevisionMappingRow { RevisionWordId = 503, IsAddedWord = true, ParentRevisionWordId = 502, Origin = "carried" },
        ]);

        var response = (await Service().GetAsync(50))!;

        response.RevisionVerseId.ShouldBe(50);
        response.Level.ShouldBe(200);
        response.Version.ShouldBe(2);
        response.Language.ShouldBe("greek");
        (response.BibleBookId, response.Chapter, response.Verse).ShouldBe((43, 3, 16));
        response.GermanWords.Select(w => (w.Id, w.Change)).ShouldBe([(501, "same"), (502, "changed"), (503, "new")]);
        response.OriginalWords[0].Gloss.ShouldBeNull();
        response.Links.Select(l => (l.ElbWordId, l.StepWordId, l.IsAddedWord, l.ParentElbWordId, l.Origin))
            .ShouldBe([(501, (int?)10, false, (int?)null, "manual"), (502, 20, false, null, "carried"), (503, null, true, 502, "carried")]);
    }

    [Fact]
    public async Task GetAsync_DropsRowsThatPointOutsideTheVerse()
    {
        Variant();
        _repository.GetMappingAsync(50, Arg.Any<CancellationToken>()).Returns([
            new RevisionMappingRow { RevisionWordId = 501, StepWordId = 999, Origin = "seed" },
            new RevisionMappingRow { RevisionWordId = 999, StepWordId = 10, Origin = "seed" },
            new RevisionMappingRow { RevisionWordId = 503, IsAddedWord = true, ParentRevisionWordId = null, Origin = "carried" },
        ]);

        (await Service().GetAsync(50))!.Links.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsync_UsesHebrewForOldTestament()
    {
        _repository.GetHeaderAsync(7, Arg.Any<CancellationToken>()).Returns(new RevisionVerseHeaderRow { Id = 7, HebRefId = 1001001, Level = 200, Version = 1 });
        _repository.GetWordsAsync(7, Arg.Any<CancellationToken>()).Returns([]);
        _repository.GetMappingAsync(7, Arg.Any<CancellationToken>()).Returns([]);
        _verseMapping.GetOriginalWordsAsync(true, 1, 1, 1, Arg.Any<CancellationToken>()).Returns([]);

        var response = (await Service().GetAsync(7))!;

        response.Language.ShouldBe("hebrew");
    }

    private static VerseMappingLink Link(int word, int? step, bool added = false, int? parent = null) =>
        new() { ElbWordId = word, StepWordId = step, IsAddedWord = added, ParentElbWordId = parent };

    [Fact]
    public async Task SaveAsync_ReturnsNotFound_WhenTheVariantDoesNotExist()
    {
        (await Service().SaveAsync(1, [], 5)).Outcome.ShouldBe(SaveRevisionMappingOutcome.NotFound);
    }

    [Fact]
    public async Task SaveAsync_RejectsAForeignWordId_WithoutWriting()
    {
        Variant();
        _repository.GetMappingAsync(50, Arg.Any<CancellationToken>()).Returns([]);

        var result = await Service().SaveAsync(50, [Link(9999, 10), Link(501, 777)], 5);

        result.Outcome.ShouldBe(SaveRevisionMappingOutcome.Invalid);
        result.Errors.Count.ShouldBe(2);
        await _repository.DidNotReceive().ReplaceMappingAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<RevisionMappingRow>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_KeepsOriginOfUnchangedRows_AndMarksTheOthersManual()
    {
        Variant();
        _repository.GetMappingAsync(50, Arg.Any<CancellationToken>()).Returns([
            new RevisionMappingRow { RevisionWordId = 501, StepWordId = 10, Origin = "carried", GermanWordPart = "a" },
            new RevisionMappingRow { RevisionWordId = 502, StepWordId = 20, Origin = "seed" },
        ]);
        IReadOnlyList<RevisionMappingRow>? saved = null;
        _repository.ReplaceMappingAsync(50, "G", Arg.Do<IReadOnlyList<RevisionMappingRow>>(r => saved = r), 5, Arg.Any<CancellationToken>()).Returns(true);

        var result = await Service().SaveAsync(50, [Link(501, 10), Link(502, 20), Link(503, 10), Link(503, null, added: true, parent: 502)], 5);

        result.Outcome.ShouldBe(SaveRevisionMappingOutcome.Saved);
        saved!.Select(r => (r.RevisionWordId, r.StepWordId, r.Origin)).ShouldBe([(501, (int?)10, "carried"), (502, 20, "seed"), (503, 10, "manual"), (503, null, "manual")]);
        saved[0].GermanWordPart.ShouldBe("a");
    }

    [Fact]
    public async Task SaveAsync_ReturnsConflict_WhenTheVariantWasWithdrawnOrRewrittenDuringTheSave()
    {
        Variant();
        _repository.GetMappingAsync(50, Arg.Any<CancellationToken>()).Returns([]);
        _repository.ReplaceMappingAsync(50, Arg.Any<string>(), Arg.Any<IReadOnlyList<RevisionMappingRow>>(), 5, Arg.Any<CancellationToken>()).Returns(false);

        (await Service().SaveAsync(50, [Link(501, 10)], 5)).Outcome.ShouldBe(SaveRevisionMappingOutcome.Conflict);
    }
}
