using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;

namespace UnshackledWord.Application.Features.StrongsOccurrences;

public interface IStrongsOccurrenceService
{
    /// <summary>Returns null when the Strong's number cannot be parsed. Page is 1-based.</summary>
    Task<GetStrongsOccurrencesResponse?> GetAsync(string strongs, int page, int pageSize, StrongsOccurrenceFilter filter, CancellationToken token = default);

    /// <summary>
    /// The Septuagint list (roadmap 0028.9): confirmed LXX tags with this Greek Strong's number, paged. <paramref name="includeUnconfirmedCount"/>
    /// (Admin/Maintainer) adds the number of proposed ones. Null when the number cannot be parsed or is Hebrew.
    /// </summary>
    Task<GetStrongsOccurrencesResponse?> GetLxxAsync(string strongs, int page, int pageSize, bool includeUnconfirmedCount, CancellationToken token = default);
}
