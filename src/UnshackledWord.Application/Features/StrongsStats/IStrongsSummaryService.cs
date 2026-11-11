using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsSummary;

namespace UnshackledWord.Application.Features.StrongsStats;

public enum StrongsSummaryStatus
{
    Ok,
    Invalid,
    NotFound
}

public sealed record StrongsSummaryResult(StrongsSummaryStatus Status, GetStrongsSummaryResponse? Response = null);

public interface IStrongsSummaryService
{
    Task<StrongsSummaryResult> GetAsync(string strongs, CancellationToken token = default);
}
