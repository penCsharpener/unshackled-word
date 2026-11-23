using System.Data;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Tsk;
using UnshackledWord.Infrastructure.Services;
using UnshackledWord.Infrastructure.TskCuration;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0024.7 against a real Postgres (seeded TSK and Elb1871): export of <c>TskCurations</c> to <c>tsk/curations.csv</c> plus the manifest part,
/// the import rules (newer wins, unknown TSK key skipped, mirror deletes, audit <c>import</c>) and the one-time apply of SeedDb. Every test runs in one
/// rolled-back transaction and writes its files to a temp folder, never to <c>assets/seed</c>. Skipped without <c>UNSHACKLEDWORD_TEST_POSTGRES</c>.
/// </summary>
public class TskCurationSeedDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int Gen11 = 1_001_001;
    private const int Ps336 = 19_033_006;

    /// <summary>„Anfang“ in 1Mo 1,1.</summary>
    private const int Anfang = 2;

    /// <summary>Stored TSK keys under 1Mo 1,1.</summary>
    private static readonly TskLinkKey Spr822 = new(Gen11, 20_008_022, 20_008_024);

    private static readonly TskLinkKey Joh11 = new(Gen11, 43_001_001, 43_001_003);
    private static readonly TskLinkKey Heb110 = new(Gen11, 58_001_010, null);

    /// <summary>A link TSK does not have (added).</summary>
    private static readonly TskLinkKey AddedPs336 = new(Gen11, Ps336, null);

    private static async Task WithTransactionAsync(Func<IDbConnection, IDbTransaction, SeedLayout, TskCurationSeedService, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        var root = Path.Combine(Path.GetTempPath(), "tsk-seed-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            using var transaction = connection.BeginTransaction();

            // The test sees only its own curations; everything is rolled back.
            await connection.ExecuteAsync("""DELETE FROM "unshackled-word"."TskCurations" c""", null, transaction);

            var fileService = new FileService();
            var settings = new DatabaseSeedSettings { SolutionAssetsPath = root, SolutionTempPath = root };
            var layout = new SeedLayout(fileService, settings, Path.Combine(root, "seed"));
            var service = new TskCurationSeedService(Substitute.For<IDbConnectionFactory>(), fileService, new SeedManifestStore(fileService),
                Options.Create(new AppSettings { DatabaseSeeding = settings }), NullLogger<TskCurationSeedService>.Instance);

            await test(connection, transaction, layout, service);

            transaction.Rollback();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static Task<int> NewUserAsync(IDbConnection c, IDbTransaction t, string name) =>
        c.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "DisplayName")
            VALUES (@Name || '@example.org', @Name || '@example.org', @Name)
            RETURNING "Id"
            """, new { Name = name }, t);

    private static Task ApplyAsync(IDbConnection c, IDbTransaction t, TskLinkKey key, TskCurationState state, int userId) =>
        TskCurationRepository.ApplyAsync(c, t, key, _ => state, userId, TskCurationSources.Edit, TestContext.Current.CancellationToken);

    /// <summary>Three curations of 1Mo 1,1 by one user: a highlight with a CSV-hostile comment, a word link, an added link.</summary>
    private static async Task<(int UserId, string Name)> CurateAsync(IDbConnection c, IDbTransaction t)
    {
        var name = "tsk-seed-" + Guid.NewGuid().ToString("N");
        var user = await NewUserAsync(c, t, name);
        await ApplyAsync(c, t, Spr822, new TskCurationState(TskOrigins.Tsk, TskRatings.Highlight, "Weisheit, \"Anfang\"\nzweite Zeile", null, null, null), user);
        await ApplyAsync(c, t, Joh11, new TskCurationState(TskOrigins.Tsk, null, null, Anfang, Anfang, "Anfang"), user);
        await ApplyAsync(c, t, AddedPs336, new TskCurationState(TskOrigins.Added, null, "Wort des Herrn", null, null, null), user);

        return (user, name);
    }

    private static async Task<List<(TskCurationRecord Record, int? UserId)>> RowsAsync(IDbConnection c, IDbTransaction t)
    {
        var lines = await TskCurationSeed.ReadAsync(c, t, TestContext.Current.CancellationToken);
        var result = new List<(TskCurationRecord, int?)>();
        foreach (var line in lines)
        {
            var record = (await TskCurationRepository.GetAsync(c, t, new TskLinkKey(line.SourceRef, line.TargetStart, line.TargetEnd)))!;
            result.Add((record with { UpdatedByUserId = null }, record.UpdatedByUserId));
        }

        return result;
    }

    private static Task<List<(string Source, int? UserId, string? Before, string? After)>> ImportAuditAsync(IDbConnection c, IDbTransaction t) =>
        c.QueryAsync<(string, int?, string?, string?)>("""
            SELECT a."Source", a."UserId", a."Before"::text, a."After"::text
            FROM "unshackled-word"."TskCurationAudit" a
            WHERE 1=1
                AND a."Source" = 'import'
                AND a."SourceHebRefId" = @Gen11
            ORDER BY a."Id"
            """, new { Gen11 }, t).ContinueWith(r => r.Result.ToList());

    private static Task<SeedStepReport> ImportAsync(IDbConnection c, IDbTransaction t, SeedLayout layout, TskCurationSeedService service, bool mirror = false) =>
        service.ImportAsync(c, t, layout, mirror, null, TestContext.Current.CancellationToken);

    private static async Task WriteFileAsync(SeedLayout layout, IEnumerable<TskCurationSeedLine> lines)
    {
        var path = layout.Resolve(SeedLayout.TskCurationsPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, TskCurationSeed.Serialize(lines).Bytes, TestContext.Current.CancellationToken);
    }

    private static TskCurationSeedLine Line(TskLinkKey key, string origin, string? rating, DateTimeOffset updatedAt, string? comment = null) => new()
    {
        Source = TskCurationSeed.FormatSource(key.SourceHebRefId),
        SourceRef = key.SourceHebRefId,
        TargetStart = key.TargetStartHebRefId,
        TargetEnd = key.TargetEndHebRefId,
        Origin = origin,
        Rating = rating,
        Comment = comment,
        UpdatedAt = TskCurationSeedLine.FormatTime(updatedAt),
    };

    /// <summary>The 0024.7 "done when": export, delete everything, import gives identical rows; the file carries no user data.</summary>
    [Fact]
    public async Task RoundTrip_ExportDeleteImport_GivesIdenticalRows_AndTheFileHasNoUserData() => await WithTransactionAsync(async (c, t, layout, service) =>
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, name) = await CurateAsync(c, t);
        var before = await RowsAsync(c, t);
        before.Count.ShouldBe(3);
        before.ShouldAllBe(r => r.UserId == user);

        var export = await service.ExportAsync(c, t, layout, null, ct);
        export.Counts["rows"].ShouldBe(3);

        var path = layout.Resolve(SeedLayout.TskCurationsPath);
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var text = Encoding.UTF8.GetString(bytes);
        text.Split('\n')[0].ShouldBe("source,sourceRef,targetStart,targetEnd,origin,rating,startPosition,endPosition,quote,comment,updatedAt");
        text.ShouldNotContain(name);
        text.ShouldNotContain("@");
        text.ShouldNotContain("UserId", Case.Insensitive);
        text.ShouldContain("\"1Mo 1,1\",1001001,20008022,20008024,tsk,highlight,,,,\"Weisheit, \"\"Anfang\"\"");
        bytes.Take(3).ShouldNotBe(new byte[] { 0xEF, 0xBB, 0xBF });

        // sorted by source, target start, target end
        TskCurationSeed.Parse(bytes).Select(l => l.TargetStart).ShouldBe([Ps336, Spr822.TargetStartHebRefId, Joh11.TargetStartHebRefId]);

        var manifest = (await new SeedManifestStore(new FileService()).ReadAsync(layout.ManifestPath, ct))!;
        manifest.Tsk.ShouldNotBeNull();
        manifest.Tsk.Rows.ShouldBe(3);
        manifest.Tsk.Files.Single().Path.ShouldBe("tsk/curations.csv");
        manifest.Tsk.Files.Single().Sha256.ShouldBe(SeedCsv.Sha256Hex(bytes));

        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."TskCurations" c""", null, t);

        var report = await ImportAsync(c, t, layout, service);

        report.Counts[TskCurationSeed.Inserted].ShouldBe(3);
        report.Counts[TskCurationSeed.Skipped].ShouldBe(0);
        var after = await RowsAsync(c, t);
        after.Select(r => r.Record).ShouldBe(before.Select(r => r.Record));
        after.ShouldAllBe(r => r.UserId == null);

        var audit = await ImportAuditAsync(c, t);
        audit.Count.ShouldBe(3);
        audit.ShouldAllBe(a => a.Source == "import" && a.UserId == null && a.Before == null && a.After != null);

        // a second export of the imported rows is byte-identical
        await service.ExportAsync(c, t, layout, null, ct);
        (await File.ReadAllBytesAsync(path, ct)).ShouldBe(bytes);
    });

    [Fact]
    public async Task ReImport_OfAnUnchangedFile_ReportsAllUnchanged_AndWritesNoAudit() => await WithTransactionAsync(async (c, t, layout, service) =>
    {
        await CurateAsync(c, t);
        await service.ExportAsync(c, t, layout, null, TestContext.Current.CancellationToken);

        var report = await ImportAsync(c, t, layout, service);

        report.Counts[TskCurationSeed.Unchanged].ShouldBe(3);
        report.Counts[TskCurationSeed.Inserted].ShouldBe(0);
        report.Counts[TskCurationSeed.Updated].ShouldBe(0);
        report.Counts[TskCurationSeed.Deleted].ShouldBe(0);
        report.Counts[TskCurationSeed.Skipped].ShouldBe(0);
        (await ImportAuditAsync(c, t)).ShouldBeEmpty();
    });

    [Fact]
    public async Task Import_NewerFileWins_OlderFileIsKept_UnknownTskKeyIsSkippedAndListed() => await WithTransactionAsync(async (c, t, layout, service) =>
    {
        var ct = TestContext.Current.CancellationToken;
        await CurateAsync(c, t);
        var unknown = new TskLinkKey(Gen11, 66_022_021, null);
        (await TskCurationRepository.TskLinkExistsAsync(c, t, unknown, ct)).ShouldBeFalse();
        var now = DateTimeOffset.UtcNow;

        await WriteFileAsync(layout, [
            Line(Spr822, TskOrigins.Tsk, TskRatings.Hide, now.AddDays(1)),          // newer: wins
            Line(Joh11, TskOrigins.Tsk, TskRatings.Hide, now.AddDays(-1)),          // older: database kept
            Line(unknown, TskOrigins.Tsk, TskRatings.Highlight, now),               // no Tsk row: skipped
            Line(Heb110, TskOrigins.Tsk, TskRatings.Highlight, now),                // only in the file: inserted
        ]);

        var report = await ImportAsync(c, t, layout, service);

        report.Counts[TskCurationSeed.Updated].ShouldBe(1);
        report.Counts[TskCurationSeed.Unchanged].ShouldBe(1);
        report.Counts[TskCurationSeed.Inserted].ShouldBe(1);
        report.Counts[TskCurationSeed.Skipped].ShouldBe(1);
        report.Counts[TskCurationSeed.Deleted].ShouldBe(0);
        report.Lists[TskCurationSeed.SkippedList].Items.Single().ShouldBe("1001001 → 66022021: no TSK row with this key");
        (await TskCurationRepository.GetAsync(c, t, Spr822))!.State.Rating.ShouldBe(TskRatings.Hide);
        (await TskCurationRepository.GetAsync(c, t, Spr822))!.State.Comment.ShouldBeNull();
        (await TskCurationRepository.GetAsync(c, t, Joh11))!.State.QuotedText.ShouldBe("Anfang");
        (await TskCurationRepository.GetAsync(c, t, Heb110))!.State.Rating.ShouldBe(TskRatings.Highlight);
        (await TskCurationRepository.GetAsync(c, t, unknown)).ShouldBeNull();
        (await TskCurationRepository.GetAsync(c, t, AddedPs336)).ShouldNotBeNull(); // only in the database: kept
        (await ImportAuditAsync(c, t)).Count.ShouldBe(2);
    });

    [Fact]
    public async Task Mirror_PreviewCountsAndDeletesRowsOnlyInTheDatabase() => await WithTransactionAsync(async (c, t, layout, service) =>
    {
        var ct = TestContext.Current.CancellationToken;
        await CurateAsync(c, t);
        await service.ExportAsync(c, t, layout, null, ct);
        var lines = await service.ReadFileAsync(layout, ct);
        (await TskCurationSeed.CountMirrorDeletesAsync(c, t, lines!, ct)).ShouldBe(0);

        // a new highlight after the export
        var user = await NewUserAsync(c, t, "tsk-seed-" + Guid.NewGuid().ToString("N"));
        await ApplyAsync(c, t, Heb110, new TskCurationState(TskOrigins.Tsk, TskRatings.Highlight, null, null, null, null), user);
        (await TskCurationSeed.CountMirrorDeletesAsync(c, t, lines!, ct)).ShouldBe(1);

        var kept = await ImportAsync(c, t, layout, service);
        kept.Counts[TskCurationSeed.Deleted].ShouldBe(0);
        (await TskCurationRepository.GetAsync(c, t, Heb110)).ShouldNotBeNull();

        var mirrored = await ImportAsync(c, t, layout, service, mirror: true);

        mirrored.Counts[TskCurationSeed.Deleted].ShouldBe(1);
        mirrored.Counts[TskCurationSeed.Unchanged].ShouldBe(3);
        (await TskCurationRepository.GetAsync(c, t, Heb110)).ShouldBeNull();
        var audit = await ImportAuditAsync(c, t);
        audit.Single().After.ShouldBeNull();
        audit.Single().Before.ShouldNotBeNull();
    });

    [Fact]
    public async Task Import_WordLinks_DifferingQuoteIsImported_MissingPositionsAreSkipped() => await WithTransactionAsync(async (c, t, layout, service) =>
    {
        var now = DateTimeOffset.UtcNow;
        var drifted = Line(Spr822, TskOrigins.Tsk, null, now);
        drifted.StartPosition = Anfang;
        drifted.EndPosition = Anfang;
        drifted.Quote = "Uranfang";
        var missing = Line(Joh11, TskOrigins.Tsk, null, now);
        missing.StartPosition = 900;
        missing.EndPosition = 901;
        missing.Quote = "nichts";
        await WriteFileAsync(layout, [drifted, missing]);

        var report = await ImportAsync(c, t, layout, service);

        report.Counts[TskCurationSeed.Inserted].ShouldBe(1);
        report.Counts[TskCurationSeed.Skipped].ShouldBe(1);
        report.Lists[TskCurationSeed.SkippedList].Items.Single().ShouldContain("no words at positions 900..901");
        (await TskCurationRepository.GetAsync(c, t, Spr822))!.State.QuotedText.ShouldBe("Uranfang");
    });

    /// <summary>SeedDb: the file is applied once, only on an empty table; no file is no seed.</summary>
    [Fact]
    public async Task ApplyInitialSeed_OnlyWhenTheTableIsEmpty() => await WithTransactionAsync(async (c, t, layout, service) =>
    {
        var ct = TestContext.Current.CancellationToken;
        var missing = await service.ApplyInitialSeedAsync(c, t, layout, ct);
        missing.Skipped.ShouldBeTrue();
        missing.Reason.ShouldBe(SeedSkipReason.NoSeed);

        await CurateAsync(c, t);
        await service.ExportAsync(c, t, layout, null, ct);
        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."TskCurations" c""", null, t);

        var applied = await service.ApplyInitialSeedAsync(c, t, layout, ct);
        applied.Skipped.ShouldBeFalse();
        applied.Counts[TskCurationSeed.Inserted].ShouldBe(3);

        // a later start leaves the table alone, even when the database differs from the file
        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."TskCurations" c WHERE c."TargetStartHebRefId" = @Ps336""", new { Ps336 }, t);
        var again = await service.ApplyInitialSeedAsync(c, t, layout, ct);
        again.Skipped.ShouldBeTrue();
        again.Reason.ShouldBe(SeedSkipReason.TablesNotEmpty);
        (await TskCurationSeed.CountRowsAsync(c, t, ct)).ShouldBe(2);
    });

    [Fact]
    public void Format_SourceIsGermanReadable_TimeRoundTripsExactly()
    {
        TskCurationSeed.FormatSource(Gen11).ShouldBe("1Mo 1,1");
        TskCurationSeed.FormatSource(43_003_016).ShouldBe("Joh 3,16");
        TskCurationSeedLine.FormatTime(new DateTimeOffset(2026, 11, 23, 14, 0, 0, TimeSpan.Zero)).ShouldBe("2026-11-23T14:00:00Z");

        var precise = new DateTimeOffset(2026, 11, 23, 14, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);
        var text = TskCurationSeedLine.FormatTime(precise);
        text.ShouldBe("2026-11-23T14:00:00.123456Z");
        TskCurationSeedLine.TryParseTime(text, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(precise);
    }
}
