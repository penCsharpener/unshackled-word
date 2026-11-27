using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0030.5: manual root edits of <see cref="VocabRootEditor"/> against a real Postgres with the generated NT lexicon
/// (μαρτυρέω = G3140, μαρτυρία = G3141, συνεπιμαρτυρέω = G4901). One rolled-back transaction per test.
/// Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c>, otherwise the tests are skipped.
/// </summary>
[Collection("VocabDatabase")]
public class VocabRootEditorDatabaseTests
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

    private static Task<int> ContentVersionAsync(IDbConnection c, IDbTransaction t, int entryId) =>
        c.QuerySingleAsync<int>("""SELECT "ContentVersion" FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { Id = entryId }, t);

    private static Task<int> NewUserAsync(IDbConnection c, IDbTransaction t)
    {
        var name = "vocab-root-" + Guid.NewGuid().ToString("N");

        return c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "DisplayName")
            VALUES (@Name || '@example.org', @Name || '@example.org', @Name)
            RETURNING "Id"
            """, new { Name = name }, t);
    }

    private static Task<int> MartyreoAsync(IDbConnection c, IDbTransaction t) =>
        VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert { Track = "G", Root = "μαρτυρέω", StrongsKey = "G3140" });

    private static Task AgentCompoundAsync(IDbConnection c, IDbTransaction t, int rootId) =>
        VocabRootRepository.UpsertWordRootAsync(c, t, new VocabWordRootUpsert
        {
            Lang = "G",
            StrongsKey = "G4901",
            VocabRootId = rootId,
            Prefixes = "σύν",
            PrefixKeys = ["G4862"],
            Origin = "agent",
        });

    [Fact]
    public Task Manual_save_turns_the_agent_row_manual_and_logs_a_root_change_without_a_bump() => WithTransactionAsync(async (c, t) =>
    {
        var root = await MartyreoAsync(c, t);
        await AgentCompoundAsync(c, t, root);
        var entry = await EntryIdAsync(c, t, "G4901");
        var version = await ContentVersionAsync(c, t, entry);

        var outcome = await VocabRootEditor.SetEntryRootAsync(c, t, entry, new SetVocabEntryRootRequest { Id = entry, PrefixKeys = ["G4862", "G1909"], RootId = root }, null);

        outcome.Status.ShouldBe(VocabEditStatus.Ok);
        outcome.Response!.Bumped.ShouldBeFalse();
        (await ContentVersionAsync(c, t, entry)).ShouldBe(version);

        var row = await c.QuerySingleAsync<(string Origin, string Prefixes, string[] PrefixKeys, int RootId)>(
            """SELECT "Origin", "Prefixes", "PrefixKeys", "VocabRootId" FROM "unshackled-word"."VocabStrongsRoots" WHERE "Lang" = 'G' AND "StrongsKey" = 'G4901'""", transaction: t);
        row.Origin.ShouldBe("manual");
        row.Prefixes.ShouldBe("σύν + ἐπί");
        row.PrefixKeys.ShouldBe(["G4862", "G1909"]);
        row.RootId.ShouldBe(root);

        var change = await c.QuerySingleAsync<(string Kind, int ContentVersion)>("""SELECT "Kind", "ContentVersion" FROM "unshackled-word"."VocabEntryChanges" WHERE "VocabEntryId" = @Id ORDER BY "Id" DESC LIMIT 1""", new { Id = entry }, t);
        change.Kind.ShouldBe("root");
        change.ContentVersion.ShouldBe(version);
    });

    [Fact]
    public Task Root_change_is_not_shown_to_learners() => WithTransactionAsync(async (c, t) =>
    {
        var root = await MartyreoAsync(c, t);
        await AgentCompoundAsync(c, t, root);
        var entry = await EntryIdAsync(c, t, "G4901");
        var user = await NewUserAsync(c, t);
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabCards" ("UserId", "VocabEntryId", "State", "Stability", "Difficulty", "Due", "Reps", "Lapses", "LastReview", "SeenContentVersion")
            SELECT @UserId, e."Id", 'review', 5, 5, now(), 3, 0, now(), e."ContentVersion"
            FROM "unshackled-word"."VocabEntries" e WHERE e."Id" = @EntryId
            """, new { UserId = user, EntryId = entry }, t);

        await VocabRootEditor.SetEntryRootAsync(c, t, entry, new SetVocabEntryRootRequest { Id = entry, PrefixKeys = ["G4862", "G1909"], RootId = root }, null);

        // no bump: no "changed words" notice
        (await VocabChangeService.CountAsync(c, t, user)).ShouldBe(0);

        // a real change flags the card; the list shows it but not the root edit made at the same version
        await VocabEntryEditor.UpdateEntryAsync(c, t, entry, "συνεπιμαρτυρέω (Test)", null, false, null);
        await VocabRootEditor.SetEntryRootAsync(c, t, entry, new SetVocabEntryRootRequest { Id = entry, PrefixKeys = ["G4862"], RootId = root }, null);

        var list = (await VocabChangeService.ListAsync(c, t, user)).Response!;
        var kinds = list.Items.Single().Changes.Select(x => x.Kind).ToList();
        kinds.ShouldContain("form");
        kinds.ShouldNotContain("root");
    });

    [Fact]
    public Task Greek_root_by_key_is_created_and_simple_word_is_its_own_root() => WithTransactionAsync(async (c, t) =>
    {
        var entry = await EntryIdAsync(c, t, "G4901");

        (await VocabRootEditor.SetEntryRootAsync(c, t, entry, new SetVocabEntryRootRequest { Id = entry, PrefixKeys = ["G4862", "G1909"], RootKey = "G3140" }, null)).Status.ShouldBe(VocabEditStatus.Ok);
        (await c.QuerySingleAsync<string>("""SELECT "Root" FROM "unshackled-word"."VocabRoots" WHERE "StrongsKey" = 'G3140'""", transaction: t)).Normalize(System.Text.NormalizationForm.FormC).ShouldBe("μαρτυρέω".Normalize(System.Text.NormalizationForm.FormC));

        var simple = await EntryIdAsync(c, t, "G3141");
        (await VocabRootEditor.SetEntryRootAsync(c, t, simple, new SetVocabEntryRootRequest { Id = simple, Simple = true }, null)).Status.ShouldBe(VocabEditStatus.Ok);
        (await c.QuerySingleAsync<string>("""SELECT r."Root" FROM "unshackled-word"."VocabRoots" r JOIN "unshackled-word"."VocabStrongsRoots" s ON s."VocabRootId" = r."Id" WHERE s."StrongsKey" = 'G3141'""", transaction: t)).Normalize(System.Text.NormalizationForm.FormC).ShouldBe("μαρτυρία".Normalize(System.Text.NormalizationForm.FormC));
    });

    [Fact]
    public Task Invented_prefix_is_refused_with_the_validator_reason() => WithTransactionAsync(async (c, t) =>
    {
        var entry = await EntryIdAsync(c, t, "G4901");

        var outcome = await VocabRootEditor.SetEntryRootAsync(c, t, entry, new SetVocabEntryRootRequest { Id = entry, PrefixKeys = ["G3056"], RootKey = "G3140" }, null);

        outcome.Status.ShouldBe(VocabEditStatus.Invalid);
        outcome.Message.ShouldContain("preposition table");
        (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabStrongsRoots" WHERE "StrongsKey" = 'G4901'""", transaction: t)).ShouldBe(0);
    });

    [Fact]
    public Task Hebrew_root_without_verb_key_is_created_with_its_homograph() => WithTransactionAsync(async (c, t) =>
    {
        var entry = await c.QuerySingleAsync<int>("""
            SELECT e."Id" FROM "unshackled-word"."VocabEntries" e WHERE e."Track" = 'H' AND e."RetiredAt" IS NULL AND NOT e."IsMorpheme" ORDER BY e."FrequencyRank" NULLS LAST LIMIT 1
            """, transaction: t);

        var outcome = await VocabRootEditor.SetEntryRootAsync(c, t, entry, new SetVocabEntryRootRequest { Id = entry, RootLetters = "ענה", Homograph = 77 }, null);

        outcome.Status.ShouldBe(VocabEditStatus.Ok);
        var key = await c.QuerySingleAsync<string?>("""
            SELECT r."StrongsKey"
            FROM "unshackled-word"."VocabRoots" r
            WHERE r."Track" = 'H' AND r."Root" = 'ענה' AND r."Homograph" = 77
            """, transaction: t);
        key.ShouldBeNull();
    });

    [Fact]
    public Task Parent_is_set_and_cleared_and_a_cycle_is_refused() => WithTransactionAsync(async (c, t) =>
    {
        var martyreo = await MartyreoAsync(c, t);
        var martyria = await VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert { Track = "G", Root = "μαρτυρία", StrongsKey = "G3141" });

        (await VocabRootEditor.SetParentAsync(c, t, martyria, martyreo, null, null)).Status.ShouldBe(VocabEditStatus.Ok);
        (await c.QuerySingleAsync<int>("""SELECT "ParentRootId" FROM "unshackled-word"."VocabRoots" WHERE "Id" = @Id""", new { Id = martyria }, t)).ShouldBe(martyreo);

        var cycle = await VocabRootEditor.SetParentAsync(c, t, martyreo, martyria, null, null);
        cycle.Status.ShouldBe(VocabEditStatus.Invalid);
        cycle.Message.ShouldContain("cycle");
        cycle.Message.ShouldContain("μαρτυρία → μαρτυρέω");

        (await VocabRootEditor.SetParentAsync(c, t, martyreo, martyreo, null, null)).Status.ShouldBe(VocabEditStatus.Invalid);
        (await VocabRootEditor.SetParentAsync(c, t, martyria, null, null, null)).Status.ShouldBe(VocabEditStatus.Ok);
        (await c.QuerySingleAsync<int?>("""SELECT "ParentRootId" FROM "unshackled-word"."VocabRoots" WHERE "Id" = @Id""", new { Id = martyria }, t)).ShouldBeNull();
    });

    [Fact]
    public Task Parent_by_key_creates_the_greek_root_and_depth_over_four_is_refused() => WithTransactionAsync(async (c, t) =>
    {
        var martyria = await VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert { Track = "G", Root = "μαρτυρία", StrongsKey = "G3141" });

        (await VocabRootEditor.SetParentAsync(c, t, martyria, null, "G3140", null)).Status.ShouldBe(VocabEditStatus.Ok);
        (await c.QuerySingleAsync<string>("""SELECT p."Root" FROM "unshackled-word"."VocabRoots" r JOIN "unshackled-word"."VocabRoots" p ON p."Id" = r."ParentRootId" WHERE r."Id" = @Id""", new { Id = martyria }, t)).Normalize(System.Text.NormalizationForm.FormC).ShouldBe("μαρτυρέω".Normalize(System.Text.NormalizationForm.FormC));

        // a Hebrew chain of six roots: five links are one too many
        var ids = new List<int>();

        for (var i = 1; i <= 6; i++)
        {
            ids.Add(await VocabRootRepository.UpsertRootAsync(c, t, new VocabRootUpsert { Track = "H", Root = "טסט", Homograph = (short)(80 + i) }));
        }

        for (var i = 0; i < 4; i++)
        {
            (await VocabRootEditor.SetParentAsync(c, t, ids[i], ids[i + 1], null, null)).Status.ShouldBe(VocabEditStatus.Ok);
        }

        var tooDeep = await VocabRootEditor.SetParentAsync(c, t, ids[4], ids[5], null, null);
        tooDeep.Status.ShouldBe(VocabEditStatus.Invalid);
        tooDeep.Message.ShouldContain("levels");

        // the other track is refused
        (await VocabRootEditor.SetParentAsync(c, t, martyria, ids[0], null, null)).Status.ShouldBe(VocabEditStatus.Invalid);
    });
}
