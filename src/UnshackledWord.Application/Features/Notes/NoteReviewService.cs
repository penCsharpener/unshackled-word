using System.Text.Json;
using UnshackledWord.Application.Features.UserAdmin;

namespace UnshackledWord.Application.Features.Notes;

/// <summary>
/// Maintainer actions on notes (roadmap 0023.5): approve, reject, hide, unhide. Each applies only in the right state, and every success is
/// appended to the admin log (roadmap 0020) with the note's author as target and <c>{ noteId, reason }</c> as details.
/// </summary>
public sealed class NoteReviewService
{
    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly INoteRepository _repository;
    private readonly IAdminLogRepository _adminLog;

    public NoteReviewService(INoteRepository repository, IAdminLogRepository adminLog)
    {
        _repository = repository;
        _adminLog = adminLog;
    }

    public Task<NoteReviewResult> ApproveAsync(int actorId, Guid id, CancellationToken token = default) =>
        RunAsync(NoteReviewAction.Approve, AdminLogActions.NoteApproved, actorId, id, null, "The note is not waiting for approval.", token);

    public Task<NoteReviewResult> RejectAsync(int actorId, Guid id, string? reason, CancellationToken token = default) =>
        RunAsync(NoteReviewAction.Reject, AdminLogActions.NoteRejected, actorId, id, reason, "The note is not waiting for approval.", token);

    public Task<NoteReviewResult> HideAsync(int actorId, Guid id, string? reason, CancellationToken token = default) =>
        RunAsync(NoteReviewAction.Hide, AdminLogActions.NoteHidden, actorId, id, reason, "Only internal or public notes that are not hidden can be hidden.", token);

    public Task<NoteReviewResult> UnhideAsync(int actorId, Guid id, CancellationToken token = default) =>
        RunAsync(NoteReviewAction.Unhide, AdminLogActions.NoteUnhidden, actorId, id, null, "The note is not hidden.", token);

    private async Task<NoteReviewResult> RunAsync(NoteReviewAction action, string logAction, int actorId, Guid id, string? reason, string wrongState, CancellationToken token)
    {
        var cleanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var author = await _repository.ReviewAsync(id, action, actorId, cleanReason, token);

        if (author is null)
        {
            return await _repository.ExistsAsync(id, token)
                ? new NoteReviewResult(NoteStatus.Invalid, wrongState)
                : new NoteReviewResult(NoteStatus.NotFound, "No such note.");
        }

        var details = JsonSerializer.Serialize(new { noteId = id, reason = cleanReason }, Json);
        await _adminLog.AppendAsync(new AdminLogEntry(actorId, logAction, author, null, details), null, token);

        return NoteReviewResult.Done;
    }
}
