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

        await new VerseSearchRepository(reader).GetBookCountsAsync("(('gott'))", 0);

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

        await new VerseSearchRepository(reader).GetHitsAsync("(('ging' | 'gehen':B))", "(('ging'))", 0, 43_000_000, 43_999_999, 20, 10);

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

        await new VerseSearchRepository(reader).GetHitsAsync("(('gott'))", null, 0, 0, int.MaxValue, 0, 20);

        reader.LastSql.ShouldNotContain("@Literal");
        reader.LastSql.ShouldContain("ORDER BY ts_rank_cd(v.\"SearchVector\", CAST(@Query AS tsquery)) DESC");
    }

    [Fact]
    public async Task GetHitsAsync_AtBaseLevel_NeverTouchesTheRevisionTables()
    {
        var reader = new FakeDbReader().Returns(new VerseSearchHitRow { HebRefId = 1001001 });

        await new VerseSearchRepository(reader).GetHitsAsync("(('gott'))", null, 0, 0, int.MaxValue, 0, 20);

        reader.LastSql.ShouldNotContain("Revision");
    }

    [Fact]
    public async Task GetHitsAsync_AtOtherLevel_MatchesBaseOrEffectiveVariant_OnePerVerse_PreferringTheVariant()
    {
        var reader = new FakeDbReader().Returns(new VerseSearchHitRow { HebRefId = 43003016, RevisionVerseId = 7, Level = 200 });

        var rows = await new VerseSearchRepository(reader).GetHitsAsync("(('einzig'))", "(('einzig'))", 200, 43_000_000, 43_999_999, 0, 20);

        rows.Single().RevisionVerseId.ShouldBe(7);
        reader.LastSql.ShouldContain("LATERAL");
        reader.LastSql.ShouldContain("v.\"SearchVector\" @@ CAST(@Query AS tsquery) OR rv.\"SearchVector\" @@ CAST(@Query AS tsquery)");
        reader.LastSql.ShouldContain("CASE WHEN rv.\"SearchVector\" @@ CAST(@Query AS tsquery) THEN rv.\"Id\" END \"RevisionVerseId\"");
        reader.LastSql.ShouldContain("ORDER BY (c.\"Vector\" @@ CAST(@Literal AS tsquery)) DESC");
        reader.LastSql.ShouldContain("LIMIT @Limit OFFSET @Offset");
        reader.LastParam("Level").ShouldBe(200);
        reader.LastParam("FromRefId").ShouldBe(43_000_000);
        reader.LastParam("ToRefId").ShouldBe(43_999_999);
    }

    [Fact]
    public async Task GetBookCountsAsync_AtOtherLevel_CountsVersesMatchingBaseOrVariant_OverAllBooks()
    {
        var reader = new FakeDbReader().Returns(new VerseSearchBookCountRow { BookId = 43, Count = 1 });

        await new VerseSearchRepository(reader).GetBookCountsAsync("(('einzig'))", 200);

        reader.LastSql.ShouldContain("LATERAL");
        reader.LastSql.ShouldContain("OR rv.\"SearchVector\" @@");
        reader.LastParam("Level").ShouldBe(200);
        reader.LastParam("ToRefId").ShouldBe(int.MaxValue);
    }

    [Fact]
    public async Task GetRevisionWordsAsync_ReadsTheVariantWords_WithoutLemma()
    {
        var reader = new FakeDbReader().Returns(new VerseSearchWordRow { HebRefId = 43003016, WordInContext = "einzigen" });

        await new VerseSearchRepository(reader).GetRevisionWordsAsync([7]);

        reader.LastSql.ShouldContain("FROM \"unshackled-word\".\"RevisionWords\" w");
        reader.LastSql.ShouldContain("NULL::text \"Lemma\"");
        ((int[])reader.LastParam("RevisionVerseIds")!).ShouldBe([7]);
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
