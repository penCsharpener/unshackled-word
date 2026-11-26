using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Lxx;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Lxx;
using UnshackledWord.Infrastructure.Services;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0028.8 against a real Postgres (Swete text and STEP Hebrew words imported), over Ruth 1:1: export of the confirmed tags and links to
/// <c>lxx/tags.csv</c> and <c>links.csv</c>, the import rules (anchor mismatch skipped and counted, merge never deletes, "Exakt wie Datei" deletes confirmed rows
/// the files lack but keeps proposals, audit <c>import</c> without user) and the one-time apply of SeedDb. Every test runs in one rolled-back transaction and writes
/// its files to a temp folder, never to <c>assets/seed</c>. Skipped without <c>UNSHACKLEDWORD_TEST_POSTGRES</c> or without the LXX text.
/// </summary>
[Collection("LxxDatabase")]
public partial class LxxSeedDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int RuthOneOne = 8_001_001;

    [GeneratedRegex("[Ͱ-Ͽἀ-῿֐-׿]")]
    private static partial Regex GreekOrHebrew();

    private sealed record Fixture(NpgsqlConnection C, NpgsqlTransaction T, SeedLayout Layout, LxxSeedService Service, int[] Words, int[] Hebrew, int UnitId);

    private sealed record TagState(int LxxWordId, string Lemma, string Strongs, string Morphology, string Status, string Source, int? UserId);

    private sealed record LinkState(int LxxWordId, int StepHebrewWordId, string Status, int? UserId);

    private static async Task WithRuthAsync(Func<Fixture, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        var root = Path.Combine(Path.GetTempPath(), "lxx-seed-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await using var c = new NpgsqlConnection(connectionString);
            await c.OpenAsync(TestContext.Current.CancellationToken);
            await using var t = await c.BeginTransactionAsync(TestContext.Current.CancellationToken);

            var unitId = await c.ExecuteScalarAsync<int?>("""SELECT l."Id" FROM "unshackled-word"."LxxVerses" l WHERE l."HebRefId" = @RuthOneOne ORDER BY l."SortOrder" LIMIT 1""", new { RuthOneOne }, t);
            Assert.SkipWhen(unitId is null, "The LXX text (Ruth 1:1) is not imported.");
            var words = (await c.QueryAsync<int>("""SELECT w."Id" FROM "unshackled-word"."LxxWords" w WHERE w."LxxVerseId" = @unitId ORDER BY w."Position" """, new { unitId }, t)).ToArray();
            var hebrew = (await c.QueryAsync<int>(LxxAgentSql.HebrewWords, new { HebRefIds = new[] { RuthOneOne } }, t)).Take(6).ToArray();
            Assert.SkipWhen(words.Length < 8 || hebrew.Length < 4, "Ruth 1:1 words or Hebrew words are missing.");
            var tbesg = await c.ExecuteScalarAsync<int>("""SELECT count(*)::int4 FROM "unshackled-word"."StepStrongsLexicon" ssl WHERE ssl."LanguageId" = 2""", transaction: t);
            Assert.SkipWhen(tbesg == 0, "TBESG is not imported.");

            // The test sees only its own confirmed rows (dev data may have some); everything is rolled back.
            await c.ExecuteAsync("""DELETE FROM "unshackled-word"."LxxHebrewLinks" WHERE "Status" = 'confirmed'""", transaction: t);
            await c.ExecuteAsync("""DELETE FROM "unshackled-word"."LxxWordTags" WHERE "Status" = 'confirmed'""", transaction: t);
            await c.ExecuteAsync("""DELETE FROM "unshackled-word"."LxxHebrewLinks" WHERE "LxxWordId" = ANY(@words)""", new { words }, t);
            await c.ExecuteAsync("""DELETE FROM "unshackled-word"."LxxWordTags" WHERE "LxxWordId" = ANY(@words)""", new { words }, t);
            await c.ExecuteAsync("""DELETE FROM "unshackled-word"."LxxTagAudit" WHERE "LxxVerseId" = @unitId AND "Action" = 'import'""", new { unitId }, t);

            var fileService = new FileService();
            var settings = new DatabaseSeedSettings { SolutionAssetsPath = root, SolutionTempPath = root };
            var layout = new SeedLayout(fileService, settings, Path.Combine(root, "seed"));
            var service = new LxxSeedService(Substitute.For<IDbConnectionFactory>(), fileService, new SeedManifestStore(fileService),
                Options.Create(new AppSettings { DatabaseSeeding = settings }), NullLogger<LxxSeedService>.Instance);

            await test(new Fixture(c, t, layout, service, words, hebrew, unitId!.Value));
            await t.RollbackAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static Task<string> LemmaAsync(Fixture f, string strongs) =>
        f.C.ExecuteScalarAsync<string>("""
            SELECT btrim(ssl."OriginalWord")
            FROM "unshackled-word"."StepStrongsLexicon" ssl
            WHERE 1=1
                AND ssl."LanguageId" = 2
                AND 'G' || lpad(ssl."Number"::text, greatest(4, length(ssl."Number"::text)), '0') || COALESCE(ssl."Extra", '') = @strongs
            ORDER BY ssl."Id"
            LIMIT 1
            """, new { strongs }, f.T)!;

    private static async Task SetTagAsync(Fixture f, int word, string strongs, string morphology, string status, string source)
    {
        var lemma = (await LemmaAsync(f, strongs)).Normalize(System.Text.NormalizationForm.FormC);
        await f.C.ExecuteAsync("""
            INSERT INTO "unshackled-word"."LxxWordTags" ("LxxWordId", "Lemma", "Strongs", "Morphology", "Status", "Source", "UserId")
            VALUES (@word, @lemma, @strongs, @morphology, @status, @source, NULL)
            ON CONFLICT ("LxxWordId") DO UPDATE
            SET "Lemma" = EXCLUDED."Lemma", "Strongs" = EXCLUDED."Strongs", "Morphology" = EXCLUDED."Morphology", "Status" = EXCLUDED."Status", "Source" = EXCLUDED."Source"
            """, new { word = f.Words[word], lemma, strongs, morphology, status, source }, f.T);
    }

    private static Task SetLinkAsync(Fixture f, int word, int hebrew, string status = "confirmed") =>
        f.C.ExecuteAsync("""
            INSERT INTO "unshackled-word"."LxxHebrewLinks" ("LxxWordId", "StepHebrewWordId", "Status", "UserId")
            VALUES (@word, @hebrew, @status, NULL)
            ON CONFLICT ("LxxWordId", "StepHebrewWordId") DO UPDATE SET "Status" = EXCLUDED."Status"
            """, new { word = f.Words[word], hebrew = f.Hebrew[hebrew], status }, f.T);

    /// <summary>Confirmed tags 0 (ai), 1 (human), 2 (nt-form); confirmed links 0→0, 0→1, 1→2; a proposed tag and link on word 3.</summary>
    private static async Task CurateAsync(Fixture f)
    {
        await SetTagAsync(f, 0, "G2532", "CONJ", "confirmed", "ai");
        await SetTagAsync(f, 1, "G1096", "V-2ADI-3S", "confirmed", "human");
        await SetTagAsync(f, 2, "G3056", "N-NSM", "confirmed", "nt-form");
        await SetLinkAsync(f, 0, 0);
        await SetLinkAsync(f, 0, 1);
        await SetLinkAsync(f, 1, 2);
        await SetTagAsync(f, 3, "G2532", "CONJ", "proposed", "nt-form");
        await SetLinkAsync(f, 3, 3, "proposed");
    }

    private static async Task<List<TagState>> TagsAsync(Fixture f, string? status = null) =>
        (await f.C.QueryAsync<TagState>("""
            SELECT t."LxxWordId", t."Lemma", t."Strongs", t."Morphology", t."Status", t."Source", t."UserId"
            FROM "unshackled-word"."LxxWordTags" t
            WHERE 1=1
                AND t."LxxWordId" = ANY(@words)
                AND (@status::text IS NULL OR t."Status" = @status)
            ORDER BY t."LxxWordId"
            """, new { words = f.Words, status }, f.T)).ToList();

    private static async Task<List<LinkState>> LinksAsync(Fixture f, string? status = null) =>
        (await f.C.QueryAsync<LinkState>("""
            SELECT k."LxxWordId", k."StepHebrewWordId", k."Status", k."UserId"
            FROM "unshackled-word"."LxxHebrewLinks" k
            WHERE 1=1
                AND k."LxxWordId" = ANY(@words)
                AND (@status::text IS NULL OR k."Status" = @status)
            ORDER BY k."LxxWordId", k."StepHebrewWordId"
            """, new { words = f.Words, status }, f.T)).ToList();

    private static Task WipeConfirmedAsync(Fixture f) =>
        f.C.ExecuteAsync("""
            DELETE FROM "unshackled-word"."LxxHebrewLinks" WHERE "Status" = 'confirmed';
            DELETE FROM "unshackled-word"."LxxWordTags" WHERE "Status" = 'confirmed';
            """, transaction: f.T);

    private static async Task<int> AuditCountAsync(Fixture f) =>
        await f.C.ExecuteScalarAsync<int>("""SELECT count(*)::int4 FROM "unshackled-word"."LxxTagAudit" a WHERE a."LxxVerseId" = @UnitId AND a."Action" = 'import' AND a."UserId" IS NULL""", new { f.UnitId }, f.T);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExportThenImport_RoundTripsTheConfirmedLayer_WithoutGreekTextOrUsers()
    {
        await WithRuthAsync(async f =>
        {
            await CurateAsync(f);
            var tagsBefore = await TagsAsync(f, "confirmed");
            var linksBefore = await LinksAsync(f, "confirmed");

            var export = await f.Service.ExportAsync(f.C, f.T, f.Layout, null, Token);

            (export.Counts["tags"], export.Counts["links"]).ShouldBe((3, 3));
            var tagsText = await File.ReadAllTextAsync(f.Layout.Resolve(SeedLayout.LxxTagsPath), Token);
            var linksText = await File.ReadAllTextAsync(f.Layout.Resolve(SeedLayout.LxxLinksPath), Token);
            tagsText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(4);
            linksText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(4);
            tagsText.ShouldStartWith("RahlfsRef,TextForm,Position,SrcHash,Strongs,Morphology,Source\n");
            tagsText.ShouldNotContain('\r');
            GreekOrHebrew().IsMatch(tagsText).ShouldBeFalse();
            GreekOrHebrew().IsMatch(linksText).ShouldBeFalse();
            tagsText.ShouldNotContain("@");
            var manifest = await new SeedManifestStore(new FileService()).ReadAsync(f.Layout.ManifestPath, Token);
            manifest!.Lxx!.Files.Select(x => (x.Path, x.Rows)).ShouldBe([("lxx/tags.csv", 3), ("lxx/links.csv", 3)]);

            // a second export of the same data writes the same bytes (deterministic)
            await f.Service.ExportAsync(f.C, f.T, f.Layout, null, Token);
            (await File.ReadAllTextAsync(f.Layout.Resolve(SeedLayout.LxxTagsPath), Token)).ShouldBe(tagsText);
            (await File.ReadAllTextAsync(f.Layout.Resolve(SeedLayout.LxxLinksPath), Token)).ShouldBe(linksText);

            await WipeConfirmedAsync(f);
            var import = await f.Service.ImportAsync(f.C, f.T, f.Layout, mirror: false, null, Token);

            (import.Counts["inserted"], import.Counts["updated"], import.Counts["unchanged"], import.Counts["deleted"], import.Counts["skipped"]).ShouldBe((6, 0, 0, 0, 0));
            (await TagsAsync(f, "confirmed")).ShouldBe(tagsBefore);
            (await LinksAsync(f, "confirmed")).ShouldBe(linksBefore);
            (await TagsAsync(f, "confirmed")).ShouldAllBe(t => t.UserId == null);
            (await TagsAsync(f, "proposed")).Count.ShouldBe(1);
            (await AuditCountAsync(f)).ShouldBe(1);

            var again = await f.Service.ImportAsync(f.C, f.T, f.Layout, mirror: false, null, Token);
            (again.Counts["inserted"], again.Counts["updated"], again.Counts["unchanged"]).ShouldBe((0, 0, 6));
            (await AuditCountAsync(f)).ShouldBe(1);
        });
    }

    [Fact]
    public async Task Import_SkipsAndCountsLinesWhoseAnchorNoLongerMatches()
    {
        await WithRuthAsync(async f =>
        {
            await CurateAsync(f);
            await f.Service.ExportAsync(f.C, f.T, f.Layout, null, Token);
            await WipeConfirmedAsync(f);
            // word 1 (tag and link) now has another hash: the text import changed
            await f.C.ExecuteAsync("""UPDATE "unshackled-word"."LxxWords" SET "SrcHash" = 'ffffffff' WHERE "Id" = @id""", new { id = f.Words[1] }, f.T);

            var import = await f.Service.ImportAsync(f.C, f.T, f.Layout, mirror: false, null, Token);

            (import.Counts["skipped"], import.Counts["anchorMismatch"], import.Counts["inserted"]).ShouldBe((2, 2, 4));
            import.Lists["skippedRows"].Items.ShouldAllBe(s => s.Contains("hash changed"));
            (await TagsAsync(f, "confirmed")).Select(t => t.LxxWordId).ShouldBe([f.Words[0], f.Words[2]]);
            (await LinksAsync(f, "confirmed")).Select(l => l.LxxWordId).Distinct().ShouldBe([f.Words[0]]);
        });
    }

    [Fact]
    public async Task Import_SkipsAHebrewWordThatDoesNotExist_ButKeepsTheTag()
    {
        await WithRuthAsync(async f =>
        {
            var tags = new List<LxxTagSeedLine>();
            var links = new List<LxxLinkSeedLine>();
            await CurateAsync(f);
            tags.AddRange(await LxxSeed.ReadTagsAsync(f.C, f.T, Token));
            var (exported, _) = await LxxSeed.ReadLinksAsync(f.C, f.T, Token);
            links.AddRange(exported);
            links[0].HebLxxRefId = 99_999_999;
            await WipeConfirmedAsync(f);

            var import = await LxxSeed.ImportAsync(f.C, f.T, tags, links, mirror: false, null, Token);

            (import.Counts["skipped"], import.Counts["inserted"]).ShouldBe((1, 5));
            import.Lists["skippedRows"].Items.Single().ShouldContain("Hebrew word was not found");
        });
    }

    [Fact]
    public async Task Merge_UpdatesChangedAndProposedTags_ButNeverDeletes()
    {
        await WithRuthAsync(async f =>
        {
            await CurateAsync(f);
            await f.Service.ExportAsync(f.C, f.T, f.Layout, null, Token);
            // database differs: word 0 has another code, word 3's proposal equals nothing in the file, an extra confirmed tag and link exist on word 5
            await SetTagAsync(f, 0, "G2532", "ADV", "confirmed", "human");
            await SetTagAsync(f, 5, "G3056", "N-NSM", "confirmed", "human");
            await SetLinkAsync(f, 5, 0);

            var import = await f.Service.ImportAsync(f.C, f.T, f.Layout, mirror: false, null, Token);

            (import.Counts["inserted"], import.Counts["updated"], import.Counts["unchanged"], import.Counts["deleted"]).ShouldBe((0, 1, 5, 0));
            var confirmed = await TagsAsync(f, "confirmed");
            confirmed.Single(t => t.LxxWordId == f.Words[0]).Morphology.ShouldBe("CONJ");
            confirmed.Select(t => t.LxxWordId).ShouldContain(f.Words[5]);
            (await LinksAsync(f, "confirmed")).Select(l => l.LxxWordId).ShouldContain(f.Words[5]);
        });
    }

    [Fact]
    public async Task Merge_TurnsAProposedTagAndLinkTheFileConfirmsIntoConfirmedRows()
    {
        await WithRuthAsync(async f =>
        {
            await CurateAsync(f);
            await f.Service.ExportAsync(f.C, f.T, f.Layout, null, Token);
            await WipeConfirmedAsync(f);
            // word 0's AI proposal and its link to Hebrew 0 are open proposals in the database
            await SetTagAsync(f, 0, "G1096", "V-2ADI-3S", "proposed", "ai");
            await SetLinkAsync(f, 0, 0, "proposed");

            var import = await f.Service.ImportAsync(f.C, f.T, f.Layout, mirror: false, null, Token);

            (import.Counts["inserted"], import.Counts["updated"]).ShouldBe((4, 2));
            var tag = (await TagsAsync(f)).Single(t => t.LxxWordId == f.Words[0]);
            (tag.Status, tag.Strongs, tag.Source, tag.UserId).ShouldBe(("confirmed", "G2532", "ai", null));
            (await LinksAsync(f)).Single(l => l.LxxWordId == f.Words[0] && l.StepHebrewWordId == f.Hebrew[0]).Status.ShouldBe("confirmed");
        });
    }

    [Fact]
    public async Task Mirror_DeletesConfirmedRowsTheFilesLack_ButKeepsProposals()
    {
        await WithRuthAsync(async f =>
        {
            await CurateAsync(f);
            await f.Service.ExportAsync(f.C, f.T, f.Layout, null, Token);
            await SetTagAsync(f, 5, "G3056", "N-NSM", "confirmed", "human");
            await SetLinkAsync(f, 5, 0);
            await SetTagAsync(f, 6, "G2532", "CONJ", "proposed", "ai");
            await SetLinkAsync(f, 6, 1, "proposed");

            // the preview counts what would go and leaves everything as it is
            var files = (await f.Service.ReadFilesAsync(f.Layout, Token))!.Value;
            await f.T.SaveAsync("preview", Token);
            var (tags, links) = await LxxSeed.CountMirrorDeletesAsync(f.C, f.T, files.Tags, files.Links, Token);
            await f.T.RollbackAsync("preview", Token);
            (tags, links).ShouldBe((1, 1));
            (await TagsAsync(f, "confirmed")).Count.ShouldBe(4);

            var import = await f.Service.ImportAsync(f.C, f.T, f.Layout, mirror: true, null, Token);

            (import.Counts["deleted"], import.Counts["unchanged"], import.Counts["inserted"]).ShouldBe((2, 6, 0));
            (await TagsAsync(f, "confirmed")).Select(t => t.LxxWordId).ShouldBe([f.Words[0], f.Words[1], f.Words[2]]);
            (await LinksAsync(f, "confirmed")).Select(l => l.LxxWordId).Distinct().ShouldBe([f.Words[0], f.Words[1]]);
            (await TagsAsync(f, "proposed")).Select(t => t.LxxWordId).ShouldBe([f.Words[3], f.Words[6]]);
            (await LinksAsync(f, "proposed")).Select(l => l.LxxWordId).ShouldBe([f.Words[3], f.Words[6]]);
            (await AuditCountAsync(f)).ShouldBe(1);
        });
    }

    [Fact]
    public async Task ApplyInitialSeed_RunsOnlyWithoutConfirmedTags()
    {
        await WithRuthAsync(async f =>
        {
            await CurateAsync(f);
            await f.Service.ExportAsync(f.C, f.T, f.Layout, null, Token);

            // confirmed rows exist: nothing happens
            var skipped = await f.Service.ApplyInitialSeedAsync(f.C, f.T, f.Layout, Token);
            (skipped.Skipped, skipped.Reason).ShouldBe((true, SeedSkipReason.TablesNotEmpty));

            // only proposals left: the files are applied
            await WipeConfirmedAsync(f);
            var applied = await f.Service.ApplyInitialSeedAsync(f.C, f.T, f.Layout, Token);
            applied.Skipped.ShouldBeFalse();
            (await TagsAsync(f, "confirmed")).Count.ShouldBe(3);
        });
    }

    [Fact]
    public async Task RelinkAfterStepImport_ReplacesStaleLinksWithTheFilesLinks_AndKeepsTags()
    {
        await WithRuthAsync(async f =>
        {
            await CurateAsync(f);
            await f.Service.ExportAsync(f.C, f.T, f.Layout, null, Token);

            // after a STEP re-import the stored ids point to the old numbering: a link the files lack stands for such a stale row
            await SetLinkAsync(f, 2, 4);
            var tagsBefore = await TagsAsync(f);

            var report = await f.Service.RelinkAfterStepImportAsync(f.C, f.T, f.Layout, Token);

            report.ShouldNotBeNull();
            report.Counts["staleLinksDeleted"].ShouldBeGreaterThanOrEqualTo(5);
            report.Counts["inserted"].ShouldBe(3);
            (await LinksAsync(f)).Select(l => (l.LxxWordId, l.StepHebrewWordId, l.Status)).ShouldBe([
                (f.Words[0], f.Hebrew[0], "confirmed"), (f.Words[0], f.Hebrew[1], "confirmed"), (f.Words[1], f.Hebrew[2], "confirmed"),
            ], ignoreOrder: true);
            (await TagsAsync(f)).ShouldBe(tagsBefore);

            // without any link (a fresh database) there is nothing to re-resolve
            await f.C.ExecuteAsync("""DELETE FROM "unshackled-word"."LxxHebrewLinks" """, transaction: f.T);
            (await f.Service.RelinkAfterStepImportAsync(f.C, f.T, f.Layout, Token)).ShouldBeNull();
        });
    }

    [Fact]
    public async Task Import_WithoutFiles_IsSkippedAsNoSeed()
    {
        await WithRuthAsync(async f =>
        {
            var report = await f.Service.ImportAsync(f.C, f.T, f.Layout, mirror: true, null, Token);
            (report.Skipped, report.Reason).ShouldBe((true, SeedSkipReason.NoSeed));
        });
    }
}
