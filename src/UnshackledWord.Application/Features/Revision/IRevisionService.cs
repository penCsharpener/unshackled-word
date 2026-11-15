using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Application.Features.Revision;

public enum SaveRevisionOutcome
{
    Saved,
    NotFound,
    Invalid,
    Conflict,
}

public sealed class SaveRevisionResult
{
    public SaveRevisionOutcome Outcome { get; init; }
    public string? Error { get; init; }
    public SaveRevisionVerseResponse? Saved { get; init; }
}

public enum WithdrawRevisionOutcome
{
    Withdrawn,
    NotFound,

    /// <summary>The version is no longer the active one (already withdrawn or replaced).</summary>
    Conflict,
}

public interface IRevisionService
{
    Task<SaveRevisionResult> SaveTextAsync(int bibleBookId, int chapter, int verse, SaveRevisionVerseRequest request, int userId, CancellationToken token = default);

    /// <summary>Elb1871 text plus the active variants; null when the verse does not exist.</summary>
    Task<RevisionVerseResponse?> GetVerseAsync(int bibleBookId, int chapter, int verse, CancellationToken token = default);

    /// <summary>Every version of the level, newest first, ended ones included; null when the verse does not exist.</summary>
    Task<RevisionHistoryResponse?> GetHistoryAsync(int bibleBookId, int chapter, int verse, int level, CancellationToken token = default);

    /// <summary>Ends the active variant without a successor (the level falls back for that verse); no row is deleted.</summary>
    Task<WithdrawRevisionOutcome> WithdrawAsync(int revisionVerseId, int userId, CancellationToken token = default);
}
