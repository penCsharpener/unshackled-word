using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.LinkRoles.Backfill;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.TestTooling;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>SQL shape of the link-roles backfill (roadmap 0018.4): apply writes only rows that still hold their before values, undo only rows that still hold their after values.</summary>
public class LinkRoleBackfillRepositoryTests
{
    private static readonly string[] WrittenColumns = ["Role", "StrongsNumber", "IsAddedWord", "ParentGermanWordId", "StepWordId"];

    [Theory]
    [InlineData('G', "\"Elb1871GreekMapping\"")]
    [InlineData('H', "\"Elb1871HebrewMapping\"")]
    public void Apply_and_undo_pick_the_table_by_language(char lang, string table)
    {
        LinkRoleBackfillSql.ApplyMapping(lang).ShouldContain($"UPDATE \"unshackled-word\".{table} m");
        LinkRoleBackfillSql.UndoMapping(lang).ShouldContain($"UPDATE \"unshackled-word\".{table} m");
        LinkRoleBackfillSql.MappingRows(lang).ShouldContain($"FROM \"unshackled-word\".{table} m");
    }

    [Fact]
    public void Apply_sets_after_values_only_where_every_before_value_still_holds()
    {
        var sql = LinkRoleBackfillSql.ApplyMapping('H');

        foreach (var column in WrittenColumns)
        {
            sql.ShouldContain($"\"{column}\" = r.\"After{column}\"");
            sql.ShouldContain(column == "Role" ? "m.\"Role\" = r.\"BeforeRole\"" : $"m.\"{column}\" IS NOT DISTINCT FROM r.\"Before{column}\"");
        }

        sql.ShouldContain("r.\"ChangeSetId\" = @ChangeSetId");
        sql.ShouldContain("r.\"Lang\" = @Lang");
    }

    [Fact]
    public void Undo_restores_before_values_only_where_every_after_value_still_holds()
    {
        var sql = LinkRoleBackfillSql.UndoMapping('G');

        foreach (var column in WrittenColumns)
        {
            sql.ShouldContain($"\"{column}\" = r.\"Before{column}\"");
            sql.ShouldContain(column == "Role" ? "m.\"Role\" = r.\"AfterRole\"" : $"m.\"{column}\" IS NOT DISTINCT FROM r.\"After{column}\"");
        }
    }

    [Fact]
    public void Lemma_undo_restores_the_before_lemma_where_the_after_lemma_still_holds_and_audits_under_the_undo_set()
    {
        LinkRoleBackfillSql.UndoLemmas.ShouldContain("e.\"BeforeValues\" ->> 'Lemma'");
        LinkRoleBackfillSql.UndoLemmas.ShouldContain("m.\"Lemma\" IS NOT DISTINCT FROM e.\"AfterValues\" ->> 'Lemma'");
        LinkRoleBackfillSql.UndoLemmas.ShouldContain("@UndoChangeSetId");
        LinkRoleBackfillSql.ApplyLemmas.ShouldContain("m.\"Lemma\" IS NOT DISTINCT FROM i.\"OldLemma\"");
        LinkRoleBackfillSql.ApplyLemmas.ShouldContain("\"Elb1871MorphologyEdits\"");
    }

    [Fact]
    public void The_previous_applied_at_travels_in_the_change_set_scope()
    {
        LinkRoleBackfillSql.InsertChangeSet.ShouldContain("'previousLinkRolesAppliedAt', @Previous::timestamptz");
        LinkRoleBackfillSql.RestoreAppliedAt.ShouldContain("(cs.\"Scope\" ->> 'previousLinkRolesAppliedAt')::timestamptz");
        LinkRoleBackfillSql.SetAppliedAtNow.ShouldContain("\"LinkRolesAppliedAt\" = now()");
    }

    [Fact]
    public void Mapping_rows_go_in_as_one_array_per_column_in_insert_order()
    {
        var change = new MappingRowChange('H', 9, 1001003, 31, "1", new MappingRowValues("head", null, null, null, 21), new MappingRowValues("head", "H9001", null, null, 21));

        var param = LinkRoleBackfillRepository.MappingRowsParam(3, [change]);

        var properties = param.GetType().GetProperties().Select(p => p.Name).ToList();
        properties.ShouldBe(["ChangeSetId", "Lang", "MappingId", "HebRefId", "ElbWordId", "Rule",
            "BeforeRole", "BeforeStrongsNumber", "BeforeIsAddedWord", "BeforeParentGermanWordId", "BeforeStepWordId",
            "AfterRole", "AfterStrongsNumber", "AfterIsAddedWord", "AfterParentGermanWordId", "AfterStepWordId"]);
        var insert = LinkRoleBackfillSql.InsertMappingRows;
        properties.Skip(1).Select(p => insert.IndexOf($"@{p}::", StringComparison.Ordinal)).ShouldBeInOrder();
        ((string[])param.GetType().GetProperty("Lang")!.GetValue(param)!).ShouldBe(["H"]);
        ((string?[])param.GetType().GetProperty("AfterStrongsNumber")!.GetValue(param)!).ShouldBe(["H9001"]);
    }

    [Fact]
    public async Task Words_use_the_plain_word_so_rule_6_never_writes_punctuation()
    {
        var reader = new FakeDbReader();
        var repository = new LinkRoleBackfillRepository(reader, Substitute.For<IDbConnectionFactory>());

        var book = await repository.LoadBookAsync(40, CancellationToken.None);

        LinkRoleBackfillSql.Words.ShouldContain("COALESCE(NULLIF(ew.\"PlainWord\", ''), ew.\"WordInContext\") \"Text\"");
        reader.Calls.Select(c => c.Sql).ShouldContain(LinkRoleBackfillSql.Words);
        reader.LastParam("BookId").ShouldBe(40);
        book.BookName.ShouldBe("40");
    }

    [Fact]
    public void Active_agent_runs_are_the_ones_the_run_loop_still_works_on()
    {
        LinkRoleBackfillSql.ActiveAgentRuns.ShouldContain("('queued', 'running', 'waiting-for-provider')");
    }
}
