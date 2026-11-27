using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Infrastructure.Repositories;

namespace UnshackledWord.Infrastructure.Tests;

public class AgentSqlTests
{
    [Fact]
    public void Store_SupersedesTheOlderOpenProposalOfTheVerse()
    {
        AgentStoreSql.SupersedeOpen.ShouldContain("SET \"Status\" = 'superseded'");
        AgentStoreSql.SupersedeOpen.ShouldContain("\"Status\" = 'open'");
        AgentStoreSql.SupersedeOpen.ShouldContain("\"BibleBookId\" = @BibleBookId");
        AgentStoreSql.SupersedeOpen.ShouldContain("\"Chapter\" = @Chapter");
        AgentStoreSql.SupersedeOpen.ShouldContain("\"Verse\" = @Verse");
    }

    /// <summary>
    /// Every <c>@Name</c> of the run insert is bound: an unbound <c>@IncludeConfirmed</c> is Postgres' absolute-value operator on a column and failed
    /// the first real LXX run with 42703 (0.13.18).
    /// </summary>
    [Fact]
    public void Store_InsertRun_BindsEveryParameter()
    {
        var bound = AgentStoreSql.InsertRunParameters(new AgentRunInsert { IncludeConfirmed = true, TextForm = "og" }).GetType().GetProperties().Select(p => p.Name).ToHashSet();
        var used = System.Text.RegularExpressions.Regex.Matches(AgentStoreSql.InsertRun, "@([A-Za-z]+)").Select(m => m.Groups[1].Value).Distinct();

        used.Where(name => !bound.Contains(name)).ShouldBeEmpty();
    }

    /// <summary>Roadmap 0028.7: the supersede key is the one of AgentVerseProposals_Open_UQ, so LXX and Hebrew proposals of Gen 1:1 never touch each other.</summary>
    [Fact]
    public void Store_SupersedeAndCount_UseTheKeyOfTheOpenProposalIndex()
    {
        AgentStoreSql.SupersedeOpen.ShouldContain("\"Language\" = @Language");
        AgentStoreSql.SupersedeOpen.ShouldContain("COALESCE(\"Flags\" ->> 'textForm', '') = @TextForm");
        AgentStoreSql.SupersedeOpen.ShouldContain("COALESCE(\"Flags\" ->> 'subverse', '0') = @Subverse");
        AgentStoreSql.CountProposedVerses.ShouldContain("\"Language\", COALESCE(\"Flags\" ->> 'textForm', ''), COALESCE(\"Flags\" ->> 'subverse', '0')");
    }

    [Fact]
    public void Morphology_IsJoinedByRefAndPosition_NeverById()
    {
        foreach (var sql in new[] { AgentSql.GermanWords, AgentSql.FormLemmaCandidates, AgentSql.FormLemmaTrusted, AgentSql.GreekConfirmedPairs, AgentSql.HebrewConfirmedPairs })
        {
            sql.ShouldContain("\"HebRefId\" = ew.\"HebRefId\"");
            sql.ShouldContain("\"PositionInVerse\" = ew.\"PositionInVerse\"");
            sql.ShouldNotContain("m.\"Id\" = ew.\"Id\"");
        }
    }

    [Fact]
    public void ConfirmedPairs_CountOnlyHumanAuthors()
    {
        foreach (var sql in new[] { AgentSql.GreekConfirmedPairs, AgentSql.HebrewConfirmedPairs })
        {
            sql.ShouldContain("\"CreatedByUserId\" IS NOT NULL");
            sql.ShouldContain("ur.\"Role\" = 'ai'");
        }
    }

    [Fact]
    public void HumanEditedLemma_ExcludesAiAuthorsAndMatchesTheCurrentLemma()
    {
        AgentSql.GermanWords.ShouldContain("e.\"AfterValues\" ->> 'Lemma' = m.\"Lemma\"");
        AgentSql.GermanWords.ShouldContain("ur.\"Role\" = 'ai'");
        AgentSql.GermanWords.ShouldContain("e.\"HebRefId\" = ew.\"HebRefId\"");
    }
}

public class AgentScoreSqlTests
{
    [Fact]
    public void Proposals_AreReadPerRun_WithTheJsonColumnsAsText()
    {
        AgentScoreSql.Proposals.ShouldContain("FROM \"unshackled-word\".\"AgentVerseProposals\" p");
        AgentScoreSql.Proposals.ShouldContain("p.\"RunId\" = @RunId");
        foreach (var column in new[] { "Links", "Lemmas", "Roles", "Flags" })
        {
            AgentScoreSql.Proposals.ShouldContain($"p.\"{column}\"::text");
        }

        AgentScoreSql.Proposals.ShouldContain("ORDER BY p.\"Id\"");
    }

    [Fact]
    public void Packages_CarryTheMapAndTheUsage()
    {
        AgentScoreSql.Packages.ShouldContain("p.\"LocalIdMap\"::text");
        foreach (var column in new[] { "InputTokens", "CachedInputTokens", "OutputTokens", "CostUsd" })
        {
            AgentScoreSql.Packages.ShouldContain($"p.\"{column}\"");
        }

        AgentScoreSql.Packages.ShouldContain("p.\"RunId\" = @RunId");
    }

    [Fact]
    public void SaveScore_WritesJsonbAndTimestampOfOneRun()
    {
        AgentScoreSql.SaveScore.ShouldContain("SET \"Score\" = @Score::jsonb");
        AgentScoreSql.SaveScore.ShouldContain("\"ScoredAt\" = now()");
        AgentScoreSql.SaveScore.ShouldContain("\"Id\" = @RunId");
    }

    [Fact]
    public void Migration_0034_UsesNoDollarTagQuotingAndIsIdempotent()
    {
        var path = Directory.EnumerateFiles(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "UnshackledWord.Tooling.Postgres.Migration", "scripts")), "0034_AgentRunScore.sql").Single();
        var sql = File.ReadAllText(path);

        sql.ShouldContain("ADD COLUMN IF NOT EXISTS \"Score\" jsonb");
        sql.ShouldContain("ADD COLUMN IF NOT EXISTS \"ScoredAt\" timestamptz");
        sql.ShouldNotMatch(@"\$\w+\$");
    }
}
