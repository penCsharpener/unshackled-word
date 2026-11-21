using System.Data;
using Dapper;
using Npgsql;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0022.1 against a real Postgres (migrations up to 0039, STEP lexicon and word tables imported). Every test works inside one
/// transaction that is rolled back, with fixture lexicon and word rows (Greek numbers 90001+, lemma "ΖΖΤΕΣΤΑ"), so nothing stays in the
/// database. Set <c>UNSHACKLEDWORD_TEST_POSTGRES</c> (a copy of the dev database is the safe choice), otherwise the tests are skipped.
/// </summary>
public class VocabLexiconGeneratorDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const string Lemma = "ΖΖΤΕΣΤΑ";

    private static string? ConnectionString => Environment.GetEnvironmentVariable(EnvVar);

    private sealed record EntryRow(int Id, string Lemma, int Occurrences, int? FrequencyRank, int ContentVersion, string? DictionaryForm, string? DictionaryFormOrigin);

    private static async Task<(IDbConnection Connection, IDbTransaction Transaction)> BeginAsync()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), $"{EnvVar} is not set.");
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        return (connection, connection.BeginTransaction());
    }

    private static Task AddLexiconAsync(IDbConnection c, IDbTransaction t, int number, string? extra, string lexiconText) =>
        c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."StepStrongsLexicon"
                ("Id", "LanguageId", "Number", "Extra", "OriginalWord", "OriginalWordNoDiacritics", "Transliteration", "Morphology", "Gloss", "Lexicon")
            VALUES ((SELECT COALESCE(max("Id"), 0) + 1 FROM "unshackled-word"."StepStrongsLexicon"), 2, @Number, @Extra, @Lemma, 'ζζτεστα', 'zztesta', 'G:N-M', 'test', @Text)
            """, new { Number = number, Extra = extra, Lemma, Text = lexiconText }, t);

    private static Task AddWordsAsync(IDbConnection c, IDbTransaction t, string key, int count) =>
        c.ExecuteAsync("""
            INSERT INTO "unshackled-word"."StepGreekWords"
                ("Id", "LxxRefId", "PositionInVerse", "Type", "IsInNestleAland", "IsInTextusReceptus", "IsInOther", "Greek", "GreekNoDiacritics",
                 "Transliteration", "English", "DisambiguatedStrongs", "Morphology", "Lemma", "LemmaNoDiacritics", "Gloss", "Editions")
            SELECT (SELECT COALESCE(max("Id"), 0) FROM "unshackled-word"."StepGreekWords") + g, 1, g, 'x', true, true, false, 'ζ', 'ζ', 'z', 'z', @Key, 'G:N-M', 'ζ', 'ζ', 'z', 'NA'
            FROM generate_series(1, @Count) g
            """, new { Key = key, Count = count }, t);

    private static Task<EntryRow> EntryAsync(IDbConnection c, IDbTransaction t) =>
        c.QuerySingleAsync<EntryRow>("""
            SELECT e."Id", e."Lemma", e."Occurrences", e."FrequencyRank", e."ContentVersion", e."DictionaryForm", e."DictionaryFormOrigin"
            FROM "unshackled-word"."VocabEntries" e
            WHERE e."Track" = 'G' AND e."Lemma" = @Lemma
            """, new { Lemma }, t);

    private static async Task<List<string>> KeysAsync(IDbConnection c, IDbTransaction t, int entryId) =>
        (await c.QueryAsync<string>("""SELECT vs."StrongsKey" FROM "unshackled-word"."VocabStrongs" vs WHERE vs."VocabEntryId" = @entryId ORDER BY 1""", new { entryId }, t)).ToList();

    private static Task<string> SnapshotAsync(IDbConnection c, IDbTransaction t) =>
        c.QuerySingleAsync<string>("""
            SELECT md5(COALESCE((SELECT string_agg(e::text, '|' ORDER BY e."Id") FROM "unshackled-word"."VocabEntries" e), '')
                    || COALESCE((SELECT string_agg(vs::text, '|' ORDER BY vs."Lang", vs."StrongsKey") FROM "unshackled-word"."VocabStrongs" vs), '')
                    || COALESCE((SELECT string_agg(ch::text, '|' ORDER BY ch."Id") FROM "unshackled-word"."VocabEntryChanges" ch), ''))
            """, transaction: t);

    [Fact]
    public async Task Generate_GroupsTwoKeysWithTheSameOriginalWord_IntoOneEntry()
    {
        var (c, t) = await BeginAsync();
        using (c) using (t)
        {
            await AddLexiconAsync(c, t, 90001, null, "");
            await AddLexiconAsync(c, t, 90002, "a", "");
            await AddWordsAsync(c, t, "G90001", 3);
            await AddWordsAsync(c, t, "G90002A", 2);

            await VocabLexiconGenerator.GenerateAsync(c, t, null);

            var entry = await EntryAsync(c, t);
            (await KeysAsync(c, t, entry.Id)).ShouldBe(["G90001", "G90002A"]);
            entry.Occurrences.ShouldBe(5);
            entry.FrequencyRank.ShouldNotBeNull();
            (await c.QuerySingleAsync<string>("""SELECT "LemmaNormalized" FROM "unshackled-word"."VocabEntries" WHERE "Id" = @Id""", new { entry.Id }, t)).ShouldBe("ζζτεστα");
        }
    }

    [Fact]
    public async Task Generate_RerunChangesNothing()
    {
        var (c, t) = await BeginAsync();
        using (c) using (t)
        {
            await AddLexiconAsync(c, t, 90001, null, "");
            await AddWordsAsync(c, t, "G90001", 3);

            await VocabLexiconGenerator.GenerateAsync(c, t, null);
            var before = await SnapshotAsync(c, t);
            var second = await VocabLexiconGenerator.GenerateAsync(c, t, null);

            (await SnapshotAsync(c, t)).ShouldBe(before);
            second.Counts.Where(x => !x.Key.Contains('.')).ShouldAllBe(x => x.Value == 0);
        }
    }

    [Fact]
    public async Task Generate_RerunAddsANewKeyToTheMatchingEntryAndLogsIt()
    {
        var (c, t) = await BeginAsync();
        using (c) using (t)
        {
            await AddLexiconAsync(c, t, 90001, null, "");
            await AddWordsAsync(c, t, "G90001", 3);
            await VocabLexiconGenerator.GenerateAsync(c, t, null);
            var first = await EntryAsync(c, t);

            await AddLexiconAsync(c, t, 90003, "b", "");
            await AddWordsAsync(c, t, "G90003B", 4);
            await VocabLexiconGenerator.GenerateAsync(c, t, null);

            var entry = await EntryAsync(c, t);
            entry.Id.ShouldBe(first.Id);
            (await KeysAsync(c, t, entry.Id)).ShouldBe(["G90001", "G90003B"]);
            entry.Occurrences.ShouldBe(7);
            entry.ContentVersion.ShouldBe(first.ContentVersion + 1);
            (await c.QuerySingleAsync<int>("""SELECT count(*)::int FROM "unshackled-word"."VocabEntryChanges" WHERE "VocabEntryId" = @Id AND "Kind" = 'keys'""", new { entry.Id }, t)).ShouldBe(1);
        }
    }

    [Fact]
    public async Task Generate_GeneratesASafeFormAndNeverTouchesAManualOne()
    {
        var (c, t) = await BeginAsync();
        using (c) using (t)
        {
            await AddLexiconAsync(c, t, 90001, null, $"<b>{Lemma}</b>, -ου, ὁ (test)");
            await AddWordsAsync(c, t, "G90001", 3);
            await VocabLexiconGenerator.GenerateAsync(c, t, null);

            var generated = await EntryAsync(c, t);
            generated.DictionaryForm.ShouldBe($"{Lemma}, -ου, ὁ");
            generated.DictionaryFormOrigin.ShouldBe("generated");

            await c.ExecuteAsync("""UPDATE "unshackled-word"."VocabEntries" SET "DictionaryForm" = 'von Hand', "DictionaryFormOrigin" = 'manual' WHERE "Id" = @Id""", new { generated.Id }, t);
            await AddWordsAsync(c, t, "G90001", 2);
            await VocabLexiconGenerator.GenerateAsync(c, t, null);

            var after = await EntryAsync(c, t);
            after.DictionaryForm.ShouldBe("von Hand");
            after.DictionaryFormOrigin.ShouldBe("manual");
            after.ContentVersion.ShouldBe(generated.ContentVersion);
            after.Occurrences.ShouldBe(5);
        }
    }

    [Fact]
    public async Task Generate_LeavesTheFormNullWhenTheLexiconLineIsNotSafe()
    {
        var (c, t) = await BeginAsync();
        using (c) using (t)
        {
            await AddLexiconAsync(c, t, 90001, null, "<b>etwas anderes</b>, -ου, ὁ");
            await AddWordsAsync(c, t, "G90001", 1);
            await VocabLexiconGenerator.GenerateAsync(c, t, null);

            (await EntryAsync(c, t)).DictionaryForm.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Generate_GivesNoRankToHebrewMorphemes_AndRanksTheRestWithoutGaps()
    {
        var (c, t) = await BeginAsync();
        using (c) using (t)
        {
            await VocabLexiconGenerator.GenerateAsync(c, t, null);

            var morphemes = await c.QueryAsync<(bool IsMorpheme, int? Rank)>("""
                SELECT e."IsMorpheme", e."FrequencyRank"
                FROM "unshackled-word"."VocabEntries" e
                    JOIN "unshackled-word"."VocabStrongs" vs ON vs."VocabEntryId" = e."Id"
                WHERE vs."Lang" = 'H' AND substring(vs."StrongsKey" FROM '^H(\d+)')::int BETWEEN 9001 AND 9049
                """, transaction: t);
            Assert.SkipWhen(!morphemes.Any(), "The lexicon holds no H9001-H9049 entry.");
            morphemes.ShouldAllBe(m => m.IsMorpheme && m.Rank == null);

            var gaps = await c.QueryAsync<(string Track, int Ranked, int MaxRank)>("""
                SELECT e."Track", count(e."FrequencyRank")::int, COALESCE(max(e."FrequencyRank"), 0)::int
                FROM "unshackled-word"."VocabEntries" e
                GROUP BY e."Track"
                """, transaction: t);
            gaps.ShouldAllBe(g => g.Ranked == g.MaxRank);
        }
    }
}
