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
            // A failed load is not cached; the next call tries again.
            return _map ??= new VerseCountingMap(await _load(token));
        }
        finally
        {
            _gate.Release();
        }
    }
}
