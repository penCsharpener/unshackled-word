using UnshackledWord.Application.Features.ChapterOriginals;
using UnshackledWord.Application.Features.VerseCounting;
using UnshackledWord.Application.Features.WordDetail;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.BibleStructure.References;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>Roadmap 0019.10: <c>definitionSegments</c> in the WordDetail and ChapterOriginals responses, with stubbed data sources.</summary>
public class LexiconSegmentsTests
{
    // Shape of the real G1188 (δεξιός) entry: a tagged reference in the lexicon text.
    private const string DexiosLexicon = "<b>right</b> (hand)<br/>see <ref='Mat.5.29'>Mat.5:29</ref>; <ref='Mat.5.30'>Mat.5:30</ref> and Mal.4.5";

    private static readonly IVerseCountingMapProvider Counting = new VerseCountingMapProvider(_ =>
        Task.FromResult<IReadOnlyList<VerseCountingRow>>([new VerseCountingRow(39004005, 39003023)]));

    private static IStrongsLexiconRepository LexiconStub()
    {
        var lexicon = Substitute.For<IStrongsLexiconRepository>();
        lexicon.GetEntriesAsync(Arg.Is<StrongsNumber>(s => s.Number == 1188), Arg.Any<CancellationToken>()).Returns([
            new LexiconEntry
            {
                LanguageId = (int)StrongsLanguage.Greek, Number = 1188, OriginalWord = "δεξιός", Transliteration = "dexios",
                Gloss = "right", Lexicon = DexiosLexicon,
            },
        ]);
        return lexicon;
    }

    [Fact]
    public async Task WordDetail_G1188_LinksMat529_To40005029()
    {
        var words = Substitute.For<IWordDetailRepository>();
        words.GetWordAsync(1, Arg.Any<CancellationToken>()).Returns(new ElbWordRow
        {
            Id = 1, BibleBookId = 40, Chapter = 5, Verse = 29, HebRefId = 40005029, PositionInVerse = 3, WordInContext = "rechte",
        });
        words.GetGreekReadingsAsync(1, Arg.Any<CancellationToken>()).Returns([
            new GreekReadingRow { StepWordId = 1, PositionInVerse = 3, Original = "δεξιός", Strongs = "G1188", Gloss = "right", MappingCreatedAt = DateTime.UtcNow },
        ]);
        words.GetCoMappedWordsAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);
        words.GetRenderingsAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        var tsk = Substitute.For<ITskRepository>();
        tsk.GetOutgoingAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        var service = new WordDetailService(words, Substitute.For<IHebrewMorphemeRepository>(), LexiconStub(), tsk, Counting);

        var lexicon = (await service.GetAsync(1))!.Readings.Single().Lexicon!;

        lexicon.Definition.ShouldNotBeNull();
        var segments = lexicon.DefinitionSegments.ShouldNotBeNull();
        string.Concat(segments.Select(s => s.Text)).ShouldBe(lexicon.Definition);
        var mat = segments.Single(s => s.Text == "Mat.5:29");
        mat.Ref.ShouldNotBeNull();
        mat.Ref.BookId.ShouldBe(40);
        mat.Ref.Path.ShouldBe("/bible/40/5?v=29#v29");
        var span = mat.Ref.Spans.ShouldHaveSingleItem();
        (span.StartChapter, span.StartVerse, span.EndChapter, span.EndVerse).ShouldBe((5, 29, 5, 29));

        // Untagged note, English counting 4:5 is Hebrew 3:23.
        var mal = segments.Single(s => s.Text == "Mal.4.5");
        mal.Ref!.Path.ShouldBe("/bible/39/3?v=23#v23");
    }

    [Fact]
    public async Task ChapterOriginals_DefinitionSegments_LinkReferences()
    {
        var repository = Substitute.For<IVerseMappingRepository>();
        repository.GetChapterOriginalWordsAsync(false, 40, 5, Arg.Any<CancellationToken>()).Returns([
            new ChapterOriginalWordRow { Id = 1, HebRefId = 40005029, Position = 1, Original = "δεξιός", Strongs = "G1188" },
        ]);
        repository.GetChapterMappingRowsAsync(false, 40, 5, Arg.Any<CancellationToken>()).Returns([]);

        var response = await new ChapterOriginalsService(repository, LexiconStub(), Counting, Substitute.For<IGreekEditionRepository>()).GetAsync(40, 5);

        var entry = response.Lexicon!["G1188"];
        var segments = entry.DefinitionSegments.ShouldNotBeNull();
        string.Concat(segments.Select(s => s.Text)).ShouldBe(entry.Definition);
        segments.Single(s => s.Text == "Mat.5:30").Ref!.Path.ShouldBe("/bible/40/5?v=30#v30");
    }
}
