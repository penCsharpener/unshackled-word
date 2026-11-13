using UnshackledWord.Domain.WebApi.Elberfelder.SearchOriginals;

namespace UnshackledWord.Application.Features.StrongsStats;

public interface ISearchOriginalsService
{
    /// <summary>Null while the stats snapshot was never refreshed (the endpoint answers 503).</summary>
    Task<SearchOriginalsResponse?> LookupAsync(string query, int top, bool includeFrequent, CancellationToken token = default);
}
