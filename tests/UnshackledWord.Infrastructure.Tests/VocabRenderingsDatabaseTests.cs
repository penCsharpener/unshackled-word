using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0022.2 against a real Postgres (migrations up to 0039). The scenario works inside one transaction that is rolled back (the view
/// refresh included), with fixture verses at book 66, chapter 99 and Strong's keys G90001/G90002. Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c>
/// (a copy of the dev database is the safe choice), otherwise the tests are skipped. The refresh takes about 30 s and locks the view meanwhile.
/// </summary>
public class VocabRenderingsDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int VariantVerse = 66099001;
    private const int PlainVerse = 66099002;

    private static string? ConnectionString => Environment.GetEnvironmentVariable(EnvVar);

    /// <summary>Verse 1 has an Elberfelder 2027 variant, verse 2 has not. Both keys belong to one entry. Returns the entry id.</summary>
    private static async Task<int> SeedScenarioAsync(IDbConnection c, IDbTransaction t)
    {
        var revisionId = await c.QuerySingleAsync<int>("""SELECT r."Id" FROM "unshackled-word"."Revisions" r WHERE r."Code" = 'elb2027'""", transaction: t);

        // original words: key G90001 at position 1, key G90002 at position 2 of each verse
        foreach (var (verse, position, key) in new[] { (VariantVerse, 1, "G90001"), (VariantVerse, 2, "G90002"), (PlainVerse, 1, "G90001"), (PlainVerse, 2, "G90002") })
        {
            await c.ExecuteAsync("""
                INSERT INTO "unshackled-word"."StepGreekWords"
                    ("Id", "LxxRefId", "PositionInVerse", "Type", "IsInNestleAland", "IsInTextusReceptus", "IsInOther", "Greek", "GreekNoDiacritics",
                     "Transliteration", "English", "DisambiguatedStrongs", "Morphology", "Lemma", "LemmaNoDiacritics", "Gloss", "Editions")
                VALUES ((SELECT COALESCE(max("Id"), 0) + 1 FROM "unshackled-word"."StepGreekWords"), @Verse, @Position, 'x', true, true, false, 'ζ', 'ζ', 'z', 'z', @Key, 'G:N-M', 'ζ', 'ζ', 'z', 'NA')
                """, new { Verse = verse, Position = position, Key = key }, t);
        }

        // Elb1871: verse 1 "Wort Rede" (would count wort + rede, but has a variant), verse 2 "Rede Rede" (both keys render as "Rede")
        foreach (var (verse, words) in new[] { (VariantVerse, new[] { "Wort", "Rede" }), (PlainVerse, new[] { "Rede", "Rede" }) })
        {
            await c.ExecuteAsync("""INSERT INTO "unshackled-word"."Elb1871Verses" ("Id", "HebRefId", "LxxRefId", "VerseText") VALUES ((SELECT COALESCE(max("Id"), 0) + 1 FROM "unshackled-word"."Elb1871Verses"), @Verse, @Verse, @Text)""", new { Verse = verse, Text = string.Join(' ', words) }, t);

            foreach (var (position, word) in words.Select((w, i) => (i + 1, w)))
            {
                await c.ExecuteAsync("""
                    WITH "W" AS
                    (
                        INSERT INTO "unshackled-word"."Elb1871Words" ("Id", "BibleBookId", "Chapter", "Verse", "HebRefId", "PositionInVerse", "WordInContext", "PlainWord")
                        VALUES ((SELECT COALESCE(max("Id"), 0) + 1 FROM "unshackled-word"."Elb1871Words"), 66, 99, @Verse - 66099000, @Verse, @Position, @Word, @Word)
                        RETURNING "Id"
                    )
                    INSERT INTO "unshackled-word"."Elb1871GreekMapping" ("ElbWordId", "StepWordId", "HebRefId", "PositionInVerse", "Role")
                    SELECT w."Id", s."Id", @Verse, @Position, 'head'
                    FROM "W" w, "unshackled-word"."StepGreekWords" s
                    WHERE s."LxxRefId" = @Verse AND s."PositionInVerse" = @Position
                    """, new { Verse = verse, Position = position, Word = word }, t);
            }
        }

        // 2027 variant of verse 1: "Sprache" replaces "Wort", "Rede" stays
        var variantId = await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."RevisionVerses" ("HebRefId", "Version", "VerseText", "BaseTextHash", "RevisionId")
            VALUES (@Verse, 1, 'Sprache Rede', 'x', @RevisionId)
            RETURNING "Id"
            """, new { Verse = VariantVerse, RevisionId = revisionId }, t);

        foreach (var (position, word, change) in new[] { (1, "Sprache", "changed"), (2, "Rede", "same") })
        {
            await c.ExecuteAsync("""
                WITH "W" AS
                (
                    INSERT INTO "unshackled-word"."RevisionWords" ("RevisionVerseId", "PositionInVerse", "WordInContext", "PlainWord", "BasePositionInVerse", "Change")
                    VALUES (@VariantId, @Position, @Word, @Word, @Position, @Change)
                    RETURNING "Id"
                )
                INSERT INTO "unshackled-word"."RevisionMapping" ("RevisionWordId", "Lang", "StepWordId", "Origin", "Role")
                SELECT w."Id", 'G', s."Id", 'seed', 'head'
                FROM "W" w, "unshackled-word"."StepGreekWords" s
                WHERE s."LxxRefId" = @Verse AND s."PositionInVerse" = @Position
                """, new { VariantId = variantId, Verse = VariantVerse, Position = position, Word = word, Change = change }, t);
        }

        var entryId = await c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."VocabEntries" ("Track", "LanguageId", "Lemma", "LemmaNormalized", "Occurrences", "FrequencyRank")
            VALUES ('G', 2, 'ΖΖΡΕΝΔ', 'ζζρενδ', 4, 1)
            RETURNING "Id"
            """, transaction: t);
        await c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."VocabStrongs" ("Lang", "StrongsKey", "VocabEntryId", "Occurrences")
            VALUES ('G', 'G90001', @entryId, 2), ('G', 'G90002', @entryId, 2)
            """, new { entryId }, t);

        return entryId;
    }

    [Fact]
    public async Task Entry_CountsTheVariantLinksNotTheElb1871Ones_AndSumsOverBothKeys()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var entryId = await SeedScenarioAsync(connection, transaction);
        await connection.ExecuteAsync("""REFRESH MATERIALIZED VIEW "unshackled-word"."StatsVocabRenderings" """, transaction: transaction, commandTimeout: 900);
        await connection.ExecuteAsync("""UPDATE "unshackled-word"."DqRefreshState" SET "VocabRenderingsRefreshedAt" = now() WHERE "Id" = 1""", transaction: transaction);

        var entry = await new VocabEntryRepository(new TransactionDbReader(connection, transaction)).GetAsync(entryId);

        entry.ShouldNotBeNull();
        entry.StrongsKeys.Select(k => k.StrongsKey).ShouldBe(["G90001", "G90002"], ignoreOrder: true);
        entry.RefreshedAt.ShouldNotBeNull();

        // live text: verse 1 = variant (Sprache, Rede), verse 2 = Elb1871 (Rede, Rede); the Elb1871 link "Wort" of verse 1 does not count
        entry.TotalLinks.ShouldBe(4);
        entry.RenderingsTotal.ShouldBe(2);
        var byKey = entry.Renderings.ToDictionary(r => r.GermanKey);
        byKey.Keys.ShouldBe(["rede", "sprache"], ignoreOrder: true);

        // "rede" sums over both keys: G90002 in the variant (verse 1) + G90001 and G90002 in Elb1871 (verse 2)
        byKey["rede"].LinkCount.ShouldBe(3);
        byKey["rede"].FromRevisionCount.ShouldBe(1);
        byKey["rede"].Share.ShouldBe(75.0m);
        byKey["rede"].KeyIsForm.ShouldBeTrue();
        byKey["sprache"].LinkCount.ShouldBe(1);
        byKey["sprache"].FromRevisionCount.ShouldBe(1);
        byKey["sprache"].Share.ShouldBe(25.0m);
        entry.Renderings[0].GermanKey.ShouldBe("rede");

        // samples: both verses, the German text of verse 1 is the variant
        var sample = entry.SampleVerses.Single(s => s.BibleBookId == 66 && s.Chapter == 99 && s.Verse == 1);
        sample.FromRevision.ShouldBeTrue();
        sample.German.Select(w => w.Text).ShouldBe(["Sprache", "Rede"]);
        sample.German.All(w => w.IsHit).ShouldBeTrue();
        entry.SampleVerses.Single(s => s.Verse == 2).FromRevision.ShouldBeFalse();

        transaction.Rollback();
    }

    [Fact]
    public async Task Refresher_FillsTheView_AndStampsTheRefreshTime()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var factory = new VerseCountingRepositoryTests.DirectFactory(ConnectionString!);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var report = await new VocabRenderingsRefresher(factory).RefreshAsync(null, CancellationToken.None);

        report.Counts["rows"].ShouldBeGreaterThan(0);
        using var connection = new NpgsqlConnection(ConnectionString);
        var stamp = await connection.QuerySingleAsync<DateTime>("""SELECT s."VocabRenderingsRefreshedAt" FROM "unshackled-word"."DqRefreshState" s WHERE s."Id" = 1""");
        stamp.ToUniversalTime().ShouldBeGreaterThan(before);
    }

    /// <summary>Runs the repository's queries on the test's transaction so it sees the fixture rows.</summary>
    private sealed class TransactionDbReader : IDbReader
    {
        private readonly IDbConnection _connection;
        private readonly IDbTransaction _transaction;

        public TransactionDbReader(IDbConnection connection, IDbTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object? param = null) =>
            _connection.QueryFirstOrDefaultAsync<T>(sql, param, _transaction);

        public async Task<IEnumerable<T>> ReadAsListAsync<T>(string sql, object? param = null) =>
            await _connection.QueryAsync<T>(sql, param, _transaction);

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null) =>
            _connection.ExecuteScalarAsync<T>(sql, param, _transaction);

        public Task<List<T>> ReadMultipleAsListAsync<T>(string sql, object? param, Func<IMultiDbReader, Task<List<T>>> mappingFunc, CancellationToken token = default) =>
            throw new NotSupportedException();
    }
}
