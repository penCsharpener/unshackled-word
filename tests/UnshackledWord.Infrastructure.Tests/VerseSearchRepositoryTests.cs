using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.TestTooling;

namespace UnshackledWord.Infrastructure.Tests;

public class VerseSearchRepositoryTests
{
    [Fact]
    public async Task GetLexemesAsync_StemsWithTheDictionaryWithoutStopWords()
    {
        var reader = new FakeDbReader().Returns(new VerseSearchLexemeRow { Word = "ich", Lexeme = "ich" });

        (await new VerseSearchRepository(reader).GetLexemesAsync(["ich"])).Single().Lexeme.ShouldBe("ich");

        reader.LastSql.ShouldContain("ts_lexize");
        reader.LastSql.ShouldContain("uw_german_stem");
        ((string[])reader.LastParam("Words")!).ShouldBe(["ich"]);
    }

    [Fact]
    public async Task GetBookCountsAsync_GroupsAllMatchesByBook_WithoutBookFilterOrPaging()
    {
        var reader = new FakeDbReader().Returns(new VerseSearchBookCountRow { BookId = 43, Count = 17 });

        await new VerseSearchRepository(reader).GetBookCountsAsync("(('gott'))");

        reader.LastSql.ShouldContain("GROUP BY v.\"HebRefId\" / 1000000");
        reader.LastSql.ShouldContain("CAST(@Query AS tsquery)");
        reader.LastSql.ShouldNotContain("LIMIT");
        reader.LastSql.ShouldNotContain("@FirstRefId");
        reader.LastParam("Query").ShouldBe("(('gott'))");
    }

    [Fact]
    public async Task GetHitsAsync_OrdersLiteralFirst_ThenRank_ThenBibleOrder_AndNeverConcatenatesTheQuery()
    {
        var reader = new FakeDbReader().Returns(new VerseSearchHitRow { HebRefId = 43003016 });

        await new VerseSearchRepository(reader).GetHitsAsync("(('ging' | 'gehen':B))", "(('ging'))", 43_000_000, 43_999_999, 20, 10);

        reader.LastSql.ShouldContain("ORDER BY (v.\"SearchVector\" @@ CAST(@Literal AS tsquery)) DESC");
        reader.LastSql.ShouldContain("ts_rank_cd(v.\"SearchVector\", CAST(@Query AS tsquery)) DESC");
        reader.LastSql.ShouldContain("v.\"HebRefId\"\n");
        reader.LastSql.ShouldContain("LIMIT @Limit OFFSET @Offset");
        reader.LastSql.ShouldNotContain("gehen");
        reader.LastParam("Query").ShouldBe("(('ging' | 'gehen':B))");
        reader.LastParam("Literal").ShouldBe("(('ging'))");
        reader.LastParam("FirstRefId").ShouldBe(43_000_000);
        reader.LastParam("Offset").ShouldBe(20);
        reader.LastParam("Limit").ShouldBe(10);
    }

    [Fact]
    public async Task GetHitsAsync_WithoutLiteralQuery_RanksOnly()
    {
        var reader = new FakeDbReader().Returns(new VerseSearchHitRow { HebRefId = 1001001 });

        await new VerseSearchRepository(reader).GetHitsAsync("(('gott'))", null, 0, int.MaxValue, 0, 20);

        reader.LastSql.ShouldNotContain("@Literal");
        reader.LastSql.ShouldContain("ORDER BY ts_rank_cd(v.\"SearchVector\", CAST(@Query AS tsquery)) DESC");
    }

    [Fact]
    public async Task GetWordsAsync_ReadsTheStemsAndTheMorphologyLemmaOfTheVerses()
    {
        var reader = new FakeDbReader().Returns(new VerseSearchWordRow { HebRefId = 43003016, WordInContext = "Gott", Stems = ["gott"], Lemma = "gott" });

        await new VerseSearchRepository(reader).GetWordsAsync([43003016]);

        reader.LastSql.ShouldContain("tsvector_to_array(to_tsvector('\"unshackled-word\".uw_german'::regconfig, w.\"PlainWord\"))");
        reader.LastSql.ShouldContain("LEFT JOIN \"unshackled-word\".\"Elb1871Morphology\"");
        ((int[])reader.LastParam("HebRefIds")!).ShouldBe([43003016]);
    }
}
