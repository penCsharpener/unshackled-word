using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class DqRoleChecksTests
{
    [Fact]
    public void RoleChecks_AreListedInTheOverview_AfterTheLemmaChecks()
    {
        DqChecks.Codes[^6..].ShouldBe(["R1", "R2", "R3", "R4", "R5", "R6"]);
        DqChecks.IsKnown("R4").ShouldBeTrue();
    }

    [Theory]
    [InlineData("R1")]
    [InlineData("R2")]
    [InlineData("R3")]
    public void MorphemeChecks_JoinTheInstanceAggregate(string code)
    {
        var (ctes, where, join) = DqRoleChecks.Build(code, " AND f.\"BibleBookId\" = 8");

        ctes.ShouldContain("WITH \"Inst\" AS MATERIALIZED");
        ctes.ShouldContain("GROUP BY f.\"Lang\", f.\"StepWordId\", f.\"MorphemeStrongs\"");
        ctes.ShouldContain("AND f.\"BibleBookId\" = 8");
        join.ShouldContain("JOIN \"Inst\" i");
        where.ShouldStartWith("f.\"Kind\" = 'aligned'");
    }

    [Fact]
    public void R1_ListsOneRowPerInstance_AndSkipsContractions()
    {
        var (_, where, _) = DqRoleChecks.Build("R1", string.Empty);

        where.ShouldContain("i.\"Heads\" = 0");
        where.ShouldContain("NOT i.\"AllContraction\"");
        where.ShouldContain("i.\"FirstElbWordId\"");
    }

    [Fact]
    public void R2_HonoursTheMultiHeadFlag()
    {
        DqRoleChecks.Build("R2", string.Empty).Where.ShouldContain("NOT i.\"AllMultiHead\"");
    }

    [Fact]
    public void R3_ExemptsTheContractionArticle()
    {
        var (_, where, _) = DqRoleChecks.Build("R3", string.Empty);

        where.ShouldContain("f.\"Role\" = 'unit'");
        where.ShouldContain("'APPRART'");
        where.ShouldContain("'G3588', 'H9009'");
    }

    [Fact]
    public void R4_GroupsLemmasPerMorpheme()
    {
        var (ctes, _, join) = DqRoleChecks.Build("R4", string.Empty);

        ctes.ShouldContain("HAVING count(*) > 1");
        join.ShouldContain("d.\"RenderLemmaLower\" = f.\"RenderLemmaLower\"");
    }

    [Fact]
    public void R5_NeedsAFiniteVerbStartingWithTheParticle_AndSkipsInfinitiveZu()
    {
        var (ctes, where, join) = DqRoleChecks.Build("R5", string.Empty);

        ctes.ShouldBeEmpty();
        join.ShouldBeEmpty();
        where.ShouldContain("f.\"Stts\" = 'PTKVZ'");
        where.ShouldContain("starts_with(v.\"LemmaLower\", f.\"PlainLower\")");
        where.ShouldContain("'%INF'");
    }

    [Fact]
    public void R6_UsesTheAgentLegacyList()
    {
        DqRoleChecks.Build("R6", string.Empty).Where.ShouldContain("ANY(@legacy)");
        DqRoleChecks.LegacySpellings.Length.ShouldBe(AgentLemmaRules.LegacySpellings.Count);
        DqRoleChecks.LegacySpellings.ShouldContain("thür");
        DqRoleChecks.LegacySpellings.ShouldAllBe(s => s == s.ToLowerInvariant());
    }

    [Fact]
    public void UnknownCode_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => DqRoleChecks.Build("R9", string.Empty));
    }

    [Theory]
    [InlineData("R1", UnmappedRules.Error)]
    [InlineData("R2", UnmappedRules.Warning)]
    [InlineData("R3", UnmappedRules.Error)]
    [InlineData("R4", UnmappedRules.Warning)]
    [InlineData("R5", UnmappedRules.Warning)]
    [InlineData("R6", UnmappedRules.Error)]
    public void Judge_SetsTheSeverity(string code, string severity)
    {
        var row = new FactRow { Plain = "aus", Lemma = "thun", RenderLemma = "thun", SourceWord = "וַיֹּאמֶר", MorphemeStrongs = "H9001" };

        DqRoleChecks.Judge(code, row).Severity.ShouldBe(severity);
    }

    [Fact]
    public void Judge_R6_NamesTheModernHeadword()
    {
        DqRoleChecks.Judge("R6", new FactRow { Lemma = "thun" }).Detail.ShouldContain("modern „tun“");
    }
}
