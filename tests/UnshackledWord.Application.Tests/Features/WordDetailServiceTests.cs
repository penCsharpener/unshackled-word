using UnshackledWord.Application.Features.WordDetail;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Tests.Features;

public class WordDetailServiceTests
{
    private static readonly DateTime Created = new(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc);

    private readonly IWordDetailRepository _words = Substitute.For<IWordDetailRepository>();
    private readonly IHebrewMorphemeRepository _morphemes = Substitute.For<IHebrewMorphemeRepository>();
    private readonly IStrongsLexiconRepository _lexicon = Substitute.For<IStrongsLexiconRepository>();
    private readonly WordDetailService _sut;

    public WordDetailServiceTests()
    {
        _sut = new WordDetailService(_words, _morphemes, _lexicon);
        _words.GetCoMappedWordsAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);
        _lexicon.GetEntriesAsync(Arg.Any<StrongsNumber>(), Arg.Any<CancellationToken>()).Returns([]);
        _words.GetOccurrenceCountAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(0);
        _words.GetRenderingsAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private void GivenWord(int id, int bookId, string text, int position = 5, string? lemma = null)
    {
        _words.GetWordAsync(id, Arg.Any<CancellationToken>()).Returns(new ElbWordRow
        {
            Id = id, BibleBookId = bookId, Chapter = 1, Verse = 1, HebRefId = bookId * 1_000_000 + 1001, PositionInVerse = position, WordInContext = text, GermanLemma = lemma
        });
    }

    private void GivenGreek(int id, params GreekReadingRow[] rows)
    {
        _words.GetGreekReadingsAsync(id, Arg.Any<CancellationToken>()).Returns(rows);
    }

    private void GivenHebrew(int id, params HebrewReadingRow[] rows)
    {
        _words.GetHebrewReadingsAsync(id, Arg.Any<CancellationToken>()).Returns(rows);
    }

    private static GreekReadingRow Logos() => new()
    {
        StepWordId = 140005, PositionInVerse = 5, Original = "λόγος", Transliteration = "logos", Lemma = "λόγος",
        Strongs = "G3056", Gloss = "word", ContextGloss = "Word", MorphologyCode = "N-NSM", MappingCreatedAt = Created,
    };

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        (await _sut.GetAsync(1)).ShouldBeNull();
        await _words.DidNotReceive().GetGreekReadingsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_NtWord_BuildsGreekReadingWithDecodedMorphologyAndLexicon()
    {
        GivenWord(26001, 43, "Wort", lemma: "Wort");
        GivenGreek(26001, Logos());
        _lexicon.GetEntriesAsync(Arg.Is<StrongsNumber>(s => s.Number == 3056), Arg.Any<CancellationToken>()).Returns([
            new LexiconEntry
            {
                LanguageId = (int)StrongsLanguage.Greek, Number = 3056, OriginalWord = "λόγος", Transliteration = "logos",
                Gloss = "word", Lexicon = "<b>a word</b><br/>spoken"
            }
        ]);

        var detail = await _sut.GetAsync(26001);

        detail.ShouldNotBeNull();
        detail.WordInContext.ShouldBe("Wort");
        detail.GermanLemma.ShouldBe("Wort");
        detail.IsAddedWord.ShouldBeNull();
        detail.Mapping!.Source.ShouldBe("ai");
        detail.Mapping.CreatedAt.ShouldBe("2026-03-14T10:00:00Z");
        detail.Mapping.Model.ShouldBeNull();
        detail.Hints.ShouldBeNull();
        var reading = detail.Readings.ShouldHaveSingleItem();
        reading.Language.ShouldBe("greek");
        reading.Strongs.ShouldBe("G3056");
        reading.Morphology!.Code.ShouldBe("N-NSM");
        reading.Morphology.PartOfSpeech.ShouldBe("noun");
        reading.Morphology.Case.ShouldBe("nominative");
        reading.Morphology.Description.ShouldBeNull();
        reading.Lexicon!.Definition.ShouldBe("a word spoken");
        reading.Lexicon.DefinitionTruncated.ShouldBeNull();
        reading.Morphemes.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_NtWord_AddsOccurrenceCountAndTopRenderings()
    {
        GivenWord(26001, 43, "Wort");
        GivenGreek(26001, Logos());
        _words.GetOccurrenceCountAsync("G3056", false, Arg.Any<CancellationToken>()).Returns(137);
        _words.GetRenderingsAsync("G3056", false, 5, Arg.Any<CancellationToken>())
            .Returns([new RenderingRow { Word = "Wort", Count = 112 }, new RenderingRow { Word = "Rede", Count = 14 }]);

        var reading = (await _sut.GetAsync(26001))!.Readings.Single();

        reading.OccurrenceCount.ShouldBe(137);
        reading.Renderings!.Select(r => (r.Word, r.Count)).ShouldBe([("Wort", 112), ("Rede", 14)]);
    }

    [Fact]
    public async Task GetAsync_NoStatistics_OmitsCountAndRenderings()
    {
        GivenWord(26001, 43, "Wort");
        GivenGreek(26001, Logos());

        var reading = (await _sut.GetAsync(26001))!.Readings.Single();

        reading.OccurrenceCount.ShouldBeNull();
        reading.Renderings.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_OtWord_QueriesHebrewStatistics()
    {
        GivenWord(500, 1, "Anfang");
        GivenHebrew(500, new HebrewReadingRow { StepWordId = 77, PositionInVerse = 1, Original = "בְּרֵאשִׁית", Strongs = "H7225G", MappingCreatedAt = Created });
        _words.GetOccurrenceCountAsync("H7225G", true, Arg.Any<CancellationToken>()).Returns(51);

        var reading = (await _sut.GetAsync(500))!.Readings.Single();

        reading.OccurrenceCount.ShouldBe(51);
    }

    [Fact]
    public async Task GetAsync_UndecodableGreekCode_FallsBackToStepDescription()
    {
        GivenWord(1, 43, "x");
        var row = new GreekReadingRow
        {
            StepWordId = 1, PositionInVerse = 1, Original = "α", Strongs = "G1", MorphologyCode = "Z-WEIRD",
            StepPartOfSpeech = "Particle", StepCase = "Genitive", MappingCreatedAt = Created
        };
        GivenGreek(1, row);

        var morphology = (await _sut.GetAsync(1))!.Readings.Single().Morphology!;

        morphology.PartOfSpeech.ShouldBeNull();
        morphology.Description.ShouldBe("Particle, Genitive");
    }

    [Fact]
    public async Task GetAsync_TwoReadings_AreSortedByOriginalPosition()
    {
        GivenWord(1, 43, "x");
        var late = new GreekReadingRow { StepWordId = 2, PositionInVerse = 9, Original = "β", Strongs = "G2", MappingCreatedAt = Created };
        var early = new GreekReadingRow { StepWordId = 1, PositionInVerse = 3, Original = "α", Strongs = "G1", MappingCreatedAt = Created };
        GivenGreek(1, late, early);

        var detail = await _sut.GetAsync(1);

        detail!.Readings.Select(r => r.StepWordId).ShouldBe([1, 2]);
    }

    [Fact]
    public async Task GetAsync_ManyReadingsAndUnclearPart_AddHints()
    {
        GivenWord(1, 43, "Der");
        GivenGreek(1,
            new GreekReadingRow { StepWordId = 1, PositionInVerse = 1, Original = "α", Strongs = "G1", GermanWordPart = "*", MappingCreatedAt = Created },
            new GreekReadingRow { StepWordId = 2, PositionInVerse = 2, Original = "β", Strongs = "G2", MappingCreatedAt = Created },
            new GreekReadingRow { StepWordId = 3, PositionInVerse = 3, Original = "γ", Strongs = "G3", MappingCreatedAt = Created });

        var detail = await _sut.GetAsync(1);

        detail!.Hints.ShouldBe(["manyOriginals", "unclearWordPart"]);
    }

    [Fact]
    public async Task GetAsync_ProperNoun_AddsPersonWithCleanedNameAndBrief()
    {
        GivenWord(1, 44, "Paulus");
        GivenGreek(1, new GreekReadingRow { StepWordId = 1, PositionInVerse = 1, Original = "Παῦλος", Strongs = "G3972G", MappingCreatedAt = Created });
        _words.GetNamedEntityAsync("G3972G", Arg.Any<CancellationToken>())
            .Returns(new NamedEntityRow { Kind = "person", Name = "a_sample_person", Brief = "A <b>sample</b>  brief." });

        var entity = (await _sut.GetAsync(1))!.Readings.Single().Entity;

        entity.ShouldNotBeNull();
        entity.Kind.ShouldBe("person");
        entity.Name.ShouldBe("a sample person");
        entity.Brief.ShouldBe("A sample brief.");
    }

    [Fact]
    public async Task GetAsync_HebrewInstanceSuffix_IsDroppedForPlaceLookup()
    {
        GivenWord(1, 1, "Babel");
        GivenHebrew(1, new HebrewReadingRow { StepWordId = 1, PositionInVerse = 1, Original = "בָּבֶל", Strongs = "H0894_A", MappingCreatedAt = Created });
        _words.GetNamedEntityAsync("H0894", Arg.Any<CancellationToken>())
            .Returns(new NamedEntityRow { Kind = "place", Name = "Babel", Brief = "Sample brief." });

        var entity = (await _sut.GetAsync(1))!.Readings.Single().Entity;

        entity!.Kind.ShouldBe("place");
    }

    [Fact]
    public async Task GetAsync_NoEntity_OmitsEntity()
    {
        GivenWord(26001, 43, "Wort");
        GivenGreek(26001, Logos());

        (await _sut.GetAsync(26001))!.Readings.Single().Entity.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_NtVerseWithApparatus_AddsTextVariantsHint()
    {
        GivenWord(26001, 43, "Wort");
        GivenGreek(26001, Logos());
        _words.HasTextVariantsAsync(43001001, Arg.Any<CancellationToken>()).Returns(true);

        (await _sut.GetAsync(26001))!.Hints.ShouldBe(["textVariants"]);
    }

    [Fact]
    public async Task GetAsync_OtWord_NeverQueriesApparatus()
    {
        GivenWord(1, 1, "Anfang");
        GivenHebrew(1, new HebrewReadingRow { StepWordId = 1, PositionInVerse = 1, Original = "רֵאשִׁית", Strongs = "H7225G", MappingCreatedAt = Created });
        _words.HasTextVariantsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);

        (await _sut.GetAsync(1))!.Hints.ShouldBeNull();
        await _words.DidNotReceive().HasTextVariantsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_GermanWordPart_IsOmittedWhenWholeWordOrStar()
    {
        GivenWord(1, 43, "Wort");
        GivenGreek(1,
            new GreekReadingRow { StepWordId = 1, PositionInVerse = 1, Original = "α", Strongs = "G1", GermanWordPart = "wort", MappingCreatedAt = Created },
            new GreekReadingRow { StepWordId = 2, PositionInVerse = 2, Original = "β", Strongs = "G2", GermanWordPart = "Wo", MappingCreatedAt = Created });

        var readings = (await _sut.GetAsync(1))!.Readings;

        readings[0].GermanWordPart.ShouldBeNull();
        readings[1].GermanWordPart.ShouldBe("Wo");
    }

    [Fact]
    public async Task GetAsync_OtWord_BuildsHebrewReadingWithMorphemes()
    {
        GivenWord(500, 1, "Im");
        GivenHebrew(500, new HebrewReadingRow
        {
            StepWordId = 77, PositionInVerse = 1, Original = "בְּרֵאשִׁית", Transliteration = "bere'shit",
            Strongs = "H7225G", DisambiguatedStrongs = "H9003/{H7225G}", ContextGloss = "in beginning",
            MorphologyCode = "HR/Ncfsa", MappingCreatedAt = Created
        });
        _morphemes.GetByStepWordIdsAsync(Arg.Any<int[]>(), Arg.Any<CancellationToken>()).Returns([
            new HebrewMorpheme { StepHebrewWordsId = 77, PositionInWord = 2, IsRoot = true, Hebrew = "רֵאשִׁית", StrongsNumber = "H7225G" },
            new HebrewMorpheme { StepHebrewWordsId = 77, PositionInWord = 1, IsRoot = false, Hebrew = "בְּ", StrongsNumber = "H9003" },
        ]);
        _lexicon.GetEntriesAsync(Arg.Is<StrongsNumber>(s => s.Number == 7225 && s.Extra == "G"), Arg.Any<CancellationToken>()).Returns([
            new LexiconEntry { Extra = "F", Number = 7225, OriginalWord = "wrong", Transliteration = "t", Gloss = "g" },
            new LexiconEntry { Extra = "G", Number = 7225, OriginalWord = "רֵאשִׁית", Transliteration = "re'shit", Gloss = "beginning" },
        ]);

        var reading = (await _sut.GetAsync(500))!.Readings.Single();

        reading.Language.ShouldBe("hebrew");
        reading.Strongs.ShouldBe("H7225G");
        reading.Lemma.ShouldBe("רֵאשִׁית");
        reading.Gloss.ShouldBe("beginning");
        reading.ContextGloss.ShouldBe("in beginning");
        reading.Morphology!.PartOfSpeech.ShouldBe("noun");
        reading.Morphology.Code.ShouldBe("HR/Ncfsa");
        reading.Lexicon!.Lemma.ShouldBe("רֵאשִׁית");
        reading.Morphemes!.Select(m => m.Role).ShouldBe(["prefix", "root"]);
        reading.Morphemes[0].Morphology!.PartOfSpeech.ShouldBe("preposition");
        reading.Morphemes[1].Morphology!.PartOfSpeech.ShouldBe("noun");
    }

    [Fact]
    public async Task GetAsync_AramaicCode_IsAramaic()
    {
        GivenWord(500, 27, "König");
        GivenHebrew(500, new HebrewReadingRow
        {
            StepWordId = 1, PositionInVerse = 1, Original = "מֶלֶךְ", Strongs = "H4430", MorphologyCode = "ANcmsd", MappingCreatedAt = Created
        });

        (await _sut.GetAsync(500))!.Readings.Single().Language.ShouldBe("aramaic");
    }

    [Fact]
    public async Task GetAsync_StrongsThatFailsToParse_GivesReadingWithoutLexicon()
    {
        GivenWord(1, 43, "x");
        GivenGreek(1, new GreekReadingRow { StepWordId = 1, PositionInVerse = 1, Original = "α", Strongs = "not-a-number", MappingCreatedAt = Created });

        var reading = (await _sut.GetAsync(1))!.Readings.Single();

        reading.Lexicon.ShouldBeNull();
        await _lexicon.DidNotReceive().GetEntriesAsync(Arg.Any<StrongsNumber>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_LexiconWithoutExtra_PrefersEntryWithoutExtra()
    {
        GivenWord(1, 43, "x");
        GivenGreek(1, new GreekReadingRow { StepWordId = 1, PositionInVerse = 1, Original = "α", Strongs = "G26", MappingCreatedAt = Created });
        _lexicon.GetEntriesAsync(Arg.Any<StrongsNumber>(), Arg.Any<CancellationToken>()).Returns([
            new LexiconEntry { Extra = "A", OriginalWord = "with-extra", Transliteration = "", Gloss = "" },
            new LexiconEntry { Extra = null, OriginalWord = "plain", Transliteration = "", Gloss = "" },
        ]);

        (await _sut.GetAsync(1))!.Readings.Single().Lexicon!.Lemma.ShouldBe("plain");
    }

    [Fact]
    public async Task GetAsync_AddedWord_HasNoReadingsAndLinksItsParent()
    {
        GivenWord(7, 43, "das", position: 4);
        GivenGreek(7, new GreekReadingRow { IsAddedWord = true, StepWordId = null, MappingCreatedAt = Created });
        _words.GetCoMappedWordsAsync(7, false, Arg.Any<CancellationToken>()).Returns([
            new CoMappedWordRow { WordId = 8, WordInContext = "Wort", PositionInVerse = 5, Relation = "parent" }
        ]);

        var detail = await _sut.GetAsync(7);

        detail!.IsAddedWord.ShouldBe(true);
        detail.Readings.ShouldBeEmpty();
        detail.Mapping.ShouldNotBeNull();
        detail.CoMappedWords!.ShouldHaveSingleItem().Relation.ShouldBe("parent");
    }

    [Fact]
    public async Task GetAsync_NoMappingRows_HasNoMappingNoCoMappedAndNoHints()
    {
        GivenWord(1, 43, "x");
        GivenGreek(1);

        var detail = await _sut.GetAsync(1);

        detail!.Readings.ShouldBeEmpty();
        detail.Mapping.ShouldBeNull();
        detail.CoMappedWords.ShouldBeNull();
        detail.Hints.ShouldBeNull();
        detail.IsAddedWord.ShouldBeNull();
        await _words.DidNotReceive().GetCoMappedWordsAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_SharedOriginal_FillsCoMappedWordsAndSharedWithWordIds()
    {
        GivenWord(2, 1, "Erde", position: 4);
        GivenHebrew(2, new HebrewReadingRow { StepWordId = 90, PositionInVerse = 4, Original = "וְהָאָרֶץ", Strongs = "H776", MorphologyCode = "HC/Td/Ncbsa", MappingCreatedAt = Created });
        _words.GetCoMappedWordsAsync(2, true, Arg.Any<CancellationToken>()).Returns([
            new CoMappedWordRow { WordId = 3, WordInContext = "die", PositionInVerse = 3, Relation = "sameOriginal", StepWordId = 90 },
            new CoMappedWordRow { WordId = 1, WordInContext = "Und", PositionInVerse = 2, Relation = "sameOriginal", StepWordId = 90 },
            new CoMappedWordRow { WordId = 1, WordInContext = "Und", PositionInVerse = 2, Relation = "addedWord" },
        ]);

        var detail = await _sut.GetAsync(2);

        detail!.CoMappedWords!.Select(c => (c.WordId, c.Relation)).ShouldBe([(1, "addedWord"), (3, "sameOriginal")]);
        detail.Readings.Single().SharedWithWordIds.ShouldBe([1, 3]);
    }
}
