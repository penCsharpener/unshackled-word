using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.TestTooling;

namespace UnshackledWord.Infrastructure.Tests;

public class StrongsStatsRepositoryTests
{
    private static readonly string[] Keys = ["G3004G", "G3004H"];

    private const string Greek = "\"StepGreekWords\"";
    private const string Hebrew = "\"StepHebrewWords\"";

    private static void ShouldQueryTestament(FakeDbReader reader, bool hebrew)
    {
        reader.LastSql.ShouldContain(hebrew ? Hebrew : Greek);
        reader.LastSql.ShouldNotContain(hebrew ? Greek : Hebrew);
        reader.LastSql.ShouldContain("@Keys");
        ((string[])reader.LastParam("Keys")!).ShouldBe(Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetBookCountsAsync_PicksTheTablesOfTheTestament(bool hebrew)
    {
        var reader = new FakeDbReader().Returns(new StrongsBookCountRow { BibleBookId = 40, Count = 3, VerseCount = 2 });

        var rows = await new StrongsStatsRepository(reader).GetBookCountsAsync(Keys, hebrew);

        rows.Single().Count.ShouldBe(3);
        ShouldQueryTestament(reader, hebrew);
        reader.LastSql.ShouldContain("\"Elb1871Verses\"");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetKeyCountsAsync_GroupsByKey(bool hebrew)
    {
        var reader = new FakeDbReader().Returns(new StrongsKeyCountRow { Strongs = "G3004G", Count = 3 });

        (await new StrongsStatsRepository(reader).GetKeyCountsAsync(Keys, hebrew)).Single().Strongs.ShouldBe("G3004G");

        ShouldQueryTestament(reader, hebrew);
        reader.LastSql.ShouldContain("GROUP BY");
    }

    [Theory]
    [InlineData(false, "\"Elb1871GreekMapping\"", "\"Elb1871HebrewMapping\"")]
    [InlineData(true, "\"Elb1871HebrewMapping\"", "\"Elb1871GreekMapping\"")]
    public async Task GetRenderingsAsync_UsesTheMappingOfTheTestamentAndTheGermanKey(bool hebrew, string mapping, string other)
    {
        var reader = new FakeDbReader().Returns(new StrongsRenderingRow { Word = "Versammlung", Key = "versammlung", Count = 77 });

        (await new StrongsStatsRepository(reader).GetRenderingsAsync(Keys, hebrew)).Single().Key.ShouldBe("versammlung");

        ShouldQueryTestament(reader, hebrew);
        reader.LastSql.ShouldContain(mapping);
        reader.LastSql.ShouldNotContain(other);
        reader.LastSql.ShouldContain("NULLIF(ew.\"Lemma\", '')");
        reader.LastSql.ShouldContain("NOT ");
    }

    [Theory]
    [InlineData(false, "sgw.\"Morphology\" = ANY(@Codes)")]
    [InlineData(true, "shw.\"Grammar\" = ANY(@Codes)")]
    public async Task OccurrencesAsync_RestrictToTheMorphologyCodes(bool hebrew, string condition)
    {
        var filter = new StrongsOccurrenceFilter(Codes: ["N-NSF"]);
        var count = new FakeDbReader().Returns(5);
        await new StrongsStatsRepository(count).CountOccurrencesAsync(Keys, hebrew, filter);
        count.LastSql.ShouldContain(condition);
        ((string[])count.LastParam("Codes")!).ShouldBe(["N-NSF"]);

        var page = new FakeDbReader().Returns(new StrongsOccurrenceRow());
        await new StrongsStatsRepository(page).GetOccurrencesAsync(Keys, hebrew, filter, 0, 50);
        page.LastSql.ShouldContain(condition);
        ((string[])page.LastParam("Codes")!).ShouldBe(["N-NSF"]);
    }

    [Theory]
    [InlineData(false, "\"Elb1871GreekMapping\"")]
    [InlineData(true, "\"Elb1871HebrewMapping\"")]
    public async Task GetMappedCountAsync_CountsDistinctOriginalWords(bool hebrew, string mapping)
    {
        var reader = new FakeDbReader().Returns(113);

        (await new StrongsStatsRepository(reader).GetMappedCountAsync(Keys, hebrew)).ShouldBe(113);

        ShouldQueryTestament(reader, hebrew);
        reader.LastSql.ShouldContain(mapping);
        reader.LastSql.ShouldContain("COUNT(DISTINCT");
    }

    [Fact]
    public async Task GetWordBookCountsAsync_PassesWordAndBookRange()
    {
        var reader = new FakeDbReader().Returns(new WordBookCountRow { BibleBookId = 19, Count = 4 });

        (await new StrongsStatsRepository(reader).GetWordBookCountsAsync("versammlung", 1, 39)).Single().Count.ShouldBe(4);

        reader.LastSql.ShouldContain("\"Elb1871Words\"");
        reader.LastSql.ShouldContain("\"Elb1871GreekMapping\"");
        reader.LastSql.ShouldContain("\"Elb1871HebrewMapping\"");
        reader.LastParam("Word").ShouldBe("versammlung");
        reader.LastParam("FirstBook").ShouldBe(1);
        reader.LastParam("LastBook").ShouldBe(39);
    }

    [Fact]
    public async Task GetWordOriginalsAsync_ReadsBothTestamentsAndTheLexicon()
    {
        var reader = new FakeDbReader().Returns(new WordOriginalRow { Strongs = "H6951", Count = 105, TotalCount = 123 });

        (await new StrongsStatsRepository(reader).GetWordOriginalsAsync("versammlung", 1, 66)).Single().TotalCount.ShouldBe(123);

        reader.LastSql.ShouldContain(Greek);
        reader.LastSql.ShouldContain(Hebrew);
        reader.LastSql.ShouldContain("\"StepStrongsLexicon\"");
        reader.LastParam("Word").ShouldBe("versammlung");
        reader.LastParam("FirstBook").ShouldBe(1);
        reader.LastParam("LastBook").ShouldBe(66);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CountOccurrencesAsync_PassesKeysBookAndRendering(bool hebrew)
    {
        var reader = new FakeDbReader().Returns(20);

        var count = await new StrongsStatsRepository(reader).CountOccurrencesAsync(Keys, hebrew, new StrongsOccurrenceFilter(44, "versammlung"));

        count.ShouldBe(20);
        ShouldQueryTestament(reader, hebrew);
        reader.LastSql.ShouldContain("@Book::int");
        reader.LastSql.ShouldContain("@Rendering::text");
        reader.LastParam("Book").ShouldBe(44);
        reader.LastParam("Rendering").ShouldBe("versammlung");
    }

    [Theory]
    [InlineData(false, "\"Elb1871GreekMapping\"")]
    [InlineData(true, "\"Elb1871HebrewMapping\"")]
    public async Task GetOccurrencesAsync_PagesWithTheFilter(bool hebrew, string mapping)
    {
        var reader = new FakeDbReader().Returns(new StrongsOccurrenceRow { BibleBookId = 44, Chapter = 5, Verse = 11 });

        var rows = await new StrongsStatsRepository(reader).GetOccurrencesAsync(Keys, hebrew, StrongsOccurrenceFilter.None, 100, 50);

        rows.Count.ShouldBe(1);
        ShouldQueryTestament(reader, hebrew);
        reader.LastSql.ShouldContain(mapping);
        reader.LastSql.ShouldContain("\"Elb1871Verses\"");
        reader.LastSql.ShouldContain("LIMIT @Take OFFSET @Skip");
        reader.LastParam("Book").ShouldBeNull();
        reader.LastParam("Rendering").ShouldBeNull();
        reader.LastParam("Skip").ShouldBe(100);
        reader.LastParam("Take").ShouldBe(50);
    }

    [Theory]
    [InlineData(false, "G")]
    [InlineData(true, "H")]
    public async Task GetSnapshotStrongsAsync_ReadsTheStatsView_ForTheLanguage(bool hebrew, string lang)
    {
        var reader = new FakeDbReader().Returns(new SnapshotStrongsRow { Strongs = "G3004G", Occurrences = 3 });

        var rows = await new StrongsStatsRepository(reader).GetSnapshotStrongsAsync(Keys, hebrew);

        rows.Single().Occurrences.ShouldBe(3);
        reader.LastSql.ShouldContain("\"StatsStrongs\"");
        reader.LastSql.ShouldNotContain("DqWordFacts");
        reader.LastParam("Lang").ShouldBe(lang);
        ((string[])reader.LastParam("Keys")!).ShouldBe(Keys);
    }

    [Fact]
    public async Task GetSnapshotRenderingsAsync_GroupsByLemmaAndPos_OverTheKeys()
    {
        var reader = new FakeDbReader().Returns(new SnapshotRenderingRow { Word = "gehen", Key = "gehen", Pos = "VERB", Count = 2 });

        await new StrongsStatsRepository(reader).GetSnapshotRenderingsAsync(Keys, false);

        reader.LastSql.ShouldContain("\"StatsStrongsRenderings\"");
        reader.LastSql.ShouldContain("GROUP BY r.\"GermanKey\", r.\"Pos\"");
    }

    [Fact]
    public async Task GetSnapshotRenderingsAsync_CarriesTheNoOwnLemmaFlag()
    {
        var reader = new FakeDbReader().Returns(new SnapshotRenderingRow { Word = "", Key = "", Count = 4, NoOwnLemma = true });

        (await new StrongsStatsRepository(reader).GetSnapshotRenderingsAsync(Keys, false)).Single().NoOwnLemma.ShouldBeTrue();

        reader.LastSql.ShouldContain("bool_and(r.\"NoOwnLemma\") \"NoOwnLemma\"");
    }

    [Fact]
    public async Task GetLinkRolesAppliedAtAsync_ReadsTheRefreshState()
    {
        var at = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc);
        var reader = new FakeDbReader().Returns<DateTime?>(at);

        (await new StrongsStatsRepository(reader).GetLinkRolesAppliedAtAsync()).ShouldBe(at);

        reader.LastSql.ShouldContain("\"LinkRolesAppliedAt\"");
        reader.LastSql.ShouldContain("\"DqRefreshState\"");
    }

    [Fact]
    public async Task ResolveSnapshotLemmasAsync_UsesTheFormTable_AndTheLemmaOnlyAsFallback()
    {
        var reader = new FakeDbReader().Returns(new SnapshotLemmaRow { Key = "gehen", Pos = "VERB" });

        await new StrongsStatsRepository(reader).ResolveSnapshotLemmasAsync("ging");

        reader.LastSql.ShouldContain("\"StatsGermanLemma\"");
        reader.LastSql.ShouldContain("\"StatsGermanForms\"");
        reader.LastSql.ShouldContain("NOT EXISTS (SELECT 1 FROM \"unshackled-word\".\"StatsGermanForms\"");
        reader.LastParam("Word").ShouldBe("ging");
    }

    [Fact]
    public async Task GetSnapshotLemmaOriginalsAsync_PassesLemmaPairsAndTop()
    {
        var reader = new FakeDbReader().Returns(new SnapshotLemmaOriginalRow { Key = "gehen", Strongs = "G4198" });

        await new StrongsStatsRepository(reader).GetSnapshotLemmaOriginalsAsync([new SnapshotLemmaKey("gehen", "VERB"), new SnapshotLemmaKey("und", null)], 3);

        ((string[])reader.LastParam("Keys")!).ShouldBe(["gehen", "und"]);
        ((string?[])reader.LastParam("Poss")!).ShouldBe(["VERB", null]);
        reader.LastParam("Top").ShouldBe(3);
        reader.LastSql.ShouldContain("IS NOT DISTINCT FROM");
    }
}
