using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.TestTooling;

namespace UnshackledWord.Infrastructure.Tests;

public class RevisionRepositoryTests
{
    private static RevisionRepository Repository(FakeDbReader reader) => new(reader, Substitute.For<UnshackledWord.Application.Abstractions.IDbConnectionFactory>());

    [Fact]
    public async Task GetActiveAsync_FiltersOnVerseAndLevel_WithoutALifecycleFilter()
    {
        var reader = new FakeDbReader().Returns(new RevisionActiveRow { Id = 4, Version = 2, RevisionId = 7 });

        var active = await Repository(reader).GetActiveAsync(43003016, 200);

        active!.Version.ShouldBe(2);
        active.RevisionId.ShouldBe(7);
        reader.LastSql.ShouldNotContain("IsActive");
        reader.LastParam("HebRefId").ShouldBe(43003016);
        reader.LastParam("Level").ShouldBe(200);
    }

    [Fact]
    public void NextVersion_ContinuesFromTheHighestAuditedVersion()
    {
        RevisionRepositorySql.NextVersion.ShouldContain("max(a.\"Version\")");
        RevisionRepositorySql.NextVersion.ShouldContain("\"RevisionVerseAudit\"");
        RevisionRepositorySql.NextVersion.ShouldNotContain("\"RevisionVerses\"");
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
        reader.LastSql.ShouldNotContain("IsActive");
    }

    [Fact]
    public void InsertSql_StoresTheCarriedBaseAndChange_AndReturnsTheId()
    {
        RevisionRepositorySql.InsertWord.ShouldContain("@BasePositionInVerse");
        RevisionRepositorySql.InsertWord.ShouldContain("@Change");
        RevisionRepositorySql.InsertWord.ShouldContain("RETURNING \"Id\"");
        RevisionRepositorySql.InsertMapping.ShouldContain("\"Origin\"");
        RevisionRepositorySql.UpdateVerse.ShouldContain("\"UpdatedByUserId\"");
    }

    [Fact]
    public void UpdateSql_RewritesTheLiveRowInPlace_AndResetsTheMappingReview()
    {
        RevisionRepositorySql.UpdateVerse.ShouldContain("UPDATE \"unshackled-word\".\"RevisionVerses\"");
        RevisionRepositorySql.UpdateVerse.ShouldContain("\"MappingReviewedAt\"       = NULL");
        RevisionRepositorySql.UpdateVerse.ShouldContain("\"BasedOnVersion\"");
        RevisionRepositorySql.DeleteWords.ShouldContain("\"RevisionVerseId\" = @Id");
        RevisionRepositorySql.DeleteVerse.ShouldContain("DELETE");
        RevisionRepositorySql.LockVerse.ShouldContain("FOR UPDATE");
    }

    [Fact]
    public async Task GetHeaderAsync_ReadsTheRowById()
    {
        var reader = new FakeDbReader().Returns(new RevisionVerseHeaderRow { Id = 3, HebRefId = 43003016 });

        (await Repository(reader).GetHeaderAsync(3))!.HebRefId.ShouldBe(43003016);
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
    public void ReplaceMappingSql_LocksTheRow_AndStampsTheReview()
    {
        RevisionRepositorySql.LockActive.ShouldContain("FOR UPDATE");
        RevisionRepositorySql.LockActive.ShouldNotContain("IsActive");
        RevisionRepositorySql.MarkReviewed.ShouldContain("\"MappingReviewedAt\"");
        RevisionRepositorySql.DeleteMapping.ShouldContain("rw.\"RevisionVerseId\" = @Id");
    }

    [Fact]
    public async Task GetHistoryAsync_ReadsOnlyTheAuditTable_NewestFirst()
    {
        var reader = new FakeDbReader().Returns(new RevisionHistoryRow { AuditId = 4, Version = 2, Action = "text", VerseText = "x" });

        var rows = await Repository(reader).GetHistoryAsync(43003016, 200);

        rows.Count.ShouldBe(1);
        reader.LastSql.ShouldContain("\"RevisionVerseAudit\"");
        reader.LastSql.ShouldNotContain("\"RevisionVerses\"");
        reader.LastSql.ShouldContain("ORDER BY a.\"Id\" DESC");
        reader.LastSql.ShouldContain("LEFT JOIN \"unshackled-word\".\"Users\"");
        reader.LastParam("HebRefId").ShouldBe(43003016);
        reader.LastParam("Level").ShouldBe(200);
    }

    [Fact]
    public void InsertVerse_StoresTheRevisionAndVersionItWasBasedOn()
    {
        RevisionRepositorySql.InsertVerse.ShouldContain("@BasedOnRevisionId");
        RevisionRepositorySql.InsertVerse.ShouldContain("@BasedOnVersion");
    }
}
