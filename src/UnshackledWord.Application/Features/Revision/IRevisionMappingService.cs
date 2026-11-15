using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Application.Features.Revision;

public interface IRevisionMappingService
{
    /// <summary>The mapping of one variant version (active or ended); null when the id does not exist.</summary>
    Task<RevisionVerseMappingResponse?> GetAsync(int revisionVerseId, CancellationToken token = default);

    /// <summary>
    /// Replaces the whole mapping of the active variant and stamps it reviewed. Rows that equal a carried row keep
    /// <c>Origin = carried</c>, every other row is <c>manual</c>.
    /// </summary>
    Task<SaveRevisionMappingResult> SaveAsync(int revisionVerseId, IReadOnlyList<VerseMappingLink> links, int userId, CancellationToken token = default);
}

public enum SaveRevisionMappingOutcome
{
    Saved,
    NotFound,

    /// <summary>The variant is no longer the active version (a newer text save or a withdrawal).</summary>
    Conflict,
    Invalid,
}

public sealed class SaveRevisionMappingResult
{
    public SaveRevisionMappingOutcome Outcome { get; init; }
    public RevisionVerseMappingResponse? Mapping { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
