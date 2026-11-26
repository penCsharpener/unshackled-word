using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Application.Features.VerseCounting;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Tests.Features.Lxx;

/// <summary>Roadmap 0028.5: public reads show confirmed tags only, Admin/Maintainer also proposed ones (chapter words and word box).</summary>
public class LxxTagVisibilityTests
{
    private static LxxWordRow Word(int id, int pos, string greek, string? status = null, string morphology = "N-DSF") => new()
    {
        Id = id,
        LxxVerseId = 1,
        Position = pos,
        Greek = greek,
        Lemma = status is null ? null : "ἀρχή",
        Strongs = status is null ? null : "G0746",
        Morphology = status is null ? null : morphology,
        Status = status,
        Source = status is null ? null : "human",
        Gloss = status is null ? null : "beginning",
    };

    private static LxxWordDetailRow Detail(string? status, string morphology = "N-DSF") => new()
    {
        Id = 2,
        LxxVerseId = 1,
        Position = 2,
        Greek = "ἀρχῇ",
        Lemma = status is null ? null : "ἀρχή",
        Strongs = status is null ? null : "G0746",
        Morphology = status is null ? null : morphology,
        Status = status,
        Source = status is null ? null : "ai",
        BibleBookId = 1,
        Chapter = 1,
        Verse = 1,
        HebRefId = 1_001_001,
        MapKind = "one",
    };

    private static LxxChapterRows Rows(params LxxWordRow[] words) => new()
    {
        ElbHebRefIds = [1_001_001],
        Units = [new LxxUnitRow { Id = 1, SortOrder = 1, BibleBookId = 1, Chapter = 1, Verse = 1, HebRefId = 1_001_001, MapKind = "one" }],
        Words = words.ToList(),
    };

    [Theory]
    [InlineData(null, false, false)]
    [InlineData(null, true, false)]
    [InlineData("confirmed", false, true)]
    [InlineData("confirmed", true, true)]
    [InlineData("proposed", false, false)]
    [InlineData("proposed", true, true)]
    [InlineData("rejected", true, false)]
    public void IsVisible(string? status, bool includeProposed, bool expected) =>
        LxxTagVisibility.IsVisible(status, includeProposed).ShouldBe(expected);

    [Fact]
    public void Chapter_Anonymous_SeesConfirmedTagsOnly()
    {
        var rows = Rows(Word(1, 1, "ἐν"), Word(2, 2, "ἀρχῇ", "confirmed"), Word(3, 3, "ἐποίησεν", "proposed", "V-AAI-3S"));

        var words = LxxChapterReadService.Build(1, 1, rows).Verses.Single().Units.Single().Words;

        words[0].Tag.ShouldBeNull();
        words[2].Tag.ShouldBeNull();
        var tag = words[1].Tag.ShouldNotBeNull();
        tag.Lemma.ShouldBe("ἀρχή");
        tag.Strongs.ShouldBe("G0746");
        tag.Gloss.ShouldBe("beginning");
        tag.Status.ShouldBe("confirmed");
        tag.Morphology.Code.ShouldBe("N-DSF");
        tag.Morphology.PartOfSpeech.ShouldBe("noun");
        tag.Morphology.Case.ShouldBe("dative");
    }

    [Fact]
    public void Chapter_Maintainer_SeesProposedTagsFlagged()
    {
        var rows = Rows(Word(2, 2, "ἀρχῇ", "confirmed"), Word(3, 3, "ἐποίησεν", "proposed", "V-AAI-3S"));

        var words = LxxChapterReadService.Build(1, 1, rows, includeProposed: true).Verses.Single().Units.Single().Words;

        words[0].Tag!.Status.ShouldBe("confirmed");
        var proposed = words[1].Tag.ShouldNotBeNull();
        proposed.Status.ShouldBe("proposed");
        proposed.Morphology.Tense.ShouldBe("aorist");
    }

    [Fact]
    public void Word_Untagged_ReturnsFormAndRefOnly()
    {
        var response = LxxWordReadService.Build(Detail(null), includeProposed: true, lexicon: null);

        response.Greek.ShouldBe("ἀρχῇ");
        response.Ref.ShouldBe("Gen.1:1");
        response.HebRefId.ShouldBe(1_001_001);
        response.Tag.ShouldBeNull();
    }

    [Fact]
    public void Word_Confirmed_ReturnsLemmaStrongsAndParse()
    {
        var lexicon = new LexiconSummaryResponse { Strongs = "G0746", Lemma = "ἀρχή", Gloss = "beginning" };

        var tag = LxxWordReadService.Build(Detail("confirmed"), includeProposed: false, lexicon).Tag.ShouldNotBeNull();

        tag.Lemma.ShouldBe("ἀρχή");
        tag.Strongs.ShouldBe("G0746");
        tag.Gloss.ShouldBe("beginning");
        tag.ParseGerman.ShouldBe("Nomen Dativ Singular feminin");
        tag.ParseEnglish.ShouldBe("noun dative singular feminine");
        tag.Morphology.Gender.ShouldBe("feminine");
        tag.Status.ShouldBe("confirmed");
        tag.Source.ShouldBeNull();
        tag.Lexicon.ShouldBe(lexicon);
    }

    [Fact]
    public void Word_Proposed_HiddenFromPublic_ShownToMaintainerWithSource()
    {
        LxxWordReadService.Build(Detail("proposed"), includeProposed: false, lexicon: null).Tag.ShouldBeNull();

        var tag = LxxWordReadService.Build(Detail("proposed"), includeProposed: true, lexicon: null).Tag.ShouldNotBeNull();
        tag.Status.ShouldBe("proposed");
        tag.Source.ShouldBe("ai");
    }

    [Fact]
    public void Word_UnparsableCode_KeepsCodeWithoutDescription()
    {
        var tag = LxxWordReadService.Build(Detail("confirmed", "V-XYZ"), includeProposed: false, lexicon: null).Tag.ShouldNotBeNull();

        tag.Morphology.Code.ShouldBe("V-XYZ");
        tag.ParseGerman.ShouldBeNull();
        tag.ParseEnglish.ShouldBeNull();
    }

    [Fact]
    public async Task Service_ProposedTag_ForAnonymous_SkipsLexiconLookup()
    {
        var repository = Substitute.For<ILxxReadRepository>();
        repository.GetWordAsync(2, Arg.Any<CancellationToken>()).Returns(Detail("proposed"));
        var lexicon = Substitute.For<IStrongsLexiconRepository>();
        var service = new LxxWordReadService(repository, lexicon, Substitute.For<IVerseCountingMapProvider>());

        var response = await service.GetAsync(2, includeProposed: false, TestContext.Current.CancellationToken);

        response.ShouldNotBeNull().Tag.ShouldBeNull();
        await lexicon.DidNotReceiveWithAnyArgs().GetEntriesAsync(default, TestContext.Current.CancellationToken);
        (await service.GetAsync(99, includeProposed: true, TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
