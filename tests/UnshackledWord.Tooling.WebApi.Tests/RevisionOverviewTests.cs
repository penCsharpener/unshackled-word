using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Domain.Models.Dbo.Revision;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.TestTooling;
using UnshackledWord.Tooling.WebApi.Endpoints.Revision;
using UnshackledWord.Tooling.WebApi.Endpoints.Revision.GetOverview;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class RevisionOverviewTests
{
    private const int Joh316 = 43003016;
    private const int Gen11 = 1001001;

    private static readonly RevisionLevelDbo BaseLevel = new() { Level = 100, Code = "elb1871", NameDe = "Elberfelder 1871", NameEn = "Elberfelder 1871", IsBase = true };
    private static readonly RevisionLevelDbo Revised = new() { Level = 200, Code = "revision", NameDe = "Revidierte Elberfelder", NameEn = "Revised Elberfelder", IsDefault = true };

    private static string HashOf(int hebRefId, params string[] words) =>
        ChapterFingerprint.Compute(words.Select((w, i) => AnchorToken.ForWord(hebRefId, i + 1, w)));

    private static RevisionOverviewVariantRow Variant(int id, int hebRefId, string hash, bool reviewed = true, int unlinkedGerman = 0, int unlinkedOriginal = 0, int level = 200) => new()
    {
        RevisionVerseId = id,
        HebRefId = hebRefId,
        Level = level,
        Version = 1,
        BaseTextHash = hash,
        CreatedAt = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc),
        MappingReviewedAt = reviewed ? new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc) : null,
        UnlinkedGermanWords = unlinkedGerman,
        UnlinkedOriginalWords = unlinkedOriginal,
    };

    private static RevisionOverviewBaseWordRow[] Words(int hebRefId, params string[] words) =>
        words.Select((w, i) => new RevisionOverviewBaseWordRow { HebRefId = hebRefId, PositionInVerse = i + 1, PlainWord = w }).ToArray();

    // Order of reader calls: levels, variants, total verses, base words.
    private static RevisionOverviewService Service(RevisionOverviewVariantRow[] variants, RevisionOverviewBaseWordRow[] baseWords, int total = 31102) =>
        new(
            new RevisionOverviewRepository(new FakeDbReader().Returns(variants).Returns(total).Returns(baseWords)),
            new RevisionLevelRepository(new FakeDbReader().Returns(BaseLevel, Revised), Substitute.For<UnshackledWord.Application.Abstractions.IDbConnectionFactory>()));

    [Fact]
    public async Task Get_CountsRevisedVersesPerLevelAndBook_WithoutTheBaseLevel()
    {
        var service = Service(
            [Variant(1, Joh316, "x"), Variant(2, 43003017, "x"), Variant(3, Gen11, "x")],
            []);

        var response = await service.GetAsync(new RevisionOverviewRequest(), "de");

        var level = response.Levels.ShouldHaveSingleItem();
        level.Level.ShouldBe(200);
        level.Name.ShouldBe("Revidierte Elberfelder");
        level.RevisedVerses.ShouldBe(3);
        level.TotalVerses.ShouldBe(31102);
        level.Books.Select(b => (b.BibleBookId, b.RevisedVerses)).ShouldBe([(1, 1), (43, 2)]);
        response.List.ShouldBeNull();
    }

    [Fact]
    public async Task Get_UnreviewedList_HoldsOnlyVariantsWithoutReviewStamp()
    {
        var good = HashOf(Joh316, "a", "b");
        var service = Service(
            [Variant(1, Joh316, good, reviewed: false), Variant(2, 43003017, "x", reviewed: true)],
            Words(Joh316, "a", "b"));

        var response = await service.GetAsync(new RevisionOverviewRequest { List = RevisionWorkListNames.Unreviewed }, "en");

        response.Counts.Unreviewed.ShouldBe(1);
        var item = response.List!.Items.ShouldHaveSingleItem();
        (item.RevisionVerseId, item.BibleBookId, item.Chapter, item.Verse).ShouldBe((1, 43, 3, 16));
    }

    [Fact]
    public async Task Get_IncompleteList_HoldsVariantsWithUnlinkedGermanOrOriginalWords()
    {
        var service = Service(
            [Variant(1, Joh316, "x", unlinkedGerman: 1), Variant(2, 43003017, "x", unlinkedOriginal: 2), Variant(3, 43003018, "x")],
            []);

        var response = await service.GetAsync(new RevisionOverviewRequest { List = RevisionWorkListNames.Incomplete }, "en");

        response.Counts.Incomplete.ShouldBe(2);
        response.List!.Items.Select(i => i.RevisionVerseId).ShouldBe([1, 2]);
        response.List.Items[1].UnlinkedOriginalWords.ShouldBe(2);
    }

    [Fact]
    public async Task Get_StaleList_HoldsVariantsWhoseBaseTextHashNoLongerMatches()
    {
        var service = Service(
            [Variant(1, Joh316, HashOf(Joh316, "a", "b")), Variant(2, 43003017, HashOf(43003017, "old"))],
            [.. Words(Joh316, "a", "b"), .. Words(43003017, "new")]);

        var response = await service.GetAsync(new RevisionOverviewRequest { List = RevisionWorkListNames.Stale }, "en");

        response.Counts.Stale.ShouldBe(1);
        response.List!.Items.ShouldHaveSingleItem().RevisionVerseId.ShouldBe(2);
    }

    [Fact]
    public async Task Get_List_PagesAndFiltersByLevel()
    {
        var service = Service(
            [Variant(1, 43003016, "x", reviewed: false), Variant(2, 43003017, "x", reviewed: false), Variant(3, 43003018, "x", reviewed: false), Variant(4, 43003019, "x", reviewed: false, level: 400)],
            []);

        var response = await service.GetAsync(new RevisionOverviewRequest { List = RevisionWorkListNames.Unreviewed, Level = 200, Page = 2, PageSize = 2 }, "en");

        response.List!.Total.ShouldBe(3);
        response.List.Items.ShouldHaveSingleItem().RevisionVerseId.ShouldBe(3);
        response.Counts.Unreviewed.ShouldBe(4);
    }

    [Fact]
    public async Task ActiveVariantsSql_ReadsTheLiveRowsAndJoinsMappingWithLang()
    {
        var reader = new FakeDbReader();

        await new RevisionOverviewRepository(reader).GetActiveVariantsAsync();

        reader.LastSql.ShouldNotContain("IsActive");
        reader.LastSql.ShouldContain("rm.\"Lang\" = 'H'");
        reader.LastSql.ShouldContain("rm.\"Lang\" = 'G'");
    }

    [Fact]
    public async Task BaseWords_PassesTheVerseIdsAsArray()
    {
        var reader = new FakeDbReader();

        await new RevisionOverviewRepository(reader).GetBaseWordsAsync([Joh316]);

        reader.LastSql.ShouldContain("ANY (@HebRefIds)");
        ((int[])reader.LastParam("HebRefIds")!).ShouldBe([Joh316]);
    }

    [Theory]
    [InlineData("unreviewed", 1, 50, true)]
    [InlineData("bogus", 1, 50, false)]
    [InlineData(null, 0, 50, false)]
    [InlineData(null, 1, 251, false)]
    public void Validator_ChecksListAndPaging(string? list, int page, int pageSize, bool valid)
    {
        new Validator().Validate(new RevisionOverviewRequest { List = list, Page = page, PageSize = pageSize }).IsValid.ShouldBe(valid);
    }
}
