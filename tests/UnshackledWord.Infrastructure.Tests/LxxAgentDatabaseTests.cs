using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Lxx;
using UnshackledWord.Infrastructure.Repositories;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0028.7 against a real Postgres with the LXX text: the read SQL of <see cref="LxxAgentDataRepository"/>, migration 0052, the
/// language-aware supersede of <see cref="AgentStoreSql.SupersedeOpen"/>, and (only when <c>UNSHACKLEDWORD_LXX_FIXTURE_OUT</c> is set) the export of the
/// prompt-example fixture. Every write runs in a rolled-back transaction. Skipped without <c>UNSHACKLEDWORD_TEST_POSTGRES</c>.
/// </summary>
[Collection("LxxDatabase")]
public class LxxAgentDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const string FixtureEnvVar = "UNSHACKLEDWORD_LXX_FIXTURE_OUT";

    /// <summary>The units of the fixture: Gen 1:1, 1:3, Ruth 1:1, 1:2, 1:4 and the addition Job 36:28.1 (book, chapter, verse, subverse).</summary>
    public static readonly (int Book, int Chapter, int Verse, int Subverse)[] FixtureUnits = [(1, 1, 1, 0), (1, 1, 3, 0), (8, 1, 1, 0), (8, 1, 2, 0), (8, 1, 4, 0), (18, 36, 28, 1)];

    private static async Task WithTransactionAsync(Func<NpgsqlConnection, NpgsqlTransaction, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await test(connection, transaction);

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UnshackledWord.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Solution root not found.");
    }

    [Fact]
    public async Task Migration0052_RunsTwice_AndAllowsTheModeAndFiveDigitStrongs() => await WithTransactionAsync(async (c, t) =>
    {
        var script = await File.ReadAllTextAsync(Path.Combine(SolutionRoot(), "src", "UnshackledWord.Tooling.Postgres.Migration", "scripts", "0052_LxxAgentMode.sql"), TestContext.Current.CancellationToken);
        await c.ExecuteAsync(script, transaction: t);
        await c.ExecuteAsync(script, transaction: t);

        var check = await c.ExecuteScalarAsync<string>("""
            SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'AgentRuns_Mode_CK'
            """, transaction: t);
        check.ShouldContain("lxx-tag");
        var strongs = await c.ExecuteScalarAsync<string>("""
            SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'LxxWordTags_Strongs_CK'
            """, transaction: t);
        strongs.ShouldContain("{4,5}");
    });

    [Fact]
    public async Task ReadSql_RuthOne_HasUnitsWordsAndHebrew_AndTheLexiconKeysMatchTheTagFormat() => await WithTransactionAsync(async (c, t) =>
    {
        var units = (await c.QueryAsync<LxxAgentUnitRow>(LxxAgentSql.RangeUnits, new { BibleBookId = 8, FromChapter = 1, FromVerse = 1, ToChapter = 1, ToVerse = 999 }, t)).ToList();
        Assert.SkipWhen(units.Count == 0, "The LXX text is not imported.");
        units.Count.ShouldBe(22);
        units.Select(u => u.SortOrder).ShouldBeInOrder();

        var words = (await c.QueryAsync<LxxAgentWordRow>(LxxAgentSql.Words, new { Ids = units.Select(u => u.Id).ToArray() }, t)).ToList();
        words.Count.ShouldBe(515);
        var hebrew = (await c.QueryAsync<AgentSourceWordRow>(LxxAgentSql.HebrewWords, new { HebRefIds = units.Select(u => u.HebRefId!.Value).ToArray() }, t)).ToList();
        hebrew.Where(h => h.RefId == 8_001_001).Select(h => h.Position).ShouldBe(Enumerable.Range(1, 19));

        var lexicon = (await c.QueryAsync<(string Strongs, string Lemma, string Gloss)>(LxxAgentSql.Lexicon, transaction: t)).ToList();
        lexicon.ShouldContain(e => e.Strongs == "G0746" && LxxLexicon.Key(e.Lemma) == LxxLexicon.Key("ἀρχή"));
        lexicon.ShouldContain(e => e.Strongs == "G1487G");
        lexicon.ShouldContain(e => e.Strongs.Length == 6 && e.Strongs.StartsWith("G2"));
        lexicon.ShouldAllBe(e => LxxLexicon.NormalizeStrongs(e.Strongs) == e.Strongs);
    });

    /// <summary>
    /// The regression of 0028.7: an LXX proposal for Gen 1:1 supersedes only the open LXX proposal of the same unit (text form, subverse), never an open
    /// Hebrew mapping proposal of Gen 1:1; a second Hebrew proposal still supersedes the first one.
    /// </summary>
    [Fact]
    public async Task SupersedeOpen_IsLanguageAndUnitAware() => await WithTransactionAsync(async (c, t) =>
    {
        var userId = await c.ExecuteScalarAsync<int?>("""SELECT u."Id" FROM "unshackled-word"."Users" u ORDER BY u."Id" LIMIT 1""", transaction: t);
        Assert.SkipWhen(userId is null, "No user in the database.");

        // Gen 1:1 may have open proposals in the dev database: close them inside the transaction so the counts are this test's own.
        await c.ExecuteAsync("""UPDATE "unshackled-word"."AgentVerseProposals" SET "Status" = 'discarded' WHERE "BibleBookId" = 1 AND "Chapter" = 1 AND "Verse" = 1 AND "Status" = 'open'""", transaction: t);
        var runId = await c.ExecuteScalarAsync<int>(AgentStoreSql.InsertRun, new
        {
            CreatedByUserId = userId, AiUserId = userId, Profile = "test", Model = "test", PromptVersion = "0", BibleBookId = 1, FromHebRefId = 1_001_001, ToHebRefId = 1_001_001,
            Mode = "redo", Status = "completed", StatusNote = (string?)null, EstimatedTokens = (long?)null, EstimatedCostUsd = (decimal?)null, MaxTokens = (long?)null, MaxCostUsd = (decimal?)null, ApiTokenId = (int?)null, IncludeConfirmed = false, TextForm = (string?)null,
        }, t);
        var packageId = Guid.NewGuid();
        await c.ExecuteAsync(AgentStoreSql.InsertPackage, new { Id = packageId, RunId = runId, FromHebRefId = 1_001_001, ToHebRefId = 1_001_001, WordIdsHash = "x", LocalIdMap = "{}", ExpiresAt = DateTime.UtcNow.AddHours(1) }, t);

        async Task<int> StoreAsync(AgentProposalInsert p)
        {
            var superseded = await c.ExecuteAsync(AgentStoreSql.SupersedeOpen, new { p.BibleBookId, p.Chapter, p.Verse, p.Language, p.TextForm, Subverse = p.Subverse.ToString(System.Globalization.CultureInfo.InvariantCulture) }, t);
            await c.ExecuteScalarAsync<long>(AgentStoreSql.InsertProposal, new
            {
                RunId = runId, PackageId = packageId, p.BibleBookId, p.Chapter, p.Verse, p.Language, Links = p.LinksJson, Lemmas = p.LemmasJson, Parts = p.PartsJson,
                Roles = p.RolesJson, Flags = p.FlagsJson, p.Note,
            }, t);
            return superseded;
        }

        AgentProposalInsert Hebrew() => new() { BibleBookId = 1, Chapter = 1, Verse = 1, Language = "hebrew", FlagsJson = """[{"code":"many-to-one","message":"m"}]""" };
        AgentProposalInsert Lxx(string textForm = "", int subverse = 0) => new()
        {
            BibleBookId = 1, Chapter = 1, Verse = 1, Language = "lxx", TextForm = textForm, Subverse = subverse,
            FlagsJson = JsonSerializer.Serialize(new LxxProposalFlags { TextForm = textForm, Subverse = subverse, LxxVerseId = 1 }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        };

        (await StoreAsync(Hebrew())).ShouldBe(0);
        (await StoreAsync(Lxx())).ShouldBe(0, "an LXX proposal does not touch the open Hebrew proposal of Gen 1:1");
        (await StoreAsync(Lxx(subverse: 1))).ShouldBe(0, "another subverse is another unit");
        (await StoreAsync(Lxx())).ShouldBe(1, "the same unit: the older LXX proposal is superseded");
        (await StoreAsync(Hebrew())).ShouldBe(1, "the Hebrew proposal is superseded only by a Hebrew one");

        var open = (await c.QueryAsync<(string Language, string? TextForm, string? Subverse)>("""
            SELECT p."Language", p."Flags" ->> 'textForm', p."Flags" ->> 'subverse'
            FROM "unshackled-word"."AgentVerseProposals" p
            WHERE p."RunId" = @RunId AND p."Status" = 'open'
            ORDER BY p."Id"
            """, new { RunId = runId }, t)).ToList();
        open.ShouldBe([("lxx", "", "1"), ("lxx", "", "0"), ("hebrew", null, null)]);

        (await c.ExecuteScalarAsync<int>(AgentStoreSql.CountProposedVerses, new { PackageId = packageId }, t)).ShouldBe(3, "Hebrew Gen 1:1, LXX Gen 1:1 and LXX Gen 1:1.1");
        (await c.QueryAsync<int>(LxxAgentSql.ProposedUnitIds, new { RunId = runId }, t)).ShouldBe([1]);
    });

    /// <summary>
    /// The real repository and builder over Ruth (dry run, nothing stored): one package per chapter, every unit with its Hebrew verse. The size figures
    /// of the 0028.7 status come from here (written to <c>UNSHACKLEDWORD_LXX_STATS_OUT</c> when set).
    /// </summary>
    [Fact]
    public async Task DryRun_Ruth_OnePackagePerChapter()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");
        using var factory = new VerseCountingRepositoryTests.DirectFactory(connectionString!);
        var builder = new LxxAgentPackageBuilder(new LxxAgentDataRepository(factory), Substitute.For<IAgentStoreRepository>(), Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(), TimeProvider.System);

        var built = await builder.BuildAsync(new AgentBuildRequest
        {
            Range = new AgentRange { BibleBookId = 8, FromChapter = 1, FromVerse = 1, ToChapter = 4, ToVerse = 999 },
            Mode = AgentMode.LxxTag,
            DryRun = true,
        }, TestContext.Current.CancellationToken);
        Assert.SkipWhen(built is null, "The LXX text is not imported.");

        built!.Packages.Select(p => p.Package.Verses.Count).ShouldBe([22, 23, 18, 22]);
        built.Packages.SelectMany(p => p.Package.Verses).ShouldAllBe(v => v.Heb != null && v.Heb.Count > 0);
        built.Stats.GermanTokens.ShouldBe(2066, "LXX words of Ruth");

        if (Environment.GetEnvironmentVariable("UNSHACKLEDWORD_LXX_STATS_OUT") is { Length: > 0 } statsPath)
        {
            var lines = built.Packages.Select(p => $"package {p.FromRefId}-{p.ToRefId}: {p.Package.Verses.Count} verses, {p.Stats.GermanTokens} words, {p.EstimatedTokens} est. tokens, {JsonSerializer.Serialize(p.Package, AgentPackageJson.Options).Length} chars, prefilled {p.Stats.Hint}, open {p.Stats.Open}");
            await File.WriteAllLinesAsync(statsPath, lines, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Writes <c>Fixtures/lxx-prompt-examples.json</c> of the Application tests when <c>UNSHACKLEDWORD_LXX_FIXTURE_OUT</c> names the file; read-only.</summary>
    [Fact]
    public async Task ExportPromptFixture() => await WithTransactionAsync(async (c, t) =>
    {
        var path = Environment.GetEnvironmentVariable(FixtureEnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(path), $"{FixtureEnvVar} is not set.");

        var units = new List<LxxAgentUnitRow>();
        foreach (var (book, chapter, verse, subverse) in FixtureUnits)
        {
            units.AddRange(await c.QueryAsync<LxxAgentUnitRow>("""
                SELECT l."Id", l."BibleBookId", l."Chapter", l."Verse", l."Subverse", l."TextForm", l."HebRefId", l."MapKind", l."SortOrder"
                FROM "unshackled-word"."LxxVerses" l
                WHERE l."BibleBookId" = @Book AND l."Chapter" = @Chapter AND l."Verse" = @Verse AND l."Subverse" = @Subverse
                """, new { Book = book, Chapter = chapter, Verse = verse, Subverse = subverse }, t));
        }

        var words = (await c.QueryAsync<LxxAgentWordRow>(LxxAgentSql.Words, new { Ids = units.Select(u => u.Id).ToArray() }, t)).ToList();
        var hebrew = (await c.QueryAsync<AgentSourceWordRow>(LxxAgentSql.HebrewWords, new { HebRefIds = units.Where(u => u.HebRefId is not null).Select(u => u.HebRefId!.Value).ToArray() }, t)).ToList();
        var lexicon = (await c.QueryAsync<(string Strongs, string Lemma, string Gloss)>(LxxAgentSql.Lexicon, transaction: t)).Select(r => new LxxLexiconEntry(r.Strongs, r.Lemma, r.Gloss)).ToList();
        var nt = (await c.QueryAsync<(string Greek, string Strongs, int Count)>(LxxAgentSql.NtForms, transaction: t)).Select(r => new LxxNtFormCount(r.Greek, r.Strongs, r.Count)).ToList();

        // Keep what the candidate index can reach for these words (see LxxCandidateIndex): NT rows of the same form (with or without accents),
        // TBESG entries sharing the first MinStem letters with the form or the form without augment, and the prefill Strong's.
        var forms = words.Select(w => LxxFormNormalizer.Normalize(w.Greek)).ToHashSet();
        var bares = words.Select(w => LxxLexicon.Bare(w.Greek)).ToList();
        var prefixes = bares.Concat(bares.Where(b => b.Length > LxxCandidateIndex.MinStem + 1 && b[0] == 'ε').Select(b => b[1..]))
            .Select(b => b[..Math.Min(b.Length, LxxCandidateIndex.MinStem)]).Where(p => p.Length > 0).ToHashSet();
        var ntPrefixes = bares.Where(b => b.Length >= LxxCandidateIndex.MinNtStem).Select(b => b[..LxxCandidateIndex.MinNtStem]).ToHashSet();
        var ntKept = nt.Where(f => forms.Contains(LxxFormNormalizer.Normalize(f.Greek)) || bares.Contains(LxxLexicon.Bare(f.Greek))
                                   || ntPrefixes.Any(p => LxxLexicon.Bare(f.Greek).StartsWith(p, StringComparison.Ordinal))).ToList();
        var strongs = ntKept.Select(f => f.Strongs).Concat(words.Where(w => w.TagStrongs is not null).Select(w => w.TagStrongs!)).ToHashSet();
        var lexKept = lexicon.Where(e => strongs.Contains(e.Strongs) || prefixes.Any(p => LxxLexicon.Bare(e.Lemma.Split(',')[0]).StartsWith(p, StringComparison.Ordinal))).ToList();
        var kept = lexKept.Select(e => e.Strongs).ToHashSet();

        // One row with an empty form per Strong's carries the rest of its NT count, so the frequency ranking is the full database's.
        var totals = nt.GroupBy(f => f.Strongs).ToDictionary(g => g.Key, g => g.Sum(f => f.Count));
        var keptTotals = ntKept.GroupBy(f => f.Strongs).ToDictionary(g => g.Key, g => g.Sum(f => f.Count));
        var rest = kept.Where(totals.ContainsKey).Select(s => new LxxNtFormCount(string.Empty, s, totals[s] - keptTotals.GetValueOrDefault(s))).Where(f => f.Count > 0);

        var fixture = new { units, words, hebrew, lexicon = lexKept, ntForms = ntKept.Concat(rest).OrderBy(f => f.Strongs, StringComparer.Ordinal).ThenBy(f => f.Greek, StringComparer.Ordinal).ToList() };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = false, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        await File.WriteAllTextAsync(path!, JsonSerializer.Serialize(fixture, options) + "\n", TestContext.Current.CancellationToken);
    });
}
