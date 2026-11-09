using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.TestTooling;

namespace UnshackledWord.Infrastructure.Tests;

public class WordDetailRepositoryTests
{
    [Fact]
    public async Task GetWordAsync_QueriesWordsTableWithParameter()
    {
        var reader = new FakeDbReader().Returns(new ElbWordRow { Id = 26001, WordInContext = "Wort" });

        var word = await new WordDetailRepository(reader).GetWordAsync(26001);

        word!.WordInContext.ShouldBe("Wort");
        reader.LastSql.ShouldContain("\"Elb1871Words\"");
        reader.LastSql.ShouldContain("@WordId");
        reader.LastParam("WordId").ShouldBe(26001);
    }

    [Fact]
    public async Task GetGreekReadingsAsync_QueriesGreekMappingWithParameter()
    {
        var reader = new FakeDbReader().Returns(new GreekReadingRow { StepWordId = 5 });

        var rows = await new WordDetailRepository(reader).GetGreekReadingsAsync(42);

        rows.Count.ShouldBe(1);
        reader.LastSql.ShouldContain("\"Elb1871GreekMapping\"");
        reader.LastSql.ShouldContain("\"StepGreekMorphology\"");
        reader.LastSql.ShouldContain("@WordId");
        reader.LastParam("WordId").ShouldBe(42);
    }

    [Fact]
    public async Task GetHebrewReadingsAsync_QueriesHebrewMappingWithParameter()
    {
        var reader = new FakeDbReader().Returns<HebrewReadingRow>();

        var rows = await new WordDetailRepository(reader).GetHebrewReadingsAsync(42);

        rows.ShouldBeEmpty();
        reader.LastSql.ShouldContain("\"Elb1871HebrewMapping\"");
        reader.LastSql.ShouldContain("@WordId");
        reader.LastParam("WordId").ShouldBe(42);
    }

    [Theory]
    [InlineData(false, "\"Elb1871GreekMapping\"", "\"Elb1871HebrewMapping\"")]
    [InlineData(true, "\"Elb1871HebrewMapping\"", "\"Elb1871GreekMapping\"")]
    public async Task GetCoMappedWordsAsync_PicksTheMappingTableOfTheTestament(bool hebrew, string expected, string notExpected)
    {
        var reader = new FakeDbReader();

        await new WordDetailRepository(reader).GetCoMappedWordsAsync(9, hebrew);

        reader.LastSql.ShouldContain(expected);
        reader.LastSql.ShouldNotContain(notExpected);
        reader.LastSql.ShouldContain("@WordId");
        reader.LastParam("WordId").ShouldBe(9);
    }

    [Theory]
    [InlineData(false, "\"StepGreekWords\"", "\"StepHebrewWords\"")]
    [InlineData(true, "\"StepHebrewWords\"", "\"StepGreekWords\"")]
    public async Task GetOccurrenceCountAsync_CountsInTheTestamentOfTheStrongs(bool hebrew, string expected, string notExpected)
    {
        var reader = new FakeDbReader().Returns(137);

        var count = await new WordDetailRepository(reader).GetOccurrenceCountAsync("G3056", hebrew);

        count.ShouldBe(137);
        reader.LastSql.ShouldContain(expected);
        reader.LastSql.ShouldNotContain(notExpected);
        reader.LastParam("Strongs").ShouldBe("G3056");
    }

    [Theory]
    [InlineData(false, "\"Elb1871GreekMapping\"", "\"Elb1871HebrewMapping\"")]
    [InlineData(true, "\"Elb1871HebrewMapping\"", "\"Elb1871GreekMapping\"")]
    public async Task GetRenderingsAsync_PicksTheMappingTableAndPassesLimit(bool hebrew, string expected, string notExpected)
    {
        var reader = new FakeDbReader().Returns(new RenderingRow { Word = "Wort", Count = 3 });

        var rows = await new WordDetailRepository(reader).GetRenderingsAsync("G3056", hebrew, 5);

        rows.Single().Word.ShouldBe("Wort");
        reader.LastSql.ShouldContain(expected);
        reader.LastSql.ShouldNotContain(notExpected);
        reader.LastParam("Strongs").ShouldBe("G3056");
        reader.LastParam("Limit").ShouldBe(5);
    }

    [Fact]
    public async Task HebrewMorphemeRepository_PassesIdsAndSkipsEmptyInput()
    {
        var reader = new FakeDbReader().Returns(new HebrewMorpheme { StepHebrewWordsId = 3 });
        var repository = new HebrewMorphemeRepository(reader);

        (await repository.GetByStepWordIdsAsync([])).ShouldBeEmpty();
        reader.Calls.ShouldBeEmpty();

        var rows = await repository.GetByStepWordIdsAsync([3, 4]);

        rows.Count.ShouldBe(1);
        reader.LastSql.ShouldContain("\"StepHebrewWordsNormalizedToHebrewWords\"");
        reader.LastSql.ShouldContain("@StepHebrewWordIds");
        reader.LastParam("StepHebrewWordIds").ShouldBe(new[] { 3, 4 });
    }
}

public class WordDetailRepositoryOccurrencesTests
{
    [Theory]
    [InlineData(false, "\"StepGreekWords\"", "\"StepHebrewWords\"", "\"Elb1871GreekMapping\"")]
    [InlineData(true, "\"StepHebrewWords\"", "\"StepGreekWords\"", "\"Elb1871HebrewMapping\"")]
    public async Task GetOccurrencesAsync_PicksTheTablesOfTheTestamentAndPages(bool hebrew, string expected, string notExpected, string mapping)
    {
        var reader = new FakeDbReader().Returns(new StrongsOccurrenceRow { BibleBookId = 40, Chapter = 24, Verse = 12 });

        var rows = await new WordDetailRepository(reader).GetOccurrencesAsync("G0026", hebrew, 100, 50);

        rows.Count.ShouldBe(1);
        reader.LastSql.ShouldContain(expected);
        reader.LastSql.ShouldNotContain(notExpected);
        reader.LastSql.ShouldContain(mapping);
        reader.LastSql.ShouldContain("\"Elb1871Verses\"");
        reader.LastSql.ShouldContain("\"Elb1871Words\"");
        reader.LastSql.ShouldContain("LIMIT @Take OFFSET @Skip");
        reader.LastParam("Strongs").ShouldBe("G0026");
        reader.LastParam("Skip").ShouldBe(100);
        reader.LastParam("Take").ShouldBe(50);
    }
}
