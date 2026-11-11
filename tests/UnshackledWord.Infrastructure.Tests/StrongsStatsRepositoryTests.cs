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
}
