using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.VerseMapping;

public interface IVerseMappingService
{
    /// <summary>Returns null when the verse has no Elberfelder words.</summary>
    Task<GetVerseMappingResponse?> GetAsync(int bibleBookId, int chapter, int verse, CancellationToken token = default);

    /// <summary>Replaces the whole mapping of the verse. Rejected without touching the database while editing is disabled.</summary>
    Task<SaveVerseMappingResult> SaveAsync(int bibleBookId, int chapter, int verse, IReadOnlyList<VerseMappingLink> links, CancellationToken token = default);
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
