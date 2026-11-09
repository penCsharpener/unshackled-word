using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.TestTooling;

namespace UnshackledWord.Infrastructure.Tests;

public class VerseMappingRepositoryTests
{
    private static VerseMappingRepository Create(FakeDbReader reader)
    {
        return new VerseMappingRepository(reader, Substitute.For<IDbConnectionFactory>());
    }

    [Fact]
    public async Task GetGermanWordsAsync_QueriesWordsOfTheVerseInOrder()
    {
        var reader = new FakeDbReader().Returns(new VerseGermanWordRow { Id = 1, Text = "Im" });

        var rows = await Create(reader).GetGermanWordsAsync(1, 2, 3);

        rows.Count.ShouldBe(1);
        reader.LastSql.ShouldContain("\"Elb1871Words\"");
        reader.LastSql.ShouldContain("ORDER BY ew.\"PositionInVerse\"");
        reader.LastParam("BibleBookId").ShouldBe(1);
        reader.LastParam("Chapter").ShouldBe(2);
        reader.LastParam("Verse").ShouldBe(3);
    }

    [Theory]
    [InlineData(true, "\"StepHebrewWords\"")]
    [InlineData(false, "\"StepGreekWords\"")]
    public async Task GetOriginalWordsAsync_PicksTheStepTableByLanguage(bool hebrew, string table)
    {
        var reader = new FakeDbReader().Returns<VerseOriginalWordRow>();

        await Create(reader).GetOriginalWordsAsync(hebrew, 1, 1, 1);

        reader.LastSql.ShouldContain(table);
        reader.LastSql.ShouldContain("\"LxxRefId\" IN");
        reader.LastSql.ShouldContain("\"HebRefId\"");
    }

    [Theory]
    [InlineData(true, "\"Elb1871HebrewMapping\"")]
    [InlineData(false, "\"Elb1871GreekMapping\"")]
    public async Task GetMappingRowsAsync_PicksTheMappingTableByLanguage(bool hebrew, string table)
    {
        var reader = new FakeDbReader().Returns<VerseMappingRow>();

        await Create(reader).GetMappingRowsAsync(hebrew, 1, 1, 1);

        reader.LastSql.ShouldContain(table);
        reader.LastSql.ShouldContain("\"ParentGermanWordId\" \"ParentElbWordId\"");
        reader.LastParam("Verse").ShouldBe(1);
    }

    [Fact]
    public void WriteSql_DeletesByVerseAndInsertsWithSeedConventions()
    {
        VerseMappingSql.DeleteGreek.ShouldContain("DELETE");
        VerseMappingSql.DeleteGreek.ShouldContain("\"Elb1871GreekMapping\"");
        VerseMappingSql.DeleteGreek.ShouldContain("ew.\"Verse\" = @Verse");
        VerseMappingSql.DeleteHebrew.ShouldContain("\"Elb1871HebrewMapping\"");

        foreach (var insert in new[] { VerseMappingSql.InsertGreek, VerseMappingSql.InsertHebrew })
        {
            insert.ShouldContain("\"CreatedAt\"");
            insert.ShouldContain("now()");
            insert.ShouldContain("ew.\"HebRefId\"");
            insert.ShouldContain("ew.\"PositionInVerse\"");
            insert.ShouldContain("@IsAddedWord");
            insert.ShouldContain("@ParentElbWordId");
            insert.ShouldContain("@StepWordId");
        }

        VerseMappingSql.InsertGreek.ShouldContain("\"StrongsNumber\"");
        VerseMappingSql.InsertHebrew.ShouldNotContain("StrongsNumber");
    }
}
