using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Infrastructure.Seed.Vocab;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0022.14 (vocab seed) against a real Postgres with the generated lexicon. Every test runs in one transaction that is rolled back.
/// Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c>, otherwise the tests are skipped.
/// </summary>
[Collection("VocabDatabase")]
public class VocabSeedDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

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

    private static async Task<VocabSeedData> ExportAsync(IDbConnection c, IDbTransaction t)
    {
        var data = await VocabSeedExporter.ReadAsync(c, t);
        Assert.SkipWhen(data.Entries.Count < 4, "The vocab lexicon is not generated in this database.");

        return data;
    }

    [Fact]
    public async Task RoundTrip_ExportWipeImportExport_GivesIdenticalFiles() => await WithTransactionAsync(async (c, t) =>
    {
        var data = await ExportAsync(c, t);
        var first = VocabSeedExporter.Serialize(data);

        foreach (var table in new[] { "VocabReviewLog", "VocabCards", "VocabEntryChanges", "VocabMeanings", "VocabStrongs", "VocabEntries" })
        {
            await c.ExecuteAsync($"""DELETE FROM "unshackled-word"."{table}" """, transaction: t);
        }

        var report = await VocabSeedImporter.ImportAsync(c, t, VocabSeedImporter.Parse(first[0].Bytes, first[1].Bytes, first[2].Bytes, first[3].Bytes, first[4].Bytes));
        report.Counts["created"].ShouldBe(data.Entries.Count);
        report.Counts["bumped"].ShouldBe(0);

        var second = VocabSeedExporter.Serialize(await VocabSeedExporter.ReadAsync(c, t));

        second.Count.ShouldBe(5);

        for (var i = 0; i < first.Count; i++)
        {
            second[i].Bytes.ShouldBe(first[i].Bytes, $"{first[i].Name} differs after the round trip");
        }
    });

    [Fact]
    public async Task RoundTrip_AfterASplit_TellsTheTwoEntriesWithOneLemmaApart() => await WithTransactionAsync(async (c, t) =>
    {
        await ExportAsync(c, t);
        var source = await c.QueryFirstOrDefaultAsync<(int Id, string Key)>("""
            SELECT s."VocabEntryId", max(s."StrongsKey" COLLATE "C")
            FROM "unshackled-word"."VocabStrongs" s
                JOIN "unshackled-word"."VocabEntries" e ON e."Id" = s."VocabEntryId"
            WHERE e."RetiredAt" IS NULL
            GROUP BY s."VocabEntryId"
            HAVING count(*) >= 2
            ORDER BY s."VocabEntryId"
            LIMIT 1
            """, transaction: t);
        Assert.SkipWhen(source.Id == 0, "No entry with two Strong's keys in this database.");

        // a split keeps the lemma (H1254B gets its own בָּרָא), so (Track, Lemma) alone is no identity any more
        var split = await VocabEntryEditor.MoveKeyAsync(c, t, source.Id, source.Key, null, null);
        split.Status.ShouldBe(VocabEditStatus.Ok);

        var data = await VocabSeedExporter.ReadAsync(c, t);
        var lemma = data.Entries.Single(e => e.Homograph == 2);
        data.Entries.Count(e => e.Track == lemma.Track && e.Lemma == lemma.Lemma).ShouldBe(2);
        data.Keys.Single(k => k.StrongsKey == source.Key).Homograph.ShouldBe(2);
        var first = VocabSeedExporter.Serialize(data);

        foreach (var table in new[] { "VocabReviewLog", "VocabCards", "VocabEntryChanges", "VocabMeanings", "VocabStrongs", "VocabEntries" })
        {
            await c.ExecuteAsync($"""DELETE FROM "unshackled-word"."{table}" """, transaction: t);
        }

        var report = await VocabSeedImporter.ImportAsync(c, t, VocabSeedImporter.Parse(first[0].Bytes, first[1].Bytes, first[2].Bytes, first[3].Bytes, first[4].Bytes));
        report.Counts["created"].ShouldBe(data.Entries.Count);
        report.Counts["keyConflicts"].ShouldBe(0);

        var second = VocabSeedExporter.Serialize(await VocabSeedExporter.ReadAsync(c, t));

        for (var i = 0; i < first.Count; i++)
        {
            second[i].Bytes.ShouldBe(first[i].Bytes, $"{first[i].Name} differs after the round trip");
        }

        // a second import over the same files matches every entry, including both homographs
        var again = await VocabSeedImporter.ImportAsync(c, t, VocabSeedImporter.Parse(first[0].Bytes, first[1].Bytes, first[2].Bytes, first[3].Bytes, first[4].Bytes));
        again.Counts["created"].ShouldBe(0);
        again.Counts["updated"].ShouldBe(0);
    });

    [Fact]
    public async Task ImportOverLearners_BumpsOnlyEntriesWhoseLearningContentChanged() => await WithTransactionAsync(async (c, t) =>
    {
        var data = await ExportAsync(c, t);
        var userId = await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "DisplayName")
            VALUES ('vocab-seed@example.org', 'vocab-seed@example.org', 'vocab-seed')
            RETURNING "Id"
            """, transaction: t);

        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabCards" ("UserId", "VocabEntryId", "State", "Stability", "Difficulty", "Due", "Reps", "Lapses", "LastReview", "SeenContentVersion")
            SELECT @UserId, e."Id", 'review', 10, 5, now(), 3, 0, now(), e."ContentVersion"
            FROM "unshackled-word"."VocabEntries" e
            WHERE e."Track" = 'G' AND e."FrequencyRank" BETWEEN 1 AND 3
            """, new { UserId = userId }, t);

        var top = data.Entries.Where(e => e.Track == "G" && e.FrequencyRank is >= 1 and <= 3).OrderBy(e => e.FrequencyRank).ToList();
        var formChanged = top[0];
        var silentChange = top[1];
        var meaningChanged = top[2];

        formChanged.DictionaryForm = (formChanged.DictionaryForm ?? "x") + " (Seed)";
        formChanged.DictionaryFormOrigin = "manual";
        silentChange.Occurrences += 1;
        data.Meanings.Add(new VocabSeedMeaningLine
        {
            Track = meaningChanged.Track,
            Lemma = meaningChanged.Lemma,
            German = "Seed-Testbedeutung",
            Rank = 99,
            IsAccepted = true,
            Origin = "manual"
        });

        var versionsBefore = await VersionsAsync(c, t);
        var report = await VocabSeedImporter.ImportAsync(c, t, data);

        report.Counts["created"].ShouldBe(0);
        report.Counts["bumped"].ShouldBe(2);
        report.Counts["updated"].ShouldBe(3);

        var versionsAfter = await VersionsAsync(c, t);
        var changed = versionsAfter.Where(v => versionsBefore[v.Key] != v.Value).Select(v => v.Key).ToList();
        changed.Count.ShouldBe(2);
        changed.ShouldContain(await EntryIdAsync(c, t, formChanged));
        changed.ShouldContain(await EntryIdAsync(c, t, meaningChanged));

        var kinds = (await c.QueryAsync<string>("""
            SELECT "Kind" FROM "unshackled-word"."VocabEntryChanges" WHERE "VocabEntryId" = ANY(@Ids) ORDER BY "Kind"
            """, new { Ids = changed.ToArray() }, t)).ToList();
        kinds.ShouldBe(["form", "meanings"]);

        (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabCards" WHERE "UserId" = @UserId AND "State" = 'review' AND "Reps" = 3""", new { UserId = userId }, t)).ShouldBe(3);

        var again = await VocabSeedImporter.ImportAsync(c, t, data);
        again.Counts["bumped"].ShouldBe(0);
        again.Counts["updated"].ShouldBe(0);
    });

    private static async Task<Dictionary<int, int>> VersionsAsync(IDbConnection c, IDbTransaction t) =>
        (await c.QueryAsync<(int Id, int Version)>("""SELECT "Id", "ContentVersion" FROM "unshackled-word"."VocabEntries" """, transaction: t)).ToDictionary(x => x.Id, x => x.Version);

    private static async Task<int> EntryIdAsync(IDbConnection c, IDbTransaction t, VocabSeedEntryLine line) =>
        await c.QuerySingleAsync<int>("""SELECT "Id" FROM "unshackled-word"."VocabEntries" WHERE "Track" = @Track AND "Lemma" = @Lemma AND "RetiredAt" IS NULL""", new { line.Track, line.Lemma }, t);
}
