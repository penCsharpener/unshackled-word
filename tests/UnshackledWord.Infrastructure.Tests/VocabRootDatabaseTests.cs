using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0030.1: the <see cref="VocabRootRepository"/> writes and the <c>VocabEntryRoots</c> view against a real Postgres with the generated
/// NT lexicon (μαρτυρέω = G3140, συνεπιμαρτυρέω = G4901). One rolled-back transaction per test.
/// Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c>, otherwise the tests are skipped.
/// </summary>
[Collection("VocabDatabase")]
public class VocabRootDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    private static async Task WithTransactionAsync(Func<IDbConnection, IDbTransaction, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        // start from no roots for the keys, whatever the dev DB holds (rolled back with the transaction)
        await connection.ExecuteAsync("""
            DELETE FROM "unshackled-word"."VocabStrongsRoots" WHERE "StrongsKey" IN ('G3140', 'G3141', 'G4901', 'H3789')
                OR "VocabRootId" IN (SELECT "Id" FROM "unshackled-word"."VocabRoots" WHERE "StrongsKey" IN ('G3140', 'G3141', 'G4901', 'H3789'));
            UPDATE "unshackled-word"."VocabRoots" SET "ParentRootId" = NULL
            WHERE "ParentRootId" IN (SELECT "Id" FROM "unshackled-word"."VocabRoots" WHERE "StrongsKey" IN ('G3140', 'G3141', 'G4901', 'H3789'));
            DELETE FROM "unshackled-word"."VocabRoots" WHERE "StrongsKey" IN ('G3140', 'G3141', 'G4901', 'H3789');
            """, transaction: transaction);

        await test(connection, transaction);

        transaction.Rollback();
    }

    private static Task<int> EntryIdAsync(IDbConnection c, IDbTransaction t, string key) =>
        c.QuerySingleAsync<int>("""SELECT "VocabEntryId" FROM "unshackled-word"."VocabStrongs" WHERE "StrongsKey" = @Key""", new { Key = key }, t);

    private static Task<int> UpsertMartyreoAsync(IDbConnection c, IDbTransaction t) =>
        VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert { Track = "G", Root = "μαρτυρέω", StrongsKey = "G3140" });

    private static VocabWordRootUpsert Compound(int rootId, string origin) => new()
    {
        Lang = "G",
        StrongsKey = "G4901",
        VocabRootId = rootId,
        Prefixes = "σύν + ἐπί",
        PrefixKeys = ["G4862", "G1909"],
        Origin = origin
    };

    [Fact]
    public Task Compound_and_simple_word_read_through_the_view() => WithTransactionAsync(async (c, t) =>
    {
        var rootId = await UpsertMartyreoAsync(c, t);
        (await VocabRootRepository.UpsertWordRootAsync(c, t, Compound(rootId, "manual"))).ShouldBeTrue();
        (await VocabRootRepository.UpsertWordRootAsync(c, t, new VocabWordRootUpsert { Lang = "G", StrongsKey = "G3140", VocabRootId = rootId, Origin = "prefill" })).ShouldBeTrue();

        var compoundId = await EntryIdAsync(c, t, "G4901");
        var simpleId = await EntryIdAsync(c, t, "G3140");
        var roots = await VocabRootRepository.ReadByEntryIdsAsync(new TestDbReader(c, t), [compoundId, simpleId]);

        var compound = roots[compoundId];
        compound.Id.ShouldBe(rootId);
        compound.Root.ShouldBe("μαρτυρέω");
        compound.StrongsKey.ShouldBe("G3140");
        compound.Prefixes.ShouldBe("σύν + ἐπί");
        compound.Origin.ShouldBe("manual");
        compound.IsSimple.ShouldBeFalse();

        var simple = roots[simpleId];
        simple.Prefixes.ShouldBeNull();
        simple.IsSimple.ShouldBeTrue();

        var keys = await c.QuerySingleAsync<string[]>("""SELECT "PrefixKeys" FROM "unshackled-word"."VocabStrongsRoots" WHERE "StrongsKey" = 'G4901'""", transaction: t);
        keys.ShouldBe(["G4862", "G1909"]);
    });

    [Fact]
    public Task Root_upsert_keeps_the_id_and_the_key() => WithTransactionAsync(async (c, t) =>
    {
        // Hebrew: the key may be omitted (a Greek root always carries one, the check sees the inserted row before the conflict)
        var first = await VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert { Track = "H", Root = "כתב", Homograph = 9, StrongsKey = "H3789" });
        var second = await VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert { Track = "H", Root = "כתב", Homograph = 9 });

        second.ShouldBe(first);
        var row = await c.QuerySingleAsync<(string Normalized, string Key)>("""SELECT "RootNormalized", "StrongsKey" FROM "unshackled-word"."VocabRoots" WHERE "Id" = @Id""", new { Id = first }, t);
        row.Normalized.ShouldBe(VocabText.Normalize("כתב"));
        row.Key.ShouldBe("H3789");
    });

    [Fact]
    public Task Manual_row_is_not_overwritten_by_agent() => WithTransactionAsync(async (c, t) =>
    {
        var rootId = await UpsertMartyreoAsync(c, t);
        await VocabRootRepository.UpsertWordRootAsync(c, t, Compound(rootId, "manual"));

        var written = await VocabRootRepository.UpsertWordRootAsync(c, t, Compound(rootId, "agent"));

        written.ShouldBeFalse();
        (await c.QuerySingleAsync<string>("""SELECT "Origin" FROM "unshackled-word"."VocabStrongsRoots" WHERE "StrongsKey" = 'G4901'""", transaction: t)).ShouldBe("manual");
    });

    [Fact]
    public Task Agent_row_is_replaced_by_manual() => WithTransactionAsync(async (c, t) =>
    {
        var rootId = await UpsertMartyreoAsync(c, t);
        await VocabRootRepository.UpsertWordRootAsync(c, t, Compound(rootId, "agent"));

        (await VocabRootRepository.UpsertWordRootAsync(c, t, Compound(rootId, "manual"))).ShouldBeTrue();
        (await c.QuerySingleAsync<string>("""SELECT "Origin" FROM "unshackled-word"."VocabStrongsRoots" WHERE "StrongsKey" = 'G4901'""", transaction: t)).ShouldBe("manual");
    });

    [Fact]
    public Task Course_list_carries_the_root() => WithTransactionAsync(async (c, t) =>
    {
        var rootId = await UpsertMartyreoAsync(c, t);
        await VocabRootRepository.UpsertWordRootAsync(c, t, Compound(rootId, "manual"));

        var result = await new VocabCourseRepository(new TestDbReader(c, t)).GetEntriesAsync("nt-greek", 0, 10, VocabText.Normalize("συνεπιμαρτυρέω"), "συνεπιμαρτυρέω");

        var item = result!.Items.ShouldHaveSingleItem();
        item.Root.ShouldNotBeNull();
        item.Root.Root.ShouldBe("μαρτυρέω");
        item.Root.Prefixes.ShouldBe("σύν + ἐπί");
    });

    // μαρτυρέω (G3140) with its simple word, the compound συνεπιμαρτυρέω (G4901) and, below it, the root μαρτυρία (G3141) with its own word row
    private static async Task<(int Root, int Sub)> FamilyFixtureAsync(IDbConnection c, IDbTransaction t)
    {
        var root = await UpsertMartyreoAsync(c, t);
        await VocabRootRepository.UpsertWordRootAsync(c, t, Compound(root, "prefill"));
        await VocabRootRepository.UpsertWordRootAsync(c, t, new VocabWordRootUpsert { Lang = "G", StrongsKey = "G3140", VocabRootId = root, Origin = "prefill" });

        var sub = await VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert { Track = "G", Root = "μαρτυρία", StrongsKey = "G3141", ParentRootId = root });
        await VocabRootRepository.UpsertWordRootAsync(c, t, new VocabWordRootUpsert { Lang = "G", StrongsKey = "G3141", VocabRootId = sub, Origin = "prefill" });

        return (root, sub);
    }

    private static async Task<List<int>> FamilyIdsAsync(IDbConnection c, IDbTransaction t, int rootId, bool subRoots)
    {
        var result = await new VocabCourseRepository(new TestDbReader(c, t)).GetEntriesAsync("nt-greek", 0, 100, null, null, rootId, subRoots);

        return result!.Items.Select(i => i.Id).ToList();
    }

    [Fact]
    public Task Family_filter_lists_the_root_entry_first_then_its_compounds_by_rank() => WithTransactionAsync(async (c, t) =>
    {
        var (root, _) = await FamilyFixtureAsync(c, t);
        var ownId = await EntryIdAsync(c, t, "G3140");
        var compoundId = await EntryIdAsync(c, t, "G4901");
        var subId = await EntryIdAsync(c, t, "G3141");

        var ids = await FamilyIdsAsync(c, t, root, subRoots: true);

        // the root's own entry first, then by rank: the compound and (as a sub-root) μαρτυρία, nothing else
        ids[0].ShouldBe(ownId);
        ids.ToHashSet().ShouldBe([ownId, compoundId, subId], ignoreOrder: true);
        ids.Count.ShouldBe(3);

        var ranks = await c.QueryAsync<int>("""SELECT "FrequencyRank" FROM "unshackled-word"."VocabEntries" WHERE "Id" = ANY(@Ids) AND "Id" <> @Own ORDER BY "FrequencyRank" """, new { Ids = ids.ToArray(), Own = ownId }, t);
        var expected = (await c.QueryAsync<int>("""SELECT "Id" FROM "unshackled-word"."VocabEntries" WHERE "Id" = ANY(@Ids) AND "Id" <> @Own ORDER BY "FrequencyRank" """, new { Ids = ids.ToArray(), Own = ownId }, t)).ToList();
        ranks.Count().ShouldBe(2);
        ids.Skip(1).ToList().ShouldBe(expected);
    });

    [Fact]
    public Task Root_entry_without_its_own_row_still_leads_its_family() => WithTransactionAsync(async (c, t) =>
    {
        var (root, _) = await FamilyFixtureAsync(c, t);
        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."VocabStrongsRoots" WHERE "StrongsKey" = 'G3140'""", transaction: t);

        var ids = await FamilyIdsAsync(c, t, root, subRoots: false);

        ids[0].ShouldBe(await EntryIdAsync(c, t, "G3140"));
        ids.ShouldContain(await EntryIdAsync(c, t, "G4901"));
        ids.Count.ShouldBe(2);
    });

    [Fact]
    public Task Sub_roots_are_left_out_when_asked() => WithTransactionAsync(async (c, t) =>
    {
        var (root, sub) = await FamilyFixtureAsync(c, t);
        var subId = await EntryIdAsync(c, t, "G3141");

        (await FamilyIdsAsync(c, t, root, subRoots: true)).ShouldContain(subId);
        (await FamilyIdsAsync(c, t, root, subRoots: false)).ShouldNotContain(subId);

        // the sub-root alone is its own narrow family
        (await FamilyIdsAsync(c, t, sub, subRoots: true)).ShouldBe([subId]);
    });

    [Fact]
    public Task Editor_list_filters_family_and_missing_root() => WithTransactionAsync(async (c, t) =>
    {
        var (root, _) = await FamilyFixtureAsync(c, t);
        var reader = new TestDbReader(c, t);
        var editor = new VocabEditorRepository(reader, new VocabEntryRepository(reader));
        var compoundId = await EntryIdAsync(c, t, "G4901");
        var ownId = await EntryIdAsync(c, t, "G3140");

        var family = await editor.ListAsync(new ListVocabEditorEntriesRequest { Course = "nt-greek", Root = root, SubRoots = false, Take = 50 });
        family!.Items.Select(i => i.Id).ToList().ShouldBe([ownId, compoundId], ignoreOrder: true);
        family.Items[0].Id.ShouldBe(ownId);

        // drop the compound's row: only it shows up under "no root"
        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."VocabStrongsRoots" WHERE "StrongsKey" = 'G4901'""", transaction: t);
        var missing = await editor.ListAsync(new ListVocabEditorEntriesRequest { Course = "nt-greek", MissingRoot = true, Q = "συνεπιμαρτυρέω", Take = 50 });
        missing!.Items.ShouldHaveSingleItem().Id.ShouldBe(compoundId);

        var rooted = await editor.ListAsync(new ListVocabEditorEntriesRequest { Course = "nt-greek", MissingRoot = true, Q = "μαρτυρέω", Take = 50 });
        rooted!.Items.Select(i => i.Id).ShouldNotContain(ownId);
    });

    [Fact]
    public Task Root_detail_has_parent_sub_roots_members_and_total() => WithTransactionAsync(async (c, t) =>
    {
        var (root, sub) = await FamilyFixtureAsync(c, t);
        var repository = new VocabRootRepository(new TestDbReader(c, t), null!);

        var detail = await repository.GetDetailAsync(root);

        detail.ShouldNotBeNull();
        detail.Root.ShouldBe("μαρτυρέω");
        detail.StrongsKey.ShouldBe("G3140");
        detail.Parent.ShouldBeNull();
        detail.SubRoots.ShouldHaveSingleItem().Id.ShouldBe(sub);
        detail.MemberTotal.ShouldBe(3);
        detail.Members.Count.ShouldBe(3);
        detail.Members[0].EntryId.ShouldBe(await EntryIdAsync(c, t, "G3140"));

        var child = await repository.GetDetailAsync(sub);
        child!.Parent!.Id.ShouldBe(root);

        (await repository.GetDetailAsync(int.MaxValue)).ShouldBeNull();
    });

    /// <summary>Runs the repository's queries on the test's transaction so it sees the fixture rows.</summary>
    private sealed class TestDbReader(IDbConnection connection, IDbTransaction transaction) : IDbReader
    {
        public Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object? param = null) => connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction);

        public async Task<IEnumerable<T>> ReadAsListAsync<T>(string sql, object? param = null) => await connection.QueryAsync<T>(sql, param, transaction);

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null) => connection.ExecuteScalarAsync<T>(sql, param, transaction);

        public Task<List<T>> ReadMultipleAsListAsync<T>(string sql, object? param, Func<IMultiDbReader, Task<List<T>>> mappingFunc, CancellationToken token = default) =>
            throw new NotSupportedException();
    }
}
