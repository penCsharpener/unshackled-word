using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Scoring;
using UnshackledWord.Domain.WebApi.Maintainer;
using static UnshackledWord.Application.Tests.Features.AgentMapping.Scoring.ScoringTestData;

namespace UnshackledWord.Application.Tests.Features.AgentMapping.Scoring;

public class AgentReferenceFreezeTests
{
    private static readonly TimeProvider Clock = new FixedClock(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static AgentReferenceFreezer Freezer(AgentRangeData? range = null)
    {
        var data = Substitute.For<IAgentDataRepository>();
        data.GetRangeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<int>(2) == 17 ? range ?? Range() : new AgentRangeData()));

        return new AgentReferenceFreezer(data, Clock);
    }

    private static FreezeAgentReferenceRequest Request(params (int Book, int Chapter, int Verse)[] verses) => new()
    {
        Version = "v1",
        Verses = [.. verses.Select(v => new AgentReferenceVerseKey { BibleBookId = v.Book, Chapter = v.Chapter, Verse = v.Verse })],
    };

    [Fact]
    public async Task Freeze_ExportsLinksRolesLemmasAndAddedWords_AndRoundTripsThroughTheFile()
    {
        var result = await Freezer().FreezeAsync(Request((Book, 1, 17)));

        result.Ok.ShouldBeTrue(string.Join("; ", result.Errors));
        var verse = result.Reference!.Verses.Single();
        verse.Language.ShouldBe("greek");
        verse.Words.Select(w => (w.Pos, w.Lemma, w.Stts)).ShouldBe([(1, "und", "KON"), (2, "Jesus", "NE"), (3, "sprechen", "VVFIN"), (4, "er", "PPER")]);
        verse.Links.Select(l => (l.G, l.SRef, l.S, l.Role)).ShouldBe([(1, OriginalRef, 1, "head"), (2, OriginalRef, 2, "head"), (3, OriginalRef, 3, "head")]);
        verse.Added.Single().ShouldSatisfyAllConditions(a => a.G.ShouldBe(4), a => a.Parent.ShouldBe(3));

        var json = ReferenceSetFile.Serialize(result.Reference);
        json.ShouldContain("\"role\": \"head\"");
        json.ShouldNotContain("morpheme");
        var back = ReferenceSetFile.Parse(json);
        ReferenceSetFile.Validate(back).ShouldBeEmpty();
        back.Version.ShouldBe("v1");
        back.FrozenAt.ShouldBe(Clock.GetUtcNow());
        ReferenceSetFile.Serialize(back).ShouldBe(json);

        // A run that changes nothing agrees with its own frozen reference.
        Score(Input(back.Verses.Single())).BulkMerge.Meets.ShouldBeTrue();
    }

    [Fact]
    public async Task Freeze_KeepsRoleMorphemePartAndPartLemma_OfHebrewLinks()
    {
        var range = new AgentRangeData
        {
            GermanWords =
            [
                new AgentGermanWordRow { Id = 1, Chapter = 2, Verse = 17, HebRefId = 8002017, PositionInVerse = 1, Text = "Und", Plain = "Und", Lemma = "und", Stts = "KON" },
                new AgentGermanWordRow { Id = 2, Chapter = 2, Verse = 17, HebRefId = 8002017, PositionInVerse = 2, Text = "sprach", Plain = "sprach", Lemma = "sprechen", Stts = "VVFIN" },
                new AgentGermanWordRow { Id = 3, Chapter = 2, Verse = 17, HebRefId = 8002017, PositionInVerse = 3, Text = "Gerstenernte", Plain = "Gerstenernte", Lemma = "Gerstenernte", Stts = "NN" },
            ],
            SourceWords =
            [
                new AgentSourceWordRow { Id = 10, RefId = 8002017, Position = 1, Text = "וַיֹּאמֶר", ExpandedStrongTags = "H9001=ו=and/{H0559=אָמַר=to say}" },
                new AgentSourceWordRow { Id = 11, RefId = 8002017, Position = 2, Text = "שְׂעֹרִים", ExpandedStrongTags = "{H8184=שְׂעֹרָה=barley}" },
            ],
            Mappings =
            [
                new AgentMappingRow { ElbWordId = 1, StepWordId = 10, StrongsNumber = "H9001" },
                new AgentMappingRow { ElbWordId = 2, StepWordId = 10 },
                new AgentMappingRow { ElbWordId = 3, StepWordId = 11, PartOrder = 1, GermanWordPart = "Gersten", PartLemma = "Gerste" },
            ],
        };
        var data = Substitute.For<IAgentDataRepository>();
        data.GetRangeAsync(8, 2, 17, 2, 17, Arg.Any<CancellationToken>()).Returns(range);

        var result = await new AgentReferenceFreezer(data, Clock).FreezeAsync(Request((8, 2, 17)));

        result.Ok.ShouldBeTrue(string.Join("; ", result.Errors));
        var links = result.Reference!.Verses.Single().Links;
        links.Single(l => l.G == 1).Morpheme.ShouldBe("H9001");
        links.Single(l => l.G == 2).Morpheme.ShouldBe("H0559"); // main morpheme spelled out
        links.Single(l => l.G == 3).ShouldSatisfyAllConditions(l => l.Part.ShouldBe(1), l => l.PartText.ShouldBe("Gersten"), l => l.PartLemma.ShouldBe("Gerste"));
        result.Reference.Verses.Single().Language.ShouldBe("hebrew");
    }

    [Theory]
    [InlineData(41, 1, 35)]
    [InlineData(1, 1, 1)]
    [InlineData(8, 1, 22)]
    public async Task Freeze_RejectsThePromptExampleVerses(int book, int chapter, int verse)
    {
        var result = await Freezer().FreezeAsync(Request((Book, 1, 17), (book, chapter, verse)));

        result.Ok.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().ShouldContain("worked example");
    }

    [Fact]
    public void AllTenPromptExamples_AreRejectedByTheFileValidation_Too()
    {
        ReferenceSetFile.PromptExampleVerses.Count.ShouldBe(10);
        var set = new ReferenceSet();
        foreach (var (book, chapter, verse) in ReferenceSetFile.PromptExampleVerses)
        {
            set.Verses.Add(new ReferenceVerse { Book = book, Chapter = chapter, Verse = verse, Language = "greek", Words = [new ReferenceWord { Pos = 1, Text = "x" }] });
        }

        ReferenceSetFile.Validate(set).Count(e => e.Contains("worked example")).ShouldBe(10);
    }

    [Fact]
    public async Task Freeze_RejectsDuplicatesUnknownVersesAndABadVersionName()
    {
        var duplicate = await Freezer().FreezeAsync(Request((Book, 1, 17), (Book, 1, 17)));
        duplicate.Errors.ShouldContain(e => e.Contains("listed twice"));

        var unknown = await Freezer().FreezeAsync(Request((Book, 1, 99)));
        unknown.Errors.ShouldContain(e => e.Contains("does not exist"));

        var badName = await Freezer().FreezeAsync(new FreezeAgentReferenceRequest { Version = "../x", Verses = [new AgentReferenceVerseKey { BibleBookId = Book, Chapter = 1, Verse = 17 }] });
        badName.Errors.ShouldContain(e => e.Contains("version"));

        (await Freezer().FreezeAsync(Request())).Errors.ShouldContain("verses is empty.");
    }

    [Fact]
    public async Task Freeze_WarnsAboutWordsWithoutLemma()
    {
        var range = Range();
        range.GermanWords[2].Lemma = null;

        var result = await Freezer(range).FreezeAsync(Request((Book, 1, 17)));

        result.Ok.ShouldBeTrue();
        result.Warnings.ShouldContain(w => w.Contains("word 3") && w.Contains("no lemma"));
    }

    [Fact]
    public void ReferenceStore_LoadsTheNewestFile_OrTheNamedOne()
    {
        var dir = Path.Combine(Path.GetTempPath(), "agent-reference-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new AgentReferenceStore(dir);
            store.Load(null).ShouldBeNull();

            File.WriteAllText(Path.Combine(dir, "reference-v1.json"), ReferenceSetFile.Serialize(new ReferenceSet { Version = "v1" }));
            File.WriteAllText(Path.Combine(dir, "reference-v10.json"), ReferenceSetFile.Serialize(new ReferenceSet { Version = "v10" }));
            File.WriteAllText(Path.Combine(dir, "reference-v2.json"), ReferenceSetFile.Serialize(new ReferenceSet { Version = "v2" }));

            store.Load(null)!.Version.ShouldBe("v10");
            store.Load("v1")!.Version.ShouldBe("v1");
            store.Load("reference-v2.json")!.Version.ShouldBe("v2");
            store.Load("../../etc/passwd").ShouldBeNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
