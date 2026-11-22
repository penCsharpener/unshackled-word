using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Domain.Models.Vocab;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0022.4 (vocab editor) against a real Postgres with the generated vocab lexicon (λόγος G3056 = entry with one key, בָּרָא with
/// H1254A and H1254B). Every test runs in one transaction that is rolled back. Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c> (a copy of the dev
/// database is the safe choice), otherwise the tests are skipped.
/// </summary>
[Collection("VocabDatabase")]
public class VocabEntryEditorDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    private sealed record EntryState(int Version, string? Form, string? Origin, int Occurrences, int? Rank, DateTime? ReviewedAt, DateTime? RetiredAt);

    private static async Task WithTransactionAsync(Func<IDbConnection, IDbTransaction, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        await test(connection, transaction);

        transaction.Rollback();
    }

    private static async Task<int> UserAsync(IDbConnection c, IDbTransaction t) =>
        await c.QueryFirstAsync<int>("""SELECT "Id" FROM "unshackled-word"."Users" ORDER BY "Id" LIMIT 1""", transaction: t);

    private static async Task<int> EntryOfAsync(IDbConnection c, IDbTransaction t, string key) =>
        await c.QuerySingleAsync<int>("""SELECT "VocabEntryId" FROM "unshackled-word"."VocabStrongs" WHERE "StrongsKey" = @Key""", new { Key = key }, t);

    private static async Task<EntryState> StateAsync(IDbConnection c, IDbTransaction t, int id) =>
        await c.QuerySingleAsync<EntryState>("""
            SELECT e."ContentVersion" AS "Version", e."DictionaryForm" AS "Form", e."DictionaryFormOrigin" AS "Origin", e."Occurrences", e."FrequencyRank" AS "Rank", e."ReviewedAt", e."RetiredAt"
            FROM "unshackled-word"."VocabEntries" e
            WHERE e."Id" = @Id
            """, new { Id = id }, t);

    private static async Task<List<string>> ChangeKindsAsync(IDbConnection c, IDbTransaction t, int id) =>
        (await c.QueryAsync<string>("""SELECT "Kind" FROM "unshackled-word"."VocabEntryChanges" WHERE "VocabEntryId" = @Id ORDER BY "Id" """, new { Id = id }, t)).ToList();

    private static async Task<List<string>> KeysOfAsync(IDbConnection c, IDbTransaction t, int id) =>
        (await c.QueryAsync<string>("""SELECT "StrongsKey" FROM "unshackled-word"."VocabStrongs" WHERE "VocabEntryId" = @Id ORDER BY 1""", new { Id = id }, t)).ToList();

    /// <summary>Forces the deferred unique rank constraint and checks that every track is numbered 1..n without gaps.</summary>
    private static async Task AssertRanksConsistentAsync(IDbConnection c, IDbTransaction t)
    {
        await c.ExecuteAsync("SET CONSTRAINTS ALL IMMEDIATE", transaction: t);
        var tracks = await c.QueryAsync<(string Track, int Ranked, int Distinct, int Max)>("""
            SELECT e."Track"::text, count(e."FrequencyRank")::int, count(DISTINCT e."FrequencyRank")::int, COALESCE(max(e."FrequencyRank"), 0)::int
            FROM "unshackled-word"."VocabEntries" e
            GROUP BY e."Track"
            """, transaction: t);

        foreach (var track in tracks)
        {
            track.Distinct.ShouldBe(track.Ranked);
            track.Max.ShouldBe(track.Ranked);
        }

        (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabEntries" WHERE "FrequencyRank" IS NOT NULL AND ("RetiredAt" IS NOT NULL OR "IsMorpheme")""", transaction: t)).ShouldBe(0);
    }

    [Fact]
    public async Task FormEdit_BumpsOnce_SameFormTranslitAndReviewedBumpNothing() => await WithTransactionAsync(async (c, t) =>
    {
        var id = await EntryOfAsync(c, t, "G3056");
        await c.ExecuteAsync("""UPDATE "unshackled-word"."VocabEntries" SET "DictionaryForm" = NULL, "DictionaryFormOrigin" = NULL, "ReviewedAt" = NULL, "ReviewedByUserId" = NULL WHERE "Id" = @Id""", new { Id = id }, t);
        var start = await StateAsync(c, t, id);
        var changesBefore = (await ChangeKindsAsync(c, t, id)).Count;

        var edit = await VocabEntryEditor.UpdateEntryAsync(c, t, id, "λόγος, -ου, ὁ", "logos", false, null);

        edit.Status.ShouldBe(VocabEditStatus.Ok);
        edit.Response!.Bumped.ShouldBeTrue();
        var afterForm = await StateAsync(c, t, id);
        afterForm.Version.ShouldBe(start.Version + 1);
        afterForm.Form.ShouldBe("λόγος, -ου, ὁ");
        afterForm.Origin.ShouldBe("manual");
        (await ChangeKindsAsync(c, t, id)).Skip(changesBefore).ShouldBe(["form"]);

        // the same form again, a new transliteration and the reviewed mark are not learning-relevant
        var again = await VocabEntryEditor.UpdateEntryAsync(c, t, id, "λόγος, -ου, ὁ", "lógos", true, null);

        again.Response!.Bumped.ShouldBeFalse();
        var afterAgain = await StateAsync(c, t, id);
        afterAgain.Version.ShouldBe(afterForm.Version);
        afterAgain.ReviewedAt.ShouldNotBeNull();
        (await ChangeKindsAsync(c, t, id)).Count.ShouldBe(changesBefore + 1);

        // clearing the form is a change again, with no origin left
        var cleared = await VocabEntryEditor.UpdateEntryAsync(c, t, id, " ", "lógos", true, null);
        cleared.Response!.Bumped.ShouldBeTrue();
        var afterClear = await StateAsync(c, t, id);
        afterClear.Form.ShouldBeNull();
        afterClear.Origin.ShouldBeNull();
        afterClear.Version.ShouldBe(afterForm.Version + 1);
    });

    [Fact]
    public async Task UpdateEntry_UnknownEntryIsNotFound() => await WithTransactionAsync(async (c, t) =>
    {
        (await VocabEntryEditor.UpdateEntryAsync(c, t, -5, null, null, false, null)).Status.ShouldBe(VocabEditStatus.NotFound);
    });

    [Fact]
    public async Task RankChanges_BumpNothing() => await WithTransactionAsync(async (c, t) =>
    {
        var id = await EntryOfAsync(c, t, "G3056");
        var start = await StateAsync(c, t, id);
        var changesBefore = (await ChangeKindsAsync(c, t, id)).Count;

        // shift the entry's occurrences so that the recompute has to renumber the track, then renumber
        await c.ExecuteAsync("""UPDATE "unshackled-word"."VocabStrongs" SET "Occurrences" = "Occurrences" + 100000 WHERE "VocabEntryId" = @Id""", new { Id = id }, t);
        await c.ExecuteAsync("""UPDATE "unshackled-word"."VocabEntries" SET "Occurrences" = "Occurrences" + 100000 WHERE "Id" = @Id""", new { Id = id }, t);
        await VocabEntryEditor.RecomputeRanksAsync(c, t);

        var after = await StateAsync(c, t, id);
        after.Rank.ShouldBe(1);
        after.Version.ShouldBe(start.Version);
        (await ChangeKindsAsync(c, t, id)).Count.ShouldBe(changesBefore);
        await AssertRanksConsistentAsync(c, t);
    });

    [Fact]
    public async Task Meanings_ReorderAndPinBumpNothing_AcceptedSetChangesBumpOnce() => await WithTransactionAsync(async (c, t) =>
    {
        var id = await EntryOfAsync(c, t, "G3056");
        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id""", new { Id = id }, t);
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabMeanings" ("VocabEntryId", "German", "Rank", "IsAccepted", "Origin", "IsPinned")
            VALUES (@Id, 'Wort', 1, true, 'derived', false), (@Id, 'Rede', 2, true, 'derived', false), (@Id, 'Lehre', 3, false, 'derived', false)
            """, new { Id = id }, t);
        var ids = (await c.QueryAsync<int>("""SELECT "Id" FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id ORDER BY "Rank" """, new { Id = id }, t)).ToList();
        var start = await StateAsync(c, t, id);
        var changesBefore = (await ChangeKindsAsync(c, t, id)).Count;

        // reorder without touching the accepted set: no bump, no change row, ranks 1..n in the new order
        var reorder = await VocabEntryEditor.ReorderMeaningsAsync(c, t, id, [ids[1], ids[2], ids[0]], null);
        reorder.Response!.Bumped.ShouldBeFalse();
        (await c.QueryAsync<string>("""SELECT "German" FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id ORDER BY "Rank" """, new { Id = id }, t)).ShouldBe(["Rede", "Lehre", "Wort"]);
        (await c.QueryAsync<int>("""SELECT "Rank" FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id ORDER BY "Rank" """, new { Id = id }, t)).ShouldBe([1, 2, 3]);

        // a wrong id list is refused and changes nothing
        (await VocabEntryEditor.ReorderMeaningsAsync(c, t, id, [ids[0], ids[1]], null)).Status.ShouldBe(VocabEditStatus.Invalid);

        // pinning bumps nothing
        (await VocabEntryEditor.UpdateMeaningAsync(c, t, id, ids[0], null, true, null)).Response!.Bumped.ShouldBeFalse();
        (await StateAsync(c, t, id)).Version.ShouldBe(start.Version);
        (await ChangeKindsAsync(c, t, id)).Count.ShouldBe(changesBefore);

        // accepting an unaccepted meaning changes the set: one bump, one meanings row
        var accept = await VocabEntryEditor.UpdateMeaningAsync(c, t, id, ids[2], true, null, null);
        accept.Response!.Bumped.ShouldBeTrue();
        (await StateAsync(c, t, id)).Version.ShouldBe(start.Version + 1);
        (await ChangeKindsAsync(c, t, id)).Skip(changesBefore).ShouldBe(["meanings"]);

        // unaccepting a derived meaning pins it so that the next derivation keeps the decision
        var unaccept = await VocabEntryEditor.UpdateMeaningAsync(c, t, id, ids[1], false, null, null);
        unaccept.Response!.Bumped.ShouldBeTrue();
        (await c.QuerySingleAsync<bool>("""SELECT "IsPinned" FROM "unshackled-word"."VocabMeanings" WHERE "Id" = @Id""", new { Id = ids[1] }, t)).ShouldBeTrue();

        // a derived meaning cannot be deleted, a manual one can; the last accepted meaning is protected
        (await VocabEntryEditor.DeleteMeaningAsync(c, t, id, ids[1], null)).Status.ShouldBe(VocabEditStatus.Conflict);
        var add = await VocabEntryEditor.AddMeaningAsync(c, t, id, " Vernunft ", null);
        add.Response!.Bumped.ShouldBeTrue();
        (await VocabEntryEditor.AddMeaningAsync(c, t, id, "Vernunft", null)).Status.ShouldBe(VocabEditStatus.Conflict);
        var manualId = add.Response.MeaningId!.Value;
        (await c.QuerySingleAsync<string>("""SELECT "Origin" FROM "unshackled-word"."VocabMeanings" WHERE "Id" = @Id""", new { Id = manualId }, t)).ShouldBe("manual");

        var delete = await VocabEntryEditor.DeleteMeaningAsync(c, t, id, manualId, null);
        delete.Response!.Bumped.ShouldBeTrue();
        (await c.QueryAsync<int>("""SELECT "Rank" FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id ORDER BY "Rank" """, new { Id = id }, t)).ShouldBe([1, 2, 3]);

        var accepted = (await c.QueryAsync<int>("""SELECT "Id" FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id AND "IsAccepted" """, new { Id = id }, t)).ToList();
        accepted.Count.ShouldBe(2);
        (await VocabEntryEditor.UpdateMeaningAsync(c, t, id, accepted[0], false, null, null)).Response!.Bumped.ShouldBeTrue();
        (await VocabEntryEditor.UpdateMeaningAsync(c, t, id, accepted[1], false, null, null)).Status.ShouldBe(VocabEditStatus.Conflict);

        // a meaning of another entry is not found
        var other = await EntryOfAsync(c, t, "H1254A");
        (await VocabEntryEditor.UpdateMeaningAsync(c, t, other, ids[0], null, true, null)).Status.ShouldBe(VocabEditStatus.NotFound);
    });

    [Fact]
    public async Task MeaningOverride_ReplacesTheShownText_KeepsStats_AndBumpsOnce() => await WithTransactionAsync(async (c, t) =>
    {
        var id = await EntryOfAsync(c, t, "G3056");
        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id""", new { Id = id }, t);
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabMeanings" ("VocabEntryId", "German", "GermanKey", "Rank", "Count", "Share", "IsAccepted", "Origin", "IsPinned")
            VALUES (@Id, 'der', 'der', 1, 10437, 99.74, true, 'derived', false), (@Id, 'Rede', 'rede', 2, 5, 0.1, true, 'derived', false)
            """, new { Id = id }, t);
        var ids = (await c.QueryAsync<int>("""SELECT "Id" FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id ORDER BY "Rank" """, new { Id = id }, t)).ToList();
        var start = await StateAsync(c, t, id);

        var set = await VocabEntryEditor.UpdateMeaningAsync(c, t, id, ids[0], null, null, null, "  der, die, das ");
        set.Response!.Bumped.ShouldBeTrue();
        (await StateAsync(c, t, id)).Version.ShouldBe(start.Version + 1);
        var row = await c.QuerySingleAsync<(string German, string? Override, int? Count, decimal? Share)>("""SELECT "German", "GermanOverride", "Count", "Share" FROM "unshackled-word"."VocabMeanings" WHERE "Id" = @Id""", new { Id = ids[0] }, t);
        row.ShouldBe(("der", "der, die, das", 10437, 99.74m));

        // the accepted set the quiz and the lists read is the override
        (await c.QueryAsync<string>("""SELECT COALESCE("GermanOverride", "German") FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id AND "IsAccepted" ORDER BY "Rank" """, new { Id = id }, t))
            .ShouldBe(["der, die, das", "Rede"]);

        // the same text on another meaning is refused; an override equal to the derived text is none; blank removes it
        (await VocabEntryEditor.UpdateMeaningAsync(c, t, id, ids[1], null, null, null, "der, die, das")).Status.ShouldBe(VocabEditStatus.Conflict);
        (await VocabEntryEditor.UpdateMeaningAsync(c, t, id, ids[0], null, null, null, "")).Response!.Bumped.ShouldBeTrue();
        (await c.QuerySingleAsync<string?>("""SELECT "GermanOverride" FROM "unshackled-word"."VocabMeanings" WHERE "Id" = @Id""", new { Id = ids[0] }, t)).ShouldBeNull();
        (await VocabEntryEditor.UpdateMeaningAsync(c, t, id, ids[0], null, null, null, "der")).Response!.Bumped.ShouldBeFalse();
    });

    [Fact]
    public async Task MoveKey_ToNewEntry_SplitsOccurrencesAndKeepsRanksConsistent() => await WithTransactionAsync(async (c, t) =>
    {
        var user = await UserAsync(c, t);
        var source = await EntryOfAsync(c, t, "H1254B");
        (await KeysOfAsync(c, t, source)).ShouldBe(["H1254A", "H1254B"]);
        var start = await StateAsync(c, t, source);
        var keyOccurrences = await c.QuerySingleAsync<int>("""SELECT "Occurrences" FROM "unshackled-word"."VocabStrongs" WHERE "StrongsKey" = 'H1254B'""", transaction: t);

        var move = await VocabEntryEditor.MoveKeyAsync(c, t, source, "H1254B", null, user);

        move.Status.ShouldBe(VocabEditStatus.Ok);
        var created = move.Response!.TargetEntryId!.Value;
        created.ShouldNotBe(source);
        (await KeysOfAsync(c, t, source)).ShouldBe(["H1254A"]);
        (await KeysOfAsync(c, t, created)).ShouldBe(["H1254B"]);

        var sourceAfter = await StateAsync(c, t, source);
        var createdAfter = await StateAsync(c, t, created);
        createdAfter.Occurrences.ShouldBe(keyOccurrences);
        (sourceAfter.Occurrences + createdAfter.Occurrences).ShouldBe(start.Occurrences);
        move.Response.Occurrences.ShouldBe(sourceAfter.Occurrences);
        move.Response.TargetOccurrences.ShouldBe(createdAfter.Occurrences);
        sourceAfter.Version.ShouldBe(start.Version + 1);
        createdAfter.Version.ShouldBe(1);
        createdAfter.Rank.ShouldNotBeNull();

        var split = await LastChangeAsync(c, t, source);
        split.Kind.ShouldBe("split");
        split.Target.ShouldBe(created);
        split.Version.ShouldBe(sourceAfter.Version);
        split.User.ShouldBe(user);
        (await ChangeKindsAsync(c, t, created)).ShouldBeEmpty();

        var lemma = await c.QuerySingleAsync<(string Lemma, string Track, string Normalized)>("""SELECT "Lemma", "Track"::text, "LemmaNormalized" FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { Id = created }, t);
        lemma.Lemma.ShouldNotBeNullOrEmpty();
        lemma.Track.ShouldBe("H");
        lemma.Normalized.ShouldNotBeNullOrEmpty();

        await AssertRanksConsistentAsync(c, t);
    });

    [Fact]
    public async Task MoveKey_OnlyKeyToNewEntry_TargetFromAnotherTrackAndUnknownKeyAreRefused() => await WithTransactionAsync(async (c, t) =>
    {
        var logos = await EntryOfAsync(c, t, "G3056");
        var bara = await EntryOfAsync(c, t, "H1254A");

        (await VocabEntryEditor.MoveKeyAsync(c, t, logos, "G3056", null, null)).Status.ShouldBe(VocabEditStatus.Conflict);
        (await VocabEntryEditor.MoveKeyAsync(c, t, bara, "H1254A", logos, null)).Status.ShouldBe(VocabEditStatus.Conflict);
        (await VocabEntryEditor.MoveKeyAsync(c, t, bara, "H0001", null, null)).Status.ShouldBe(VocabEditStatus.NotFound);
        (await VocabEntryEditor.MoveKeyAsync(c, t, bara, "H1254A", bara, null)).Status.ShouldBe(VocabEditStatus.Invalid);
    });

    [Fact]
    public async Task MoveKey_ToExistingEntryAndBack_WritesKeysRowsOnBoth_EmptyEntryCanBeDeleted_EntryWithKeysCannot() => await WithTransactionAsync(async (c, t) =>
    {
        var source = await EntryOfAsync(c, t, "H1254B");
        var total = (await StateAsync(c, t, source)).Occurrences;
        var split = (await VocabEntryEditor.MoveKeyAsync(c, t, source, "H1254B", null, null)).Response!.TargetEntryId!.Value;

        // an entry with keys cannot be deleted
        var refused = await VocabEntryEditor.DeleteAsync(c, t, split, null);
        refused.Status.ShouldBe(VocabEditStatus.Conflict);
        (await StateAsync(c, t, split)).Version.ShouldBe(1);

        // move the key back to the existing entry: both get a keys row, the new entry is empty and unranked
        var back = await VocabEntryEditor.MoveKeyAsync(c, t, split, "H1254B", source, null);
        back.Status.ShouldBe(VocabEditStatus.Ok);
        (await ChangeKindsAsync(c, t, source)).TakeLast(2).ShouldBe(["split", "keys"]);
        (await ChangeKindsAsync(c, t, split)).ShouldBe(["keys"]);
        (await StateAsync(c, t, source)).Occurrences.ShouldBe(total);
        var emptied = await StateAsync(c, t, split);
        emptied.Occurrences.ShouldBe(0);
        emptied.Rank.ShouldBeNull();

        var delete = await VocabEntryEditor.DeleteAsync(c, t, split, null);
        delete.Status.ShouldBe(VocabEditStatus.Ok);
        (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { Id = split }, t)).ShouldBe(0);
        (await ChangeKindsAsync(c, t, split)).ShouldBe(["keys", "delete"]);
        await AssertRanksConsistentAsync(c, t);
    });

    [Fact]
    public async Task Merge_MovesKeysAndMeaningsWithoutDuplicates_DeletesSource_WritesRowsOnBoth() => await WithTransactionAsync(async (c, t) =>
    {
        var target = await EntryOfAsync(c, t, "H1254A");
        var total = (await StateAsync(c, t, target)).Occurrences;
        var source = (await VocabEntryEditor.MoveKeyAsync(c, t, target, "H1254B", null, null)).Response!.TargetEntryId!.Value;

        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = ANY(@Ids)""", new { Ids = new[] { source, target } }, t);
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabMeanings" ("VocabEntryId", "German", "Rank", "IsAccepted", "Origin", "IsPinned")
            VALUES (@Target, 'schaffen', 1, true, 'derived', false), (@Source, 'schaffen', 1, true, 'derived', false), (@Source, 'erschaffen', 2, true, 'manual', true)
            """, new { Target = target, Source = source }, t);
        var before = await StateAsync(c, t, target);

        var merge = await VocabEntryEditor.MergeAsync(c, t, source, target, await UserAsync(c, t));

        merge.Status.ShouldBe(VocabEditStatus.Ok);
        merge.Response!.TargetEntryId.ShouldBe(target);
        (await KeysOfAsync(c, t, target)).ShouldBe(["H1254A", "H1254B"]);
        var after = await StateAsync(c, t, target);
        after.Occurrences.ShouldBe(total);
        after.Version.ShouldBe(before.Version + 1);
        (await c.QueryAsync<(string German, int Rank, string Origin, bool Pinned)>("""SELECT "German", "Rank", "Origin", "IsPinned" FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id ORDER BY "Rank" """, new { Id = target }, t))
            .ShouldBe([("schaffen", 1, "derived", false), ("erschaffen", 2, "manual", true)]);
        (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { Id = source }, t)).ShouldBe(0);
        (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id""", new { Id = source }, t)).ShouldBe(0);

        var targetRow = await LastChangeAsync(c, t, target);
        targetRow.Kind.ShouldBe("merge");
        targetRow.Target.ShouldBe(source);
        targetRow.Version.ShouldBe(after.Version);
        var sourceRow = await LastChangeAsync(c, t, source);
        sourceRow.Kind.ShouldBe("merge");
        sourceRow.Target.ShouldBe(target);

        await AssertRanksConsistentAsync(c, t);

        (await VocabEntryEditor.MergeAsync(c, t, target, target, null)).Status.ShouldBe(VocabEditStatus.Invalid);
        (await VocabEntryEditor.MergeAsync(c, t, target, await EntryOfAsync(c, t, "G3056"), null)).Status.ShouldBe(VocabEditStatus.Conflict);
    });

    [Fact]
    public async Task Retire_SetsRetiredAtClearsRankBumpsOnce_RanksStayConsistent() => await WithTransactionAsync(async (c, t) =>
    {
        var id = await EntryOfAsync(c, t, "G3056");
        var start = await StateAsync(c, t, id);
        start.Rank.ShouldNotBeNull();

        var retire = await VocabEntryEditor.RetireAsync(c, t, id, null);

        retire.Status.ShouldBe(VocabEditStatus.Ok);
        var after = await StateAsync(c, t, id);
        after.RetiredAt.ShouldNotBeNull();
        after.Rank.ShouldBeNull();
        after.Version.ShouldBe(start.Version + 1);
        (await ChangeKindsAsync(c, t, id)).Last().ShouldBe("retire");
        await AssertRanksConsistentAsync(c, t);

        (await VocabEntryEditor.RetireAsync(c, t, id, null)).Status.ShouldBe(VocabEditStatus.Conflict);
    });

    /// <summary>The newest change row of an entry: version, kind, target entry and user.</summary>
    private static async Task<(int Version, string Kind, int? Target, int? User)> LastChangeAsync(IDbConnection c, IDbTransaction t, int id) =>
        await c.QuerySingleAsync<(int, string, int?, int?)>("""
            SELECT "ContentVersion", "Kind", "TargetEntryId", "ChangedByUserId"
            FROM "unshackled-word"."VocabEntryChanges"
            WHERE "VocabEntryId" = @Id
            ORDER BY "Id" DESC
            LIMIT 1
            """, new { Id = id }, t);
}
