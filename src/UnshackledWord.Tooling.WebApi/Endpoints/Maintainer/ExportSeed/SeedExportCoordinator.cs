namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ExportSeed;

/// <summary>Allows one seed export at a time (one API instance; the flag is process-wide).</summary>
public sealed class SeedExportCoordinator
{
    private int _running;

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>False (409) when an export already runs; otherwise the caller must <see cref="Exit"/>.</summary>
    public bool TryEnter() => Interlocked.CompareExchange(ref _running, 1, 0) == 0;

    public void Exit() => Volatile.Write(ref _running, 0);
}
