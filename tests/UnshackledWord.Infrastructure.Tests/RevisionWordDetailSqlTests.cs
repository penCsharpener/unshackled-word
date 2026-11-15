using UnshackledWord.Infrastructure.Repositories;

namespace UnshackledWord.Infrastructure.Tests;

public class RevisionWordDetailSqlTests
{
    [Fact]
    public void ReadingQueries_JoinStepOnlyThroughTheLangOfTheMapping()
    {
        RevisionWordDetailSql.GreekReadings.ShouldContain("rm.\"Lang\" = 'G'");
        RevisionWordDetailSql.HebrewReadings.ShouldContain("rm.\"Lang\" = 'H'");
        RevisionWordDetailSql.GreekReadings.ShouldContain("\"MappingOrigin\"");
    }

    [Fact]
    public void WordQuery_TakesTheLemmaOnlyForSameWords_AndTheBaseWordOnlyForChangedOnes()
    {
        RevisionWordDetailSql.Word.ShouldContain("WHEN rw.\"Change\" = 'same' THEN m.\"Lemma\"");
        RevisionWordDetailSql.Word.ShouldContain("WHEN rw.\"Change\" = 'changed' THEN bw.\"WordInContext\"");
    }

    [Fact]
    public void CoMappedQuery_StaysInsideTheVariantAndKeepsLang()
    {
        RevisionWordDetailSql.CoMapped.ShouldContain("ow.\"RevisionVerseId\" = own.\"RevisionVerseId\"");
        RevisionWordDetailSql.CoMapped.ShouldContain("orm.\"Lang\" = own.\"Lang\"");
    }
}
