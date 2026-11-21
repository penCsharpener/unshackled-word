using Dapper;
using Npgsql;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0022.3 against a real Postgres with a filled <c>StatsVocabRenderings</c> and the generated vocab lexicon (λόγος, G3056, in
/// course nt-greek). Everything runs in one transaction that is rolled back. Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c> (a copy of the dev
/// database is the safe choice), otherwise the test is skipped.
/// </summary>
public class VocabMeaningDeriverDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    [Fact]
    public async Task Derive_DryRunWritesNothing_RealRunKeepsManualAndPinnedRows_SecondRunChangesNothing()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var entry = await connection.QuerySingleAsync<(int Id, int Rank, int Version)>("""
            SELECT e."Id", e."FrequencyRank", e."ContentVersion"
            FROM "unshackled-word"."VocabEntries" e
            INNER JOIN "unshackled-word"."VocabStrongs" vs ON vs."VocabEntryId" = e."Id" AND vs."StrongsKey" = 'G3056'
            """, transaction: transaction);
        var scope = new VocabMeaningScope("nt-greek", entry.Rank, entry.Rank);
        var options = new VocabOptions();

        await connection.ExecuteAsync("""DELETE FROM "unshackled-word"."VocabMeanings" WHERE "VocabEntryId" = @Id""", new { entry.Id }, transaction);
        await connection.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabMeanings" ("VocabEntryId", "German", "Rank", "IsAccepted", "Origin", "IsPinned")
            VALUES (@Id, 'Zeichen', 1, true, 'manual', false), (@Id, 'Rede', 2, true, 'derived', true), (@Id, 'Alt', 3, true, 'derived', false)
            """, new { entry.Id }, transaction);

        async Task<List<(string German, int Rank, string Origin, bool Pinned)>> Meanings() =>
            (await connection.QueryAsync<(string, int, string, bool)>("""
                SELECT m."German", m."Rank", m."Origin", m."IsPinned"
                FROM "unshackled-word"."VocabMeanings" m
                WHERE m."VocabEntryId" = @Id
                ORDER BY m."Rank"
                """, new { entry.Id }, transaction)).ToList();

        var dry = await VocabMeaningDeriver.DeriveAsync(connection, transaction, options, scope, true, null, null);
        dry.Counts["entriesProcessed"].ShouldBe(1);
        dry.Counts["entriesChanged"].ShouldBe(1);
        (await Meanings()).Select(m => m.German).ShouldBe(["Zeichen", "Rede", "Alt"]);

        var real = await VocabMeaningDeriver.DeriveAsync(connection, transaction, options, scope, false, null, null);
        real.Counts["contentVersionBumps"].ShouldBe(1);
        var meanings = await Meanings();
        meanings[0].ShouldBe(("Zeichen", 1, "manual", false));
        meanings[1].ShouldBe(("Rede", 2, "derived", true));
        meanings.Select(m => m.German).ShouldContain("Wort");
        meanings.Select(m => m.German).ShouldNotContain("Alt");
        meanings.Where(m => m.Origin == "derived" && !m.Pinned).Count().ShouldBeLessThanOrEqualTo(6);
        meanings.Select(m => m.Rank).Distinct().Count().ShouldBe(meanings.Count);

        var version = await connection.QuerySingleAsync<int>("""SELECT "ContentVersion" FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { entry.Id }, transaction);
        version.ShouldBe(entry.Version + 1);
        var changes = await connection.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabEntryChanges" WHERE "VocabEntryId" = @Id AND "Kind" = 'meanings' AND "ContentVersion" = @Version""", new { entry.Id, Version = version }, transaction);
        changes.ShouldBe(1);

        var again = await VocabMeaningDeriver.DeriveAsync(connection, transaction, options, scope, false, null, null);
        again.Counts["entriesChanged"].ShouldBe(0);
        again.Counts["contentVersionBumps"].ShouldBe(0);
        (await connection.QuerySingleAsync<int>("""SELECT "ContentVersion" FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { entry.Id }, transaction)).ShouldBe(version);

        transaction.Rollback();
    }
}
