namespace UnshackledWord.Application.Features.StrongsStats;

/// <summary>Whether the stats/facts refresh pipeline runs right now (implemented by the API's refresh coordinator).</summary>
public interface IStatsRefreshStatus
{
    bool IsRunning { get; }
}
