using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Application.Features.UserAdmin;

namespace UnshackledWord.Application.Tests.Features.Notes;

public class NoteReviewServiceTests
{
    private const int Actor = 3;
    private const int Author = 7;

    private readonly INoteRepository _repository = Substitute.For<INoteRepository>();
    private readonly IAdminLogRepository _adminLog = Substitute.For<IAdminLogRepository>();

    private NoteReviewService Service() => new(_repository, _adminLog);

    [Fact]
    public async Task RejectAsync_ShouldAppendAnAdminLogEntryForTheAuthor_WhenTheNoteWasPending()
    {
        var id = Guid.NewGuid();
        _repository.ReviewAsync(id, NoteReviewAction.Reject, Actor, "zu persönlich", Arg.Any<CancellationToken>()).Returns(Author);

        var result = await Service().RejectAsync(Actor, id, "  zu persönlich ");

        result.Status.ShouldBe(NoteStatus.Ok);
        await _adminLog.Received(1).AppendAsync(
            Arg.Is<AdminLogEntry>(e => e.ActorUserId == Actor && e.TargetUserId == Author && e.Action == AdminLogActions.NoteRejected
                                       && e.DetailsJson.Contains(id.ToString()) && e.DetailsJson.Contains("zu persönlich")),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveAsync_ShouldBeInvalidAndWriteNoLog_WhenTheNoteIsNotPending()
    {
        var id = Guid.NewGuid();
        _repository.ExistsAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        (await Service().ApproveAsync(Actor, id)).Status.ShouldBe(NoteStatus.Invalid);
        await _adminLog.DidNotReceiveWithAnyArgs().AppendAsync(default!, default, default);
    }

    [Fact]
    public async Task HideAsync_ShouldBeNotFound_WhenNoSuchNoteExists()
    {
        (await Service().HideAsync(Actor, Guid.NewGuid(), null)).Status.ShouldBe(NoteStatus.NotFound);
        await _adminLog.DidNotReceiveWithAnyArgs().AppendAsync(default!, default, default);
    }

    [Theory]
    [InlineData(NoteReviewAction.Approve, AdminLogActions.NoteApproved)]
    [InlineData(NoteReviewAction.Hide, AdminLogActions.NoteHidden)]
    [InlineData(NoteReviewAction.Unhide, AdminLogActions.NoteUnhidden)]
    public async Task Actions_ShouldLogTheirOwnAction_WhenTheyApply(NoteReviewAction action, string logAction)
    {
        var id = Guid.NewGuid();
        _repository.ReviewAsync(id, action, Actor, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Author);
        var service = Service();

        var result = action switch
        {
            NoteReviewAction.Approve => await service.ApproveAsync(Actor, id),
            NoteReviewAction.Hide => await service.HideAsync(Actor, id, null),
            _ => await service.UnhideAsync(Actor, id),
        };

        result.Status.ShouldBe(NoteStatus.Ok);
        await _adminLog.Received(1).AppendAsync(Arg.Is<AdminLogEntry>(e => e.Action == logAction), null, Arg.Any<CancellationToken>());
    }
}

public class NoteVisibilityValidatorTests
{
    [Theory]
    [InlineData("private", true)]
    [InlineData("internal", true)]
    [InlineData("public", true)]
    [InlineData("everyone", false)]
    [InlineData("", false)]
    public void SetVisibility_ShouldOnlyAcceptTheThreeLevels(string value, bool valid) =>
        new SetNoteVisibilityRequestValidator().Validate(new Domain.WebApi.Notes.SetNoteVisibilityRequest { Visibility = value }).IsValid.ShouldBe(valid);

    [Fact]
    public void ReviewAction_ShouldRefuseAReasonOver500Characters() =>
        new NoteReviewActionRequestValidator().Validate(new Domain.WebApi.Notes.NoteReviewActionRequest { Reason = new string('x', 501) }).IsValid.ShouldBeFalse();
}
