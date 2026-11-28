using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Application.Features.VerseCounting;

/// <summary>Singleton that loads the differing verse-counting rows once per process (the table only changes with a re-seed) and keeps the pure map.</summary>
public interface IVerseCountingMapProvider
{
    Task<IVerseCountingMap> GetAsync(CancellationToken token = default);
}

public sealed class VerseCountingMapProvider : IVerseCountingMapProvider
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<VerseCountingRow>>> _load;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IVerseCountingMap? _map;

    /// <param name="load">Reads the rows with its own scope (the repository is scoped, this class is a singleton).</param>
    public VerseCountingMapProvider(Func<CancellationToken, Task<IReadOnlyList<VerseCountingRow>>> load)
    {
        _load = load;
    }

    public async Task<IVerseCountingMap> GetAsync(CancellationToken token = default)
    {
        if (_map is { } ready)
        {
            return ready;
        }

        await _gate.WaitAsync(token);
        try
        {
            if (_map is { } loaded)
            {
                return loaded;
            }

            // A failed load is not cached; neither is an empty one (a database before the setup page's Elb1871 text step, roadmap 0031),
            // so the next call reads the seeded rows.
            var rows = await _load(token);
            var map = new VerseCountingMap(rows);
            if (rows.Count > 0)
            {
                _map = map;
            }

            return map;
        }
        finally
        {
            _gate.Release();
        }
    }
}
