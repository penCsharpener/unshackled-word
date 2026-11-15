using UnshackledWord.Domain.Models.Dbo.Revision;
using UnshackledWord.TestTooling;
using UnshackledWord.Tooling.WebApi.Endpoints.Revision;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class RevisionLevelsTests
{
    private static readonly RevisionLevelDbo Revised = new() { Level = 300, Code = "revision", NameDe = "Revidierte Elberfelder", NameEn = "Revised Elberfelder", IsDefault = true };
    private static readonly RevisionLevelDbo Base = new() { Level = 200, Code = "elb1871", NameDe = "Elberfelder 1871", NameEn = "Elberfelder 1871 (EN)", IsBase = true };

    [Fact]
    public async Task GetAllAsync_ReadsTheLevelsTableOrderedByLevel()
    {
        var reader = new FakeDbReader().Returns(Base, Revised);

        var levels = await new RevisionLevelRepository(reader, Substitute.For<UnshackledWord.Application.Abstractions.IDbConnectionFactory>()).GetAllAsync();

        levels.Count.ShouldBe(2);
        reader.LastSql.ShouldContain("\"Revisions\"");
        reader.LastSql.ShouldContain("ORDER BY l.\"Level\"");
    }

    [Fact]
    public void ToEntries_SortsByLevelAndPicksTheGermanName()
    {
        var entries = RevisionLevelMapper.ToEntries([Revised, Base], "de");

        entries.Select(e => e.Level).ShouldBe([200, 300]);
        entries[0].IsBase.ShouldBeTrue();
        entries[1].IsDefault.ShouldBeTrue();
        entries[1].Name.ShouldBe("Revidierte Elberfelder");
    }

    [Fact]
    public void ToEntries_PicksTheEnglishNameForAnyOtherLocale()
    {
        RevisionLevelMapper.ToEntries([Base], "en")[0].Name.ShouldBe("Elberfelder 1871 (EN)");
    }
}
