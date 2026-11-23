using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Infrastructure.TskCuration;
using static UnshackledWord.Infrastructure.Tests.TskCurationDatabaseTests;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Roadmap 0024.6 against a real Postgres (migration 0046, seeded TSK and Elb1871): suggestions of non-maintainers through <see cref="TskCurationService"/>,
/// the author-only <c>pendingProposal</c> in <see cref="ChapterCrossReferenceService"/>, and the review of <see cref="TskProposalReviewService"/>
/// (stale check, audit <c>Source</c> <c>proposal</c>, admin log). One rolled-back transaction per test, fresh users. Needs <c>UNSHACKLEDWORD_TEST_POSTGRES</c>.
/// </summary>
public class TskCurationProposalDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";
    private const int Gen12 = 1_001_002;
    private const int Gen5020 = 1_050_020;

    /// <summary>Ps 104,30 („Du sendest aus deinen Odem …“) under 1Mo 1,2, stored key checked in the data.</summary>
    private static readonly TskLinkKey Ps10430UnderGen12 = new(Gen12, 19_104_030, null);

    /// <summary>„Geist“ in 1Mo 1,2 (Elb1871 position 16, checked in the data).</summary>
    private const int Geist = 16;

    private sealed record Ctx(IDbConnection C, IDbTransaction T, TskCurationService Service, TskProposalReviewService Review, ITskCurationProposalRepository Proposals)
    {
        public CancellationToken Ct => TestContext.Current.CancellationToken;

        public Task<GetChapterCrossReferencesResponse> ChapterAsync(int chapter, int? userId, bool includeHidden = false) =>
            new ChapterCrossReferenceService(new TskRepository(new TransactionDbReader(C, T)), Proposals)
                .GetAsync(1, chapter, includeHidden, proposalsOfUserId: userId, token: Ct);
    }

    private static async Task WithTransactionAsync(Func<Ctx, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await connection.ExecuteAsync("""
            DELETE FROM "unshackled-word"."TskCurations" c
            WHERE c."SourceHebRefId" IN (@Gen12, @Gen5020)
            """, new { Gen12, Gen5020 }, transaction);

        var proposals = new TransactionProposalRepository(connection, transaction);
        var ctx = new Ctx(connection, transaction, new TskCurationService(new TransactionRepository(connection, transaction), proposals),
            new TskProposalReviewService(proposals, new TransactionAdminLog(transaction)), proposals);
        await test(ctx);

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    private static Task<int> NewUserAsync(Ctx x, string prefix) =>
        x.C.QuerySingleAsync<int>("""
            INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "DisplayName")
            VALUES (@Name || '@example.org', @Name || '@example.org', @Name)
            RETURNING "Id"
            """, new { Name = prefix + "-" + Guid.NewGuid().ToString("N") }, x.T);

    private static Task<string> DisplayNameAsync(Ctx x, int userId) =>
        x.C.QuerySingleAsync<string>("""SELECT "DisplayName" FROM "unshackled-word"."Users" WHERE "Id" = @userId""", new { userId }, x.T);

    private static SaveTskCurationRequest Save(TskLinkKey key, string? rating, string? comment = null) => new()
    {
        SourceHebRefId = key.SourceHebRefId, TargetStartHebRefId = key.TargetStartHebRefId, TargetEndHebRefId = key.TargetEndHebRefId, Rating = rating, Comment = comment,
    };

    private static SaveTskWordLinkRequest Link(TskLinkKey key, int from, int to) => new()
    {
        SourceHebRefId = key.SourceHebRefId,
        Keys = [new TskLinkTargetKey { TargetStartHebRefId = key.TargetStartHebRefId, TargetEndHebRefId = key.TargetEndHebRefId }],
        StartPosition = from,
        EndPosition = to,
    };

    private static CrossReferenceResponse? Ref(GetChapterCrossReferencesResponse response, int verse, int book, int chapter, int start, int? end = null) =>
        response.Verses.SingleOrDefault(v => v.Verse == verse)?.References
            .SingleOrDefault(r => (r.BibleBookId, r.Chapter, r.Verse, r.EndVerse) == (book, chapter, start, end));

    private sealed record ProposalStateRow(int Id, string State, string? Rating, int? StartPosition, string? QuotedText);

    private static async Task<List<ProposalStateRow>> ProposalsOfAsync(Ctx x, int userId) =>
        (await x.C.QueryAsync<ProposalStateRow>("""
            SELECT p."Id", p."State", p."Rating", p."StartPosition", p."QuotedText"
            FROM "unshackled-word"."TskCurationProposals" p
            WHERE p."UserId" = @userId
            ORDER BY p."Id"
            """, new { userId }, x.T)).ToList();

    private sealed record AuditRow(string? After, int? UserId, string Source);

    private static async Task<List<AuditRow>> AuditAsync(Ctx x, TskLinkKey key) =>
        (await x.C.QueryAsync<AuditRow>("""
            SELECT a."After"::text AS "After", a."UserId", a."Source"
            FROM "unshackled-word"."TskCurationAudit" a
            WHERE a."SourceHebRefId" = @SourceHebRefId
              AND a."TargetStartHebRefId" = @TargetStartHebRefId
              AND a."TargetEndHebRefId" IS NOT DISTINCT FROM @TargetEndHebRefId
            ORDER BY a."Id"
            """, key, x.T)).ToList();

    private sealed record LogRow(int ActorUserId, int? TargetUserId, string Action, string Details);

    private static async Task<List<LogRow>> AdminLogAsync(Ctx x, int actor) =>
        (await x.C.QueryAsync<LogRow>("""
            SELECT l."ActorUserId", l."TargetUserId", l."Action", l."Details"::text AS "Details"
            FROM "unshackled-word"."AdminLog" l
            WHERE l."ActorUserId" = @actor
            ORDER BY l."Id"
            """, new { actor }, x.T)).ToList();

    /// <summary>The 0024.6 "done when" scenario.</summary>
    [Fact]
    public async Task Scenario_BProposesHidingPs10430AndLinkingItToGeist_OnlyBSeesIt_MaintainerAccepts() => await WithTransactionAsync(async x =>
    {
        var a = await NewUserAsync(x, "tsk-a");
        var b = await NewUserAsync(x, "tsk-b");
        var maintainer = await NewUserAsync(x, "tsk-m");

        var hide = await x.Service.SaveAsync(b, false, Save(Ps10430UnderGen12, TskRatings.Hide), x.Ct);
        hide.Status.ShouldBe(TskCurationStatus.Proposed);
        hide.Proposal!.State.ShouldBe(TskProposalStates.Pending);
        hide.Proposal.Proposed.Rating.ShouldBe(TskRatings.Hide);
        hide.Proposal.Proposed.Origin.ShouldBe(TskOrigins.Tsk);

        var link = await x.Service.SaveWordLinkAsync(b, false, Link(Ps10430UnderGen12, Geist, Geist), x.Ct);
        link.Status.ShouldBe(TskCurationStatus.Proposed);
        var proposal = link.Proposals!.Proposals.ShouldHaveSingleItem();
        // the second request builds on B's still-fresh pending proposal: hide AND the word in one desired state
        proposal.Proposed.Rating.ShouldBe(TskRatings.Hide);
        proposal.Proposed.Word!.QuotedText.ShouldBe("Geist");
        (proposal.Proposed.Word.StartPosition, proposal.Proposed.Word.EndPosition).ShouldBe((Geist, Geist));

        var rows = await ProposalsOfAsync(x, b);
        rows.Select(r => r.State).ShouldBe([TskProposalStates.Superseded, TskProposalStates.Pending]);
        (await x.Service.SaveAsync(b, false, Save(Ps10430UnderGen12, TskRatings.Hide), x.Ct)).Status.ShouldBe(TskCurationStatus.Proposed);
        (await ProposalsOfAsync(x, b)).Count(r => r.State == TskProposalStates.Pending).ShouldBe(1);
        var pendingId = (await ProposalsOfAsync(x, b)).Single(r => r.State == TskProposalStates.Pending).Id;
        (await x.C.ExecuteScalarAsync<int>("""SELECT count(*) FROM "unshackled-word"."TskCurations" WHERE "SourceHebRefId" = @Gen12""", new { Gen12 }, x.T)).ShouldBe(0);

        // B's chapter shows the pending proposal on the (still visible) link; A and anonymous readers do not
        var ofB = Ref(await x.ChapterAsync(1, b), 2, 19, 104, 30).ShouldNotBeNull();
        ofB.Rating.ShouldBeNull();
        ofB.PendingProposal.ShouldNotBeNull();
        ofB.PendingProposal.Id.ShouldBe(pendingId);
        ofB.PendingProposal.Rating.ShouldBe(TskRatings.Hide);
        ofB.PendingProposal.Word!.QuotedText.ShouldBe("Geist");
        ofB.PendingProposal.Delete.ShouldBeNull();
        Ref(await x.ChapterAsync(1, a), 2, 19, 104, 30).ShouldNotBeNull().PendingProposal.ShouldBeNull();
        (await x.ChapterAsync(1, null)).Verses.SelectMany(v => v.References).ShouldAllBe(r => r.PendingProposal == null);

        // review list
        var item = (await x.Review.ListPendingAsync(x.Ct)).Proposals.Single(p => p.Id == pendingId);
        (item.SourceHebRefId, item.TargetStartHebRefId, item.TargetEndHebRefId).ShouldBe((Gen12, 19_104_030, (int?)null));
        item.InTsk.ShouldBeTrue();
        item.Current.ShouldBeNull();
        item.Stale.ShouldBeFalse();
        item.AuthorUserId.ShouldBe(b);
        item.AuthorDisplayName.ShouldBe(await DisplayNameAsync(x, b));
        item.Proposed.Word!.StartPosition.ShouldBe(Geist);

        // accept: merged state for everyone, audit Source proposal (maintainer, proposalId in After), admin log
        var accepted = await x.Review.AcceptAsync(maintainer, pendingId, false, x.Ct);
        accepted.Status.ShouldBe(TskProposalReviewStatus.Ok);
        accepted.Proposal!.State.ShouldBe(TskProposalStates.Accepted);
        accepted.Proposal.DecidedAt.ShouldNotBeNull();
        accepted.Curation!.Rating.ShouldBe(TskRatings.Hide);
        accepted.Curation.Word!.StartPosition.ShouldBe(Geist);

        Ref(await x.ChapterAsync(1, b), 2, 19, 104, 30).ShouldBeNull("hidden for B now");
        Ref(await x.ChapterAsync(1, a), 2, 19, 104, 30).ShouldBeNull("hidden for A now");
        var curated = Ref(await x.ChapterAsync(1, maintainer, includeHidden: true), 2, 19, 104, 30).ShouldNotBeNull();
        curated.Rating.ShouldBe(TskRatings.Hide);
        curated.Word!.QuotedText.ShouldBe("Geist");
        curated.Word.State.ShouldBe(TskWordLinkStates.Ok);
        curated.PendingProposal.ShouldBeNull();

        var audit = (await AuditAsync(x, Ps10430UnderGen12)).ShouldHaveSingleItem();
        audit.Source.ShouldBe(TskCurationSources.Proposal);
        audit.UserId.ShouldBe(maintainer);
        var after = JsonSerializer.Deserialize<JsonElement>(audit.After!);
        after.GetProperty("proposalId").GetInt32().ShouldBe(pendingId);
        after.GetProperty("rating").GetString().ShouldBe(TskRatings.Hide);
        after.GetProperty("quotedText").GetString().ShouldBe("Geist");

        var log = (await AdminLogAsync(x, maintainer)).ShouldHaveSingleItem();
        (log.Action, log.TargetUserId).ShouldBe((AdminLogActions.TskProposalAccepted, b));
        var details = JsonSerializer.Deserialize<JsonElement>(log.Details);
        details.GetProperty("proposalId").GetInt32().ShouldBe(pendingId);
        details.GetProperty("key").GetProperty("sourceHebRefId").GetInt32().ShouldBe(Gen12);
        details.GetProperty("key").GetProperty("targetStartHebRefId").GetInt32().ShouldBe(19_104_030);

        (await x.Review.ListPendingAsync(x.Ct)).Proposals.ShouldNotContain(p => p.Id == pendingId);
        (await x.Review.ListMineAsync(b, x.Ct)).Proposals.First().State.ShouldBe(TskProposalStates.Accepted);
        (await x.Review.AcceptAsync(maintainer, pendingId, false, x.Ct)).Status.ShouldBe(TskProposalReviewStatus.Invalid);
    });

    [Fact]
    public async Task StaleProposal_IsReported_AndNotApplied_UnlessForced() => await WithTransactionAsync(async x =>
    {
        var b = await NewUserAsync(x, "tsk-b");
        var maintainer = await NewUserAsync(x, "tsk-m");

        var proposed = await x.Service.SaveAsync(b, false, Save(Ps10430UnderGen12, TskRatings.Highlight), x.Ct);
        var id = proposed.Proposal!.Id;
        // meanwhile the maintainer comments the same link directly
        (await x.Service.SaveAsync(maintainer, true, Save(Ps10430UnderGen12, null, "Odem"), x.Ct)).Status.ShouldBe(TskCurationStatus.Ok);

        var item = (await x.Review.ListPendingAsync(x.Ct)).Proposals.Single(p => p.Id == id);
        item.Stale.ShouldBeTrue();
        item.Current!.Comment.ShouldBe("Odem");
        item.Proposed.Rating.ShouldBe(TskRatings.Highlight);
        item.Proposed.Comment.ShouldBeNull();

        var refused = await x.Review.AcceptAsync(maintainer, id, false, x.Ct);
        refused.Status.ShouldBe(TskProposalReviewStatus.Invalid);
        refused.Message!.ShouldContain("changed");
        var unchanged = Ref(await x.ChapterAsync(1, null), 2, 19, 104, 30).ShouldNotBeNull();
        (unchanged.Rating, unchanged.Comment).ShouldBe(((string?)null, "Odem"));
        (await ProposalsOfAsync(x, b)).Single(r => r.Id == id).State.ShouldBe(TskProposalStates.Pending);
        (await AdminLogAsync(x, maintainer)).ShouldBeEmpty();

        var forced = await x.Review.AcceptAsync(maintainer, id, true, x.Ct);
        forced.Status.ShouldBe(TskProposalReviewStatus.Ok);
        (forced.Curation!.Rating, forced.Curation.Comment).ShouldBe((TskRatings.Highlight, (string?)null));
        (await AuditAsync(x, Ps10430UnderGen12)).Select(a => a.Source).ShouldBe([TskCurationSources.Edit, TskCurationSources.Proposal]);
    });

    [Fact]
    public async Task Reject_WithReason_IsVisibleInMine_AndLogged_CurationUntouched() => await WithTransactionAsync(async x =>
    {
        var b = await NewUserAsync(x, "tsk-b");
        var maintainer = await NewUserAsync(x, "tsk-m");
        var id = (await x.Service.SaveAsync(b, false, Save(Ps10430UnderGen12, TskRatings.Highlight, "Geist und Odem"), x.Ct)).Proposal!.Id;

        var rejected = await x.Review.RejectAsync(maintainer, id, "  zu schwach ", x.Ct);
        rejected.Status.ShouldBe(TskProposalReviewStatus.Ok);
        rejected.Proposal!.State.ShouldBe(TskProposalStates.Rejected);

        var mine = (await x.Review.ListMineAsync(b, x.Ct)).Proposals.ShouldHaveSingleItem();
        (mine.Id, mine.State, mine.Reason).ShouldBe((id, TskProposalStates.Rejected, "zu schwach"));
        mine.Proposed.Comment.ShouldBe("Geist und Odem");
        mine.DecidedAt.ShouldNotBeNull();

        var log = (await AdminLogAsync(x, maintainer)).ShouldHaveSingleItem();
        (log.Action, log.TargetUserId).ShouldBe((AdminLogActions.TskProposalRejected, b));
        (await AuditAsync(x, Ps10430UnderGen12)).ShouldBeEmpty();
        Ref(await x.ChapterAsync(1, b), 2, 19, 104, 30).ShouldNotBeNull().PendingProposal.ShouldBeNull();

        (await x.Review.RejectAsync(maintainer, id, null, x.Ct)).Status.ShouldBe(TskProposalReviewStatus.Invalid);
        (await x.Review.AcceptAsync(maintainer, id, true, x.Ct)).Status.ShouldBe(TskProposalReviewStatus.Invalid);
        (await x.Review.RejectAsync(maintainer, int.MaxValue, null, x.Ct)).Status.ShouldBe(TskProposalReviewStatus.NotFound);
        (await x.Review.AcceptAsync(maintainer, int.MaxValue, false, x.Ct)).Status.ShouldBe(TskProposalReviewStatus.NotFound);
    });

    [Fact]
    public async Task NewProposal_Supersedes_AndAskingForTheCurrentState_Withdraws() => await WithTransactionAsync(async x =>
    {
        var b = await NewUserAsync(x, "tsk-b");
        var other = await NewUserAsync(x, "tsk-c");

        await x.Service.SaveAsync(b, false, Save(Ps10430UnderGen12, TskRatings.Hide), x.Ct);
        await x.Service.SaveAsync(b, false, Save(Ps10430UnderGen12, TskRatings.Highlight), x.Ct);
        await x.Service.SaveAsync(other, false, Save(Ps10430UnderGen12, TskRatings.Hide), x.Ct);

        var rows = await ProposalsOfAsync(x, b);
        rows.Select(r => (r.State, r.Rating)).ShouldBe([(TskProposalStates.Superseded, TskRatings.Hide), (TskProposalStates.Pending, TskRatings.Highlight)]);
        (await ProposalsOfAsync(x, other)).ShouldHaveSingleItem().State.ShouldBe(TskProposalStates.Pending, "proposals of other users stay");

        // back to neutral = the current state: the pending proposal is withdrawn, the answer is the current (plain) state
        var withdrawn = await x.Service.SaveAsync(b, false, Save(Ps10430UnderGen12, null), x.Ct);
        withdrawn.Status.ShouldBe(TskCurationStatus.Ok);
        withdrawn.Response!.Origin.ShouldBe(TskOrigins.Tsk);
        withdrawn.Response.UpdatedAt.ShouldBeNull();
        (await ProposalsOfAsync(x, b)).ShouldAllBe(r => r.State == TskProposalStates.Superseded);
        Ref(await x.ChapterAsync(1, b), 2, 19, 104, 30).ShouldNotBeNull().PendingProposal.ShouldBeNull();

        // unknown link: 404 and nothing stored
        (await x.Service.SaveAsync(b, false, Save(new TskLinkKey(Gen12, 66_022_021, null), TskRatings.Hide), x.Ct)).Status.ShouldBe(TskCurationStatus.NotFound);
        (await ProposalsOfAsync(x, b)).Count.ShouldBe(2);
    });

    [Fact]
    public async Task ProposedAddedLink_ShowsOnlyForItsAuthor_CanBeRefined_AndWithdrawnByDelete() => await WithTransactionAsync(async x =>
    {
        var a = await NewUserAsync(x, "tsk-a");
        var b = await NewUserAsync(x, "tsk-b");
        var key = new TskLinkKey(Gen5020, 45_008_028, 45_008_030);
        var add = new AddTskCurationRequest { SourceHebRefId = Gen5020, Reference = "Röm 8,28-30", Comment = "zum Guten" };

        var proposed = await x.Service.AddAsync(b, false, add, x.Ct);
        proposed.Status.ShouldBe(TskCurationStatus.Proposed);
        (proposed.Proposal!.TargetStartHebRefId, proposed.Proposal.TargetEndHebRefId).ShouldBe((45_008_028, (int?)45_008_030));
        (proposed.Proposal.Proposed.Origin, proposed.Proposal.Proposed.Comment).ShouldBe((TskOrigins.Added, "zum Guten"));

        var verse20 = (await x.ChapterAsync(50, b)).Verses.Single(v => v.Verse == 20).References;
        var link = verse20.Last();
        (link.BibleBookId, link.Chapter, link.Verse, link.EndVerse, link.Origin).ShouldBe((45, 8, 28, 30, TskOrigins.Added));
        link.Comment.ShouldBeNull();
        link.PendingProposal!.Comment.ShouldBe("zum Guten");
        Ref(await x.ChapterAsync(50, a), 20, 45, 8, 28, 30).ShouldBeNull();
        Ref(await x.ChapterAsync(50, null), 20, 45, 8, 28, 30).ShouldBeNull();

        // B refines the own proposed link: still an added link, comment and rating together
        var refined = await x.Service.SaveAsync(b, false, Save(key, TskRatings.Highlight, "zum Guten"), x.Ct);
        refined.Status.ShouldBe(TskCurationStatus.Proposed);
        (refined.Proposal!.Proposed.Origin, refined.Proposal.Proposed.Rating, refined.Proposal.Proposed.Comment)
            .ShouldBe((TskOrigins.Added, TskRatings.Highlight, "zum Guten"));
        // A cannot curate B's proposed link
        (await x.Service.SaveAsync(a, false, Save(key, TskRatings.Highlight), x.Ct)).Status.ShouldBe(TskCurationStatus.NotFound);

        // deleting the own proposed link withdraws it
        var deleted = await x.Service.DeleteAddedAsync(b, false,
            new DeleteTskCurationRequest { SourceHebRefId = Gen5020, TargetStartHebRefId = 45_008_028, TargetEndHebRefId = 45_008_030 }, x.Ct);
        deleted.Status.ShouldBe(TskCurationStatus.Ok);
        (await ProposalsOfAsync(x, b)).ShouldAllBe(r => r.State == TskProposalStates.Superseded);
        Ref(await x.ChapterAsync(50, b), 20, 45, 8, 28, 30).ShouldBeNull();

        // a TSK link cannot be proposed as added
        (await x.Service.AddAsync(b, false, new AddTskCurationRequest { SourceHebRefId = Gen12, Reference = "Ps 104,30" }, x.Ct))
            .Status.ShouldBe(TskCurationStatus.Conflict);
    });

    [Fact]
    public async Task ProposedDeletionOfAnAddedLink_IsAppliedOnAccept_WithProposalIdInTheAudit() => await WithTransactionAsync(async x =>
    {
        var b = await NewUserAsync(x, "tsk-b");
        var maintainer = await NewUserAsync(x, "tsk-m");
        var key = new TskLinkKey(Gen5020, 45_008_028, 45_008_030);
        (await x.Service.AddAsync(maintainer, true, new AddTskCurationRequest { SourceHebRefId = Gen5020, Reference = "Röm 8,28-30" }, x.Ct))
            .Status.ShouldBe(TskCurationStatus.Ok);

        var request = new DeleteTskCurationRequest { SourceHebRefId = Gen5020, TargetStartHebRefId = 45_008_028, TargetEndHebRefId = 45_008_030 };
        var proposed = await x.Service.DeleteAddedAsync(b, false, request, x.Ct);
        proposed.Status.ShouldBe(TskCurationStatus.Proposed);
        proposed.Proposal!.Proposed.Delete.ShouldBe(true);
        Ref(await x.ChapterAsync(50, b), 20, 45, 8, 28, 30)!.PendingProposal!.Delete.ShouldBe(true);
        Ref(await x.ChapterAsync(50, null), 20, 45, 8, 28, 30).ShouldNotBeNull("still there until accepted");

        (await x.Review.AcceptAsync(maintainer, proposed.Proposal.Id, false, x.Ct)).Status.ShouldBe(TskProposalReviewStatus.Ok);
        Ref(await x.ChapterAsync(50, null), 20, 45, 8, 28, 30).ShouldBeNull();
        var audit = await AuditAsync(x, key);
        audit.Select(r => r.Source).ShouldBe([TskCurationSources.Edit, TskCurationSources.Proposal]);
        var after = JsonSerializer.Deserialize<JsonElement>(audit[1].After!);
        after.GetProperty("proposalId").GetInt32().ShouldBe(proposed.Proposal.Id);
        after.TryGetProperty("origin", out _).ShouldBeFalse("a deletion carries only the proposal id");
    });

    internal sealed class TransactionProposalRepository(IDbConnection connection, IDbTransaction transaction) : ITskCurationProposalRepository
    {
        public Task<TskProposalRecord?> GetAsync(int id, CancellationToken token = default) =>
            TskCurationProposalRepository.GetAsync(connection, transaction, id, token);

        public Task<IReadOnlyList<TskProposalRecord>> GetPendingOfUserAsync(int userId, int fromHebRefId, int toHebRefId, CancellationToken token = default) =>
            TskCurationProposalRepository.GetPendingOfUserAsync(connection, transaction, userId, fromHebRefId, toHebRefId, token);

        public Task<IReadOnlyList<TskProposalRecord?>> ProposeAsync(int userId, IReadOnlyList<TskProposalWrite> writes, CancellationToken token = default) =>
            TskCurationProposalRepository.ProposeAsync(connection, transaction, userId, writes, token);

        public Task<IReadOnlyList<TskProposalReviewRecord>> GetPendingAsync(int limit, CancellationToken token = default) =>
            TskCurationProposalRepository.GetPendingAsync(connection, transaction, limit, token);

        public Task<IReadOnlyList<TskProposalRecord>> GetOfUserAsync(int userId, int limit, CancellationToken token = default) =>
            TskCurationProposalRepository.GetOfUserAsync(connection, transaction, userId, limit, token);

        public Task<TskProposalDecision> AcceptAsync(int id, int maintainerId, bool force, CancellationToken token = default) =>
            TskCurationProposalRepository.AcceptAsync(connection, transaction, id, maintainerId, force, token);

        public Task<TskProposalDecision> RejectAsync(int id, int maintainerId, string? reason, CancellationToken token = default) =>
            TskCurationProposalRepository.RejectAsync(connection, transaction, id, maintainerId, reason, token);
    }

    /// <summary>Appends on the test transaction (<see cref="AdminLogRepository"/> uses the transaction's connection when one is passed).</summary>
    private sealed class TransactionAdminLog(IDbTransaction transaction) : IAdminLogRepository
    {
        private readonly AdminLogRepository _inner = new(null!);

        public Task AppendAsync(AdminLogEntry entry, IDbTransaction? ignored = null, CancellationToken token = default) => _inner.AppendAsync(entry, transaction, token);

        public Task<IReadOnlyList<AdminLogRecord>> QueryAsync(AdminLogQuery query, CancellationToken token = default) => throw new NotSupportedException();

        public Task<AdminLogPage> QueryPageAsync(AdminLogQuery query, CancellationToken token = default) => throw new NotSupportedException();
    }
}
