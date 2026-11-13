using UnshackledWord.Domain.WebApi.Elberfelder.GetWordStats;

namespace UnshackledWord.Application.Features.StrongsStats;

public interface IWordStatsService
{
    /// <summary>Returns null when the testament is not "all", "ot" or "nt". An unknown word gives zeros and empty lists. pos filters the resolved lemmas (snapshot only).</summary>
    Task<GetWordStatsResponse?> GetAsync(string word, string testament, string? pos = null, CancellationToken token = default);
}
