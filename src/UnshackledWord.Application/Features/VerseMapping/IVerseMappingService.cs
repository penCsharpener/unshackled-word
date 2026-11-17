using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.VerseMapping;

public interface IVerseMappingService
{
    /// <summary>
    /// Returns null when the verse has no Elberfelder words. <c>EditingEnabled</c> of the response is true only while the server-wide
    /// setting is on and <paramref name="callerCanWrite"/> (the caller has a write role) is true.
    /// </summary>
    Task<GetVerseMappingResponse?> GetAsync(int bibleBookId, int chapter, int verse, bool callerCanWrite, CancellationToken token = default);

    /// <summary>
    /// Replaces the whole mapping of the verse; every written row records <paramref name="userId"/> as its author. Rejected without touching the
    /// database while editing is disabled, when a link breaks a rule, or when it would remove compound parts the verse has today
    /// (a German word with parts that comes back without them) while <paramref name="clearParts"/> is false.
    /// </summary>
    Task<SaveVerseMappingResult> SaveAsync(int bibleBookId, int chapter, int verse, IReadOnlyList<VerseMappingLink> links, int userId, bool clearParts = false, CancellationToken token = default);
}

public enum SaveVerseMappingOutcome
{
    Saved,
    NotFound,
    Forbidden,
    Invalid,
}

public sealed class SaveVerseMappingResult
{
    public SaveVerseMappingOutcome Outcome { get; init; }
    public GetVerseMappingResponse? Mapping { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
