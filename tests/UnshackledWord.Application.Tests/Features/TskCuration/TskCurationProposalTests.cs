using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Tests.Features.TskCuration;

/// <summary>Roadmap 0024.6 without a database: validators of the review requests and the desired-state rule of suggestions.</summary>
public class TskCurationProposalTests
{
    private static readonly TskLinkKey Key = new(1_001_002, 19_104_030, null);
    private static readonly DateTimeOffset T1 = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = T1.AddMinutes(5);

    private static TskProposalRecord Pending(TskCurationState state, DateTimeOffset? baseUpdatedAt, bool delete = false) =>
        new(7, Key, state, delete, baseUpdatedAt, 3, T1, TskProposalStates.Pending, null, null, null);

    private static TskCurationRecord Current(TskCurationState state, DateTimeOffset updatedAt) => new(Key, state, updatedAt, 1);

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void RejectValidator_LimitsTheReasonTo500Characters(object? reason, bool valid)
    {
        var text = reason is int length ? new string('x', length) : (string?)reason;
        new RejectTskProposalRequestValidator().Validate(new RejectTskProposalRequest { Id = 1, Reason = text }).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void RejectValidator_TrimsBeforeCounting_AndNeedsAnId()
    {
        new RejectTskProposalRequestValidator().Validate(new RejectTskProposalRequest { Id = 1, Reason = "  " + new string('x', 500) + "  " }).IsValid.ShouldBeTrue();
        new RejectTskProposalRequestValidator().Validate(new RejectTskProposalRequest { Id = 0 }).IsValid.ShouldBeFalse();
        new AcceptTskProposalRequestValidator().Validate(new AcceptTskProposalRequest { Id = 0 }).IsValid.ShouldBeFalse();
        new AcceptTskProposalRequestValidator().Validate(new AcceptTskProposalRequest { Id = 4, Force = true }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Desired_BuildsOnAFreshPendingProposal()
    {
        var pending = Pending(TskCurationState.Plain with { Rating = TskRatings.Hide }, null);

        var write = TskCurationService.Desired(Key, null, pending, s => s with { StartPosition = 16, EndPosition = 16, QuotedText = "Geist" });

        write.State.ShouldBe(new TskCurationState(TskOrigins.Tsk, TskRatings.Hide, null, 16, 16, "Geist"));
        write.BaseUpdatedAt.ShouldBeNull();
        write.Withdraw.ShouldBeFalse();
        write.Delete.ShouldBeFalse();
    }

    [Fact]
    public void Desired_IgnoresAStalePendingProposal_AndRecordsTheCurrentBase()
    {
        var pending = Pending(TskCurationState.Plain with { Rating = TskRatings.Hide }, T1);
        var current = Current(TskCurationState.Plain with { Comment = "Odem" }, T2);

        var write = TskCurationService.Desired(Key, current, pending, s => s with { Rating = TskRatings.Highlight });

        write.State.ShouldBe(TskCurationState.Plain with { Rating = TskRatings.Highlight, Comment = "Odem" });
        write.BaseUpdatedAt.ShouldBe(T2);
    }

    [Fact]
    public void Desired_EqualToTheCurrentState_Withdraws()
    {
        var current = Current(TskCurationState.Plain with { Rating = TskRatings.Highlight }, T1);
        var pending = Pending(TskCurationState.Plain with { Rating = TskRatings.Hide }, T1);

        TskCurationService.Desired(Key, current, pending, s => s with { Rating = TskRatings.Highlight }).Withdraw.ShouldBeTrue();
        TskCurationService.Desired(Key, null, null, s => s with { Rating = null }).Withdraw.ShouldBeTrue();
        TskCurationService.Desired(Key, null, null, s => s with { Rating = TskRatings.Hide }).Withdraw.ShouldBeFalse();
    }

    [Fact]
    public void Desired_IgnoresAPendingDeletion()
    {
        var added = new TskCurationState(TskOrigins.Added, null, "x", null, null, null);
        var current = Current(added, T1);

        var write = TskCurationService.Desired(Key, current, Pending(added, T1, delete: true), s => s with { Comment = "y" });

        write.State.ShouldBe(added with { Comment = "y" });
        write.Delete.ShouldBeFalse();
    }

    [Fact]
    public async Task WithoutProposalRepository_NonMaintainersAreStillRefused()
    {
        var repository = Substitute.For<ITskCurationRepository>();
        var service = new TskCurationService(repository);

        var outcome = await service.SaveAsync(3, false, new SaveTskCurationRequest { SourceHebRefId = Key.SourceHebRefId, TargetStartHebRefId = Key.TargetStartHebRefId },
            TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(TskCurationStatus.Forbidden);
        repository.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void Mapper_OmitsDeleteUnlessSet_AndBuildsTheWord()
    {
        var proposal = Pending(new TskCurationState(TskOrigins.Tsk, TskRatings.Hide, null, 16, 16, "Geist"), null);

        var pending = TskProposalMapper.ToPending(proposal);
        pending.Delete.ShouldBeNull();
        pending.Word!.State.ShouldBe(TskWordLinkStates.Ok);
        (pending.Word.StartPosition, pending.Word.EndPosition, pending.Word.QuotedText).ShouldBe((16, 16, "Geist"));

        TskProposalMapper.ToResponse(proposal with { Delete = true }).Proposed.Delete.ShouldBe(true);
    }
}
