using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.TestTooling;

namespace UnshackledWord.Infrastructure.Tests;

public class RevisionRepositoryTests
{
    private static RevisionRepository Repository(FakeDbReader reader) => new(reader, Substitute.For<UnshackledWord.Application.Abstractions.IDbConnectionFactory>());

    [Fact]
    public async Task GetActiveAsync_FiltersOnVerseLevelAndActiveRows()
    {
        var reader = new FakeDbReader().Returns(new RevisionActiveRow { Id = 4, Version = 2 });

        var active = await Repository(reader).GetActiveAsync(43003016, 200);

        active!.Version.ShouldBe(2);
        reader.LastSql.ShouldContain("rv.\"IsActive\"");
        reader.LastParam("HebRefId").ShouldBe(43003016);
        reader.LastParam("Level").ShouldBe(200);
    }

    [Fact]
    public async Task GetLastVersionAsync_CountsEndedVersionsToo_AndStartsAtZero()
    {
        var reader = new FakeDbReader();

        (await Repository(reader).GetLastVersionAsync(43003016, 200)).ShouldBe(0);
        reader.LastSql.ShouldContain("max(rv.\"Version\")");
    }

    [Fact]
    public async Task IsWritableLevelAsync_ExcludesTheBaseLevel()
    {
        var reader = new FakeDbReader();

        (await Repository(reader).IsWritableLevelAsync(100)).ShouldBeFalse();
        reader.LastSql.ShouldContain("NOT l.\"IsBase\"");
    }

    [Fact]
    public async Task GetActiveVariantsAsync_LeftJoinsUsers_SoSeedVariantsWithoutAuthorAreKept()
    {
        var reader = new FakeDbReader().Returns(new RevisionVariantRow { RevisionVerseId = 1, Level = 200 });

        (await Repository(reader).GetActiveVariantsAsync(43003016)).Count.ShouldBe(1);
        reader.LastSql.ShouldContain("LEFT JOIN \"unshackled-word\".\"Users\"");
        reader.LastSql.ShouldContain("rv.\"IsActive\"");
    }

    [Fact]
    public void InsertSql_StoresTheCarriedBaseAndChange_AndReturnsTheId()
    {
        RevisionRepositorySql.InsertWord.ShouldContain("@BasePositionInVerse");
        RevisionRepositorySql.InsertWord.ShouldContain("@Change");
        RevisionRepositorySql.InsertWord.ShouldContain("RETURNING \"Id\"");
        RevisionRepositorySql.InsertMapping.ShouldContain("\"Origin\"");
        RevisionRepositorySql.EndActive.ShouldContain("\"EndedByUserId\"");
    }

    [Fact]
    public async Task GetHeaderAsync_ReadsEndedVersionsToo()
    {
        var reader = new FakeDbReader().Returns(new RevisionVerseHeaderRow { Id = 3, HebRefId = 43003016, IsActive = false });

        (await Repository(reader).GetHeaderAsync(3))!.IsActive.ShouldBeFalse();
        reader.LastParam("Id").ShouldBe(3);
    }

    [Fact]
    public async Task GetMappingAsync_JoinsWordsOfTheVariantInReadingOrder()
    {
        var reader = new FakeDbReader().Returns(new RevisionMappingRow { RevisionWordId = 1, StepWordId = 2, Origin = "carried" });

        (await Repository(reader).GetMappingAsync(9)).Count.ShouldBe(1);
        reader.LastSql.ShouldContain("rw.\"RevisionVerseId\" = @Id");
        reader.LastSql.ShouldContain("ORDER BY rw.\"PositionInVerse\"");
    }

    [Fact]
    public void ReplaceMappingSql_LocksTheActiveRow_AndStampsTheReview()
    {
        RevisionRepositorySql.LockActive.ShouldContain("FOR UPDATE");
        RevisionRepositorySql.LockActive.ShouldContain("rv.\"IsActive\"");
        RevisionRepositorySql.MarkReviewed.ShouldContain("\"MappingReviewedAt\"");
        RevisionRepositorySql.DeleteMapping.ShouldContain("rw.\"RevisionVerseId\" = @Id");
    }

    [Fact]
    public async Task GetHistoryAsync_ReadsEndedVersionsToo_NewestFirst()
    {
        var reader = new FakeDbReader().Returns(new RevisionHistoryRow { RevisionVerseId = 4, Version = 2, VerseText = "x" });

        var rows = await Repository(reader).GetHistoryAsync(43003016, 200);

        rows.Count.ShouldBe(1);
        reader.LastSql.ShouldNotContain("AND rv.\"IsActive\"");
        reader.LastSql.ShouldContain("ORDER BY rv.\"Version\" DESC");
        reader.LastSql.ShouldContain("LEFT JOIN \"unshackled-word\".\"Users\"");
        reader.LastParam("HebRefId").ShouldBe(43003016);
        reader.LastParam("Level").ShouldBe(200);
    }

    [Fact]
    public void InsertVerse_StoresTheVariantItWasBasedOn()
    {
        RevisionRepositorySql.InsertVerse.ShouldContain("@BasedOnRevisionVerseId");
    }
}
