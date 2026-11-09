using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;

namespace UnshackledWord.Application.Features.StrongsOccurrences;

public interface IStrongsOccurrenceService
{
    /// <summary>Returns null when the Strong's number cannot be parsed. Page is 1-based.</summary>
    Task<GetStrongsOccurrencesResponse?> GetAsync(string strongs, int page, int pageSize, CancellationToken token = default);
}
