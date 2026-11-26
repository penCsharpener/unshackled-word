using System.Text.Json;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Application.Features.Lxx.Review;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Lxx;
using UnshackledWord.Infrastructure.Repositories;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// The write path of the LXX review page (roadmap 0028.7f, <see cref="LxxReviewRepository"/>) against a real Postgres, over Ruth 1:1: confirm, edit,
/// discard, "sichere" confirm, stale and conflict. Everything runs in one rolled-back transaction per test; nothing stays in the database. Skipped without
/// <c>UNSHACKLEDWORD_TEST_POSTGRES</c> or without the LXX text.
/// </summary>
[Collection("LxxDatabase")]
public class LxxReviewDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int RuthOneOne = 8_001_001;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly LxxLexicon Lexicon = LxxLexicon.Build(
    [
        new LxxLexiconEntry("G2532", "καί", "and"),
        new LxxLexiconEntry("G1096", "γίνομαι", "to become"),
        new LxxLexiconEntry("G3056", "λόγος", "word"),
    ]);

    private sealed record Fixture(NpgsqlConnection C, NpgsqlTransaction T, int UserId, int UnitId, int[] WordIds, int[] HebrewIds, int RunId, Guid PackageId);

    private static async Task WithRuthAsync(Func<Fixture, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        await using var c = new NpgsqlConnection(connectionString);
        await c.OpenAsync(TestContext.Current.CancellationToken);
        await using var t = await c.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var userId = await c.ExecuteScalarAsync<int?>("""SELECT u."Id" FROM "unshackled-word"."Users" u ORDER BY u."Id" LIMIT 1""", transaction: t);
        Assert.SkipWhen(userId is null, "No user in the database.");
        var unitId = await c.ExecuteScalarAsync<int?>("""SELECT l."Id" FROM "unshackled-word"."LxxVerses" l WHERE l."HebRefId" = @RuthOneOne ORDER BY l."SortOrder" LIMIT 1""", new { RuthOneOne }, t);
        Assert.SkipWhen(unitId is null, "The LXX text (Ruth 1:1) is not imported.");
        var words = (await c.QueryAsync<int>("""SELECT w."Id" FROM "unshackled-word"."LxxWords" w WHERE w."LxxVerseId" = @unitId ORDER BY w."Position" """, new { unitId }, t)).ToArray();
        var hebrew = (await c.QueryAsync<int>(LxxAgentSql.HebrewWords, new { HebRefIds = new[] { RuthOneOne } }, t)).Take(4).ToArray();
        Assert.SkipWhen(words.Length < 5 || hebrew.Length < 3, "Ruth 1:1 words or Hebrew words are missing.");

        // Dev data may have open LXX proposals (and confirmed tags) on this verse: this test's counts are its own.
        await c.ExecuteAsync("""
            UPDATE "unshackled-word"."AgentVerseProposals"
            SET "Status" = 'discarded'
            WHERE 1=1
                AND "Language" = 'lxx'
                AND "Status" = 'open'
                AND "BibleBookId" = 8
                AND "Chapter" = 1
                AND "Verse" = 1
            """, transaction: t);
        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."LxxHebrewLinks" WHERE "LxxWordId" = ANY(@words)""", new { words }, t);
        await c.ExecuteAsync("""DELETE FROM "unshackled-word"."LxxWordTags" WHERE "LxxWordId" = ANY(@words) AND "Status" = 'confirmed'""", new { words }, t);

        var runId = await c.ExecuteScalarAsync<int>(AgentStoreSql.InsertRun, new
        {
            CreatedByUserId = userId, AiUserId = userId, Profile = "test", Model = "test", PromptVersion = "0", BibleBookId = 8, FromHebRefId = RuthOneOne, ToHebRefId = RuthOneOne,
            Mode = "redo", Status = "completed", StatusNote = (string?)null, EstimatedTokens = (long?)null, EstimatedCostUsd = (decimal?)null, MaxTokens = (long?)null, MaxCostUsd = (decimal?)null, ApiTokenId = (int?)null, IncludeConfirmed = false, TextForm = (string?)null,
        }, t);
        var packageId = Guid.NewGuid();
        await c.ExecuteAsync(AgentStoreSql.InsertPackage, new { Id = packageId, RunId = runId, FromHebRefId = RuthOneOne, ToHebRefId = RuthOneOne, WordIdsHash = "x", LocalIdMap = "{}", ExpiresAt = DateTime.UtcNow.AddHours(1) }, t);

        await test(new Fixture(c, t, userId!.Value, unitId!.Value, words, hebrew, runId, packageId));
        await t.RollbackAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>An open AI proposal for the unit: (word index, lemma, Strong's, code, confidence, hebrew index or -1).</summary>
    private static async Task<long> InsertProposalAsync(Fixture f, params (int Word, string Lemma, string Strongs, string Morph, string Confidence, int Hebrew)[] answers)
    {
        var lemmas = answers.Select(a => new LxxWordProposal { LxxWordId = f.WordIds[a.Word], Position = a.Word + 1, Lemma = a.Lemma, Strongs = a.Strongs, Morph = a.Morph, Confidence = a.Confidence });
        var links = answers.Where(a => a.Hebrew >= 0).Select(a => new LxxLinkProposal { LxxWordId = f.WordIds[a.Word], Position = a.Word + 1, StepHebrewWordId = f.HebrewIds[a.Hebrew], HebNumber = a.Hebrew + 1 });
        return await f.C.ExecuteScalarAsync<long>(AgentStoreSql.InsertProposal, new
        {
            RunId = f.RunId, PackageId = f.PackageId, BibleBookId = 8, Chapter = 1, Verse = 1, Language = "lxx",
            Links = JsonSerializer.Serialize(links, Web), Lemmas = JsonSerializer.Serialize(lemmas, Web), Parts = (string?)null, Roles = (string?)null,
            Flags = JsonSerializer.Serialize(new LxxProposalFlags { LxxVerseId = f.UnitId }, Web), Note = "test",
        }, f.T);
    }

    private static Task<LxxReviewApplied<T>> ApplyAsync<T>(Fixture f, Func<LxxReviewState, LxxReviewPlan<T>> plan) =>
        LxxReviewRepository.ApplyAsync(f.C, f.T, [f.UnitId], plan, f.UserId, TestContext.Current.CancellationToken);

    private static LxxReviewWordInput In(Fixture f, int word, string lemma, string strongs, string morph, params int[] hebrew) =>
        new() { LxxWordId = f.WordIds[word], Lemma = lemma, Strongs = strongs, Morphology = morph, HebrewWordIds = hebrew.Select(h => f.HebrewIds[h]).ToList() };

    private static Task<(string Status, string Source, string Strongs, int? UserId)?> TagAsync(Fixture f, int word) =>
        f.C.QuerySingleOrDefaultAsync<(string, string, string, int?)?>("""
            SELECT "Status", "Source", "Strongs", "UserId"
            FROM "unshackled-word"."LxxWordTags"
            WHERE "LxxWordId" = @id
            """, new { id = f.WordIds[word] }, f.T);

    private static Task<List<(string Action, string Before, string After, int? UserId)>> AuditAsync(Fixture f) =>
        f.C.QueryAsync<(string, string, string, int?)>("""
            SELECT "Action", "Before"::text, "After"::text, "UserId"
            FROM "unshackled-word"."LxxTagAudit"
            WHERE "LxxVerseId" = @UnitId
            ORDER BY "Id"
            """, new { f.UnitId }, f.T).ContinueWith(t => t.Result.ToList());

    private static Task<(string Status, int? Reviewer)> ProposalAsync(Fixture f, long id) =>
        f.C.QuerySingleAsync<(string, int?)>("""SELECT "Status", "ReviewedByUserId" FROM "unshackled-word"."AgentVerseProposals" WHERE "Id" = @id""", new { id }, f.T);

    private static Task<List<int>> LinksAsync(Fixture f, int word) =>
        f.C.QueryAsync<int>("""SELECT "StepHebrewWordId" FROM "unshackled-word"."LxxHebrewLinks" WHERE "LxxWordId" = @id ORDER BY 1""", new { id = f.WordIds[word] }, f.T)
            .ContinueWith(t => t.Result.ToList());

    [Fact]
    public async Task ChapterCountsAndUnits_SeeTheOpenProposal() => await WithRuthAsync(async f =>
    {
        var before = (await f.C.QueryAsync<LxxReviewChapterCountRow>(LxxReviewSql.ChapterCounts, transaction: f.T)).Single(r => r.BibleBookId == 8 && r.Chapter == 1);
        await InsertProposalAsync(f, (0, "καί", "G2532", "CONJ", "high", 0), (1, "γίνομαι", "G1096", "V-2ADI-3S", "low", 1));

        var after = (await f.C.QueryAsync<LxxReviewChapterCountRow>(LxxReviewSql.ChapterCounts, transaction: f.T)).Single(r => r.BibleBookId == 8 && r.Chapter == 1);
        var units = (await f.C.QueryAsync<LxxAgentUnitRow>(LxxReviewSql.ChapterUnits, new { BibleBookId = 8, Chapter = 1 }, f.T)).ToList();

        after.AiUnits.ShouldBe(before.AiUnits + 1);
        after.LowWords.ShouldBe(before.LowWords + 1);
        after.OpenUnits.ShouldBeGreaterThanOrEqualTo(before.OpenUnits);
        units.ShouldContain(u => u.Id == f.UnitId);
        units.Select(u => u.SortOrder).ShouldBe(units.Select(u => u.SortOrder).Order());
    });

    [Fact]
    public async Task Confirm_WritesTagsLinksAuditAndClosesTheProposal_ThenAnEditIsAnEditAudit() => await WithRuthAsync(async f =>
    {
        var proposalId = await InsertProposalAsync(f, (0, "καί", "G2532", "CONJ", "high", 0), (1, "γίνομαι", "G1096", "V-2ADI-3S", "high", 1));

        var applied = await ApplyAsync(f, state => LxxReviewService.PlanConfirm(new ConfirmLxxUnitRequest
        {
            LxxVerseId = f.UnitId, ProposalId = proposalId,
            Words = [In(f, 0, "καί", "G2532", "CONJ", 0), In(f, 1, "γίνομαι", "G1096", "V-2ADI-3S", 1)],
        }, state, Lexicon));

        applied.Conflict.ShouldBeFalse();
        applied.Result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        applied.Result.Value!.ProposalStatus.ShouldBe("merged");
        (await TagAsync(f, 0)).ShouldBe(("confirmed", "ai", "G2532", f.UserId));
        (await TagAsync(f, 1)).ShouldBe(("confirmed", "ai", "G1096", f.UserId));
        (await LinksAsync(f, 0)).ShouldBe([f.HebrewIds[0]]);
        (await ProposalAsync(f, proposalId)).ShouldBe(("merged", f.UserId));
        var audit = (await AuditAsync(f)).ShouldHaveSingleItem();
        audit.Action.ShouldBe("confirm");
        audit.UserId.ShouldBe(f.UserId);
        JsonDocument.Parse(audit.After).RootElement.GetProperty("words")[0].GetProperty("status").GetString().ShouldBe("confirmed");

        // The same verse again with another tag for word 1: a confirmed tag changes, so the audit says edit; the closed proposal is not needed any more.
        var edited = await ApplyAsync(f, state => LxxReviewService.PlanConfirm(new ConfirmLxxUnitRequest
        {
            LxxVerseId = f.UnitId, Words = [In(f, 0, "λόγος", "G3056", "N-NSM", 0), In(f, 1, "γίνομαι", "G1096", "V-2ADI-3S", 1)],
        }, state, Lexicon));

        edited.Result.Value!.Action.ShouldBe("edit");
        (await TagAsync(f, 0)).ShouldBe(("confirmed", "human", "G3056", f.UserId));
        (await TagAsync(f, 1)).ShouldBe(("confirmed", "ai", "G1096", f.UserId), "unchanged word keeps its source");
        var audits = await AuditAsync(f);
        audits.Select(a => a.Action).ShouldBe(["confirm", "edit"]);
        audits[1].Before.ShouldContain("G2532");
        audits[1].After.ShouldContain("G3056");

        // Nothing changes: no new audit row.
        var same = await ApplyAsync(f, state => LxxReviewService.PlanConfirm(new ConfirmLxxUnitRequest
        {
            LxxVerseId = f.UnitId, Words = [In(f, 0, "λόγος", "G3056", "N-NSM", 0), In(f, 1, "γίνομαι", "G1096", "V-2ADI-3S", 1)],
        }, state, Lexicon));
        same.Result.Value!.Action.ShouldBeEmpty();
        (await AuditAsync(f)).Count.ShouldBe(2);
    });

    [Fact]
    public async Task Confirm_AnAcceptedNtFormPrefill_KeepsItsSource() => await WithRuthAsync(async f =>
    {
        // A pass-1 row (user NULL) is confirmed as it is; the tag text is not touched.
        await f.C.ExecuteAsync("""
            INSERT INTO "unshackled-word"."LxxWordTags" ("LxxWordId", "Lemma", "Strongs", "Morphology", "Status", "Source")
            VALUES (@id, 'γίνομαι', 'G1096', 'V-2ADI-3S', 'proposed', 'nt-form')
            ON CONFLICT ("LxxWordId") DO UPDATE SET "Lemma" = EXCLUDED."Lemma", "Strongs" = EXCLUDED."Strongs", "Morphology" = EXCLUDED."Morphology", "Status" = 'proposed', "Source" = 'nt-form', "UserId" = NULL
            """, new { id = f.WordIds[1] }, f.T);

        var applied = await ApplyAsync(f, state => LxxReviewService.PlanConfirm(new ConfirmLxxUnitRequest
        {
            LxxVerseId = f.UnitId, Words = [In(f, 1, "γίνομαι", "G1096", "V-2ADI-3S")],
        }, state, Lexicon));

        applied.Result.Value!.ProposalStatus.ShouldBeNull();
        (await TagAsync(f, 1)).ShouldBe(("confirmed", "nt-form", "G1096", f.UserId));
    });

    [Fact]
    public async Task Confirm_StaleProposal_And_ARefusedWord_WriteNothing() => await WithRuthAsync(async f =>
    {
        var proposalId = await InsertProposalAsync(f, (0, "καί", "G2532", "CONJ", "high", -1));

        var stale = await ApplyAsync(f, state => LxxReviewService.PlanConfirm(new ConfirmLxxUnitRequest
        {
            LxxVerseId = f.UnitId, ProposalId = proposalId + 1000, Words = [In(f, 0, "καί", "G2532", "CONJ")],
        }, state, Lexicon));
        var refused = await ApplyAsync(f, state => LxxReviewService.PlanConfirm(new ConfirmLxxUnitRequest
        {
            LxxVerseId = f.UnitId, ProposalId = proposalId, Words = [In(f, 0, "καί", "G99999", "CONJ")],
        }, state, Lexicon));
        var hebrewOutside = await ApplyAsync(f, state => LxxReviewService.PlanConfirm(new ConfirmLxxUnitRequest
        {
            LxxVerseId = f.UnitId, ProposalId = proposalId,
            Words = [new LxxReviewWordInput { LxxWordId = f.WordIds[0], Lemma = "καί", Strongs = "G2532", Morphology = "CONJ", HebrewWordIds = [1] }],
        }, state, Lexicon));

        stale.Result.Outcome.ShouldBe(AgentReviewOutcome.Stale);
        refused.Result.Outcome.ShouldBe(AgentReviewOutcome.Invalid);
        hebrewOutside.Result.Outcome.ShouldBe(AgentReviewOutcome.Invalid);
        (await TagAsync(f, 0)).ShouldBeNull();
        (await ProposalAsync(f, proposalId)).Status.ShouldBe("open");
        (await AuditAsync(f)).ShouldBeEmpty();
    });

    [Fact]
    public async Task ABlockedProposalClose_IsAConflict_AndNothingElseIsWritten() => await WithRuthAsync(async f =>
    {
        var proposalId = await InsertProposalAsync(f, (0, "καί", "G2532", "CONJ", "high", -1));
        await f.C.ExecuteAsync("""UPDATE "unshackled-word"."AgentVerseProposals" SET "Status" = 'discarded' WHERE "Id" = @proposalId""", new { proposalId }, f.T);

        // A plan that was made from an older read: the proposal it closes is no longer open (someone else was faster).
        var applied = await ApplyAsync(f, _ =>
        {
            var write = new LxxReviewWrite();
            write.ProposalCloses.Add(new LxxProposalClose(proposalId, "merged"));
            write.Tags.Add(new LxxTagWrite(f.WordIds[0], "καί", "G2532", "CONJ", "ai"));
            write.Audits.Add(new LxxAuditWrite(f.UnitId, "confirm", null, "{\"words\":[]}"));
            return new LxxReviewPlan<int>(1, write);
        });

        applied.Conflict.ShouldBeTrue();
        (await TagAsync(f, 0)).ShouldBeNull("the closes run first, the tag was never written");
        (await AuditAsync(f)).ShouldBeEmpty();
        (await ProposalAsync(f, proposalId)).Status.ShouldBe("discarded");
    });

    [Fact]
    public async Task Discard_ClosesTheAiProposalOnly_AndAuditsReject() => await WithRuthAsync(async f =>
    {
        await f.C.ExecuteAsync("""
            INSERT INTO "unshackled-word"."LxxWordTags" ("LxxWordId", "Lemma", "Strongs", "Morphology", "Status", "Source")
            VALUES (@id, 'γίνομαι', 'G1096', 'V-2ADI-3S', 'proposed', 'nt-form')
            ON CONFLICT ("LxxWordId") DO UPDATE SET "Status" = 'proposed', "Source" = 'nt-form', "Strongs" = 'G1096', "Lemma" = 'γίνομαι', "Morphology" = 'V-2ADI-3S'
            """, new { id = f.WordIds[1] }, f.T);
        var proposalId = await InsertProposalAsync(f, (0, "καί", "G2532", "CONJ", "high", 0));

        var applied = await ApplyAsync(f, state => LxxReviewService.PlanDiscard(new DiscardLxxUnitRequest { LxxVerseId = f.UnitId, ProposalId = proposalId }, state));
        var again = await ApplyAsync(f, state => LxxReviewService.PlanDiscard(new DiscardLxxUnitRequest { LxxVerseId = f.UnitId, ProposalId = proposalId }, state));

        applied.Result.Outcome.ShouldBe(AgentReviewOutcome.Ok);
        again.Result.Outcome.ShouldBe(AgentReviewOutcome.NotOpen);
        (await ProposalAsync(f, proposalId)).ShouldBe(("discarded", f.UserId));
        (await TagAsync(f, 1)).ShouldBe(("proposed", "nt-form", "G1096", null));
        (await TagAsync(f, 0)).ShouldBeNull();
        (await AuditAsync(f)).Select(a => a.Action).ShouldBe(["reject"]);
    });

    [Fact]
    public async Task ConfirmSafe_ConfirmsOnlyHighListedWords_AndLeavesTheProposalOpenForTheRest() => await WithRuthAsync(async f =>
    {
        var proposalId = await InsertProposalAsync(f,
            (0, "καί", "G2532", "CONJ", "high", 0),
            (1, "γίνομαι", "G1096", "V-2ADI-3S", "low", 1),
            (2, "λόγος", "G3056", "N-NSM", "high", -1));

        var applied = await ApplyAsync(f, state => LxxReviewService.PlanSafe(new ConfirmLxxSafeRequest
        {
            BibleBookId = 8, Chapter = 1,
            Units = [new LxxSafeUnitInput { LxxVerseId = f.UnitId, ProposalId = proposalId, LxxWordIds = [f.WordIds[0], f.WordIds[1]] }],
        }, state, Lexicon));

        var response = applied.Result.Value!;
        (response.Units, response.ConfirmedWords, response.SkippedLow, response.MergedProposals).ShouldBe((1, 1, 1, 0));
        (await TagAsync(f, 0)).ShouldBe(("confirmed", "ai", "G2532", f.UserId));
        // The dev data may hold nt-form prefills here; they must stay proposed.
        (await TagAsync(f, 1))?.Status.ShouldBe("proposed", "low stays open");
        (await TagAsync(f, 2))?.Status.ShouldBe("proposed", "not listed");
        (await LinksAsync(f, 0)).ShouldBe([f.HebrewIds[0]]);
        (await ProposalAsync(f, proposalId)).Status.ShouldBe("open");
        (await AuditAsync(f)).Select(a => a.Action).ShouldBe(["confirm"]);
    });
}
