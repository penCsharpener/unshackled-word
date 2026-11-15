using UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetWordsForChapter;
using UnshackledWord.TestTooling;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class ChapterWordsQueryTests
{
    private static ChapterWordRow BaseWord(int id, int verse, int pos, string word) =>
        new() { Id = id, Verse = verse, PositionInVerse = pos, WordInContext = word, PlainWord = word };

    private static ChapterWordRow RevisedWord(int revisionWordId, int baseId, int verse, int pos, string word) =>
        new()
        {
            Id = baseId, Verse = verse, PositionInVerse = pos, WordInContext = word, PlainWord = word,
            RevisionWordId = revisionWordId, RevisionVerseId = 9, RevisionLevel = 200,
        };

    [Theory]
    [InlineData(43, "Elb1871GreekMapping")]
    [InlineData(1, "Elb1871HebrewMapping")]
    public async Task ReadAsync_RunsTheUntouchedBaseQuery_AtLevel0(int book, string mappingTable)
    {
        var reader = new FakeDbReader().Returns(BaseWord(1, 1, 1, "Im"));

        var (words, revised) = await ChapterWordsQuery.ReadAsync(reader, book, 3, 0);

        reader.LastSql.ShouldContain(mappingTable);
        reader.LastSql.ShouldNotContain("RevisionVerses");
        reader.LastParam("BibleBookId").ShouldBe(book);
        reader.LastParam("Chapter").ShouldBe(3);
        words.Single().RevisionWordId.ShouldBeNull();
        revised.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_PassesLevelAndChapterRangeAsParameters_BelowOrAboveBase()
    {
        var reader = new FakeDbReader().Returns(BaseWord(1, 1, 1, "Im"));

        await ChapterWordsQuery.ReadAsync(reader, 19, 119, 200);

        reader.LastSql.ShouldContain("RevisionVerses");
        reader.LastSql.ShouldContain("@Level");
        reader.LastParam("Level").ShouldBe(200);
        reader.LastParam("FromRefId").ShouldBe(19119000);
        reader.LastParam("ToRefId").ShouldBe(19119999);
    }

    [Fact]
    public async Task ReadAsync_ReturnsNoRevisedVerses_WhenNoVariantIsPassed()
    {
        var reader = new FakeDbReader().Returns(BaseWord(1, 1, 1, "Im"), BaseWord(2, 1, 2, "Anfang"));

        var (words, revised) = await ChapterWordsQuery.ReadAsync(reader, 1, 1, 150);

        words.Count.ShouldBe(2);
        revised.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_ListsEachRevisedVerseOnce_AndKeepsBaseIdAndRevisionWordId()
    {
        var reader = new FakeDbReader().Returns(
            BaseWord(1, 15, 1, "Denn"),
            RevisedWord(500, 31, 16, 1, "Denn"),
            RevisedWord(501, 0, 16, 2, "einzigen"));

        var (words, revised) = await ChapterWordsQuery.ReadAsync(reader, 43, 3, 200);

        revised!.Count.ShouldBe(1);
        revised[0].Verse.ShouldBe(16);
        revised[0].Level.ShouldBe(200);
        revised[0].RevisionVerseId.ShouldBe(9);
        words.Select(w => (w.Id, w.RevisionWordId)).ShouldBe([(1, (int?)null), (31, 500), (0, 501)]);
    }

    [Fact]
    public void EffectiveVerses_PicksTheVariantFurthestFromBaseTowardsTheSliderValue()
    {
        var sql = UnshackledWord.Application.Features.Revision.RevisionSql.EffectiveVerses;

        sql.ShouldContain("BETWEEN least(@Level, 200) AND greatest(@Level, 200)");
        sql.ShouldContain("ORDER BY abs(rev.\"Level\" - 200) DESC");
        sql.ShouldNotContain("IsActive");
    }
}
