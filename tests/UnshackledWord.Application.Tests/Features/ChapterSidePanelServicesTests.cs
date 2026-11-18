using UnshackledWord.Application.Features.ChapterOriginals;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Application.Features.VerseCounting;
using UnshackledWord.Domain.Models.BibleStructure.References;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Tests.Features;

public class ChapterSidePanelServicesTests
{
    [Fact]
    public async Task CrossReferences_GroupPerVerse_DedupeAndKeepTskOrder()
    {
        var tsk = Substitute.For<ITskRepository>();
        tsk.GetOutgoingAsync(43_003_000, 43_003_999, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
        [
            new TskCrossReference { SourceHebRefId = 43_003_016, Scope = "loved", TargetStartHebRefId = 62_004_009, TargetEndHebRefId = 62_004_010 },
            new TskCrossReference { SourceHebRefId = 43_003_016, Scope = "loved", TargetStartHebRefId = 62_004_009, TargetEndHebRefId = 62_004_010 },
            new TskCrossReference { SourceHebRefId = 43_003_016, Scope = "", TargetStartHebRefId = 45_005_008, TargetEndHebRefId = null },
            new TskCrossReference { SourceHebRefId = 43_003_001, Scope = "man", TargetStartHebRefId = 43_007_050, TargetEndHebRefId = 43_007_048 },
        ]);

        var response = await new ChapterCrossReferenceService(tsk).GetAsync(43, 3);

        response.Verses.Select(v => v.Verse).ShouldBe([1, 16]);
        var first = response.Verses[0].References.ShouldHaveSingleItem();
        (first.Verse, first.EndVerse).ShouldBe((48, 50));
        var sixteen = response.Verses[1].References;
        sixteen.Count.ShouldBe(2);
        sixteen[0].BibleBookId.ShouldBe(62);
        sixteen[0].Keyword.ShouldBe("loved");
        sixteen[1].Keyword.ShouldBeNull();
    }

    [Fact]
    public async Task Originals_GroupWordsPerVerse_AndKeepOnlyLinksInsideTheVerse()
    {
        var repository = Substitute.For<IVerseMappingRepository>();
        repository.GetChapterOriginalWordsAsync(false, 43, 3, Arg.Any<CancellationToken>()).Returns(
        [
            new ChapterOriginalWordRow { Id = 10, HebRefId = 43_003_001, Position = 1, Original = "Ἦν", Strongs = "G1510" },
            new ChapterOriginalWordRow { Id = 11, HebRefId = 43_003_001, Position = 2, Original = "δὲ", Strongs = "G1161" },
            new ChapterOriginalWordRow { Id = 20, HebRefId = 43_003_002, Position = 1, Original = "οὗτος", Strongs = "G3778" },
        ]);
        repository.GetChapterMappingRowsAsync(false, 43, 3, Arg.Any<CancellationToken>()).Returns(
        [
            new ChapterMappingRow { ElbWordId = 100, StepWordId = 10, GermanVerse = 1 },
            new ChapterMappingRow { ElbWordId = 101, StepWordId = 20, GermanVerse = 1 }, // original of verse 2: ignored
            new ChapterMappingRow { ElbWordId = 200, StepWordId = 20, GermanVerse = 2 },
        ]);

        var response = await new ChapterOriginalsService(repository, Substitute.For<IStrongsLexiconRepository>(), new VerseCountingMapProvider(_ => Task.FromResult<IReadOnlyList<VerseCountingRow>>([]))).GetAsync(43, 3);

        response.Language.ShouldBe("greek");
        response.Verses.Select(v => v.Verse).ShouldBe([1, 2]);
        response.Verses[0].Words.Select(w => w.Original).ShouldBe(["Ἦν", "δὲ"]);
        response.Verses[0].Links.ShouldHaveSingleItem().GermanId.ShouldBe(100);
        response.Verses[1].Links.ShouldHaveSingleItem().GermanId.ShouldBe(200);
    }

    [Fact]
    public async Task Originals_OldTestamentBookIsHebrew()
    {
        var repository = Substitute.For<IVerseMappingRepository>();
        repository.GetChapterOriginalWordsAsync(true, 19, 23, Arg.Any<CancellationToken>()).Returns([]);
        repository.GetChapterMappingRowsAsync(true, 19, 23, Arg.Any<CancellationToken>()).Returns([]);

        var response = await new ChapterOriginalsService(repository, Substitute.For<IStrongsLexiconRepository>(), new VerseCountingMapProvider(_ => Task.FromResult<IReadOnlyList<VerseCountingRow>>([]))).GetAsync(19, 23);

        response.Language.ShouldBe("hebrew");
        response.Verses.ShouldBeEmpty();
    }
}
